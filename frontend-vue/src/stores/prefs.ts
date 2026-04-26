import { defineStore } from 'pinia'
import { ref, watch } from 'vue'
import type { ColorScale } from '@/utils/colorScale'

// 持久化到 localStorage 的用户偏好（每次修改即同步）。
// 只保存 UI/可视化配置，不保存业务数据。
const STORAGE_KEY = 'gnn.prefs.v1'

interface PersistedPrefs {
  scale: ColorScale
  opacity: number
  renderScale: number
  tileLayer: 'street' | 'satellite' | 'hybrid'
  selectedPollutant: string
  gridResolution: number
  domainSize: number
  customMin: number | null
  customMax: number | null
  useLogScale: boolean
}

function loadInitial(): PersistedPrefs {
  const defaults: PersistedPrefs = {
    scale: 'jet',
    opacity: 0.7,
    renderScale: 2,
    tileLayer: 'street',
    selectedPollutant: '',
    gridResolution: 100,
    domainSize: 10000,
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
  const scale = ref<ColorScale>(initial.scale)
  const opacity = ref(initial.opacity)
  const renderScale = ref(initial.renderScale)
  const tileLayer = ref(initial.tileLayer)
  const selectedPollutant = ref(initial.selectedPollutant)
  const gridResolution = ref(initial.gridResolution)
  const domainSize = ref(initial.domainSize)
  const customMin = ref<number | null>(initial.customMin)
  const customMax = ref<number | null>(initial.customMax)
  const useLogScale = ref(initial.useLogScale)

  // 任一字段变化即同步到 localStorage
  watch(
    [
      scale,
      opacity,
      renderScale,
      tileLayer,
      selectedPollutant,
      gridResolution,
      domainSize,
      customMin,
      customMax,
      useLogScale,
    ],
    () => {
      try {
        const payload: PersistedPrefs = {
          scale: scale.value,
          opacity: opacity.value,
          renderScale: renderScale.value,
          tileLayer: tileLayer.value,
          selectedPollutant: selectedPollutant.value,
          gridResolution: gridResolution.value,
          domainSize: domainSize.value,
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
    const d = loadInitial.call(null)
    // 强制读默认（忽略已存储的值）
    localStorage.removeItem(STORAGE_KEY)
    const def = loadInitial()
    scale.value = def.scale
    opacity.value = def.opacity
    renderScale.value = def.renderScale
    tileLayer.value = def.tileLayer
    selectedPollutant.value = def.selectedPollutant
    gridResolution.value = def.gridResolution
    domainSize.value = def.domainSize
    customMin.value = def.customMin
    customMax.value = def.customMax
    useLogScale.value = def.useLogScale
    return d
  }

  return {
    scale,
    opacity,
    renderScale,
    tileLayer,
    selectedPollutant,
    gridResolution,
    domainSize,
    customMin,
    customMax,
    useLogScale,
    reset,
  }
})

export const PREFS_STORAGE_KEY = STORAGE_KEY
