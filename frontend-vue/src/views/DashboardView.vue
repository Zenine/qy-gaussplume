<script setup lang="ts">
import { computed, onMounted, ref, shallowRef, watch } from 'vue'
import { ElMessage } from 'element-plus'
import {
  Aim,
  Brush,
  Close,
  Compass,
  Delete,
  Document,
  Histogram,
  MagicStick,
  VideoPlay,
} from '@element-plus/icons-vue'
import { storeToRefs } from 'pinia'
import { mapApi, meteorologyApi, receptorsApi, simulationApi, sourcesApi } from '@/api'
import type {
  EmissionSource,
  Meteorology,
  ParallelSimulationRequest,
  ParallelSimulationResult,
  Receptor,
  SimulationResult,
} from '@/types'
import MapPanel from '@/components/MapPanel.vue'
import ColorLegend from '@/components/ColorLegend.vue'
import ContributionPanel from '@/components/ContributionPanel.vue'
import FormulaDrawer from '@/components/FormulaDrawer.vue'
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
type LastSimulationInputs =
  | {
    mode: 'single'
    meteorologyId: number
    windSpeed: number
    windDirection: number
    gridResolution: number
    domainSize: number
    receptorHeight: number
    calculationPollutant: string
  }
  | {
    mode: 'parallel'
    meteorologyId: number
    windSpeed: number
    windDirections: number[]
    weights?: number[]
    gridResolution: number
    domainSize: number
    receptorHeight: number
    calculationPollutant: string
  }

const SIMULATION_RESULT_STORAGE_KEY = 'gnn.simulationResult.v1'
const MAX_PERSISTED_RESULT_BYTES = 10 * 1024 * 1024

const lastSimulationInputs = ref<LastSimulationInputs | null>(null)

const showContribution = ref(false)
const showFormula = ref(false)
const showParallel = ref(false)
const selectionEnabled = ref(false)
const selectionBounds = ref<SelectionBounds | null>(null)
const selectedRankingReceptor = ref('')
const selectedRankingPollutant = ref('')
const calculationPollutant = ref('')
const boundaryEnabled = ref(false)
const boundaryLoading = ref(false)
const boundaryGeoJson = shallowRef<unknown | null>(null)
const simulationMode = ref<'single' | 'parallel'>('single')
const parallelDirectionCount = ref<8 | 16 | 32 | 64 | 72>(16)
const parallelWindSpeed = ref(3.0)

// ---------- 偏好（持久化） ----------
const prefs = usePrefsStore()
const {
  scale,
  opacity,
  renderScale,
  heatmapDisplayMode,
  tileLayer,
  selectedPollutant,
  gridResolution,
  domainSize,
  simulationHeight,
  mapCenter,
  mapZoom,
  customMin,
  customMax,
} = storeToRefs(prefs)

// ---------- 气象控制 ----------
const draftWindDirection = ref(0)
const draftWindSpeed = ref(0.1)
const windPointer = computed(() => {
  const center = 75
  const radius = 44
  const radians = (draftWindDirection.value * Math.PI) / 180
  const x = center + Math.sin(radians) * radius
  const y = center - Math.cos(radians) * radius
  return {
    x: x.toFixed(2),
    y: y.toFixed(2),
  }
})

const selectedMeteorology = computed(
  () => meteorologies.value.find((m) => m.id === selectedMeteorologyId.value) ?? null,
)

const weatherDirty = computed(() => {
  const met = selectedMeteorology.value
  if (!met) return false
  return draftWindDirection.value !== met.windDirection || draftWindSpeed.value !== met.windSpeed
})

function isSupportedDirectionCount(value: number): value is 8 | 16 | 32 | 64 | 72 {
  return [8, 16, 32, 64, 72].includes(value)
}

function sameNumberArray(a: number[], b: number[]) {
  return a.length === b.length && a.every((value, index) => value === b[index])
}

const resultWeatherOutdated = computed(() => {
  const last = lastSimulationInputs.value
  if (!result.value || !last || !selectedMeteorologyId.value) return false
  if (last.mode === 'parallel') {
    return (
      selectedMeteorologyId.value !== last.meteorologyId
      || parallelWindSpeed.value !== last.windSpeed
      || !sameNumberArray(parallelWindDirections.value, last.windDirections)
    )
  }
  return (
    selectedMeteorologyId.value !== last.meteorologyId
    || draftWindSpeed.value !== last.windSpeed
    || draftWindDirection.value !== last.windDirection
  )
})

