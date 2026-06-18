import { mount, flushPromises } from '@vue/test-utils'
import ElementPlus from 'element-plus'
import { describe, expect, it } from 'vitest'
import ContributionPanel from '@/components/ContributionPanel.vue'
import type { SimulationResult } from '@/types'

const fakeResult: SimulationResult = {
  concentrations: [],
  gridLat: [],
  gridLon: [],
  contributions: [],
  receptorContributions: {
    学校: {
      'PM2.5': [
        {
          sourceId: 1,
          sourceName: '钢厂烟囱',
          concentration: 10,
          pollutant: 'PM2.5',
          percentage: 80,
        },
        {
          sourceId: 2,
          sourceName: '道路尾气',
          concentration: 0,
          pollutant: 'PM2.5',
          percentage: 0,
        },
      ],
    },
    医院: {
      'PM2.5': [],
      NOx: [
        {
          sourceId: 3,
          sourceName: 'X 厂',
          concentration: 5,
          pollutant: 'NOx',
          percentage: 100,
        },
      ],
    },
  },
  pollutantConcentrations: null,
  availablePollutants: ['PM2.5', 'NOx'],
}

describe('ContributionPanel', () => {
  it('无结果时显示空态', async () => {
    mount(ContributionPanel, {
      props: { visible: true, result: null },
      global: { plugins: [ElementPlus] },
      attachTo: document.body,
    })
    await flushPromises()
    const drawer = document.querySelector('.el-drawer')
    expect(drawer).not.toBeNull()
    expect(drawer!.textContent).toContain('运行模拟后会显示各空气站点的污染源贡献排名')
    // 清理 teleport
    document.body.innerHTML = ''
  })

  it('有结果时按空气站点分组展示污染物总贡献和污染源排名', async () => {
    mount(ContributionPanel, {
      props: { visible: true, result: fakeResult },
      global: { plugins: [ElementPlus] },
      attachTo: document.body,
    })
    await flushPromises()
    const text = document.querySelector('.el-drawer')!.textContent ?? ''
    expect(text).toContain('空气站点污染源贡献明细')
    expect(text).not.toContain('选择空气站点')
    expect(text).toContain('污染物指标')
    expect(text).toContain('总贡献浓度')
    expect(text).toContain('污染源名称')
    expect(text).toContain('贡献浓度 (µg/m³)')
    expect(text).toContain('贡献占比')
    expect(text).toContain('学校')
    expect(text).toContain('医院')
    expect(text).toContain('PM2.5')
    expect(text).toContain('NOx')
    expect(text).toContain('钢厂烟囱')
    expect(text).toContain('X 厂')
    expect(text).toContain('10.0000') // concentration 4 decimals
    expect(text).toContain('5.0000')
    expect(text).not.toContain('道路尾气')
    document.body.innerHTML = ''
  })

  it('污染物筛选只影响抽屉展示，不显示其他污染物分组', async () => {
    const wrapper = mount(ContributionPanel, {
      props: { visible: true, result: fakeResult },
      global: { plugins: [ElementPlus] },
      attachTo: document.body,
    })
    await flushPromises()

    const pollutantSelect = wrapper.findComponent('[data-test="panel-pollutant-select"]')
    await pollutantSelect.vm.$emit('update:modelValue', 'NOx')
    await flushPromises()

    const text = document.querySelector('.el-drawer')!.textContent ?? ''
    expect(text).toContain('医院')
    expect(text).toContain('NOx')
    expect(text).toContain('X 厂')
    expect(text).not.toContain('PM2.5')
    expect(text).not.toContain('钢厂烟囱')
    document.body.innerHTML = ''
  })
})
