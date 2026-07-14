import { mount, flushPromises } from '@vue/test-utils'
import ElementPlus from 'element-plus'
import { afterEach, describe, expect, it, vi } from 'vitest'
import FormulaDrawer from '@/components/FormulaDrawer.vue'
import { simulationApi } from '@/api'
import type { SimulationFormulaInfo } from '@/types'

const formulaInfo: SimulationFormulaInfo = {
  gaussianPlumeFormula: 'C = Q exp(...)',
  decayFormula: 'C_final = C_plume dry wet chemical',
  windAggregationFormula: 'C_agg = sum(C_direction * weight)',
  pollutants: [
    {
      type: 'PM2.5',
      name: '细颗粒物',
      gravitationalSettlingVelocity: 0.0002,
      dryResistanceRb: 100,
      dryResistanceRc: 200,
      wetScavengingA: 0.00001,
      wetScavengingB: 0.8,
      chemicalRate: 0.00002,
      chemicalEnhanced: false,
      chemicalTemperatureMultiplier: 1,
      chemicalHumidityMultiplier: 1,
      temperatureCorrected: false,
    },
    {
      type: 'NOx',
      name: '氮氧化物',
      gravitationalSettlingVelocity: 0,
      dryResistanceRb: 150,
      dryResistanceRc: 500,
      wetScavengingA: 0.000005,
      wetScavengingB: 0.7,
      chemicalRate: 0.00015,
      chemicalEnhanced: true,
      chemicalTemperatureMultiplier: 1.5,
      chemicalHumidityMultiplier: 1.3,
      temperatureCorrected: true,
    },
  ],
  sourceTypes: [
    {
      type: 'equivalent_area',
      name: '等效面源',
      formula: 'Q_equiv = f(concentration, area)',
      notes: '由实测 concentration 反算等效排放速率。',
    },
  ],
}

afterEach(() => {
  vi.restoreAllMocks()
  document.body.innerHTML = ''
})

describe('FormulaDrawer', () => {
  it('打开时加载并展示公式、污染因子和源类型', async () => {
    const spy = vi.spyOn(simulationApi, 'formulas').mockResolvedValue(formulaInfo)

    mount(FormulaDrawer, {
      props: { visible: true },
      global: { plugins: [ElementPlus] },
      attachTo: document.body,
    })
    await flushPromises()

    expect(spy).toHaveBeenCalledTimes(1)
    const text = document.querySelector('.el-drawer')?.textContent ?? ''
    expect(text).toContain('C = Q exp')
    expect(text).toContain('PM2.5')
    expect(text).toContain('NOx')
    expect(text).toContain('温度 ×1.5 / 湿度 ×1.3')
    expect(text).toContain('等效面源')
    expect(text).toContain('Q_equiv')
  })

  it('使用响应式抽屉宽度避免窄屏溢出', () => {
    vi.spyOn(simulationApi, 'formulas').mockResolvedValue(formulaInfo)

    const wrapper = mount(FormulaDrawer, {
      props: { visible: false },
      global: { plugins: [ElementPlus] },
    })

    expect(wrapper.findComponent({ name: 'ElDrawer' }).props('size')).toBe('min(720px, 100vw)')
  })
})
