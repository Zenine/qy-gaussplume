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
          concentration: 2.5,
          pollutant: 'PM2.5',
          percentage: 20,
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
    expect(drawer!.textContent).toContain('运行模拟后会显示')
    // 清理 teleport
    document.body.innerHTML = ''
  })

  it('有结果时渲染受体列表 + 第一位源贡献', async () => {
    mount(ContributionPanel, {
      props: { visible: true, result: fakeResult },
      global: { plugins: [ElementPlus] },
      attachTo: document.body,
    })
    await flushPromises()
    const text = document.querySelector('.el-drawer')!.textContent ?? ''
    expect(text).toContain('学校')
    expect(text).toContain('钢厂烟囱')
    expect(text).toContain('10.0000') // concentration 4 decimals
    document.body.innerHTML = ''
  })
})