const resultGridOutdated = computed(() => {
  const last = lastSimulationInputs.value
  if (!result.value || !last) return false
  return (
    gridResolution.value !== last.gridResolution
    || domainSize.value !== last.domainSize
    || simulationHeight.value !== last.receptorHeight
    || calculationPollutant.value !== last.calculationPollutant
  )
})

const resultParametersOutdated = computed(() => resultWeatherOutdated.value || resultGridOutdated.value)

watch(
  selectedMeteorology,
  (met) => {
    if (!met) return
    const last = lastSimulationInputs.value
    if (result.value && last?.meteorologyId === met.id) {
      if (last.mode === 'single') {
        draftWindDirection.value = last.windDirection
        draftWindSpeed.value = last.windSpeed
      } else {
        parallelWindSpeed.value = last.windSpeed
        if (isSupportedDirectionCount(last.windDirections.length)) {
          parallelDirectionCount.value = last.windDirections.length
        }
      }
      return
    }
    draftWindDirection.value = met.windDirection
    draftWindSpeed.value = met.windSpeed
    parallelWindSpeed.value = met.windSpeed
  },
  { immediate: true },
)

const parallelWindDirections = computed(() =>
  Array.from({ length: parallelDirectionCount.value }, (_, i) => (360 / parallelDirectionCount.value) * i),
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
  rankingRows.value.slice(0, 10),
)

const rankingTotalConcentration = computed(() =>
  rankingRows.value.reduce((sum, row) => sum + row.concentration, 0),
)

const isAllPollutantRanking = computed(() => !selectedPollutant.value)

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
  return (
    displayedResult.value.receptorContributions[selectedRankingReceptor.value]?.[
      selectedRankingPollutant.value
    ] ?? []
  ).filter((row) => row.concentration > 0)
})

const pollutantSummaryCards = computed(() => {
  if (!result.value) return []
  return Object.entries(result.value.receptorContributions)
    .map(([receptorName, byPollutant]) => {
      const rows = Object.entries(byPollutant)
        .map(([pollutant, contributions]) => ({
          pollutant,
          concentration: contributions.reduce(
            (sum, row) => sum + (row.concentration > 0 ? row.concentration : 0),
            0,
          ),
        }))
        .filter((row) => row.concentration > 0)
        .sort((a, b) => b.concentration - a.concentration)
      const total = rows.reduce((sum, row) => sum + row.concentration, 0)
      return {
        receptorName,
        total,
        rows: rows.map((row) => ({
          ...row,
          percentage: total > 0 ? row.concentration / total * 100 : 0,
        })),
      }
    })
    .filter((card) => card.total > 0)
    .sort((a, b) => b.total - a.total)
})

function contributionTotalFor(receptorName: string, pollutant: string) {
  return (
    displayedResult.value?.receptorContributions[receptorName]?.[pollutant] ?? []
  ).reduce((sum, row) => sum + (row.concentration > 0 ? row.concentration : 0), 0)
}

function bestReceptorFor(pollutant: string, names: string[]) {
  let best = ''
  let bestTotal = 0
  for (const name of names) {
    const total = contributionTotalFor(name, pollutant)
    if (total > bestTotal) {
      best = name
      bestTotal = total
    }
  }
  return best
}

function persistSimulationResult() {
  if (!result.value) return
  try {
    const payload = JSON.stringify({
      result: result.value,
      selectedPollutant: selectedPollutant.value,
      selectedRankingReceptor: selectedRankingReceptor.value,
      selectedRankingPollutant: selectedRankingPollutant.value,
      lastSimulationInputs: lastSimulationInputs.value,
    })
    if (payload.length > MAX_PERSISTED_RESULT_BYTES) {
      localStorage.removeItem(SIMULATION_RESULT_STORAGE_KEY)
      return
    }
    localStorage.setItem(SIMULATION_RESULT_STORAGE_KEY, payload)
  } catch {
    // localStorage 配额或隐私模式不可用时，不影响当前模拟结果。
  }
}

