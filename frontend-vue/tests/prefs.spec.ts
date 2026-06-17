import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it } from 'vitest'
import { PREFS_STORAGE_KEY, usePrefsStore } from '@/stores/prefs'
import { nextTick } from 'vue'

describe('usePrefsStore', () => {
  beforeEach(() => {
    localStorage.clear()
    setActivePinia(createPinia())
  })

  it('默认值来自 loadInitial', () => {
    const p = usePrefsStore()
    expect(p.scale).toBe('jet')
    expect(p.opacity).toBeCloseTo(0.85)
    expect(p.renderScale).toBe(2)
    expect(p.heatmapDisplayMode).toBe('plume')
    expect(p.tileLayer).toBe('street')
    expect(p.domainSize).toBe(5000)
    expect(p.mapCenter).toBeNull()
    expect(p.mapZoom).toBeNull()
    expect(p.simulationHeight).toBe(0)
    expect(p.customMin).toBeNull()
    expect(p.customMax).toBeNull()
  })

  it('从 localStorage 恢复持久化值', () => {
    localStorage.setItem(
      PREFS_STORAGE_KEY,
      JSON.stringify({
        scale: 'viridis',
        opacity: 0.3,
        tileLayer: 'satellite',
        simulationHeight: 12,
        mapCenter: [30.5, 120.5],
        mapZoom: 13,
        heatmapDisplayMode: 'continuous',
      }),
    )
    setActivePinia(createPinia())
    const p = usePrefsStore()
    expect(p.scale).toBe('viridis')
    expect(p.opacity).toBe(0.3)
    expect(p.heatmapDisplayMode).toBe('continuous')
    expect(p.tileLayer).toBe('satellite')
    expect(p.simulationHeight).toBe(12)
    expect(p.mapCenter).toEqual([30.5, 120.5])
    expect(p.mapZoom).toBe(13)
  })

  it('修改字段会同步写入 localStorage', async () => {
    const p = usePrefsStore()
    p.scale = 'turbo'
    p.opacity = 0.9
    p.heatmapDisplayMode = 'continuous'
    p.simulationHeight = 18
    p.mapCenter = [30.5, 120.5]
    p.mapZoom = 13
    await nextTick()
    const stored = JSON.parse(localStorage.getItem(PREFS_STORAGE_KEY)!)
    expect(stored.scale).toBe('turbo')
    expect(stored.opacity).toBe(0.9)
    expect(stored.heatmapDisplayMode).toBe('continuous')
    expect(stored.simulationHeight).toBe(18)
    expect(stored.mapCenter).toEqual([30.5, 120.5])
    expect(stored.mapZoom).toBe(13)
  })

  it('reset 清除 localStorage 并恢复默认', async () => {
    const p = usePrefsStore()
    p.scale = 'turbo'
    await nextTick()
    expect(localStorage.getItem(PREFS_STORAGE_KEY)).not.toBeNull()

    p.reset()
    await nextTick()

    expect(p.scale).toBe('jet')
    expect(p.heatmapDisplayMode).toBe('plume')
    expect(p.domainSize).toBe(5000)
    expect(p.mapCenter).toBeNull()
    expect(p.mapZoom).toBeNull()
    expect(p.simulationHeight).toBe(0)
    expect(localStorage.getItem(PREFS_STORAGE_KEY)).toBeNull()

    p.scale = 'viridis'
    await nextTick()
    const stored = JSON.parse(localStorage.getItem(PREFS_STORAGE_KEY)!)
    expect(stored.scale).toBe('viridis')
  })

  it('含未知字段的旧存储不破坏加载', () => {
    localStorage.setItem(
      PREFS_STORAGE_KEY,
      JSON.stringify({ scale: 'jet', unknownLegacy: 'foo' }),
    )
    setActivePinia(createPinia())
    const p = usePrefsStore()
    expect(p.scale).toBe('jet')
  })
})
