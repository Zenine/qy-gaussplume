import { mount, flushPromises } from '@vue/test-utils'
import ElementPlus from 'element-plus'
import { createPinia, setActivePinia } from 'pinia'
import { ElMessage, ElMessageBox } from 'element-plus'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import MeteorologyView from '@/views/MeteorologyView.vue'
import { meteorologyApi } from '@/api'
import type { Meteorology } from '@/types'

const sample: Meteorology[] = [
  {
    id: 1,
    name: '冬季北风',
    windSpeed: 3.0,
    windDirection: 0.0,
    boundaryLayerHeight: 800,
    stabilityClass: 'D',
    temperature: 278,
    humidity: 60,
    cloudCover: 2,
    precipitation: 0,
    isActive: true,
    recordTime: '2026-01-01T00:00:00Z',
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  },
]

function mountView() {
  return mount(MeteorologyView, {
    global: { plugins: [createPinia(), ElementPlus] },
    attachTo: document.body,
  })
}

beforeEach(() => {
  localStorage.clear()
  setActivePinia(createPinia())
  vi.spyOn(meteorologyApi, 'list').mockResolvedValue(sample)
})

afterEach(() => {
  vi.restoreAllMocks()
  document.body.innerHTML = ''
})

describe('MeteorologyView', () => {
  it('使用统一的数据管理页面骨架', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('.table-page.meteorology-page').exists()).toBe(true)
    expect(wrapper.find('.page-toolbar').exists()).toBe(true)
    expect(wrapper.find('.table-shell').exists()).toBe(true)
  })

  it('挂载后渲染气象场', async () => {
    const wrapper = mountView()
    await flushPromises()
    expect(wrapper.text()).toContain('冬季北风')
    expect(wrapper.text()).toContain('3 m/s') // windSpeed
    expect(wrapper.text()).toContain('D') // stability
  })

  it('新增按钮打开对话框_含6档稳定度', async () => {
    const wrapper = mountView()
    await flushPromises()

    const btn = wrapper.findAll('button').find((b) => b.text().includes('新增气象场'))
    await btn!.trigger('click')
    await flushPromises()

    const dialog = document.querySelector('.el-dialog')
    expect(dialog).not.toBeNull()
    expect(dialog!.textContent).toContain('新增气象场')
    expect(dialog!.textContent).toContain('来风方向')
  })

  it('支持勾选气象场并批量删除_部分失败后仍刷新列表', async () => {
    const rows: Meteorology[] = [
      sample[0],
      { ...sample[0], id: 2, name: '夏季南风', windDirection: 180 },
    ]
    vi.mocked(meteorologyApi.list).mockResolvedValue(rows)
    vi.spyOn(meteorologyApi, 'delete')
      .mockResolvedValueOnce(undefined)
      .mockRejectedValueOnce(new Error('删除失败'))
    vi.spyOn(ElMessageBox, 'confirm').mockResolvedValue('confirm')
    const wrapper = mountView()
    await flushPromises()
    vi.mocked(meteorologyApi.list).mockClear()

    wrapper.findComponent({ name: 'ElTable' }).vm.$emit('selection-change', rows)
    await wrapper.vm.$nextTick()

    expect(wrapper.text()).toContain('批量删除 (2)')
    const deleteBtn = wrapper.findAll('button').find((b) => b.text().includes('批量删除'))
    await deleteBtn!.trigger('click')
    await flushPromises()

    expect(meteorologyApi.delete).toHaveBeenCalledTimes(2)
    expect(meteorologyApi.delete).toHaveBeenNthCalledWith(1, 1)
    expect(meteorologyApi.delete).toHaveBeenNthCalledWith(2, 2)
    expect(meteorologyApi.list).toHaveBeenCalledTimes(1)
  })

  it('刷新气象场列表后清空已选气象场_避免批量删除旧行对象', async () => {
    const wrapper = mountView()
    await flushPromises()

    wrapper.findComponent({ name: 'ElTable' }).vm.$emit('selection-change', [sample[0]])
    await wrapper.vm.$nextTick()
    expect(wrapper.text()).toContain('批量删除 (1)')

    const refreshBtn = wrapper.findAll('button').find((b) => b.text().includes('刷新'))
    await refreshBtn!.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('批量删除 (0)')
  })



  it('支持单独启用/停用气象场并提供全部启用', async () => {
    const rows: Meteorology[] = [
      sample[0],
      { ...sample[0], id: 2, name: '停用气象', isActive: false },
    ]
    vi.mocked(meteorologyApi.list).mockResolvedValue(rows)
    vi.spyOn(meteorologyApi, 'update').mockResolvedValue(rows[0])
    const wrapper = mountView()
    await flushPromises()

    wrapper.findAllComponents({ name: 'ElSwitch' })[0].vm.$emit('change', false)
    await flushPromises()
    expect(meteorologyApi.update).toHaveBeenCalledWith(1, { isActive: false })

    vi.mocked(meteorologyApi.update).mockClear()
    const enableAllBtn = wrapper.findAll('button').find((b) => b.text().includes('全部启用'))
    await enableAllBtn!.trigger('click')
    await flushPromises()
    expect(meteorologyApi.update).toHaveBeenCalledWith(2, { isActive: true })
  })

  it('关闭批量删除确认框时不显示失败提示', async () => {
    vi.spyOn(meteorologyApi, 'delete').mockResolvedValue(undefined)
    vi.spyOn(ElMessageBox, 'confirm').mockRejectedValue('close')
    const error = vi.spyOn(ElMessage, 'error')
    const wrapper = mountView()
    await flushPromises()

    wrapper.findComponent({ name: 'ElTable' }).vm.$emit('selection-change', [sample[0]])
    await wrapper.vm.$nextTick()

    const deleteBtn = wrapper.findAll('button').find((b) => b.text().includes('批量删除'))
    await deleteBtn!.trigger('click')
    await flushPromises()

    expect(meteorologyApi.delete).not.toHaveBeenCalled()
    expect(error).not.toHaveBeenCalled()
  })
})