function restoreSimulationResult() {
  try {
    const raw = localStorage.getItem(SIMULATION_RESULT_STORAGE_KEY)
    if (!raw) return
    const parsed = JSON.parse(raw)
    if (!parsed?.result?.concentrations || !parsed?.result?.gridLat || !parsed?.result?.gridLon) return
    result.value = parsed.result
    selectedPollutant.value = parsed.selectedPollutant ?? selectedPollutant.value
    selectedRankingReceptor.value = parsed.selectedRankingReceptor ?? ''
    selectedRankingPollutant.value = parsed.selectedRankingPollutant ?? ''
    lastSimulationInputs.value = parsed.lastSimulationInputs ?? null
    if (lastSimulationInputs.value) {
      simulationMode.value = lastSimulationInputs.value.mode
      if (lastSimulationInputs.value.mode === 'single') {
        draftWindDirection.value = lastSimulationInputs.value.windDirection
        draftWindSpeed.value = lastSimulationInputs.value.windSpeed
      } else {
        parallelWindSpeed.value = lastSimulationInputs.value.windSpeed
        if (isSupportedDirectionCount(lastSimulationInputs.value.windDirections.length)) {
          parallelDirectionCount.value = lastSimulationInputs.value.windDirections.length
        }
      }
    }
  } catch {
    localStorage.removeItem(SIMULATION_RESULT_STORAGE_KEY)
  }
}

async function onBoundaryEnabledChange() {
  if (!boundaryEnabled.value || boundaryGeoJson.value) return
  boundaryLoading.value = true
  try {
    boundaryGeoJson.value = await mapApi.getGeoJson(true)
  } catch (e) {
    boundaryEnabled.value = false
    ElMessage.error(errorMessage(e, '加载行政边界失败'))
  } finally {
    boundaryLoading.value = false
  }
}

function onMapViewChange(payload: { center: [number, number]; zoom: number }) {
  mapCenter.value = payload.center
  mapZoom.value = payload.zoom
}

watch(
  displayedResult,
  (value) => {
    const names = value ? Object.keys(value.receptorContributions) : []
    const preferredPollutant = selectedPollutant.value
    const preferredReceptor = preferredPollutant
      ? bestReceptorFor(preferredPollutant, names)
      : ''
    selectedRankingReceptor.value = preferredReceptor
      || (names.includes(selectedRankingReceptor.value) ? selectedRankingReceptor.value : names[0] ?? '')
    const pollutants = selectedRankingReceptor.value
      ? Object.keys(value?.receptorContributions[selectedRankingReceptor.value] ?? {})
      : []
    if (!preferredPollutant) {
      selectedRankingPollutant.value = ''
      return
    }
    selectedRankingPollutant.value = pollutants.includes(preferredPollutant)
      ? selectedPollutant.value
      : pollutants.includes(selectedRankingPollutant.value)
        ? selectedRankingPollutant.value
        : pollutants[0] ?? ''
  },
  { immediate: true },
)

watch(selectedPollutant, (pollutant) => {
  if (!pollutant) {
    selectedRankingPollutant.value = ''
    return
  }
  const names = receptorContributionNames.value
  const preferredReceptor = bestReceptorFor(pollutant, names)
  if (preferredReceptor) selectedRankingReceptor.value = preferredReceptor
  const pollutants = rankingPollutants.value
  if (pollutants.includes(pollutant)) {
    selectedRankingPollutant.value = pollutant
  }
})

watch(selectedRankingPollutant, (pollutant) => {
  if (selectedPollutant.value !== pollutant) {
    selectedPollutant.value = pollutant
  }
})

watch(selectedRankingReceptor, () => {
  if (!selectedPollutant.value) {
    selectedRankingPollutant.value = ''
    return
  }
  const pollutants = rankingPollutants.value
  selectedRankingPollutant.value = pollutants.includes(selectedPollutant.value)
    ? selectedPollutant.value
    : pollutants.includes(selectedRankingPollutant.value)
      ? selectedRankingPollutant.value
      : pollutants[0] ?? ''
})

