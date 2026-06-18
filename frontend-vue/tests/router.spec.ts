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

  it('浏览器标题固定使用 GNN 品牌，不随路由变化', async () => {
    await router.push('/dashboard')
    await router.isReady()
    expect(document.title).toBe('GNN-GaussPlume')

    await router.push('/sources')
    expect(document.title).toBe('GNN-GaussPlume')
  })
})
