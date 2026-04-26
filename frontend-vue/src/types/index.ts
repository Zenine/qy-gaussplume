// 与 .NET 后端 DTO 一一对齐（字段为 camelCase，由 System.Text.Json 默认序列化约定）

// -------- 污染物 & 标记 --------
export interface PollutantTypeInfo {
  type: string
  name: string
  unit: string
  description: string
}

export interface MarkerSymbolInfo {
  symbol: string
  name: string
  icon: string
}

// -------- 排放源 --------
export type SourceType = 'point' | 'area' | 'equivalent_area' | 'line'

export interface PollutantEmission {
  id: number
  sourceId: number
  pollutantType: string
  emissionRate: number
  concentration: number | null
  createdAt: string
  updatedAt: string
}

export interface PollutantEmissionCreate {
  pollutantType: string
  emissionRate?: number
  concentration?: number | null
}

export interface EmissionSource {
  id: number
  name: string
  sourceType: string
  latitude: number
  longitude: number
  height: number
  temperature: number | null
  velocity: number | null
  diameter: number | null
  areaShape: string | null
  areaLength: number | null
  areaWidth: number | null
  areaHeight: number | null
  areaTemperature: number | null
  sigmaZ0Area: number | null
  lineType: string | null
  startLon: number | null
  startLat: number | null
  endLon: number | null
  endLat: number | null
  lineWidth: number | null
  lineHeight: number | null
  lineTemperature: number | null
  sigmaZ0Line: number | null
  lineSegmentLength: number | null
  markerSymbol: string
  markerColor: string
  isActive: boolean
  pollutants: PollutantEmission[]
  createdAt: string
  updatedAt: string
}

export interface EmissionSourceCreate {
  name: string
  sourceType?: string
  latitude: number
  longitude: number
  height?: number
  temperature?: number
  velocity?: number
  diameter?: number
  areaShape?: string
  areaLength?: number
  areaWidth?: number
  areaHeight?: number
  areaTemperature?: number
  sigmaZ0Area?: number
  lineType?: string
  startLon?: number
  startLat?: number
  endLon?: number
  endLat?: number
  lineWidth?: number
  lineHeight?: number
  lineTemperature?: number
  sigmaZ0Line?: number
  lineSegmentLength?: number
  markerSymbol?: string
  markerColor?: string
  isActive?: boolean
  pollutants?: PollutantEmissionCreate[]
}

export type EmissionSourceUpdate = Partial<EmissionSourceCreate>

// -------- 受体 --------
export interface Receptor {
  id: number
  name: string
  latitude: number
  longitude: number
  height: number
  markerSymbol: string
  markerColor: string
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface ReceptorCreate {
  name: string
  latitude: number
  longitude: number
  height?: number
  markerSymbol?: string
  markerColor?: string
  isActive?: boolean
}

export type ReceptorUpdate = Partial<ReceptorCreate>

// -------- 气象 --------
export interface Meteorology {
  id: number
  name: string
  windSpeed: number
  windDirection: number
  boundaryLayerHeight: number | null
  stabilityClass: string | null
  temperature: number | null
  humidity: number | null
  cloudCover: number | null
  precipitation: number | null
  recordTime: string
  createdAt: string
  updatedAt: string
}

export interface MeteorologyCreate {
  name: string
  windSpeed?: number
  windDirection?: number
  boundaryLayerHeight?: number
  stabilityClass?: string
  temperature?: number
  humidity?: number
  cloudCover?: number
  precipitation?: number
  recordTime?: string
}

export type MeteorologyUpdate = Partial<MeteorologyCreate>

// -------- 标记配置 --------
export interface MarkerConfig {
  id: number
  type: string
  symbol: string | null
  color: string | null
  size: number | null
  createdAt: string
  updatedAt: string
}

// -------- 模拟 --------
export interface SimulationRequest {
  meteorologyId: number
  sourceIds?: number[]
  receptorIds?: number[]
  pollutantType?: string
  gridResolution?: number
  domainSize?: number
  receptorHeight?: number
}

export interface SourceContribution {
  sourceId: number
  sourceName: string
  totalConcentration: number
  maxConcentration: number
  pollutants: string[]
}

export interface ReceptorContributionEntry {
  sourceId: number
  sourceName: string
  concentration: number
  pollutant: string
  percentage: number
}

export interface SimulationResult {
  concentrations: number[][]
  gridLat: number[]
  gridLon: number[]
  contributions: SourceContribution[]
  receptorContributions: Record<string, Record<string, ReceptorContributionEntry[]>>
  pollutantConcentrations: Record<string, number[][]> | null
  availablePollutants: string[] | null
}

// -------- 并行模拟 --------
export interface ParallelSimulationRequest {
  meteorologyId: number
  sourceIds?: number[]
  receptorIds?: number[]
  pollutantType?: string
  gridResolution?: number
  domainSize?: number
  windSpeed: number
  windDirections: number[]
  weights?: number[]
  receptorHeight?: number
  numWorkers?: number
  returnAggregatedOnly?: boolean
}

export interface WindDirectionError {
  windDirection: number
  error: string
}

export interface WindDirectionResult {
  windDirection: number
  success: boolean
  error?: string | null
  concentrations?: number[][] | null
  gridLat?: number[] | null
  gridLon?: number[] | null
  contributions?: SourceContribution[] | null
  pollutantConcentrations?: Record<string, number[][]> | null
  availablePollutants?: string[] | null
  receptorContributions?: Record<string, Record<string, ReceptorContributionEntry[]>> | null
}

export interface ParallelSimulationResult {
  success: boolean
  mode: 'aggregated' | 'detailed'
  totalWindDirections: number
  successfulSimulations: number
  failedSimulations: number
  errors?: WindDirectionError[] | null
  numWorkersUsed: number
  computationTimeSeconds: number
  speedupFactor: number
  concentrations?: number[][] | null
  gridLat?: number[] | null
  gridLon?: number[] | null
  pollutantConcentrations?: Record<string, number[][]> | null
  availablePollutants?: string[] | null
  contributions?: unknown[]
  receptorContributions?: Record<string, Record<string, ReceptorContributionEntry[]>> | null
  results?: WindDirectionResult[] | null
}

// -------- 地图 --------
export interface MapBounds {
  minLat: number
  minLon: number
  maxLat: number
  maxLon: number
}

export interface MapInfo {
  crs: string
  featureCount: number
  columns: string[]
  bounds: MapBounds
  error: string | null
}
