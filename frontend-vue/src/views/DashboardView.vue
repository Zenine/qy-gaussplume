<script setup lang="ts">
import { computed, onMounted, ref, shallowRef, watch } from 'vue'
import { ElMessage } from 'element-plus'
import {
  Aim,
  Brush,
  Close,
  Compass,
  Delete,
  Histogram,
  MagicStick,
  VideoPlay,
} from '@element-plus/icons-vue'
import { storeToRefs } from 'pinia'
import { meteorologyApi, receptorsApi, simulationApi, sourcesApi } from '@/api'
import type {
  EmissionSource,
  Meteorology,
  ParallelSimulationResult,
  Receptor,
  SimulationResult,
} from '@/types'
import MapPanel from '@/components/MapPanel.vue'
import ColorLegend from '@/components/ColorLegend.vue'
import ContributionPanel from '@/components/ContributionPanel.vue'
import ParallelSimulationDialog from '@/components/ParallelSimulationDialog.vue'
import { concentrationRange } from '@/utils/colorScale'
import { wgs84ToGcj02 } from '@/utils/coords'
import { usePrefsStore } from '@/stores/prefs'
import { errorMessage } from '@/utils/error'
import { filterEntitiesByBounds, type SelectionBounds } from '@/utils/selection'

// ---------- 基础数据 ----------
const sources = ref<EmissionSource[]>([])
const receptors = ref<Receptor[]>([])
const meteorologies = ref<Meteorology[]>([])
const selectedMeteorologyId = ref<number | null>(null)

const running = ref(false)
const result = shallowRef<SimulationResult | null>(null)
const mapRef = ref<InstanceType<typeof MapPanel> | null>(null)

const showContribution = ref(false)
const showParallel = ref(false)
const selectionEnabled = ref(false)
const selectionBounds = ref<SelectionBounds | null>(null)
const selectedRankingReceptor = ref('')
const selectedRankingPollutant = ref('')

// ---------- 偏好（持久化） ----------
const prefs = usePrefsStore()
const {
  scale,
  opacity,
  renderScale,
  tileLayer,
  selectedPollutant,
  gridResolution,
  domainSize,
  customMin,
  customMax,
} = storeToRefs(prefs)

// ---------- 气象控制 ----------
const draftWindDirection = ref(0)
const draftWindSpeed = ref(0.1)

const selectedMeteorology = computed(
  () => meteorologies.value.find((m) => m.id === selectedMeteorologyId.value) ?? null,
)

const weatherDirty = computed(() => {
  const met = selectedMeteorology.value
  if (!met) return false
  return draftWindDirection.value !== met.windDirection || draftWindSpeed.value !== met.windSpeed
})

watch(
  selectedMeteorology,
  (met) => {
    if (!met) return
    draftWindDirection.value = met.windDirection
    draftWindSpeed.value = met.windSpeed
  },
  { immediate: true },
)

// ---------- 选择区域与派生状态 ----------
function toGcjPoint(entity: { latitude: number; longitude: number }) {
  const [latitude, longitude] = wgs84ToGcj02(entity.latitude, entity.longitude)
  return { latitude, longitude }
}

const effectiveSources = computed(() =>
  filterEntitiesByBounds(sources.value, selectionBounds.value, toGcjPoint),
)
const effectiveReceptors = computed(() =>
  filterEntitiesByBounds(receptors.value, selectionBounds.value, toGcjPoint),
)

const domainSizeKm = computed({
  get: () => Math.round(domainSize.value / 1000),
  set: (v: number) => {
    domainSize.value = v * 1000
  },
})

const sourcePollutants = computed(() => {
  const values = new Set<string>()
  for (const s of sources.value) {
    for (const p of s.pollutants ?? []) values.add(p.pollutantType)
  }
  return [...values]
})

const pollutantOptions = computed(() => {
  const values = new Set<string>(result.value?.availablePollutants ?? [])
  for (const p of sourcePollutants.value) values.add(p)
  return [...values]
})

const autoRange = computed(() => {
  if (!result.value) return { min: 0, max: 0 }
  return concentrationRange(displayedResult.value?.concentrations ?? result.value.concentrations)
})

const effectiveMin = computed(() => customMin.value ?? autoRange.value.min)
const effectiveMax = computed(() => customMax.value ?? autoRange.value.max)

// 按当前选中的污染物显示对应浓度场（由后端分别返回 pollutantConcentrations 字典）
const displayedResult = computed<SimulationResult | null>(() => {
  if (!result.value) return null
  const pol = selectedPollutant.value
  if (!pol || !result.value.pollutantConcentrations?.[pol]) return result.value
  return {
    ...result.value,
    concentrations: result.value.pollutantConcentrations[pol],
  }
})

