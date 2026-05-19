import { describe, expect, it } from 'vitest'
import { filterEntitiesByBounds, pointInBounds, type SelectionBounds } from '@/utils/selection'

const bounds: SelectionBounds = {
  north: 40.1,
  south: 39.9,
  east: 116.5,
  west: 116.3,
}

describe('selection utils', () => {
  it('判断点是否落在矩形选择区域内，边界点视为命中', () => {
    expect(pointInBounds(40.0, 116.4, bounds)).toBe(true)
    expect(pointInBounds(40.1, 116.5, bounds)).toBe(true)
    expect(pointInBounds(39.8, 116.4, bounds)).toBe(false)
    expect(pointInBounds(40.0, 116.6, bounds)).toBe(false)
  })

  it('按矩形选择区域筛选带经纬度实体', () => {
    const entities = [
      { id: 1, latitude: 40.0, longitude: 116.4 },
      { id: 2, latitude: 39.85, longitude: 116.4 },
      { id: 3, latitude: 40.05, longitude: 116.45 },
    ]

    expect(filterEntitiesByBounds(entities, bounds).map((x) => x.id)).toEqual([1, 3])
    expect(filterEntitiesByBounds(entities, null).map((x) => x.id)).toEqual([1, 2, 3])
  })
})
