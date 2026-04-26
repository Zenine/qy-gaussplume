import { http } from './client'
import type { Meteorology, MeteorologyCreate, MeteorologyUpdate } from '@/types'

export const meteorologyApi = {
  list: (skip = 0, limit = 100) =>
    http.get<Meteorology[]>('/api/meteorology', { params: { skip, limit } }).then((r) => r.data),

  get: (id: number) => http.get<Meteorology>(`/api/meteorology/${id}`).then((r) => r.data),

  create: (payload: MeteorologyCreate) =>
    http.post<Meteorology>('/api/meteorology', payload).then((r) => r.data),

  createBatch: (payload: MeteorologyCreate[]) =>
    http.post<Meteorology[]>('/api/meteorology/batch', payload).then((r) => r.data),

  update: (id: number, payload: MeteorologyUpdate) =>
    http.put<Meteorology>(`/api/meteorology/${id}`, payload).then((r) => r.data),

  delete: (id: number) => http.delete(`/api/meteorology/${id}`).then((r) => r.data),
}