const rankedContributions = computed(() =>
  rankingRows.value.slice(0, 6),
)

const receptorContributionNames = computed(() =>
  displayedResult.value ? Object.keys(displayedResult.value.receptorContributions) : [],
)

const rankingPollutants = computed(() => {
  if (!displayedResult.value || !selectedRankingReceptor.value) return []
  return Object.keys(displayedResult.value.receptorContributions[selectedRankingReceptor.value] ?? {})
})

const rankingRows = computed(() => {
  if (!displayedResult.value || !selectedRankingReceptor.value || !selectedRankingPollutant.value) {
    return []
  }
  return displayedResult.value.receptorContributions[selectedRankingReceptor.value]?.[
    selectedRankingPollutant.value
  ] ?? []
})

watch(
  displayedResult,
  (value) => {
    const names = value ? Object.keys(value.receptorContributions) : []
    selectedRankingReceptor.value = names.includes(selectedRankingReceptor.value)
      ? selectedRankingReceptor.value
      : names[0] ?? ''
    const pollutants = selectedRankingReceptor.value
      ? Object.keys(value?.receptorContributions[selectedRankingReceptor.value] ?? {})
      : []
    selectedRankingPollutant.value = pollutants.includes(selectedRankingPollutant.value)
      ? selectedRankingPollutant.value
      : pollutants[0] ?? ''
  },
  { immediate: true },
)

watch(selectedRankingReceptor, () => {
  const pollutants = rankingPollutants.value
  selectedRankingPollutant.value = pollutants.includes(selectedRankingPollutant.value)
    ? selectedRankingPollutant.value
    : pollutants[0] ?? ''
})

// ---------- 数据加载与模拟 ----------
async function loadAll() {
  try {
    const [srcs, recs, mets] = await Promise.all([
      sourcesApi.list(0, 1000),
      receptorsApi.list(0, 1000),
      meteorologyApi.list(0, 1000),
    ])
    sources.value = srcs
    receptors.value = recs
    meteorologies.value = mets
    if (mets.length > 0 && selectedMeteorologyId.value === null) {
      selectedMeteorologyId.value = mets[0].id
    }
  } catch (e) {
    ElMessage.error(errorMessage(e, '加载数据失败'))
  }
}

async function runSimulation() {
  if (!selectedMeteorologyId.value) {
    ElMessage.warning('请先选择气象场')
    return
  }
  if (effectiveSources.value.length === 0) {
    ElMessage.warning(selectionBounds.value ? '选择区域内没有排放源' : '请先添加排放源')
    return
  }
  if (weatherDirty.value) {
    ElMessage.info('首页气象控制仅用于预览，请到气象场管理保存后再运行正式参数')
  }

  running.value = true
  try {
    const sourceIds = selectionBounds.value ? effectiveSources.value.map((s) => s.id) : undefined
    const receptorIds = selectionBounds.value ? effectiveReceptors.value.map((r) => r.id) : undefined
    const r = await simulationApi.run({
      meteorologyId: selectedMeteorologyId.value,
      sourceIds,
      receptorIds,
      pollutantType: selectedPollutant.value || undefined,
      gridResolution: gridResolution.value,
      domainSize: domainSize.value,
    })
    result.value = r
    if (!selectedPollutant.value && r.availablePollutants?.length) {
      selectedPollutant.value = r.availablePollutants[0]
    }
    ElMessage.success('模拟完成')
    mapRef.value?.fitBounds()
  } catch (e) {
    ElMessage.error(errorMessage(e, '模拟失败'))
  } finally {
    running.value = false
  }
}

function clearResult() {
  result.value = null
  customMin.value = null
  customMax.value = null
  selectionBounds.value = null
  selectionEnabled.value = false
  mapRef.value?.clearSelection()
}

function onSelectionChange(bounds: SelectionBounds | null) {
  selectionBounds.value = bounds
  selectionEnabled.value = false
}

function startSelection() {
  selectionEnabled.value = true
  ElMessage.info('在地图上按住并拖动，绘制模拟区域')
}

function onParallelCompleted(r: ParallelSimulationResult) {
  if (!r.concentrations || !r.gridLat || !r.gridLon) {
    ElMessage.warning('并行模拟无浓度数据（可能处于 detailed 模式）')
    return
  }
  // 用并行聚合结果替换地图展示，保留污染物分场与受体贡献数据。
  result.value = {
    concentrations: r.concentrations,
    gridLat: r.gridLat,
    gridLon: r.gridLon,
    contributions: [],
    receptorContributions: r.receptorContributions ?? {},
    pollutantConcentrations: r.pollutantConcentrations ?? null,
    availablePollutants: r.availablePollutants ?? null,
  }
  mapRef.value?.fitBounds()
}

