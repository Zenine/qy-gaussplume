import { http } from './client'
import type { Receptor, ReceptorCreate, ReceptorUpdate } from '@/types'

export const receptorsApi = {
  list: (skip = 0, limit = 100) =>
    http.get<Receptor[]>('/api/receptors', { params: { skip, limit } }).then((r) => r.data),

  get: (id: number) => http.get<Receptor>(`/api/receptors/${id}`).then((r) => r.data),

  create: (payload: ReceptorCreate) =>
    http.post<Receptor>('/api/receptors', payload).then((r) => r.data),

  createBatch: (payload: ReceptorCreate[]) =>
    http.post<Receptor[]>('/api/receptors/batch', payload).then((r) => r.data),

  update: (id: number, payload: ReceptorUpdate) =>
    http.put<Receptor>(`/api/receptors/${id}`, payload).then((r) => r.data),

  delete: (id: number) => http.delete(`/api/receptors/${id}`).then((r) => r.data),

  downloadTemplate: () =>
    http.get('/api/receptors/template', { responseType: 'blob' }).then((r) => r.data),

  importFile: (file: File) => {
    const form = new FormData()
    form.append('file', file)
    return http
      .post<{ imported_count: number; errors: string[] | null; message: string }>(
        '/api/receptors/import',
        form,
        { headers: { 'Content-Type': 'multipart/form-data' } },
      )
      .then((r) => r.data)
  },

  export: (ids: number[]) =>
    http.post('/api/receptors/export', ids, { responseType: 'blob' }).then((r) => r.data),
}
