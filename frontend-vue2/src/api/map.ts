import { http } from './client'
import type { MapBounds, MapInfo } from '@/types'

export const mapApi = {
  getBounds: () => http.get<MapBounds>('/api/map/bounds').then((r) => r.data),
  getInfo: () => http.get<MapInfo>('/api/map/info').then((r) => r.data),
  getGeoJson: (force = false) =>
    http.get('/api/map/geojson', { params: { force } }).then((r) => r.data),
}
