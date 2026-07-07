import { describe, expect, it } from 'vitest'
import { sourceFitPoints, sourceMapGeometry } from '@/utils/sourceGeometry'
import type { EmissionSource } from '@/types'

function makeSource(overrides: Partial<EmissionSource>): EmissionSource {
  return {
    id: 1,
    name: '源',
    sourceType: 'point',
    latitude: 30,
    longitude: 120,
    height: 0,
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
    markerColor: '#ef4444',
    isActive: true,
    pollutants: [],
    createdAt: '',
    updatedAt: '',
    ...overrides,
  }
}

describe('sourceMapGeometry', () => {
  it('点源使用中心点', () => {
    const geometry = sourceMapGeometry(makeSource({ sourceType: 'point' }))
    expect(geometry).toEqual({ kind: 'point', center: [30, 120] })
  })

  it('面源使用中心点和长宽生成矩形四角', () => {
    const geometry = sourceMapGeometry(makeSource({
      sourceType: 'area',
      areaLength: 200,
      areaWidth: 100,
    }))

    expect(geometry.kind).toBe('polygon')
    if (geometry.kind !== 'polygon') return
    expect(geometry.equivalent).toBe(false)
    expect(geometry.corners).toHaveLength(4)
    expect(geometry.corners[0][0]).toBeLessThan(30)
    expect(geometry.corners[0][1]).toBeLessThan(120)
    expect(geometry.corners[2][0]).toBeGreaterThan(30)
    expect(geometry.corners[2][1]).toBeGreaterThan(120)
  })

  it('面源长宽口径与后端网格一致：AreaLength 控制纬向，AreaWidth 控制经向', () => {
    const geometry = sourceMapGeometry(makeSource({
      sourceType: 'area',
      areaLength: 200,
      areaWidth: 100,
    }))

    expect(geometry.kind).toBe('polygon')
    if (geometry.kind !== 'polygon') return
    const latSpan = geometry.corners[2][0] - geometry.corners[0][0]
    const lonSpan = geometry.corners[2][1] - geometry.corners[0][1]
    const latMeters = latSpan * 111_320
    const lonMeters = lonSpan * 111_320 * Math.cos((30 * Math.PI) / 180)
    expect(latMeters).toBeCloseTo(200, 0)
    expect(lonMeters).toBeCloseTo(100, 0)
  })

  it('等效面源保留等效面源标记用于虚线样式', () => {
    const geometry = sourceMapGeometry(makeSource({
      sourceType: 'equivalent_area',
      areaLength: 200,
      areaWidth: 100,
    }))

    expect(geometry.kind).toBe('polygon')
    if (geometry.kind !== 'polygon') return
    expect(geometry.equivalent).toBe(true)
  })

  it('线源使用起终点生成线段，缺少起终点时回退中心点', () => {
    const line = makeSource({
      sourceType: 'line',
      startLat: 30.1,
      startLon: 120.1,
      endLat: 30.2,
      endLon: 120.2,
    })
    expect(sourceMapGeometry(line)).toEqual({
      kind: 'polyline',
      center: [30, 120],
      points: [[30.1, 120.1], [30.2, 120.2]],
    })
    expect(sourceMapGeometry(makeSource({ sourceType: 'line' }))).toEqual({
      kind: 'point',
      center: [30, 120],
    })
  })

  it('fitBounds 点位包含面源四角和线源端点', () => {
    expect(sourceFitPoints(makeSource({
      sourceType: 'line',
      startLat: 30.1,
      startLon: 120.1,
      endLat: 30.2,
      endLon: 120.2,
    }))).toEqual([[30.1, 120.1], [30.2, 120.2]])
  })
})
