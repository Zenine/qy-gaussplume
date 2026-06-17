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

  it('页面标题使用 GNN 品牌', async () => {
    await router.push('/dashboard')
    await router.isReady()
    expect(document.title).toBe('主控台 - GNN-GaussPlume')
  })
})
