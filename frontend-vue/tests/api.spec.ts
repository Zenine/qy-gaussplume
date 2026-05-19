import { describe, expect, it, vi, afterEach } from 'vitest'
import { http } from '@/api/client'
import { sourcesApi, receptorsApi, meteorologyApi, simulationApi, mapApi } from '@/api'
import type {
  EmissionSource,
  Meteorology,
  Receptor,
  SimulationResult,
  MapInfo,
  ParallelSimulationResult,
} from '@/types'

// 验证 API 客户端 URL/payload 正确性，不依赖真实后端
afterEach(() => vi.restoreAllMocks())

describe('sourcesApi', () => {
  it('list → GET /api/sources with skip/limit params', async () => {
    const spy = vi
      .spyOn(http, 'get')
      .mockResolvedValue({ data: [] as EmissionSource[] })
    await sourcesApi.list(10, 50)
    expect(spy).toHaveBeenCalledWith('/api/sources', { params: { skip: 10, limit: 50 } })
  })

  it('create → POST /api/sources', async () => {
    const spy = vi.spyOn(http, 'post').mockResolvedValue({ data: {} })
    await sourcesApi.create({
      name: '源',
      latitude: 39.9,
      longitude: 116.4,
      pollutants: [{ pollutantType: 'PM2.5', emissionRate: 1.0 }],
    })
    expect(spy).toHaveBeenCalledWith(
      '/api/sources',
      expect.objectContaining({ name: '源' }),
    )
  })

  it('pollutantTypes → GET /api/sources/pollutant-types', async () => {
    const spy = vi.spyOn(http, 'get').mockResolvedValue({ data: [] })
    await sourcesApi.pollutantTypes()
    expect(spy).toHaveBeenCalledWith('/api/sources/pollutant-types')
  })
})

describe('receptorsApi', () => {
  it('list 默认 skip=0 limit=100', async () => {
    const spy = vi.spyOn(http, 'get').mockResolvedValue({ data: [] as Receptor[] })
    await receptorsApi.list()
    expect(spy).toHaveBeenCalledWith('/api/receptors', { params: { skip: 0, limit: 100 } })
  })

  it('export 返回 blob', async () => {
    const blob = new Blob(['test'])
    const spy = vi.spyOn(http, 'post').mockResolvedValue({ data: blob })
    const result = await receptorsApi.export([1, 2])
    expect(spy).toHaveBeenCalledWith('/api/receptors/export', [1, 2], { responseType: 'blob' })
    expect(result).toBe(blob)
  })
})

describe('meteorologyApi', () => {
  it('create 透传 payload', async () => {
    const spy = vi.spyOn(http, 'post').mockResolvedValue({ data: {} as Meteorology })
    await meteorologyApi.create({ name: '北风', windSpeed: 3.0, windDirection: 0 })
    expect(spy).toHaveBeenCalledWith(
      '/api/meteorology',
      expect.objectContaining({ name: '北风', windSpeed: 3.0 }),
    )
  })

  it('update 部分字段', async () => {
    const spy = vi.spyOn(http, 'put').mockResolvedValue({ data: {} as Meteorology })
    await meteorologyApi.update(42, { windSpeed: 5.5 })
    expect(spy).toHaveBeenCalledWith('/api/meteorology/42', { windSpeed: 5.5 })
  })
})

describe('simulationApi', () => {
  it('run → POST /api/simulation/run', async () => {
    const spy = vi.spyOn(http, 'post').mockResolvedValue({ data: {} as SimulationResult })
    await simulationApi.run({ meteorologyId: 1 })
    expect(spy).toHaveBeenCalledWith('/api/simulation/run', { meteorologyId: 1 })
  })

  it('runParallel 带 wind_directions 列表', async () => {
    const spy = vi
      .spyOn(http, 'post')
      .mockResolvedValue({ data: {} as ParallelSimulationResult })
    await simulationApi.runParallel({
      meteorologyId: 1,
      windSpeed: 3,
      windDirections: [0, 90, 180, 270],
    })
    expect(spy).toHaveBeenCalledWith(
      '/api/simulation/run_parallel',
      expect.objectContaining({ windDirections: [0, 90, 180, 270] }),
    )
  })
})

describe('mapApi', () => {
  it('getBounds → GET /api/map/bounds', async () => {
    const spy = vi.spyOn(http, 'get').mockResolvedValue({ data: {} })
    await mapApi.getBounds()
    expect(spy).toHaveBeenCalledWith('/api/map/bounds')
  })

  it('getInfo → GET /api/map/info', async () => {
    const spy = vi.spyOn(http, 'get').mockResolvedValue({ data: {} as MapInfo })
    await mapApi.getInfo()
    expect(spy).toHaveBeenCalledWith('/api/map/info')
  })

  it('getGeoJson 默认 force=false', async () => {
    const spy = vi.spyOn(http, 'get').mockResolvedValue({ data: {} })
    await mapApi.getGeoJson()
    expect(spy).toHaveBeenCalledWith('/api/map/geojson', { params: { force: false } })
  })
})
