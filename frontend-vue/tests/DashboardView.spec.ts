import { flushPromises, mount } from '@vue/test-utils'
import ElementPlus from 'element-plus'
import { createPinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import DashboardView from '@/views/DashboardView.vue'
import { meteorologyApi, receptorsApi, simulationApi, sourcesApi } from '@/api'
import type { SelectionBounds } from '@/utils/selection'

vi.mock('@/api', () => ({
  sourcesApi: { list: vi.fn() },
  receptorsApi: { list: vi.fn() },
  meteorologyApi: { list: vi.fn() },
  simulationApi: { run: vi.fn() },
}))

const mapStub = {
  name: 'MapPanel',
  props: [
    'sources',
    'receptors',
    'result',
    'scale',
    'opacity',
    'min',
    'max',
    'renderScale',
    'tileLayer',
    'selectionEnabled',
  ],
  emits: ['selection-change'],
  methods: {
    fitBounds: vi.fn(),
    clearSelection: vi.fn(),
    fitSelection: vi.fn(),
  },
  template: '<div class="map-panel-stub"><slot /></div>',
}

function source(id: number, latitude: number, longitude: number) {
  return {
    id,
    name: `源${id}`,
    sourceType: 'point',
    latitude,
    longitude,
    height: 50,
    temperature: null,
    velocity: null,
    diameter: null,
    areaShape: null,
    areaLength: null,
    areaWidth: null,
    areaHeight: null,
    areaTemperature: null,
    sigmaZ0Area: null,
    lineType: null,
    startLon: null,
    startLat: null,
    endLon: null,
    endLat: null,
    lineWidth: null,
    lineHeight: null,
    lineTemperature: null,
    sigmaZ0Line: null,
    lineSegmentLength: null,
    markerSymbol: 'circle',
    markerColor: '#f00',
    isActive: true,
    pollutants: [],
    createdAt: '',
    updatedAt: '',
  }
}

function receptor(id: number, latitude: number, longitude: number) {
  return {
    id,
    name: `点${id}`,
    latitude,
    longitude,
    height: 1.5,
    markerSymbol: 'square',
    markerColor: '#00f',
    isActive: true,
    createdAt: '',
    updatedAt: '',
  }
}

async function mountDashboard() {
  const wrapper = mount(DashboardView, {
    global: {
      plugins: [ElementPlus, createPinia()],
      stubs: {
        MapPanel: mapStub,
        ColorLegend: { template: '<div class="legend-stub" />' },
        ContributionPanel: { template: '<div class="contribution-panel-stub" />' },
        ParallelSimulationDialog: { template: '<div class="parallel-dialog-stub" />' },
      },
    },
  })
  await flushPromises()
  return wrapper
}

describe('DashboardView', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(sourcesApi.list).mockResolvedValue([
      source(1, 40.0, 116.4),
      source(2, 39.7, 116.4),
    ])
    vi.mocked(receptorsApi.list).mockResolvedValue([
      receptor(11, 40.0, 116.4),
      receptor(12, 39.7, 116.4),
    ])
    vi.mocked(meteorologyApi.list).mockResolvedValue([
      {
        id: 7,
        name: '高德街道',
        windSpeed: 1.846,
        windDirection: 277,
        boundaryLayerHeight: 1000,
        stabilityClass: 'D',
        temperature: 293.15,
        humidity: 50,
        cloudCover: 0,
        precipitation: 0,
        recordTime: '',
        createdAt: '',
        updatedAt: '',
      },
    ])
    vi.mocked(simulationApi.run).mockResolvedValue({
      concentrations: [[0, 1]],
      gridLat: [39.9],
      gridLon: [116.3, 116.4],
      contributions: [
        { sourceId: 1, sourceName: '源1', totalConcentration: 3, maxConcentration: 2, pollutants: ['PM2.5'] },
      ],
      receptorContributions: {
        点11: {
          'PM2.5': [
            { sourceId: 1, sourceName: '源1', concentration: 2, pollutant: 'PM2.5', percentage: 100 },
          ],
        },
      },
      pollutantConcentrations: null,
      availablePollutants: ['PM2.5'],
    })
  })

  it('渲染地图悬浮工具条、左下滑块、右侧功能卡片', async () => {
    const wrapper = await mountDashboard()

    expect(wrapper.find('[data-test="floating-toolbar"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="range-panel"]').text()).toContain('模拟范围')
    expect(wrapper.find('[data-test="draw-card"]').text()).toContain('绘制选择区域')
    expect(wrapper.find('[data-test="draw-card"]').text()).toContain('矩形区域')
    expect(wrapper.find('[data-test="weather-card"]').text()).toContain('气象控制')
    expect(wrapper.find('[data-test="stats-card"]').text()).toContain('数据统计')
    expect(wrapper.text()).toContain('高德街道')
  })

  it('选择区域后运行模拟，只提交区域内排放源和受体点', async () => {
    const wrapper = await mountDashboard()
    const bounds: SelectionBounds = {
      north: 40.1,
      south: 39.9,
      east: 116.5,
      west: 116.3,
    }

    await wrapper.findComponent(mapStub).vm.$emit('selection-change', bounds)
    await wrapper.find('[data-test="run-simulation"]').trigger('click')
    await flushPromises()

    expect(simulationApi.run).toHaveBeenCalledWith(
      expect.objectContaining({
        meteorologyId: 7,
        sourceIds: [1],
        receptorIds: [11],
      }),
    )
  })

  it('运行后显示模拟结果和贡献排名，清除结果会回到初始卡片', async () => {
    const wrapper = await mountDashboard()

    await wrapper.find('[data-test="run-simulation"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test="result-card"]').text()).toContain('模拟结果')
    expect(wrapper.find('[data-test="result-card"]').text()).toContain('透明度')
    expect(wrapper.find('[data-test="ranking-card"]').text()).toContain('受体点贡献分析')
    expect(wrapper.find('[data-test="ranking-card"]').text()).toContain('源1')

    await wrapper.find('[data-test="clear-result"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test="result-card"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="draw-card"]').exists()).toBe(true)
  })
})
