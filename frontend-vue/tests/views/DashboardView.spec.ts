import { mount, flushPromises } from '@vue/test-utils'
import ElementPlus from 'element-plus'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import DashboardView from '@/views/DashboardView.vue'
import {
  mapApi,
  meteorologyApi,
  receptorsApi,
  simulationApi,
  sourcesApi,
} from '@/api'
import type { EmissionSource, Meteorology, Receptor, SimulationResult } from '@/types'

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

function contributionRows(prefix: string, count: number) {
  return Array.from({ length: count }, (_, index) => ({
    sourceId: index + 1,
    sourceName: `${prefix}${index + 1}`,
    concentration: 20 - index,
    pollutant: 'PM2.5',
    percentage: 40 - index,
  }))
}

const contributionResult: SimulationResult = {
  concentrations: [[0, 1]],
  gridLat: [39.9],
  gridLon: [116.4, 116.41],
  contributions: [],
  receptorContributions: {
    学校: {
      'PM2.5': contributionRows('工地源', 11),
      PM10: [
        {
          sourceId: 99,
          sourceName: '道路扬尘',
          concentration: 8,
          pollutant: 'PM10',
          percentage: 100,
        },
      ],
    },
  },
  pollutantConcentrations: {
    'PM2.5': [[0, 1]],
    PM10: [[0, 2]],
  },
  availablePollutants: ['PM2.5', 'PM10'],
}

const zeroFirstReceptorResult: SimulationResult = {
  ...contributionResult,
  receptorContributions: {
    学校: {
      'PM2.5': [
        {
          sourceId: 1,
          sourceName: '零贡献源',
          concentration: 0,
          pollutant: 'PM2.5',
          percentage: 0,
        },
      ],
    },
    医院: {
      'PM2.5': [
        {
          sourceId: 2,
          sourceName: '有效贡献源',
          concentration: 2.5,
          pollutant: 'PM2.5',
          percentage: 100,
        },
      ],
    },
  },
}

function mountView() {
  return mount(DashboardView, {
    global: {
      plugins: [ElementPlus],
      stubs: {
        MapPanel: {
          name: 'MapPanel',
          props: ['result', 'boundaryGeoJson', 'initialCenter', 'initialZoom'],
          emits: ['view-change'],
          template: '<div class="map-panel-stub" />',
          methods: { fitBounds: vi.fn(), clearSelection: vi.fn(), fitSelection: vi.fn() },
        },
        ColorLegend: true,
        ContributionPanel: true,
        ParallelSimulationDialog: {
          name: 'ParallelSimulationDialog',
          props: [
            'visible',
            'meteorologies',
            'selectedMeteorologyId',
            'gridResolution',
            'domainSize',
            'pollutantType',
            'receptorHeight',
          ],
          emits: ['completed'],
          template: '<div class="parallel-dialog-stub" />',
        },
        FormulaDrawer: true,
      },
    },
  })
}