onMounted(loadAll)
</script>

<template>
  <div class="dashboard-map">
    <MapPanel
      ref="mapRef"
      :sources="sources"
      :receptors="receptors"
      :result="displayedResult"
      :scale="scale"
      :opacity="opacity"
      :min="effectiveMin"
      :max="effectiveMax"
      :render-scale="renderScale"
      :tile-layer="tileLayer"
      :selection-enabled="selectionEnabled"
      @selection-change="onSelectionChange"
    />

    <div class="floating-toolbar" data-test="floating-toolbar">
      <el-select v-model="tileLayer" size="small" class="toolbar-select">
        <el-option value="street" label="高德街道" />
        <el-option value="satellite" label="高德卫星" />
        <el-option value="hybrid" label="高德混合" />
      </el-select>
      <el-select v-model="selectedMeteorologyId" size="small" class="toolbar-wind">
        <el-option
          v-for="m in meteorologies"
          :key="m.id"
          :value="m.id"
          :label="`${m.name} - 风速:${m.windSpeed} 风向:${m.windDirection}°`"
        />
      </el-select>
      <el-select
        v-model="selectedPollutant"
        size="small"
        clearable
        placeholder="全部污染物"
        class="toolbar-select"
      >
        <el-option v-for="p in pollutantOptions" :key="p" :value="p" :label="p" />
      </el-select>
      <el-button
        data-test="run-simulation"
        type="primary"
        size="small"
        :icon="VideoPlay"
        :loading="running"
        :disabled="running || !selectedMeteorologyId"
        @click="runSimulation"
      >
        运行模拟
      </el-button>
      <el-button data-test="clear-result" size="small" :icon="Delete" @click="clearResult">
        清除结果
      </el-button>
    </div>

    <div class="range-panel floating-card" data-test="range-panel">
      <div class="range-row">
        <span>模拟范围</span>
        <strong>{{ domainSizeKm }} km</strong>
      </div>
      <el-slider v-model="domainSizeKm" :min="1" :max="50" :step="1" />
      <div class="range-row">
        <span>网格分辨率</span>
        <strong>{{ gridResolution }} m</strong>
      </div>
      <el-slider v-model="gridResolution" :min="10" :max="500" :step="10" />
    </div>

    <aside class="right-stack">
      <section class="floating-card" data-test="weather-card">
        <div class="card-title">
          <span>气象控制</span>
          <el-icon><Compass /></el-icon>
        </div>
        <div class="wind-rose">
          <span>N</span>
          <span>E</span>
          <span>S</span>
          <span>W</span>
          <i :style="{ transform: `rotate(${draftWindDirection}deg)` }" />
        </div>
        <div class="field-grid">
          <label>
            风向 (°)
            <el-input-number v-model="draftWindDirection" size="small" :min="0" :max="360" :step="1" />
          </label>
          <label>
            风速 (m/s)
            <el-input-number v-model="draftWindSpeed" size="small" :min="0.1" :max="20" :step="0.1" />
          </label>
        </div>
        <p v-if="weatherDirty" class="hint warning">临时参数未保存，运行仍使用已选气象场。</p>
        <p v-else class="hint">运行模拟会使用当前选中的已保存气象场。</p>
      </section>

      <template v-if="!result">
        <section class="floating-card" data-test="draw-card">
          <div class="card-title">
            <span>绘制选择区域</span>
            <el-button size="small" type="primary" :icon="Brush" @click="startSelection">
              绘制
            </el-button>
          </div>
          <p class="hint">在地图上拖拽绘制矩形区域，仅模拟区域内排放源的影响。</p>
          <div v-if="selectionBounds" class="selection-summary">
            已选择 {{ effectiveSources.length }} 个排放源，{{ effectiveReceptors.length }} 个受体点
            <el-button link size="small" :icon="Close" @click="clearResult">清除</el-button>
          </div>
        </section>

        <section class="floating-card" data-test="stats-card">
          <div class="card-title">
            <span>数据统计</span>
            <el-icon><Histogram /></el-icon>
          </div>
          <div class="stat-grid">
            <div>
              <strong>{{ effectiveSources.length }}</strong>
              <span>排放源</span>
            </div>
            <div>
              <strong>{{ effectiveReceptors.length }}</strong>
              <span>受体点</span>
            </div>
          </div>
        </section>
      </template>

      <template v-else>
        <section class="floating-card" data-test="result-card">
          <div class="card-title">
            <span>模拟结果</span>
            <span class="complete">完成</span>
          </div>
          <label class="full-field">
            显示污染物
            <el-select v-model="selectedPollutant" size="small" clearable placeholder="全部污染物">
              <el-option v-for="p in pollutantOptions" :key="p" :value="p" :label="p" />
            </el-select>
          </label>
          <label class="full-field">
            色阶类型
            <el-select v-model="scale" size="small">
              <el-option value="jet" label="热力图" />
              <el-option value="turbo" label="Turbo" />
              <el-option value="viridis" label="Viridis" />
              <el-option value="grayscale" label="灰度" />
            </el-select>
          </label>
          <div class="field-grid">
            <label>
              最小值
              <el-input-number v-model="customMin" size="small" :controls="false" />
            </label>
            <label>
              最大值
              <el-input-number v-model="customMax" size="small" :controls="false" />
            </label>
          </div>
          <div class="visual-controls">
            <label>
              透明度
              <el-slider v-model="opacity" :min="0" :max="1" :step="0.05" />
            </label>
            <label>
              渲染精度
              <el-select v-model="renderScale" size="small">
                <el-option v-for="n in [1, 2, 4, 8, 16]" :key="n" :value="n" :label="`${n}x`" />
              </el-select>
            </label>
          </div>
          <ColorLegend
            v-if="effectiveMax > 0"
            :min="effectiveMin"
            :max="effectiveMax"
            :scale="scale"
          />
        </section>

        <section class="floating-card" data-test="ranking-card">
          <div class="card-title">
            <span>受体点贡献分析</span>
            <el-button size="small" link :icon="Histogram" @click="showContribution = true">
              详情
            </el-button>
          </div>
          <div v-if="receptorContributionNames.length" class="ranking-controls">
            <el-select v-model="selectedRankingReceptor" size="small" placeholder="受体点">
              <el-option
                v-for="name in receptorContributionNames"
                :key="name"
                :value="name"
                :label="name"
              />
            </el-select>
            <el-select v-model="selectedRankingPollutant" size="small" placeholder="污染物">
              <el-option v-for="p in rankingPollutants" :key="p" :value="p" :label="p" />
            </el-select>
          </div>
          <div class="ranking-list">
            <div v-for="item in rankedContributions" :key="item.sourceId" class="ranking-item">
              <span>{{ item.sourceName }}</span>
              <strong>{{ item.concentration.toFixed(4) }}</strong>
            </div>
            <p v-if="rankedContributions.length === 0" class="hint">暂无受体点贡献数据</p>
          </div>
        </section>
      </template>
    </aside>

    <div class="quick-actions">
      <el-tooltip content="适应全部点位" placement="right">
        <el-button circle :icon="Aim" @click="mapRef?.fitBounds()" />
      </el-tooltip>
      <el-tooltip content="多风向并行" placement="right">
        <el-button circle :icon="Compass" :disabled="!selectedMeteorologyId" @click="showParallel = true" />
      </el-tooltip>
      <el-tooltip content="恢复默认偏好" placement="right">
        <el-button circle :icon="MagicStick" @click="prefs.reset()" />
      </el-tooltip>
    </div>

    <ContributionPanel v-model:visible="showContribution" :result="displayedResult" />

    <ParallelSimulationDialog
      v-model:visible="showParallel"
      :meteorologies="meteorologies"
      :selected-meteorology-id="selectedMeteorologyId"
      :grid-resolution="gridResolution"
      :domain-size="domainSize"
      :pollutant-type="selectedPollutant"
      @completed="onParallelCompleted"
    />
  </div>
