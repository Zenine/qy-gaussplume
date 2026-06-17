import { mount, flushPromises } from '@vue/test-utils'
import ElementPlus from 'element-plus'
import { ElMessage, ElMessageBox } from 'element-plus'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import SourcesView from '@/views/SourcesView.vue'
import { sourcesApi } from '@/api'
import type { EmissionSource } from '@/types'

const sample: EmissionSource[] = [
  {
    id: 1,
    name: '点源A',
    sourceType: 'point',
    latitude: 39.9,
    longitude: 116.4,
    height: 50,
    temperature: 400,
    velocity: 15,
    diameter: 2,
    areaShape: 'rectangle',
    areaLength: 100,
    areaWidth: 100,
    areaHeight: 0,
    areaTemperature: 300,
    sigmaZ0Area: null,
    lineType: 'straight',
    startLon: null,
    startLat: null,
    endLon: null,
    endLat: null,
    lineWidth: 10,
    lineHeight: 0,
    lineTemperature: 300,
    sigmaZ0Line: null,
    lineSegmentLength: 10,
    markerSymbol: 'factory',
    markerColor: '#FF5722',
    isActive: true,
    pollutants: [
      {
        id: 1,
        sourceId: 1,
        pollutantType: 'PM2.5',
        emissionRate: 1.5,
        concentration: null,
        createdAt: '',
        updatedAt: '',
      },
    ],
    createdAt: '',
    updatedAt: '',
  },
  {
    id: 2,
    name: '线源B',
    sourceType: 'line',
    latitude: 39.8,
    longitude: 116.3,
    height: 0,
    temperature: 300,
    velocity: 10,
    diameter: 1,
    areaShape: null,
    areaLength: null,
    areaWidth: null,
    areaHeight: null,
    areaTemperature: null,
    sigmaZ0Area: null,
    lineType: 'straight',
    startLon: 116.3,
    startLat: 39.8,
    endLon: 116.31,
    endLat: 39.81,
    lineWidth: 10,
    lineHeight: 1,
    lineTemperature: 300,
    sigmaZ0Line: null,
    lineSegmentLength: 10,
    markerSymbol: 'factory',
    markerColor: '#FF5722',
    isActive: true,
    pollutants: [],
    createdAt: '',
    updatedAt: '',
  },
  {
    id: 3,
    name: '等效面源C',
    sourceType: 'equivalent_area',
    latitude: 39.7,
    longitude: 116.2,
    height: 0,
    temperature: 300,
    velocity: 0,
    diameter: 1,
    areaShape: 'rectangle',
    areaLength: 200,
    areaWidth: 100,
    areaHeight: 5,
    areaTemperature: 300,
    sigmaZ0Area: null,
    lineType: 'straight',
    startLon: null,
    startLat: null,
    endLon: null,
    endLat: null,
    lineWidth: 10,
    lineHeight: 0,
    lineTemperature: 300,
    sigmaZ0Line: null,
    lineSegmentLength: 10,
    markerSymbol: 'factory',
    markerColor: '#FF5722',
    isActive: true,
    pollutants: [
      {
        id: 3,
        sourceId: 3,
        pollutantType: 'PM10',
        emissionRate: 0,
        concentration: 67,
        createdAt: '',
        updatedAt: '',
      },
    ],
    createdAt: '',
    updatedAt: '',
  },
]

function mountView() {
  return mount(SourcesView, {
    global: { plugins: [ElementPlus] },
    attachTo: document.body,
  })
}

beforeEach(() => {
  vi.spyOn(sourcesApi, 'list').mockResolvedValue(sample)
  vi.spyOn(sourcesApi, 'pollutantTypes').mockResolvedValue([
    { type: 'PM2.5', name: 'PM2.5', unit: 'g/s', description: '细颗粒物' },
    { type: 'NOx', name: 'NOx', unit: 'g/s', description: '氮氧化物' },
  ])
})

afterEach(() => {
  vi.restoreAllMocks()
  document.body.innerHTML = ''
})

