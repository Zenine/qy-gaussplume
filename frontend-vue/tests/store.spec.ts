import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAppStore } from '@/stores/app'

describe('useAppStore', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('默认 sidebar 展开', () => {
    const s = useAppStore()
    expect(s.sidebarCollapsed).toBe(false)
  })

  it('toggleSidebar 切换折叠态', () => {
    const s = useAppStore()
    s.toggleSidebar()
    expect(s.sidebarCollapsed).toBe(true)
    s.toggleSidebar()
    expect(s.sidebarCollapsed).toBe(false)
  })
})