</template>

<style scoped>
.dashboard-map {
  position: relative;
  height: calc(100vh - 96px);
  min-height: 620px;
  overflow: hidden;
  border: 1px solid #cfdde4;
  border-radius: 8px;
  background: #eef3f4;
}

.floating-toolbar {
  position: absolute;
  top: 14px;
  right: 14px;
  z-index: 1000;
  display: flex;
  align-items: center;
  gap: 8px;
  max-width: calc(100% - 28px);
  padding: 8px;
  border: 1px solid #dce6ec;
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.96);
  box-shadow: 0 10px 28px rgba(15, 46, 60, 0.14);
}

.toolbar-select {
  width: 128px;
}

.toolbar-wind {
  width: 230px;
}

.floating-card {
  border: 1px solid #dfe7ee;
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.96);
  box-shadow: 0 10px 28px rgba(15, 46, 60, 0.12);
}

.range-panel {
  position: absolute;
  left: 18px;
  bottom: 18px;
  z-index: 1000;
  width: 230px;
  padding: 16px 16px 10px;
}

.range-row {
  display: flex;
  justify-content: space-between;
  color: #64748b;
  font-size: 12px;
}

.range-row strong {
  color: #1677ff;
}

.right-stack {
  position: absolute;
  right: 14px;
  top: 76px;
  bottom: 14px;
  z-index: 1000;
  display: flex;
  width: 300px;
  flex-direction: column;
  gap: 10px;
  overflow-y: auto;
  padding-right: 2px;
}

