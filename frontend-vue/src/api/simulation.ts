import { http } from './client'
import type {
  ParallelSimulationRequest,
  ParallelSimulationResult,
  SimulationRequest,
  SimulationResult,
} from '@/types'

export const simulationApi = {
  run: (request: SimulationRequest) =>
    http.post<SimulationResult>('/api/simulation/run', request).then((r) => r.data),

  runParallel: (request: ParallelSimulationRequest) =>
    http
      .post<ParallelSimulationResult>('/api/simulation/run_parallel', request)
      .then((r) => r.data),
}
