export interface SelectionBounds {
  north: number
  south: number
  east: number
  west: number
}

interface LatLonEntity {
  latitude: number
  longitude: number
}

// 判断一个经纬度点是否位于矩形选择范围内。边界点视为命中，便于用户贴边框选。
export function pointInBounds(latitude: number, longitude: number, bounds: SelectionBounds) {
  return (
    latitude >= bounds.south &&
    latitude <= bounds.north &&
    longitude >= bounds.west &&
    longitude <= bounds.east
  )
}

// 按矩形范围筛选带经纬度的实体。bounds 为空时返回原集合，表示未启用空间筛选。
export function filterEntitiesByBounds<T extends LatLonEntity>(
  entities: T[],
  bounds: SelectionBounds | null,
  project: (entity: T) => LatLonEntity = (entity) => entity,
) {
  if (!bounds) return entities
  return entities.filter((entity) => {
    const point = project(entity)
    return pointInBounds(point.latitude, point.longitude, bounds)
  })
}
