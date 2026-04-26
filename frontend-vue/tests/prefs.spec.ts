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
    expect(p.opacity).toBeCloseTo(0.7)
    expect(p.renderScale).toBe(2)
    expect(p.tileLayer).toBe('street')
    expect(p.customMin).toBeNull()
    expect(p.customMax).toBeNull()
  })

  it('从 localStorage 恢复持久化值', () => {
    localStorage.setItem(
      PREFS_STORAGE_KEY,
      JSON.stringify({ scale: 'viridis', opacity: 0.3, tileLayer: 'satellite' }),
    )
    setActivePinia(createPinia())
    const p = usePrefsStore()
    expect(p.scale).toBe('viridis')
    expect(p.opacity).toBe(0.3)
    expect(p.tileLayer).toBe('satellite')
  })

  it('修改字段会同步写入 localStorage', async () => {
    const p = usePrefsStore()
    p.scale = 'turbo'
    p.opacity = 0.9
    await nextTick()
    const stored = JSON.parse(localStorage.getItem(PREFS_STORAGE_KEY)!)
    expect(stored.scale).toBe('turbo')
    expect(stored.opacity).toBe(0.9)
  })

  it('reset 清除 localStorage 并恢复默认', async () => {
    const p = usePrefsStore()
    p.scale = 'turbo'
    await nextTick()
    p.reset()
    expect(p.scale).toBe('jet')
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
