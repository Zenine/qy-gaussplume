import { defineStore } from 'pinia'
import { nextTick, ref, watch } from 'vue'
import type { ColorScale } from '@/utils/colorScale'
import type { HeatmapDisplayMode } from '@/composables/useHeatmapRenderer'

// 持久化到 localStorage 的用户偏好（每次修改即同步）。
// 只保存 UI/可视化配置，不保存业务数据。
const STORAGE_KEY = 'gnn.prefs.v1'

interface PersistedPrefs {
  scale: ColorScale
  opacity: number
  renderScale: number
  heatmapDisplayMode: HeatmapDisplayMode
  tileLayer: 'street' | 'satellite' | 'hybrid'
  selectedPollutant: string
  gridResolution: number
  domainSize: number
  simulationHeight: number
  mapCenter: [number, number] | null
  mapZoom: number | null
  customMin: number | null
  customMax: number | null
  useLogScale: boolean
}

function loadInitial(): PersistedPrefs {
  const defaults: PersistedPrefs = {
    scale: 'jet',
    opacity: 0.85,
    renderScale: 2,
    heatmapDisplayMode: 'plume',
    tileLayer: 'street',
    selectedPollutant: '',
    gridResolution: 100,
    domainSize: 5000,
    simulationHeight: 0,
    mapCenter: null,
    mapZoom: null,
    customMin: null,
    customMax: null,
    useLogScale: false,
  }
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return defaults
    const parsed = JSON.parse(raw)
    return { ...defaults, ...parsed }
  } catch {
    return defaults
  }
}

export const usePrefsStore = defineStore('prefs', () => {
  const initial = loadInitial()
  let persistEnabled = true
  const scale = ref<ColorScale>(initial.scale)
  const opacity = ref(initial.opacity)
  const renderScale = ref(initial.renderScale)
  const heatmapDisplayMode = ref<HeatmapDisplayMode>(initial.heatmapDisplayMode)
  const tileLayer = ref(initial.tileLayer)
  const selectedPollutant = ref(initial.selectedPollutant)
  const gridResolution = ref(initial.gridResolution)
  const domainSize = ref(initial.domainSize)
  const simulationHeight = ref(initial.simulationHeight)
  const mapCenter = ref<[number, number] | null>(initial.mapCenter)
  const mapZoom = ref<number | null>(initial.mapZoom)
  const customMin = ref<number | null>(initial.customMin)
  const customMax = ref<number | null>(initial.customMax)
  const useLogScale = ref(initial.useLogScale)

  // 任一字段变化即同步到 localStorage
  watch(
    [
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
      useLogScale,
    ],
    () => {
      if (!persistEnabled) return
      try {
        const payload: PersistedPrefs = {
          scale: scale.value,
          opacity: opacity.value,
          renderScale: renderScale.value,
          heatmapDisplayMode: heatmapDisplayMode.value,
          tileLayer: tileLayer.value,
          selectedPollutant: selectedPollutant.value,
          gridResolution: gridResolution.value,
          domainSize: domainSize.value,
          simulationHeight: simulationHeight.value,
          mapCenter: mapCenter.value,
          mapZoom: mapZoom.value,
          customMin: customMin.value,
          customMax: customMax.value,
          useLogScale: useLogScale.value,
        }
        localStorage.setItem(STORAGE_KEY, JSON.stringify(payload))
      } catch {
        // 配额满 / 隐私模式 → 忽略
      }
    },
    { deep: true },
  )

  function reset() {
    persistEnabled = false
    localStorage.removeItem(STORAGE_KEY)
    const def = loadInitial()
    scale.value = def.scale
    opacity.value = def.opacity
    renderScale.value = def.renderScale
    heatmapDisplayMode.value = def.heatmapDisplayMode
    tileLayer.value = def.tileLayer
    selectedPollutant.value = def.selectedPollutant
    gridResolution.value = def.gridResolution
    domainSize.value = def.domainSize
    simulationHeight.value = def.simulationHeight
    mapCenter.value = def.mapCenter
    mapZoom.value = def.mapZoom
    customMin.value = def.customMin
    customMax.value = def.customMax
    useLogScale.value = def.useLogScale
    void nextTick(() => {
      persistEnabled = true
    })
  }

  return {
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
    useLogScale,
    reset,
  }
})

export const PREFS_STORAGE_KEY = STORAGE_KEY