.right-stack .floating-card {
  padding: 14px;
}

.card-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding-bottom: 10px;
  border-bottom: 1px solid #edf2f5;
  color: #102a43;
  font-size: 14px;
  font-weight: 800;
}

.hint {
  margin: 12px 0 0;
  color: #64748b;
  font-size: 12px;
  line-height: 1.6;
}

.warning {
  color: #b45309;
}

.selection-summary {
  display: flex;
  align-items: center;
  gap: 4px;
  margin-top: 10px;
  color: #31545c;
  font-size: 12px;
}

.wind-rose {
  position: relative;
  width: 150px;
  height: 150px;
  margin: 16px auto 10px;
  border: 1px solid #dbe6ec;
  border-radius: 999px;
  background:
    radial-gradient(circle, transparent 0 25%, #e8eef2 26% 27%, transparent 28% 49%, #e8eef2 50% 51%, transparent 52%),
    linear-gradient(90deg, transparent 49%, #dbe6ec 50%, transparent 51%),
    linear-gradient(0deg, transparent 49%, #dbe6ec 50%, transparent 51%);
}

.wind-rose span {
  position: absolute;
  color: #64748b;
  font-size: 11px;
}

.wind-rose span:nth-child(1) {
  top: 6px;
  left: 50%;
  transform: translateX(-50%);
}

.wind-rose span:nth-child(2) {
  right: 8px;
  top: 50%;
  transform: translateY(-50%);
}

.wind-rose span:nth-child(3) {
  bottom: 6px;
  left: 50%;
  transform: translateX(-50%);
}

.wind-rose span:nth-child(4) {
  left: 8px;
  top: 50%;
  transform: translateY(-50%);
}

.wind-rose i {
  position: absolute;
  left: 50%;
  top: 50%;
  width: 3px;
  height: 54px;
  transform-origin: 50% 100%;
  border-radius: 999px;
  background: #1677ff;
}

.wind-rose i::after {
  position: absolute;
  left: 50%;
  top: -5px;
  width: 11px;
  height: 11px;
  border-radius: 999px;
  background: #1677ff;
  content: '';
  transform: translateX(-50%);
}

.field-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
  margin-top: 12px;
}

.field-grid label,
.full-field {
  display: flex;
  min-width: 0;
  flex-direction: column;
  gap: 6px;
  color: #64748b;
  font-size: 12px;
}

.full-field {
  margin-top: 12px;
}

.visual-controls {
  display: grid;
  grid-template-columns: 1fr 92px;
  gap: 12px;
  margin-top: 12px;
}

.visual-controls label {
  display: flex;
  min-width: 0;
  flex-direction: column;
  gap: 6px;
  color: #64748b;
  font-size: 12px;
}

.stat-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
  margin-top: 14px;
}

.stat-grid div {
  display: flex;
  min-height: 72px;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  border-radius: 8px;
  background: #f5f7fa;
}

.stat-grid strong {
  color: #1677ff;
  font-size: 26px;
}

.stat-grid span {
  color: #64748b;
  font-size: 12px;
}

.complete {
  border-radius: 999px;
  padding: 2px 8px;
  color: #16a34a;
  background: #dcfce7;
  font-size: 12px;
}

.ranking-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-top: 12px;
}

.ranking-controls {
  display: grid;
  grid-template-columns: 1fr 96px;
  gap: 8px;
  margin-top: 12px;
}

.ranking-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  color: #31545c;
  font-size: 13px;
}

.ranking-item span {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ranking-item strong {
  color: #1677ff;
}

.quick-actions {
  position: absolute;
  left: 18px;
  top: 18px;
  z-index: 1000;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

@media (max-width: 1100px) {
  .dashboard-map {
    height: auto;
    min-height: 960px;
  }

  .floating-toolbar {
    left: 14px;
    flex-wrap: wrap;
  }

  .right-stack {
    top: 150px;
    width: min(300px, calc(100% - 28px));
  }
}
</style>
