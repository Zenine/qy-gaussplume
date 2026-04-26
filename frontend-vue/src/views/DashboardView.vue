<script setup lang="ts">
import { computed, onMounted, ref, shallowRef } from 'vue'
import { ElMessage } from 'element-plus'
import {
  ArrowDown,
  Compass,
  Grid,
  Histogram,
  Location,
  MagicStick,
  OfficeBuilding,
  RefreshLeft,
  SetUp,
  VideoPlay,
  WindPower,
} from '@element-plus/icons-vue'
import { storeToRefs } from 'pinia'
import {
  meteorologyApi,
  receptorsApi,
  simulationApi,
  sourcesApi,
} from '@/api'
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
import { usePrefsStore } from '@/stores/prefs'
import { errorMessage } from '@/utils/error'

// ---------- 数据 ----------
const sources = ref<EmissionSource[]>([])
const receptors = ref<Receptor[]>([])
const meteorologies = ref<Meteorology[]>([])
const selectedMeteorologyId = ref<number | null>(null)

const running = ref(false)
const result = shallowRef<SimulationResult | null>(null)
const mapRef = ref<InstanceType<typeof MapPanel> | null>(null)

const showContribution = ref(false)
const showParallel = ref(false)

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

// ---------- 派生 ----------
const autoRange = computed(() => {
  if (!result.value) return { min: 0, max: 0 }
  return concentrationRange(result.value.concentrations)
})

const effectiveMin = computed(() => customMin.value ?? autoRange.value.min)
const effectiveMax = computed(() => customMax.value ?? autoRange.value.max)

const pollutantOptions = computed(() => result.value?.availablePollutants ?? [])

// 按当前选中的污染物显示对应浓度场（由后端分别返回 PollutantConcentrations 字典）
const displayedResult = computed<SimulationResult | null>(() => {
  if (!result.value) return null
  const pol = selectedPollutant.value
  if (!pol || !result.value.pollutantConcentrations?.[pol]) return result.value
  return {
    ...result.value,
    concentrations: result.value.pollutantConcentrations[pol],
  }
})

