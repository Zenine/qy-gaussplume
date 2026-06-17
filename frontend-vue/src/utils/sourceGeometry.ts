import type { EmissionSource } from '@/types'

export type LatLngTuple = [number, number]

export type SourceMapGeometry =
  | { kind: 'point'; center: LatLngTuple }
  | { kind: 'polygon'; center: LatLngTuple; corners: LatLngTuple[]; equivalent: boolean }
  | { kind: 'polyline'; center: LatLngTuple; points: LatLngTuple[] }

const METERS_PER_LAT_DEGREE = 111_320

function isPositive(value: number | null | undefined): value is number {
  return typeof value === 'number' && Number.isFinite(value) && value > 0
}

function isNumber(value: number | null | undefined): value is number {
  return typeof value === 'number' && Number.isFinite(value)
}

function metersToLatitudeDegrees(meters: number) {
  return meters / METERS_PER_LAT_DEGREE
}

function metersToLongitudeDegrees(meters: number, latitude: number) {
  const cosine = Math.cos((latitude * Math.PI) / 180)
  return meters / (METERS_PER_LAT_DEGREE * Math.max(0.01, Math.abs(cosine)))
}

export function sourceMapGeometry(source: EmissionSource): SourceMapGeometry {
  const center: LatLngTuple = [source.latitude, source.longitude]
  if (
    (source.sourceType === 'area' || source.sourceType === 'equivalent_area')
    && isPositive(source.areaLength)
    && isPositive(source.areaWidth)
  ) {
    const halfLat = metersToLatitudeDegrees(source.areaWidth / 2)
    const halfLon = metersToLongitudeDegrees(source.areaLength / 2, source.latitude)
    return {
      kind: 'polygon',
      center,
      equivalent: source.sourceType === 'equivalent_area',
      corners: [
        [source.latitude - halfLat, source.longitude - halfLon],
        [source.latitude - halfLat, source.longitude + halfLon],
        [source.latitude + halfLat, source.longitude + halfLon],
        [source.latitude + halfLat, source.longitude - halfLon],
      ],
    }
  }

  if (
    source.sourceType === 'line'
    && isNumber(source.startLat)
    && isNumber(source.startLon)
    && isNumber(source.endLat)
    && isNumber(source.endLon)
  ) {
    return {
      kind: 'polyline',
      center,
      points: [
        [source.startLat, source.startLon],
        [source.endLat, source.endLon],
      ],
    }
  }

  return { kind: 'point', center }
}

export function sourceFitPoints(source: EmissionSource): LatLngTuple[] {
  const geometry = sourceMapGeometry(source)
  if (geometry.kind === 'polygon') return geometry.corners
  if (geometry.kind === 'polyline') return geometry.points
  return [geometry.center]
}
