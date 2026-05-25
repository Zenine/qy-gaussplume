import { mount, flushPromises } from '@vue/test-utils'
import ElementPlus from 'element-plus'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import DashboardView from '@/views/DashboardView.vue'
import {
  meteorologyApi,
  receptorsApi,
  simulationApi,
  sourcesApi,
} from '@/api'
import type { EmissionSource, Meteorology, Receptor } from '@/types'

const sources: EmissionSource[] = [
  {
    id: 1,
    name: '锅炉点源',
    sourceType: 'point',
    latitude: 39.9,
    longitude: 116.4,
    height: 50,
    temperature: 400,
    velocity: 15,
    diameter: 2,
    areaShape: null,
    areaLength: null,
    areaWidth: null,
    areaHeight: null,
    areaTemperature: null,
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
    pollutants: [],
    createdAt: '',
    updatedAt: '',
  },
]

const receptors: Receptor[] = [
  {
    id: 1,
    name: '学校',
    latitude: 39.91,
    longitude: 116.41,
    height: 1.5,
    markerSymbol: 'monitor',
    markerColor: '#2196F3',
    isActive: true,
    createdAt: '',
    updatedAt: '',
  },
  {
    id: 2,
    name: '医院',
    latitude: 39.92,
    longitude: 116.42,
    height: 1.5,
    markerSymbol: 'monitor',
    markerColor: '#22C55E',
    isActive: true,
    createdAt: '',
    updatedAt: '',
  },
]

const meteorologies: Meteorology[] = [
  {
    id: 1,
    name: '冬季北风',
    windSpeed: 3,
    windDirection: 0,
    boundaryLayerHeight: 800,
    stabilityClass: 'D',
    temperature: 278,
    humidity: 60,
    cloudCover: 2,
    precipitation: 0,
    recordTime: '',
    createdAt: '',
    updatedAt: '',
  },
]

function mountView() {
  return mount(DashboardView, {
    global: {
      plugins: [ElementPlus],
      stubs: {
        MapPanel: {
          template: '<div class="map-panel-stub" />',
          methods: { fitBounds: vi.fn(), clearSelection: vi.fn(), fitSelection: vi.fn() },
        },
        ColorLegend: true,
        ContributionPanel: true,
        ParallelSimulationDialog: true,
      },
    },
  })
}

beforeEach(() => {
  setActivePinia(createPinia())
  vi.spyOn(sourcesApi, 'list').mockResolvedValue(sources)
  vi.spyOn(receptorsApi, 'list').mockResolvedValue(receptors)
  vi.spyOn(meteorologyApi, 'list').mockResolvedValue(meteorologies)
  vi.spyOn(simulationApi, 'run').mockResolvedValue({
    concentrations: [[0, 1]],
    gridLat: [39.9],
    gridLon: [116.4, 116.41],
    contributions: [],
    receptorContributions: {},
    pollutantConcentrations: null,
    availablePollutants: ['PM2.5'],
  })
})

describe('DashboardView', () => {
  it('加载后展示地图悬浮工具条和数据统计卡片', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.find('[data-test="floating-toolbar"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="range-panel"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="draw-card"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="weather-card"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="stats-card"]').text()).toContain('1')
    expect(wrapper.find('[data-test="stats-card"]').text()).toContain('2')
    expect(wrapper.text()).toContain('冬季北风')
  })

  it('模拟完成后仍保留风速风向控制框', async () => {
    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-test="run-simulation"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test="weather-card"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="weather-card"]').text()).toContain('风向')
    expect(wrapper.find('[data-test="weather-card"]').text()).toContain('风速')
  })
})
