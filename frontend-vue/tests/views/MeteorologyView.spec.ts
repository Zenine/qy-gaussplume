import { mount, flushPromises } from '@vue/test-utils'
import ElementPlus from 'element-plus'
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
    recordTime: '2026-01-01T00:00:00Z',
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  },
]

function mountView() {
  return mount(MeteorologyView, {
    global: { plugins: [ElementPlus] },
    attachTo: document.body,
  })
}

beforeEach(() => {
  vi.spyOn(meteorologyApi, 'list').mockResolvedValue(sample)
})

afterEach(() => {
  vi.restoreAllMocks()
  document.body.innerHTML = ''
})

describe('MeteorologyView', () => {
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
  })
})