watch(boundaryEnabled, () => {
  void onBoundaryEnabledChange()
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
  running.value = true
  try {
    const sourceIds = selectionBounds.value ? effectiveSources.value.map((s) => s.id) : undefined
    const receptorIds = selectionBounds.value ? effectiveReceptors.value.map((r) => r.id) : undefined
    const r = await simulationApi.run({
      meteorologyId: selectedMeteorologyId.value,
      sourceIds,
      receptorIds,
      pollutantType: calculationPollutant.value || undefined,
      windSpeed: draftWindSpeed.value,
      windDirection: draftWindDirection.value,
      gridResolution: gridResolution.value,
      domainSize: domainSize.value,
      receptorHeight: simulationHeight.value,
    })
    if (r.availablePollutants?.length) {
      selectedPollutant.value = calculationPollutant.value
        && r.availablePollutants.includes(calculationPollutant.value)
        ? calculationPollutant.value
        : selectedPollutant.value && r.availablePollutants.includes(selectedPollutant.value)
          ? selectedPollutant.value
          : r.availablePollutants[0]
    }
    result.value = r
    lastSimulationInputs.value = {
      mode: 'single',
      meteorologyId: selectedMeteorologyId.value,
      windSpeed: draftWindSpeed.value,
      windDirection: draftWindDirection.value,
      gridResolution: gridResolution.value,
      domainSize: domainSize.value,
      receptorHeight: simulationHeight.value,
      calculationPollutant: calculationPollutant.value,
    }
    persistSimulationResult()
    ElMessage.success('模拟完成')
    mapRef.value?.fitBounds()
  } catch (e) {
    ElMessage.error(errorMessage(e, '模拟失败'))
  } finally {
    running.value = false
  }
}

async function runParallelSimulation() {
  if (!selectedMeteorologyId.value) {
    ElMessage.warning('请先选择气象场')
    return
  }
  if (effectiveSources.value.length === 0) {
    ElMessage.warning(selectionBounds.value ? '选择区域内没有排放源' : '请先添加排放源')
    return
  }
  running.value = true
  try {
    const sourceIds = selectionBounds.value ? effectiveSources.value.map((s) => s.id) : undefined
    const receptorIds = selectionBounds.value ? effectiveReceptors.value.map((r) => r.id) : undefined
    const request: ParallelSimulationRequest = {
      meteorologyId: selectedMeteorologyId.value,
      sourceIds,
      receptorIds,
      pollutantType: calculationPollutant.value || undefined,
      windSpeed: parallelWindSpeed.value,
      windDirections: parallelWindDirections.value,
      gridResolution: gridResolution.value,
      domainSize: domainSize.value,
      receptorHeight: simulationHeight.value,
      returnAggregatedOnly: true,
    }
    const r = await simulationApi.runParallel(request)
    onParallelCompleted(r, request)
    ElMessage.success('全局模拟完成')
  } catch (e) {
    ElMessage.error(errorMessage(e, '全局模拟失败'))
  } finally {
    running.value = false
  }
}

function runCurrentSimulation() {
  if (simulationMode.value === 'parallel') {
    void runParallelSimulation()
    return
  }
  void runSimulation()
}

function clearResult() {
  result.value = null
  lastSimulationInputs.value = null
  localStorage.removeItem(SIMULATION_RESULT_STORAGE_KEY)
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

function parallelInputsFromRequest(request: ParallelSimulationRequest | undefined): LastSimulationInputs | null {
  const meteorologyId = request?.meteorologyId ?? selectedMeteorologyId.value
  if (!meteorologyId) return null
  return {
    mode: 'parallel',
    meteorologyId,
    windSpeed: request?.windSpeed ?? draftWindSpeed.value,
    windDirections: request?.windDirections ?? [],
    weights: request?.weights,
    gridResolution: request?.gridResolution ?? gridResolution.value,
    domainSize: request?.domainSize ?? domainSize.value,
    receptorHeight: request?.receptorHeight ?? simulationHeight.value,
    calculationPollutant: request?.pollutantType ?? calculationPollutant.value,
  }
}

function onParallelCompleted(r: ParallelSimulationResult, request?: ParallelSimulationRequest) {
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
  if (r.availablePollutants?.length) {
    selectedPollutant.value = request?.pollutantType && r.availablePollutants.includes(request.pollutantType)
      ? request.pollutantType
      : selectedPollutant.value && r.availablePollutants.includes(selectedPollutant.value)
        ? selectedPollutant.value
        : r.availablePollutants[0]
  }
  calculationPollutant.value = request?.pollutantType ?? ''
  lastSimulationInputs.value = parallelInputsFromRequest(request)
  persistSimulationResult()
  mapRef.value?.fitBounds()
}

onMounted(() => {
  restoreSimulationResult()
  void loadAll()
})
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
      :heatmap-display-mode="heatmapDisplayMode"
      :min="effectiveMin"
      :max="effectiveMax"
      :render-scale="renderScale"
      :tile-layer="tileLayer"
      :selection-enabled="selectionEnabled"
      :boundary-geo-json="boundaryEnabled ? boundaryGeoJson : null"
      :initial-center="mapCenter"
      :initial-zoom="mapZoom"
      @selection-change="onSelectionChange"
      @view-change="onMapViewChange"
    />

    <div class="floating-toolbar" data-test="floating-toolbar">
      <el-select v-model="tileLayer" size="small" class="toolbar-select">
        <el-option value="street" label="高德街道" />
        <el-option value="satellite" label="高德卫星" />
        <el-option value="hybrid" label="高德混合" />
      </el-select>
      <el-switch
        v-model="boundaryEnabled"
        data-test="boundary-layer-switch"
        size="small"
        active-text="行政边界"
        :loading="boundaryLoading"
      />
      <el-select v-model="selectedMeteorologyId" size="small" class="toolbar-wind">
        <el-option
          v-for="m in meteorologies"
          :key="m.id"
          :value="m.id"
          :label="`${m.name} - 风速:${m.windSpeed} 风向:${m.windDirection}°`"
        />
      </el-select>
      <el-radio-group
        v-model="simulationMode"
        data-test="simulation-mode-select"
        size="small"
        class="toolbar-mode"
      >
        <el-radio-button value="single">单风向</el-radio-button>
        <el-radio-button value="parallel">多风向</el-radio-button>
      </el-radio-group>
      <template v-if="simulationMode === 'parallel'">
        <el-select
          v-model="parallelDirectionCount"
          data-test="parallel-direction-count"
          size="small"
          class="toolbar-compact"
        >
          <el-option v-for="n in [8, 16, 32, 64, 72]" :key="n" :value="n" :label="`${n} 风向`" />
        </el-select>
        <el-input-number
          v-model="parallelWindSpeed"
          data-test="parallel-wind-speed"
          size="small"
          class="toolbar-speed"
          :min="0.1"
          :max="20"
          :step="0.1"
          controls-position="right"
        />
      </template>
      <el-select
        v-model="calculationPollutant"
        size="small"
        clearable
        placeholder="计算全部污染物"
        class="toolbar-select"
        data-test="calculation-pollutant-select"
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
        :class="{ 'run-attention': resultParametersOutdated }"
        @click="runCurrentSimulation"
      >
        {{ simulationMode === 'parallel' ? '运行全局模拟' : '运行模拟' }}
      </el-button>
      <el-button data-test="clear-result" size="small" :icon="Delete" @click="clearResult">
        清除结果
      </el-button>
      <el-button data-test="formula-info" size="small" :icon="Document" @click="showFormula = true">
        公式说明
      </el-button>
    </div>

    <div class="range-panel floating-card" data-test="range-panel">
      <div class="range-row">
        <span>模拟范围</span>
        <strong>{{ domainSizeKm }} km</strong>
      </div>
      <el-slider v-model="domainSizeKm" :min="5" :max="100" :step="5" />
      <div class="range-row">
        <span>网格分辨率</span>
        <strong>{{ gridResolution }} m</strong>
      </div>
      <el-slider v-model="gridResolution" :min="10" :max="500" :step="10" />
      <div class="range-row">
        <span>模拟高度</span>
        <strong>{{ simulationHeight }} m</strong>
      </div>
      <el-slider
        v-model="simulationHeight"
        data-test="simulation-height-slider"
        :min="0"
        :max="100"
        :step="1"
      />
    </div>

    <aside class="right-stack">
      <section v-if="!result" class="floating-card" data-test="draw-card">
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

      <section class="floating-card" data-test="weather-card">
        <div class="card-title">
          <span>气象控制</span>
          <el-icon><Compass /></el-icon>
        </div>
        <div class="wind-rose">
          <svg viewBox="0 0 150 150" role="img" aria-label="风向指示">
            <circle class="wind-ring" cx="75" cy="75" r="74" />
            <circle class="wind-ring" cx="75" cy="75" r="50" />
            <circle class="wind-ring" cx="75" cy="75" r="26" />
            <line class="wind-axis" x1="75" y1="1" x2="75" y2="149" />
            <line class="wind-axis" x1="1" y1="75" x2="149" y2="75" />
            <text x="75" y="20" text-anchor="middle">N</text>
            <text x="128" y="79" text-anchor="middle">E</text>
            <text x="75" y="136" text-anchor="middle">S</text>
            <text x="22" y="79" text-anchor="middle">W</text>
            <line
              class="wind-pointer"
              data-test="wind-direction-pointer"
              x1="75"
              y1="75"
              :x2="windPointer.x"
              :y2="windPointer.y"
            />
            <circle
              class="wind-pointer-tip"
              data-test="wind-direction-pointer-tip"
              cx="75"
              cy="75"
              r="6"
            />
          </svg>
        </div>
        <div class="field-grid">
          <label>
            来风方向 (°)
            <el-input-number v-model="draftWindDirection" size="small" :min="0" :max="360" :step="1" />
          </label>
          <label>
            风速 (m/s)
            <el-input-number v-model="draftWindSpeed" size="small" :min="0.1" :max="20" :step="0.1" />
          </label>
        </div>
        <p v-if="resultWeatherOutdated" class="hint warning">
          气象参数已修改，当前结果未更新，请点击运行模拟。
        </p>
        <p v-else-if="weatherDirty" class="hint warning">将使用当前临时风速和来风方向运行，不会覆盖已保存气象场。</p>
        <p v-else class="hint">外端指向风吹来的方向，运行模拟会使用当前风速和来风方向。</p>
      </section>

      <section v-if="!result" class="floating-card" data-test="stats-card">
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

      <template v-if="result">
        <section class="floating-card" data-test="result-card">
          <div class="card-title">
            <span>模拟结果</span>
            <span class="complete">完成</span>
          </div>
          <p v-if="resultParametersOutdated" class="hint warning">
            页面参数已变化，当前结果未更新，请重新模拟。
          </p>
          <label class="full-field">
            显示污染物
            <el-select
              v-model="selectedPollutant"
              data-test="top-pollutant-select"
              size="small"
              clearable
              placeholder="全部污染物"
            >
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
              <el-option value="blue" label="蓝色" />
              <el-option value="red" label="红色" />
              <el-option value="green" label="绿色" />
              <el-option value="purple" label="紫色" />
              <el-option value="thermal" label="Thermal" />
              <el-option value="rainbow" label="Rainbow" />
              <el-option value="spectral_r" label="Spectral R" />
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
              <el-slider v-model="opacity" :min="0" :max="1.2" :step="0.05" />
            </label>
            <label>
              扩散显示
              <el-select v-model="heatmapDisplayMode" size="small">
                <el-option value="plume" label="羽流突出" />
                <el-option value="continuous" label="连续低值" />
              </el-select>
            </label>
            <label>
              渲染精度
              <el-select v-model="renderScale" size="small">
                <el-option v-for="n in [1, 2, 4, 8, 12, 16]" :key="n" :value="n" :label="`${n}x`" />
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
            <span>空气站点污染源贡献排名</span>
            <el-button size="small" link :icon="Histogram" @click="showContribution = true">
              详情
            </el-button>
          </div>
          <div v-if="receptorContributionNames.length" class="ranking-controls">
            <label>
              选择空气站点
              <el-select v-model="selectedRankingReceptor" size="small" placeholder="空气站点">
                <el-option
                  v-for="name in receptorContributionNames"
                  :key="name"
                  :value="name"
                  :label="name"
                />
              </el-select>
            </label>
            <label>
              污染物指标
              <el-select
                v-model="selectedRankingPollutant"
                data-test="ranking-pollutant-select"
                clearable
                size="small"
                placeholder="全部污染物"
              >
                <el-option v-for="p in rankingPollutants" :key="p" :value="p" :label="p" />
              </el-select>
            </label>
          </div>
          <div v-if="isAllPollutantRanking" class="ranking-summary">
            全部污染物贡献摘要
          </div>
          <div v-else-if="selectedRankingReceptor && selectedRankingPollutant" class="ranking-summary">
            总贡献浓度：{{ rankingTotalConcentration.toFixed(4) }} µg/m³
          </div>
          <div v-if="isAllPollutantRanking" class="station-summary-list">
            <div
              v-for="card in pollutantSummaryCards"
              :key="card.receptorName"
              class="station-summary-card"
            >
              <div class="station-summary-title">
                <strong>{{ card.receptorName }}</strong>
                <span>总贡献 {{ card.total.toFixed(4) }} µg/m³</span>
              </div>
              <div v-for="row in card.rows" :key="row.pollutant" class="pollutant-summary-row">
                <span class="pollutant-name">{{ row.pollutant }}</span>
                <div class="ranking-bar" aria-hidden="true">
                  <span :style="{ width: `${Math.min(100, row.percentage)}%` }" />
                </div>
                <span class="ranking-percent">{{ row.percentage.toFixed(1) }}%</span>
                <strong>{{ row.concentration.toFixed(4) }} µg/m³</strong>
              </div>
            </div>
            <p v-if="pollutantSummaryCards.length === 0" class="hint">暂无空气站点污染物贡献摘要</p>
          </div>
          <div v-else class="ranking-list">
            <div
              v-for="(item, index) in rankedContributions"
              :key="item.sourceId"
              class="ranking-item"
            >
              <span class="ranking-index">{{ index + 1 }}</span>
              <div class="ranking-main">
                <div class="ranking-name">{{ item.sourceName }}</div>
                <div class="ranking-bar" aria-hidden="true">
                  <span :style="{ width: `${Math.min(100, item.percentage)}%` }" />
                </div>
              </div>
              <span class="ranking-percent">{{ item.percentage.toFixed(1) }}%</span>
              <strong>{{ item.concentration.toFixed(4) }} µg/m³</strong>
            </div>
            <p v-if="rankedContributions.length === 0" class="hint">暂无空气站点污染源贡献数据</p>
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
    <FormulaDrawer v-model:visible="showFormula" />

    <ParallelSimulationDialog
      v-model:visible="showParallel"
      :meteorologies="meteorologies"
      :selected-meteorology-id="selectedMeteorologyId"
      :grid-resolution="gridResolution"
      :domain-size="domainSize"
      :pollutant-type="calculationPollutant"
      :receptor-height="simulationHeight"
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

.toolbar-mode {
  flex: 0 0 auto;
}

.toolbar-compact {
  width: 96px;
}

.toolbar-speed {
  width: 108px;
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

.run-attention {
  box-shadow: 0 0 0 3px rgba(245, 158, 11, 0.2);
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
}

.wind-rose svg {
  display: block;
  width: 100%;
  height: 100%;
}

.wind-rose text {
  color: #64748b;
  fill: currentColor;
  font-size: 11px;
}

.wind-ring,
.wind-axis {
  fill: none;
  stroke: #dbe6ec;
  stroke-width: 1;
}

.wind-ring:nth-child(2),
.wind-ring:nth-child(3) {
  stroke: #e8eef2;
  stroke-width: 3;
}

.wind-pointer {
  stroke: #1677ff;
  stroke-linecap: round;
  stroke-width: 4;
}

.wind-pointer-tip {
  fill: #1677ff;
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
  grid-template-columns: minmax(0, 1fr) 112px;
  gap: 8px;
  margin-top: 12px;
}

.ranking-controls label {
  display: flex;
  min-width: 0;
  flex-direction: column;
  gap: 6px;
  color: #64748b;
  font-size: 12px;
}

.ranking-summary {
  margin-top: 10px;
  color: #64748b;
  font-size: 12px;
  text-align: right;
}

.station-summary-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
  margin-top: 12px;
}

.station-summary-card {
  padding: 10px;
  border-radius: 8px;
  background: #f5f7fa;
}

.station-summary-title {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 8px;
  padding-bottom: 8px;
  border-bottom: 1px solid #e5e7eb;
}

.station-summary-title strong {
  min-width: 0;
  overflow: hidden;
  color: #1677ff;
  font-size: 13px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.station-summary-title span {
  flex: none;
  color: #64748b;
  font-size: 11px;
}

.pollutant-summary-row {
  display: grid;
  grid-template-columns: 52px minmax(0, 1fr) 52px 92px;
  align-items: center;
  gap: 8px;
  padding-top: 8px;
  color: #31545c;
  font-size: 12px;
}

.pollutant-name {
  color: #64748b;
}

.ranking-item {
  display: grid;
  grid-template-columns: 20px minmax(0, 1fr) 52px 92px;
  align-items: center;
  gap: 8px;
  color: #31545c;
  font-size: 12px;
}

.ranking-index,
.ranking-percent {
  color: #7c8794;
}

.ranking-name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ranking-bar {
  height: 8px;
  margin-top: 5px;
  overflow: hidden;
  border-radius: 999px;
  background: #d1d5db;
}

.ranking-bar span {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: #1677ff;
}

.ranking-item strong {
  color: #1f2937;
  font-weight: 700;
  text-align: right;
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