beforeEach(() => {
  localStorage.clear()
  setActivePinia(createPinia())
  vi.spyOn(sourcesApi, 'list').mockResolvedValue(sources)
  vi.spyOn(receptorsApi, 'list').mockResolvedValue(receptors)
  vi.spyOn(meteorologyApi, 'list').mockResolvedValue(meteorologies)
  vi.spyOn(mapApi, 'getGeoJson').mockResolvedValue({
    type: 'FeatureCollection',
    features: [
      {
        type: 'Feature',
        properties: { name: '测试边界' },
        geometry: {
          type: 'Polygon',
          coordinates: [
            [
              [116.3, 39.8],
              [116.5, 39.8],
              [116.5, 40.0],
              [116.3, 40.0],
              [116.3, 39.8],
            ],
          ],
        },
      },
    ],
  })
  vi.spyOn(simulationApi, 'run').mockResolvedValue({
    concentrations: [[0, 1]],
    gridLat: [39.9],
    gridLon: [116.4, 116.41],
    contributions: [],
    receptorContributions: {},
    pollutantConcentrations: null,
    availablePollutants: ['PM2.5'],
  })
  vi.spyOn(simulationApi, 'runParallel').mockResolvedValue({
    success: true,
    mode: 'aggregated',
    totalWindDirections: 64,
    successfulSimulations: 64,
    failedSimulations: 0,
    numWorkersUsed: 4,
    computationTimeSeconds: 1,
    speedupFactor: 4,
    concentrations: contributionResult.concentrations,
    gridLat: contributionResult.gridLat,
    gridLon: contributionResult.gridLon,
    receptorContributions: contributionResult.receptorContributions,
    pollutantConcentrations: contributionResult.pollutantConcentrations,
    availablePollutants: contributionResult.availablePollutants,
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
    expect(wrapper.text()).toContain('公式说明')
    expect(wrapper.find('[data-test="range-panel"]').text()).toContain('5 km')
    const sliders = wrapper.findAllComponents({ name: 'ElSlider' })
    expect(sliders[0].props('min')).toBe(5)
    expect(sliders[0].props('max')).toBe(100)
    expect(sliders[0].props('step')).toBe(5)
  })

  it('行政边界开关按需加载 GeoJSON 并传给地图', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(mapApi.getGeoJson).not.toHaveBeenCalled()
    await wrapper.findComponent('[data-test="boundary-layer-switch"]').vm.$emit('update:modelValue', true)
    await flushPromises()

    expect(mapApi.getGeoJson).toHaveBeenCalledWith(true)
    expect(wrapper.findComponent({ name: 'MapPanel' }).props('boundaryGeoJson')).toMatchObject({
      type: 'FeatureCollection',
    })
  })

  it('运行模拟时提交主界面当前风速风向', async () => {
    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-test="run-simulation"]').trigger('click')
    await flushPromises()

    expect(simulationApi.run).toHaveBeenCalledWith(
      expect.objectContaining({
        meteorologyId: 1,
        windSpeed: 3,
        windDirection: 0,
      }),
    )
  })

  it('主界面多风向模式直接运行全局模拟', async () => {
    const wrapper = mountView()
    await flushPromises()

    await wrapper.findComponent('[data-test="simulation-mode-select"]').vm.$emit('update:modelValue', 'parallel')
    await wrapper.findComponent('[data-test="parallel-direction-count"]').vm.$emit('update:modelValue', 64)
    await wrapper.findComponent('[data-test="parallel-wind-speed"]').vm.$emit('update:modelValue', 2.5)
    await wrapper.find('[data-test="run-simulation"]').trigger('click')
    await flushPromises()

    expect(simulationApi.run).not.toHaveBeenCalled()
    expect(simulationApi.runParallel).toHaveBeenCalledWith(
      expect.objectContaining({
        meteorologyId: 1,
        windSpeed: 2.5,
        gridResolution: 100,
        domainSize: 5000,
        receptorHeight: 0,
        returnAggregatedOnly: true,
      }),
    )
    const [request] = vi.mocked(simulationApi.runParallel).mock.calls[0]
    expect(request.windDirections).toHaveLength(64)
    expect(request.windDirections[1]).toBeCloseTo(5.625)
    expect(wrapper.find('[data-test="ranking-card"]').text()).toContain('工地源1')

    const restored = mountView()
    await flushPromises()

    expect(restored.findComponent('[data-test="simulation-mode-select"]').props('modelValue')).toBe('parallel')
    expect(restored.findComponent('[data-test="parallel-direction-count"]').props('modelValue')).toBe(64)
    expect(restored.findComponent('[data-test="parallel-wind-speed"]').props('modelValue')).toBe(2.5)
    expect(restored.find('[data-test="result-card"]').text()).not.toContain('当前结果未更新')
  })

  it('模拟完成后仍保留风速风向控制框', async () => {
    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-test="run-simulation"]').trigger('click')
    await flushPromises()

    const weatherCard = wrapper.find('[data-test="weather-card"]')
    expect(weatherCard.exists()).toBe(true)
    expect(weatherCard.text()).toContain('来风方向')
    expect(weatherCard.text()).toContain('外端指向风吹来的方向')
    expect(weatherCard.text()).toContain('风速')
  })

  it('运行结果保存到本地，刷新后可恢复结果展示', async () => {
    vi.mocked(simulationApi.run).mockResolvedValue(contributionResult)
    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-test="run-simulation"]').trigger('click')
    await flushPromises()

    expect(localStorage.getItem('gnn.simulationResult.v1')).toContain('工地源1')
    const restored = mountView()
    await flushPromises()

    expect(restored.find('[data-test="ranking-card"]').text()).toContain('工地源1')
    expect(restored.findComponent({ name: 'MapPanel' }).props('result')).toMatchObject({
      availablePollutants: ['PM2.5', 'PM10'],
    })
  })

  it('地图视角变化会写入偏好', async () => {
    const wrapper = mountView()
    await flushPromises()

    await wrapper.findComponent({ name: 'MapPanel' }).vm.$emit('view-change', {
      center: [30.5, 120.5],
      zoom: 13,
    })
    await wrapper.vm.$nextTick()

    const stored = JSON.parse(localStorage.getItem('gnn.prefs.v1') ?? '{}')
    expect(stored.mapCenter).toEqual([30.5, 120.5])
    expect(stored.mapZoom).toBe(13)
  })

  it('风向指针以圆点为中心并围绕圆心旋转', async () => {
    const wrapper = mountView()
    await flushPromises()

    const pointer = wrapper.find('[data-test="wind-direction-pointer"]')
    const pointerTip = wrapper.find('[data-test="wind-direction-pointer-tip"]')

    expect(pointer.exists()).toBe(true)
    expect(pointer.attributes('x1')).toBe('75')
    expect(pointer.attributes('y1')).toBe('75')
    expect(pointer.attributes('x2')).toBe('75.00')
    expect(pointer.attributes('y2')).toBe('31.00')
    expect(pointerTip.attributes('cx')).toBe('75')
    expect(pointerTip.attributes('cy')).toBe('75')

    const directionInput = wrapper.findAllComponents({ name: 'ElInputNumber' })[0]
    await directionInput.vm.$emit('update:modelValue', 125)
    await wrapper.vm.$nextTick()

    const x2 = Number(pointer.attributes('x2'))
    const y2 = Number(pointer.attributes('y2'))
    expect(pointer.attributes('x1')).toBe('75')
    expect(pointer.attributes('y1')).toBe('75')
    expect(x2).toBeCloseTo(111.04, 2)
    expect(y2).toBeCloseTo(100.24, 2)
    expect(pointerTip.attributes('cx')).toBe('75')
    expect(pointerTip.attributes('cy')).toBe('75')
  })

  it('点击公式说明后打开公式抽屉', async () => {
    const wrapper = mountView()
    await flushPromises()

    const formulaButton = wrapper.findAll('button').find((b) => b.text().includes('公式说明'))
    await formulaButton!.trigger('click')
    await wrapper.vm.$nextTick()

    const drawer = wrapper.findComponent({ name: 'FormulaDrawer' })
    expect(drawer.attributes('visible')).toBe('true')
  })

  it('运行模拟后展示空气站点污染源贡献排名前10名', async () => {
    vi.mocked(simulationApi.run).mockResolvedValueOnce(contributionResult)
    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-test="run-simulation"]').trigger('click')
    await flushPromises()

    const rankingCard = wrapper.find('[data-test="ranking-card"]')
    expect(rankingCard.text()).toContain('空气站点污染源贡献排名')
    expect(rankingCard.text()).toContain('选择空气站点')
    expect(rankingCard.text()).toContain('污染物指标')
    expect(rankingCard.text()).toContain('总贡献浓度')
    expect(rankingCard.text()).toContain('µg/m³')
    expect(rankingCard.text()).toContain('工地源1')
    expect(rankingCard.text()).toContain('40.0%')
    expect(rankingCard.text()).toContain('20.0000 µg/m³')
    expect(rankingCard.text()).toContain('工地源10')
    expect(rankingCard.text()).not.toContain('工地源11')
    expect(rankingCard.text()).not.toContain('道路扬尘')
  })

  it('默认选择当前污染物下有贡献的空气站点并隐藏零贡献源', async () => {
    vi.mocked(simulationApi.run).mockResolvedValueOnce(zeroFirstReceptorResult)
    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-test="run-simulation"]').trigger('click')
    await flushPromises()

    const rankingCard = wrapper.find('[data-test="ranking-card"]')
    expect(rankingCard.text()).toContain('有效贡献源')
    expect(rankingCard.text()).toContain('2.5000 µg/m³')
    expect(rankingCard.text()).not.toContain('零贡献源')
  })

  it('右侧污染物指标切换后同步显示污染物、地图结果和贡献排名，但不改变下一次计算筛选', async () => {
    vi.mocked(simulationApi.run).mockResolvedValue(contributionResult)
    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-test="run-simulation"]').trigger('click')
    await flushPromises()

    const mapPanel = wrapper.findComponent({ name: 'MapPanel' })
    const topPollutantSelect = wrapper.findComponent('[data-test="top-pollutant-select"]')
    const rankingCard = wrapper.find('[data-test="ranking-card"]')
    expect(rankingCard.text()).toContain('工地源1')
    expect(rankingCard.text()).not.toContain('道路扬尘')
    expect(topPollutantSelect.props('modelValue')).toBe('PM2.5')
    expect(mapPanel.props('result').concentrations).toEqual([[0, 1]])
    expect(simulationApi.run).toHaveBeenLastCalledWith(
      expect.not.objectContaining({
        pollutantType: expect.any(String),
      }),
    )

    const rankingPollutantSelect = wrapper.findComponent('[data-test="ranking-pollutant-select"]')
    await rankingPollutantSelect.vm.$emit('update:modelValue', 'PM10')
    await wrapper.vm.$nextTick()

    expect(rankingCard.text()).toContain('道路扬尘')
    expect(rankingCard.text()).toContain('8.0000 µg/m³')
    expect(rankingCard.text()).not.toContain('工地源1')
    expect(topPollutantSelect.props('modelValue')).toBe('PM10')
    expect(mapPanel.props('result').concentrations).toEqual([[0, 2]])
    expect(simulationApi.run).toHaveBeenCalledTimes(1)

    await wrapper.find('[data-test="run-simulation"]').trigger('click')
    await flushPromises()
    expect(simulationApi.run).toHaveBeenLastCalledWith(
      expect.not.objectContaining({
        pollutantType: expect.any(String),
      }),
    )
  })

  it('顶部计算污染物筛选只影响下一次运行请求，不立即切换已有地图结果', async () => {
    vi.mocked(simulationApi.run).mockResolvedValue(contributionResult)
    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-test="run-simulation"]').trigger('click')
    await flushPromises()

    const mapPanel = wrapper.findComponent({ name: 'MapPanel' })
    const calculationPollutantSelect = wrapper.findComponent('[data-test="calculation-pollutant-select"]')
    await calculationPollutantSelect.vm.$emit('update:modelValue', 'PM10')
    await wrapper.vm.$nextTick()

    expect(mapPanel.props('result').concentrations).toEqual([[0, 1]])
    expect(wrapper.find('[data-test="result-card"]').text()).toContain('页面参数已变化')

    await wrapper.find('[data-test="run-simulation"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test="result-card"]').text()).not.toContain('页面参数已变化')
    expect(simulationApi.run).toHaveBeenLastCalledWith(
      expect.objectContaining({
        pollutantType: 'PM10',
      }),
    )
  })

  it('右侧污染物指标清空后按空气站点展示全部污染物贡献摘要', async () => {
    vi.mocked(simulationApi.run).mockResolvedValue(contributionResult)
    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-test="run-simulation"]').trigger('click')
    await flushPromises()

    const rankingPollutantSelect = wrapper.findComponent('[data-test="ranking-pollutant-select"]')
    await rankingPollutantSelect.vm.$emit('update:modelValue', '')
    await wrapper.vm.$nextTick()

    const topPollutantSelect = wrapper.findComponent('[data-test="top-pollutant-select"]')
    const rankingCard = wrapper.find('[data-test="ranking-card"]')
    expect(topPollutantSelect.props('modelValue')).toBe('')
    expect(rankingCard.text()).toContain('全部污染物贡献摘要')
    expect(rankingCard.text()).toContain('学校')
    expect(rankingCard.text()).toContain('PM2.5')
    expect(rankingCard.text()).toContain('PM10')
    expect(rankingCard.text()).toContain('165.0000 µg/m³')
    expect(rankingCard.text()).toContain('8.0000 µg/m³')
    expect(rankingCard.text()).not.toContain('工地源1')
  })

  it('模拟完成后修改气象参数提示当前结果未更新，重新运行后提示消失', async () => {
    vi.mocked(simulationApi.run).mockResolvedValue(contributionResult)
    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-test="run-simulation"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test="weather-card"]').text()).not.toContain('当前结果未更新')

    const directionInput = wrapper.findAllComponents({ name: 'ElInputNumber' })[0]
    await directionInput.vm.$emit('update:modelValue', 125)
    await wrapper.vm.$nextTick()

    const weatherCard = wrapper.find('[data-test="weather-card"]')
    expect(weatherCard.text()).toContain('气象参数已修改')
    expect(weatherCard.text()).toContain('当前结果未更新')
    expect(weatherCard.text()).toContain('请点击运行模拟')

    await wrapper.find('[data-test="run-simulation"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test="weather-card"]').text()).not.toContain('当前结果未更新')
    expect(simulationApi.run).toHaveBeenLastCalledWith(
      expect.objectContaining({
        windDirection: 125,
      }),
    )
  })

  it('模拟完成后修改模拟范围或网格分辨率提示页面参数已变化并引导重新模拟', async () => {
    vi.mocked(simulationApi.run).mockResolvedValue(contributionResult)
    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-test="run-simulation"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test="result-card"]').text()).not.toContain('页面参数已变化')

    const rangeSliders = wrapper.findAllComponents({ name: 'ElSlider' })
    await rangeSliders[0].vm.$emit('update:modelValue', 20)
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-test="result-card"]').text()).toContain('页面参数已变化')
    expect(wrapper.find('[data-test="result-card"]').text()).toContain('请重新模拟')

    await rangeSliders[1].vm.$emit('update:modelValue', 200)
    await wrapper.find('[data-test="run-simulation"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test="result-card"]').text()).not.toContain('页面参数已变化')
    expect(simulationApi.run).toHaveBeenLastCalledWith(
      expect.objectContaining({
        domainSize: 20000,
        gridResolution: 200,
      }),
    )
  })

  it('模拟高度变化后提示页面参数已变化，重新运行时提交 receptorHeight', async () => {
    vi.mocked(simulationApi.run).mockResolvedValue(contributionResult)
    const wrapper = mountView()
    await flushPromises()

    await wrapper.find('[data-test="run-simulation"]').trigger('click')
    await flushPromises()

    const heightSlider = wrapper.findComponent('[data-test="simulation-height-slider"]')
    await heightSlider.vm.$emit('update:modelValue', 12)
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-test="result-card"]').text()).toContain('页面参数已变化')

    await wrapper.find('[data-test="run-simulation"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test="result-card"]').text()).not.toContain('页面参数已变化')
    expect(simulationApi.run).toHaveBeenLastCalledWith(
      expect.objectContaining({
        receptorHeight: 12,
      }),
    )
  })

  it('多风向聚合结果不使用单风向气象控件判断过期', async () => {
    const wrapper = mountView()
    await flushPromises()

    wrapper.findComponent({ name: 'ParallelSimulationDialog' }).vm.$emit('completed', {
      success: true,
      mode: 'aggregated',
      totalWindDirections: 16,
      successfulSimulations: 16,
      failedSimulations: 0,
      numWorkersUsed: 4,
      computationTimeSeconds: 1,
      speedupFactor: 4,
      concentrations: [[0, 2]],
      gridLat: [39.9],
      gridLon: [116.4, 116.41],
      receptorContributions: contributionResult.receptorContributions,
      pollutantConcentrations: contributionResult.pollutantConcentrations,
      availablePollutants: contributionResult.availablePollutants,
    }, {
      meteorologyId: 1,
      windSpeed: 3,
      windDirections: Array.from({ length: 16 }, (_, i) => i * 22.5),
      gridResolution: 100,
      domainSize: 5000,
      pollutantType: 'PM2.5',
      receptorHeight: 0,
      returnAggregatedOnly: true,
    })
    await wrapper.vm.$nextTick()

    const speedInput = wrapper.findAllComponents({ name: 'ElInputNumber' })[1]
    await speedInput.vm.$emit('update:modelValue', 5)
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-test="weather-card"]').text()).not.toContain('当前结果未更新')
    expect(wrapper.find('[data-test="result-card"]').text()).not.toContain('页面参数已变化')
  })
})
