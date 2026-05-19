import { mount, flushPromises } from '@vue/test-utils'
import ElementPlus from 'element-plus'
import { afterEach, describe, expect, it, vi } from 'vitest'
import ParallelSimulationDialog from '@/components/ParallelSimulationDialog.vue'
import { simulationApi } from '@/api'
import type { Meteorology, ParallelSimulationResult } from '@/types'

const meteorologies: Meteorology[] = [
  {
    id: 1,
    name: '默认',
    windSpeed: 3,
    windDirection: 0,
    stabilityClass: 'D',
    boundaryLayerHeight: 1000,
    temperature: 293,
    humidity: 50,
    cloudCover: 0,
    precipitation: 0,
    recordTime: '',
    createdAt: '',
    updatedAt: '',
  },
]

afterEach(() => {
  vi.restoreAllMocks()
  document.body.innerHTML = ''
})

describe('ParallelSimulationDialog', () => {
  it('弹窗打开显示 4 种风向预设按钮', async () => {
    mount(ParallelSimulationDialog, {
      props: {
        visible: true,
        meteorologies,
        selectedMeteorologyId: 1,
        gridResolution: 100,
        domainSize: 10000,
        pollutantType: '',
      },
      global: { plugins: [ElementPlus] },
      attachTo: document.body,
    })
    await flushPromises()
    const text = document.querySelector('.el-dialog')!.textContent ?? ''
    expect(text).toContain('全局模拟')
    expect(text).toContain('8')
    expect(text).toContain('16')
    expect(text).toContain('32')
    expect(text).toContain('72')
  })

  it('点击运行按钮会用等分风向数组调用 simulationApi.runParallel', async () => {
    const fakeResult: ParallelSimulationResult = {
      success: true,
      mode: 'aggregated',
      totalWindDirections: 16,
      successfulSimulations: 16,
      failedSimulations: 0,
      numWorkersUsed: 4,
      computationTimeSeconds: 2.5,
      speedupFactor: 10,
      concentrations: [],
      gridLat: [],
      gridLon: [],
    }
    const spy = vi.spyOn(simulationApi, 'runParallel').mockResolvedValue(fakeResult)

    const wrapper = mount(ParallelSimulationDialog, {
      props: {
        visible: true,
        meteorologies,
        selectedMeteorologyId: 1,
        gridResolution: 100,
        domainSize: 10000,
        pollutantType: 'PM2.5',
      },
      global: { plugins: [ElementPlus] },
      attachTo: document.body,
    })
    await flushPromises()

    // 找到 "运行并行模拟" 按钮（在 footer 的 teleport 里）
    const buttons = Array.from(document.querySelectorAll('button'))
    const runBtn = buttons.find((b) => b.textContent?.includes('运行并行模拟'))
    expect(runBtn).toBeDefined()
    runBtn!.click()
    await flushPromises()

    expect(spy).toHaveBeenCalledTimes(1)
    const [req] = spy.mock.calls[0]
    expect(req.meteorologyId).toBe(1)
    expect(req.windDirections.length).toBe(16) // 默认 16 风向
    expect(req.windDirections[0]).toBe(0)
    expect(req.windDirections[1]).toBeCloseTo(22.5)
    expect(req.pollutantType).toBe('PM2.5')

    // completed 事件被 emit
    expect(wrapper.emitted('completed')).toHaveLength(1)
  })
})
