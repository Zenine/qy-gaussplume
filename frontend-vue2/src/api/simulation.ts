import { http } from './client'
import type {
  ParallelSimulationRequest,
  ParallelSimulationResult,
  SimulationFormulaInfo,
  SimulationRequest,
  SimulationResult,
  WindProfileImportResult,
} from '@/types'

export const simulationApi = {
  run: (request: SimulationRequest) =>
    http.post<SimulationResult>('/api/simulation/run', request).then((r) => r.data),

  runParallel: (request: ParallelSimulationRequest) =>
    http
      .post<ParallelSimulationResult>('/api/simulation/run_parallel', request)
      .then((r) => r.data),

  downloadWindProfileTemplate: () =>
    http.get('/api/simulation/wind-profile/template', { responseType: 'blob' }).then((r) => r.data),

  importWindProfile: (file: File) => {
    const form = new FormData()
    form.append('file', file)
    return http
      .post<WindProfileImportResult>('/api/simulation/wind-profile/import', form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      .then((r) => r.data)
  },

  formulas: () =>
    http.get<SimulationFormulaInfo>('/api/simulation/formulas').then((r) => r.data),
}
