import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import ColorLegend from '@/components/ColorLegend.vue'

describe('ColorLegend', () => {
  it('显示最小/中值/最大值三个 tick', () => {
    const wrapper = mount(ColorLegend, {
      props: { min: 0.5, max: 10, scale: 'jet' },
    })
    const ticks = wrapper.find('.ticks').text()
    expect(ticks).toContain('0.500')
    expect(ticks).toContain('10.000')
    // 中值 = 5.25
    expect(ticks).toContain('5.250')
  })

  it('单位默认 μg/m³，可自定义', () => {
    const wrapper = mount(ColorLegend, {
      props: { min: 0, max: 1, scale: 'jet' },
    })
    expect(wrapper.text()).toContain('μg/m³')
  })
})
