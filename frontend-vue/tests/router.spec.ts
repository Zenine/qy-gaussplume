import { describe, expect, it } from 'vitest'
import router from '@/router'

describe('router', () => {
  it('/ 重定向到 /dashboard', () => {
    const root = router.getRoutes().find((r) => r.path === '/')
    expect(root?.redirect).toBe('/dashboard')
  })

  it('所有主要路由都存在', () => {
    const names = router.getRoutes().map((r) => r.name).filter(Boolean)
    expect(names).toEqual(
      expect.arrayContaining(['dashboard', 'sources', 'receptors', 'meteorology']),
    )
  })
})
