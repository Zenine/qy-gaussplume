import { http } from './client'
import type {
  EmissionSource,
  EmissionSourceCreate,
  EmissionSourceUpdate,
  MarkerSymbolInfo,
  PollutantEmission,
  PollutantEmissionCreate,
  PollutantTypeInfo,
} from '@/types'

export const sourcesApi = {
  list: (skip = 0, limit = 100) =>
    http.get<EmissionSource[]>('/api/sources', { params: { skip, limit } }).then((r) => r.data),

  get: (id: number) => http.get<EmissionSource>(`/api/sources/${id}`).then((r) => r.data),

  create: (payload: EmissionSourceCreate) =>
    http.post<EmissionSource>('/api/sources', payload).then((r) => r.data),

  createBatch: (payload: EmissionSourceCreate[]) =>
    http.post<EmissionSource[]>('/api/sources/batch', payload).then((r) => r.data),

  update: (id: number, payload: EmissionSourceUpdate) =>
    http.put<EmissionSource>(`/api/sources/${id}`, payload).then((r) => r.data),

  delete: (id: number) => http.delete(`/api/sources/${id}`).then((r) => r.data),

  pollutantTypes: () =>
    http.get<PollutantTypeInfo[]>('/api/sources/pollutant-types').then((r) => r.data),

  markerSymbols: () =>
    http.get<MarkerSymbolInfo[]>('/api/sources/marker-symbols').then((r) => r.data),

  addPollutant: (id: number, dto: PollutantEmissionCreate) =>
    http.post<PollutantEmission>(`/api/sources/${id}/pollutants`, dto).then((r) => r.data),

  deletePollutant: (sourceId: number, pollutantId: number) =>
    http.delete(`/api/sources/${sourceId}/pollutants/${pollutantId}`).then((r) => r.data),

  downloadTemplate: (sourceType: string) =>
    http.get(`/api/sources/template/${sourceType}`, { responseType: 'blob' }).then((r) => r.data),

  importFile: (sourceType: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return http
      .post<{ imported_count: number; errors: string[] | null; message: string }>(
        `/api/sources/import/${sourceType}`,
        form,
        { headers: { 'Content-Type': 'multipart/form-data' } },
      )
      .then((r) => r.data)
  },
}
