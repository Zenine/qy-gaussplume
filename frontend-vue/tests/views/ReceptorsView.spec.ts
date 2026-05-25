import { mount, flushPromises } from '@vue/test-utils'
import ElementPlus from 'element-plus'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import ReceptorsView from '@/views/ReceptorsView.vue'
import { receptorsApi } from '@/api'
import type { Receptor } from '@/types'

const sampleReceptors: Receptor[] = [
  {
    id: 1,
    name: '学校',
    latitude: 39.9,
    longitude: 116.4,
    height: 1.5,
    markerSymbol: 'monitor',
    markerColor: '#2196F3',
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  },
  {
    id: 2,
    name: '医院',
    latitude: 39.91,
    longitude: 116.41,
    height: 2.0,
    markerSymbol: 'monitor',
    markerColor: '#4CAF50',
    isActive: false,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  },
]

function mountView() {
  return mount(ReceptorsView, {
    global: { plugins: [ElementPlus] },
    attachTo: document.body,
  })
}

beforeEach(() => {
  vi.spyOn(receptorsApi, 'list').mockResolvedValue(sampleReceptors)
})

afterEach(() => {
  vi.restoreAllMocks()
  document.body.innerHTML = ''
})

describe('ReceptorsView', () => {
  it('使用统一的数据管理页面骨架', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('.table-page.receptors-page').exists()).toBe(true)
    expect(wrapper.find('.page-toolbar').exists()).toBe(true)
    expect(wrapper.find('.table-shell').exists()).toBe(true)
  })

  it('挂载后加载并渲染受体列表', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(receptorsApi.list).toHaveBeenCalled()
    expect(wrapper.text()).toContain('学校')
    expect(wrapper.text()).toContain('医院')
  })

  it('点击新增按钮会打开弹窗', async () => {
    const wrapper = mountView()
    await flushPromises()

    const btn = wrapper.findAll('button').find((b) => b.text().includes('新增受体点'))
    expect(btn).toBeDefined()
    await btn!.trigger('click')
    await flushPromises()

    const dialog = document.querySelector('.el-dialog')
    expect(dialog).not.toBeNull()
    expect(dialog!.textContent).toContain('新增受体点')
  })

  it('刷新按钮触发 list API', async () => {
    const wrapper = mountView()
    await flushPromises()
    vi.mocked(receptorsApi.list).mockClear()

    const refreshBtn = wrapper.findAll('button').find((b) => b.text().includes('刷新'))
    await refreshBtn!.trigger('click')
    await flushPromises()

    expect(receptorsApi.list).toHaveBeenCalledTimes(1)
  })
})