// ---------- 方法 ----------
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
  if (sources.value.length === 0) {
    ElMessage.warning('请先添加排放源')
    return
  }
  running.value = true
  try {
    const r = await simulationApi.run({
      meteorologyId: selectedMeteorologyId.value,
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

function onParallelCompleted(r: ParallelSimulationResult) {
  if (!r.concentrations || !r.gridLat || !r.gridLon) {
    ElMessage.warning('并行模拟无浓度数据（可能处于 detailed 模式）')
    return
  }
  // 用并行聚合结果替换地图展示
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

function applyPreset(p: { gridResolution: number; domainSize: number }) {
  gridResolution.value = p.gridResolution
  domainSize.value = p.domainSize
}

onMounted(loadAll)
</script>

<template>
  <div class="dashboard">
    <el-card shadow="never" class="control-panel">
      <div class="metric-strip" aria-label="数据状态">
        <div class="metric-card">
          <el-icon><OfficeBuilding /></el-icon>
          <span class="metric-copy">排放源 {{ sources.length }}</span>
        </div>
        <div class="metric-card">
          <el-icon><Location /></el-icon>
          <span class="metric-copy">受体点 {{ receptors.length }}</span>
        </div>
        <div class="metric-card">
          <el-icon><WindPower /></el-icon>
          <span class="metric-copy">气象场 {{ meteorologies.length }}</span>
        </div>
        <div class="metric-card metric-card-wide">
          <el-icon><Grid /></el-icon>
          <span class="metric-copy">网格 {{ gridResolution }}m / {{ Math.round(domainSize / 1000) }}km</span>
        </div>
      </div>

      <div class="ctrl-row main-controls">
        <div class="ctrl-group">
          <label>气象场</label>
          <el-select v-model="selectedMeteorologyId" size="small" class="select-meteorology">
            <el-option
              v-for="m in meteorologies"
              :key="m.id"
              :value="m.id"
              :label="`${m.name} (${m.windSpeed}m/s @ ${m.windDirection}°)`"
            />
          </el-select>
        </div>

        <div class="ctrl-group">
          <label>污染物</label>
          <el-select
            v-model="selectedPollutant"
            size="small"
            clearable
            placeholder="全部"
            style="width: 100px"
          >
            <el-option v-for="p in pollutantOptions" :key="p" :value="p" :label="p" />
          </el-select>
        </div>

        <div class="ctrl-group">
          <label>网格 (m)</label>
          <el-input-number v-model="gridResolution" size="small" :min="10" :max="500" :step="10" />
        </div>

        <div class="ctrl-group">
          <label>范围 (m)</label>
          <el-input-number v-model="domainSize" size="small" :min="1000" :max="50000" :step="1000" />
        </div>

        <div class="simulation-actions">
          <el-button
            type="primary"
            :icon="VideoPlay"
            :loading="running"
            :disabled="running || !selectedMeteorologyId"
            @click="runSimulation"
          >
            运行模拟
          </el-button>

          <el-button :icon="Compass" :disabled="!selectedMeteorologyId" @click="showParallel = true">
            多风向并行
          </el-button>

          <el-button :icon="Histogram" :disabled="!result" @click="showContribution = true">
            受体贡献
          </el-button>
        </div>

        <span class="spacer" />

        <el-dropdown trigger="click" @command="applyPreset">
          <el-button size="small" plain :icon="MagicStick">
            预设
            <el-icon class="dropdown-icon"><ArrowDown /></el-icon>
          </el-button>
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item :command="{ gridResolution: 100, domainSize: 5000 }">
                快速: 100m × 5km
              </el-dropdown-item>
              <el-dropdown-item :command="{ gridResolution: 50, domainSize: 10000 }">
                标准: 50m × 10km
              </el-dropdown-item>
              <el-dropdown-item :command="{ gridResolution: 25, domainSize: 10000 }">
                高清: 25m × 10km
              </el-dropdown-item>
              <el-dropdown-item :command="{ gridResolution: 10, domainSize: 10000 }">
                极限: 10m × 10km
              </el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
      </div>

      <div class="ctrl-row">
        <div class="ctrl-group">
          <label>色阶</label>
          <el-select v-model="scale" size="small" style="width: 110px">
            <el-option value="jet" label="Jet" />
            <el-option value="turbo" label="Turbo" />
            <el-option value="viridis" label="Viridis" />
            <el-option value="grayscale" label="灰度" />
          </el-select>
        </div>

        <div class="ctrl-group">
          <label>透明度</label>
          <el-slider v-model="opacity" :min="0" :max="1" :step="0.05" style="width: 120px" />
        </div>

        <div class="ctrl-group">
          <label>精度</label>
          <el-select v-model="renderScale" size="small" style="width: 80px">
            <el-option v-for="n in [1, 2, 4, 8, 16]" :key="n" :value="n" :label="`${n}x`" />
          </el-select>
        </div>

        <div class="ctrl-group">
          <label>图层</label>
          <el-radio-group v-model="tileLayer" size="small">
            <el-radio-button value="street">街道</el-radio-button>
            <el-radio-button value="satellite">卫星</el-radio-button>
            <el-radio-button value="hybrid">混合</el-radio-button>
          </el-radio-group>
        </div>

        <div class="ctrl-group">
          <label>自定义范围</label>
          <el-input-number
            v-model="customMin"
            placeholder="min"
            size="small"
            :controls="false"
            style="width: 100px"
          />
          <el-input-number
            v-model="customMax"
            placeholder="max"
            size="small"
            :controls="false"
            style="width: 100px"
          />
          <el-button
            size="small"
            link
            :icon="RefreshLeft"
            @click="
              customMin = null;
              customMax = null
            "
          >
            重置
          </el-button>
        </div>

        <span class="spacer" />

        <el-popconfirm title="恢复默认会清除所有可视化偏好" @confirm="prefs.reset()">
          <template #reference>
            <el-button size="small" link :icon="SetUp">恢复默认</el-button>
          </template>
        </el-popconfirm>
      </div>
    </el-card>

    <div class="map-wrapper">
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
      />
      <ColorLegend
        v-if="result && effectiveMax > 0"
        class="legend-overlay"
        :min="effectiveMin"
        :max="effectiveMax"
        :scale="scale"
      />
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
.dashboard {
  display: flex;
  flex-direction: column;
  height: calc(100vh - 96px);
  gap: 14px;
}
.control-panel {
  flex-shrink: 0;
  border: 1px solid #dbe6ec;
  border-radius: 8px;
  background: #ffffff;
  box-shadow: 0 8px 26px rgba(15, 46, 60, 0.06);
}
.control-panel :deep(.el-card__body) {
  padding: 14px;
}
.metric-strip {
  display: grid;
  grid-template-columns: repeat(4, minmax(132px, 1fr));
  gap: 10px;
  margin-bottom: 12px;
}
.metric-card {
  display: flex;
  align-items: center;
  gap: 8px;
  min-height: 38px;
  padding: 8px 10px;
  border: 1px solid #e0ece9;
  border-radius: 8px;
  color: #3b5561;
  background: linear-gradient(180deg, #f8fcfb, #f2f7f4);
  font-size: 13px;
  white-space: nowrap;
}
.metric-card .el-icon {
  color: #0f9f8f;
}
.metric-copy {
  color: #102a43;
  font-weight: 800;
}
.metric-card-wide {
  background: linear-gradient(180deg, #fffaf0, #f7fbf8);
}
.ctrl-row {
  display: flex;
  gap: 10px;
  align-items: center;
  flex-wrap: wrap;
  padding: 6px 0;
}
.ctrl-row + .ctrl-row {
  border-top: 1px solid #e5edf0;
  margin-top: 10px;
  padding-top: 12px;
}
.main-controls {
  align-items: flex-end;
}
.ctrl-group {
  display: flex;
  align-items: center;
  gap: 7px;
}
.ctrl-group label {
  font-size: 13px;
  color: #5f7180;
  font-weight: 600;
}
.select-meteorology {
  width: 220px;
}
.simulation-actions {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}
.simulation-actions .el-button + .el-button {
  margin-left: 0;
}
.dropdown-icon {
  margin-left: 4px;
}
.spacer {
  flex: 1;
}
.map-wrapper {
  flex: 1;
  min-height: 0;
  border: 1px solid #cfdde4;
  border-radius: 8px;
  overflow: hidden;
  position: relative;
  background: #eef3f4;
  box-shadow: 0 10px 30px rgba(18, 47, 61, 0.08);
}
.legend-overlay {
  position: absolute;
  right: 12px;
  bottom: 12px;
  z-index: 1000;
}

@media (max-width: 980px) {
  .dashboard {
    height: auto;
    min-height: calc(100vh - 96px);
  }
  .metric-strip {
    grid-template-columns: repeat(2, minmax(132px, 1fr));
  }
  .select-meteorology {
    width: 100%;
  }
  .ctrl-group {
    flex: 1 1 190px;
  }
  .map-wrapper {
    height: 520px;
    flex: 0 0 520px;
  }
}
</style>