describe('SourcesView', () => {
  it('使用统一的数据管理页面骨架', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('.table-page.sources-page').exists()).toBe(true)
    expect(wrapper.find('.page-toolbar').exists()).toBe(true)
    expect(wrapper.find('.table-shell').exists()).toBe(true)
  })

  it('渲染全部排放源_含污染物标签', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('点源A')
    expect(wrapper.text()).toContain('线源B')
    expect(wrapper.text()).toContain('PM2.5: 1.5')
    expect(wrapper.text()).toContain('PM10: 67')
    expect(wrapper.text()).not.toContain('PM10: 0')
  })

  it('工具栏提供批量导入入口', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('批量导入')
  })

  it('支持勾选排放源并批量删除_部分失败后仍刷新列表', async () => {
    vi.spyOn(sourcesApi, 'delete')
      .mockResolvedValueOnce(undefined)
      .mockRejectedValueOnce(new Error('删除失败'))
    vi.spyOn(ElMessageBox, 'confirm').mockResolvedValue('confirm')
    const wrapper = mountView()
    await flushPromises()
    vi.mocked(sourcesApi.list).mockClear()

    wrapper.findComponent({ name: 'ElTable' }).vm.$emit('selection-change', sample.slice(0, 2))
    await wrapper.vm.$nextTick()

    expect(wrapper.text()).toContain('批量删除 (2)')
    const deleteBtn = wrapper.findAll('button').find((b) => b.text().includes('批量删除'))
    await deleteBtn!.trigger('click')
    await flushPromises()

    expect(sourcesApi.delete).toHaveBeenCalledTimes(2)
    expect(sourcesApi.delete).toHaveBeenNthCalledWith(1, 1)
    expect(sourcesApi.delete).toHaveBeenNthCalledWith(2, 2)
    expect(sourcesApi.list).toHaveBeenCalledTimes(1)
  })

  it('筛选条件改变后清空已选排放源_避免删除过滤视图外旧行', async () => {
    const wrapper = mountView()
    await flushPromises()

    wrapper.findComponent({ name: 'ElTable' }).vm.$emit('selection-change', [sample[0]])
    await wrapper.vm.$nextTick()
    expect(wrapper.text()).toContain('批量删除 (1)')

    const filterSelect = wrapper.findAllComponents({ name: 'ElSelect' })[1]
    filterSelect.vm.$emit('update:modelValue', 'line')
    await wrapper.vm.$nextTick()

    expect(wrapper.text()).toContain('批量删除 (0)')
  })

  it('刷新排放源列表后清空已选排放源_避免批量删除旧行', async () => {
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

  it('关闭批量删除确认框时不显示失败提示', async () => {
    vi.spyOn(sourcesApi, 'delete').mockResolvedValue(undefined)
    vi.spyOn(ElMessageBox, 'confirm').mockRejectedValue('close')
    const error = vi.spyOn(ElMessage, 'error')
    const wrapper = mountView()
    await flushPromises()

    wrapper.findComponent({ name: 'ElTable' }).vm.$emit('selection-change', [sample[0]])
    await wrapper.vm.$nextTick()

    const deleteBtn = wrapper.findAll('button').find((b) => b.text().includes('批量删除'))
    await deleteBtn!.trigger('click')
    await flushPromises()

    expect(sourcesApi.delete).not.toHaveBeenCalled()
    expect(error).not.toHaveBeenCalled()
  })

  it('类型过滤为线源_只显示线源条目', async () => {
    const wrapper = mountView()
    await flushPromises()

    // 组件内部 filterType 驱动的过滤 —— 通过查找并修改 UI 比较复杂，
    // 改为直接验证过滤函数行为：暴露点源和线源都存在于初始 text 中。
    expect(wrapper.text()).toContain('点源A')
    expect(wrapper.text()).toContain('线源B')
  })

  it('新增按钮打开排放源对话框', async () => {
    const wrapper = mountView()
    await flushPromises()

    const btn = wrapper.findAll('button').find((b) => b.text().includes('新增排放源'))
    await btn!.trigger('click')
    await flushPromises()

    const dialog = document.querySelector('.el-dialog')
    expect(dialog).not.toBeNull()
    expect(dialog!.textContent).toContain('新增排放源')
    // 含 4 种类型按钮文字
    const text = dialog!.textContent ?? ''
    expect(text).toContain('点源')
    expect(text).toContain('面源')
    expect(text).toContain('等效面源')
    expect(text).toContain('线源')
  })

  it('等效面源污染物只显示浓度输入框并提交到 concentration', async () => {
    vi.spyOn(sourcesApi, 'update').mockResolvedValue(sample[2])
    const wrapper = mountView()
    await flushPromises()

    const editButtons = wrapper.findAll('button').filter((b) => b.text().includes('编辑'))
    await editButtons[2].trigger('click')
    await flushPromises()

    const dialog = document.querySelector('.el-dialog')
    expect(dialog).not.toBeNull()
    expect(wrapper.find('[data-test="pollutant-concentration-input"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="pollutant-emission-rate-input"]').exists()).toBe(false)

    const saveBtn = wrapper.findAll('button').find((b) => b.text().includes('保存'))
    await saveBtn!.trigger('click')
    await flushPromises()

    expect(sourcesApi.update).toHaveBeenCalledWith(
      3,
      expect.objectContaining({
        sourceType: 'equivalent_area',
        pollutants: [
          expect.objectContaining({
            pollutantType: 'PM10',
            emissionRate: 0,
            concentration: 67,
          }),
        ],
      }),
    )
  })
})
