<script setup lang="ts">
import { onMounted, onUnmounted, ref, shallowRef, watch } from 'vue'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import type { GeoJsonObject } from 'geojson'
import type { EmissionSource, Receptor, SimulationResult } from '@/types'
import { wgs84ToGcj02 } from '@/utils/coords'
import type { SelectionBounds } from '@/utils/selection'
import {
  computeAnchoredBounds,
  renderHeatmapToCanvas,
  type HeatmapDisplayMode,
  type HeatmapOptions,
} from '@/composables/useHeatmapRenderer'
import type { ColorScale } from '@/utils/colorScale'
import { escapeHtml, safeCssColor } from '@/utils/html'
import { sourceFitPoints, sourceMapGeometry, type LatLngTuple } from '@/utils/sourceGeometry'

const props = defineProps<{
  sources: EmissionSource[]
  heatmapSources?: EmissionSource[]
  receptors: Receptor[]
  result?: SimulationResult | null
  scale?: ColorScale
  opacity?: number
  heatmapDisplayMode?: HeatmapDisplayMode
  min?: number | null
  max?: number | null
  renderScale?: number
  tileLayer?: 'street' | 'satellite' | 'hybrid'
  selectionEnabled?: boolean
  boundaryGeoJson?: unknown | null
  initialCenter?: [number, number] | null
  initialZoom?: number | null
}>()

const emit = defineEmits<{
  'selection-change': [bounds: SelectionBounds | null]
  'view-change': [payload: { center: [number, number]; zoom: number }]
}>()

const mapEl = ref<HTMLDivElement | null>(null)
const map = shallowRef<L.Map | null>(null)
const tileLayer = shallowRef<L.TileLayer | null>(null)
const entityLayers = shallowRef<L.Layer[]>([])
const heatmapOverlay = shallowRef<L.ImageOverlay | null>(null)
const boundaryLayer = shallowRef<L.GeoJSON | null>(null)
const selectionOverlay = shallowRef<L.Rectangle | null>(null)
const selectionStart = shallowRef<L.LatLng | null>(null)

// 高德瓦片：lang=zh_cn；坐标体系是 GCJ02
// 街道：style=6, 卫星：style=6 + webst, 混合：lbs+shaded
const TILE_URLS: Record<'street' | 'satellite' | 'hybrid', string> = {
  street: 'https://webrd0{s}.is.autonavi.com/appmaptile?lang=zh_cn&size=1&scale=1&style=8&x={x}&y={y}&z={z}',
  satellite: 'https://webst0{s}.is.autonavi.com/appmaptile?style=6&x={x}&y={y}&z={z}',
  hybrid: 'https://webst0{s}.is.autonavi.com/appmaptile?style=7&x={x}&y={y}&z={z}',
}

function setTileLayer(kind: 'street' | 'satellite' | 'hybrid') {
  if (!map.value) return
  if (tileLayer.value) tileLayer.value.remove()
  tileLayer.value = L.tileLayer(TILE_URLS[kind], {
    subdomains: ['1', '2', '3', '4'],
    maxZoom: 18,
    attribution: '© 高德地图',
  }).addTo(map.value)
}

function clearEntityLayers() {
  for (const layer of entityLayers.value) layer.remove()
  entityLayers.value = []
}

function toGcjTuple(point: LatLngTuple): L.LatLngTuple {
  return wgs84ToGcj02(point[0], point[1])
}

function sourcePopup(source: EmissionSource) {
  return `<strong>${escapeHtml(source.name)}</strong><br>类型: ${escapeHtml(source.sourceType)}<br>高度: ${escapeHtml(source.height)} m`
}

function sourcePointMarker(source: EmissionSource, point: LatLngTuple, size = 14) {
  const radius = size / 2
  const icon = L.divIcon({
    className: 'gnn-marker',
    html: `<div style="width:${size}px;height:${size}px;border-radius:50%;background:${safeCssColor(source.markerColor)};border:2px solid #fff;box-shadow:0 0 3px rgba(0,0,0,0.4);"></div>`,
    iconSize: [size, size],
    iconAnchor: [radius, radius],
  })
  return L.marker(toGcjTuple(point), { icon }).bindPopup(sourcePopup(source))
}

function resultAnchorPoint(): LatLngTuple | null {
  const anchorSources = props.heatmapSources?.length ? props.heatmapSources : props.sources
  const points = anchorSources.flatMap((source) => sourceFitPoints(source))
  if (points.length === 0) return null
  const lats = points.map(([lat]) => lat)
  const lons = points.map(([, lon]) => lon)
  return [
    (Math.min(...lats) + Math.max(...lats)) / 2,
    (Math.min(...lons) + Math.max(...lons)) / 2,
  ]
}

function renderMarkers() {
  if (!map.value) return
  clearEntityLayers()

  // 排放源按类型显示：点源=点，面源/等效面源=矩形面，线源=线段。
  for (const s of props.sources) {
    const geometry = sourceMapGeometry(s)
    if (geometry.kind === 'polygon') {
      const layer = L.polygon(geometry.corners.map(toGcjTuple), {
        color: geometry.equivalent ? '#7c3aed' : safeCssColor(s.markerColor),
        weight: 2,
        dashArray: geometry.equivalent ? '6 4' : undefined,
        fillColor: geometry.equivalent ? '#8b5cf6' : safeCssColor(s.markerColor),
        fillOpacity: geometry.equivalent ? 0.16 : 0.22,
      }).bindPopup(sourcePopup(s))
      layer.addTo(map.value)
      entityLayers.value.push(layer)
    } else if (geometry.kind === 'polyline') {
      const line = L.polyline(geometry.points.map(toGcjTuple), {
        color: safeCssColor(s.markerColor),
        weight: Math.max(3, Math.min(10, s.lineWidth ?? 4)),
        opacity: 0.85,
      }).bindPopup(sourcePopup(s))
      line.addTo(map.value)
      entityLayers.value.push(line)
      for (const point of geometry.points) {
        const endpoint = sourcePointMarker(s, point, 10)
        endpoint.addTo(map.value)
        entityLayers.value.push(endpoint)
      }
    } else {
      const marker = sourcePointMarker(s, geometry.center)
      marker.addTo(map.value)
      entityLayers.value.push(marker)
    }
  }

  // 受体标记（蓝色方块）
  for (const r of props.receptors) {
    const [lat, lon] = wgs84ToGcj02(r.latitude, r.longitude)
    const icon = L.divIcon({
      className: 'gnn-marker',
      html: `<div style="width:12px;height:12px;background:${safeCssColor(r.markerColor)};border:2px solid #fff;box-shadow:0 0 3px rgba(0,0,0,0.4);"></div>`,
      iconSize: [12, 12],
      iconAnchor: [6, 6],
    })
    const marker = L.marker([lat, lon], { icon }).bindPopup(
      `<strong>${escapeHtml(r.name)}</strong><br>受体点<br>高度: ${escapeHtml(r.height)} m`,
    )
    marker.addTo(map.value)
    entityLayers.value.push(marker)
  }
}

function renderHeatmap() {
  if (!map.value || !heatmapOverlay.value && !props.result) return
  if (heatmapOverlay.value) {
    heatmapOverlay.value.remove()
    heatmapOverlay.value = null
  }
  if (!props.result) return

  const { concentrations, gridLat, gridLon } = props.result
  if (!concentrations.length || !gridLat.length || !gridLon.length) return

  let min = props.min ?? 0
  let max = props.max ?? 0
  if (!props.min || !props.max) {
    for (const row of concentrations)
      for (const v of row)
        if (v > max) max = v
    if (!props.min) min = 0
  }
  if (max <= 0) return

  const opts: HeatmapOptions = {
    concentrations,
    gridLat,
    gridLon,
    min,
    max,
    scale: props.scale ?? 'jet',
    opacity: props.opacity ?? 0.7,
    displayMode: props.heatmapDisplayMode ?? 'plume',
    renderScale: props.renderScale ?? 2,
    useGcj02: true,
  }
  const canvas = renderHeatmapToCanvas(opts)
  const url = canvas.toDataURL('image/png')
  const bounds = computeAnchoredBounds(gridLat, gridLon, resultAnchorPoint(), true)
  heatmapOverlay.value = L.imageOverlay(url, bounds, {
    opacity: 1,
    interactive: false,
  }).addTo(map.value)
}

function clearBoundaryLayer() {
  if (!boundaryLayer.value) return
  boundaryLayer.value.remove()
  boundaryLayer.value = null
}

function renderBoundaryLayer() {
  if (!map.value) return
  clearBoundaryLayer()
  if (!props.boundaryGeoJson) return

  boundaryLayer.value = L.geoJSON(props.boundaryGeoJson as GeoJsonObject, {
    style: {
      color: '#0f766e',
      weight: 1.4,
      opacity: 0.85,
      fillColor: '#14b8a6',
      fillOpacity: 0.04,
    },
    coordsToLatLng: (coords) => {
      const [lat, lon] = wgs84ToGcj02(coords[1], coords[0])
      return L.latLng(lat, lon, coords[2])
    },
  }).addTo(map.value)
}

function emitViewChange() {
  if (!map.value) return
  const center = map.value.getCenter()
  emit('view-change', {
    center: [center.lat, center.lng],
    zoom: map.value.getZoom(),
  })
}

function normalizeBounds(bounds: L.LatLngBounds): SelectionBounds {
  const north = bounds.getNorth()
  const south = bounds.getSouth()
  const east = bounds.getEast()
  const west = bounds.getWest()
  return { north, south, east, west }
}

function clearSelection() {
  if (selectionOverlay.value) {
    selectionOverlay.value.remove()
    selectionOverlay.value = null
  }
  selectionStart.value = null
  emit('selection-change', null)
}

function fitSelection() {
  if (!map.value || !selectionOverlay.value) return
  map.value.fitBounds(selectionOverlay.value.getBounds().pad(0.12), { animate: true })
}

function startSelection(e: L.LeafletMouseEvent) {
  if (!props.selectionEnabled || !map.value) return
  selectionStart.value = e.latlng
  if (selectionOverlay.value) selectionOverlay.value.remove()
  selectionOverlay.value = L.rectangle(L.latLngBounds(e.latlng, e.latlng), {
    color: '#2563eb',
    weight: 2,
    dashArray: '6 4',
    fillColor: '#60a5fa',
    fillOpacity: 0.16,
  }).addTo(map.value)
  map.value.dragging.disable()
}

function cancelSelectionDrag() {
  if (!selectionStart.value) return
  selectionStart.value = null
  if (selectionOverlay.value) {
    selectionOverlay.value.remove()
    selectionOverlay.value = null
  }
  if (map.value) map.value.dragging.enable()
}

function updateSelection(e: L.LeafletMouseEvent) {
  if (!props.selectionEnabled || !selectionStart.value || !selectionOverlay.value) return
  selectionOverlay.value.setBounds(L.latLngBounds(selectionStart.value, e.latlng))
}

function finishSelection(e: L.LeafletMouseEvent) {
  if (!props.selectionEnabled || !map.value || !selectionStart.value || !selectionOverlay.value) return
  selectionOverlay.value.setBounds(L.latLngBounds(selectionStart.value, e.latlng))
  map.value.dragging.enable()
  selectionStart.value = null
  emit('selection-change', normalizeBounds(selectionOverlay.value.getBounds()))
}

function fitBounds() {
  if (!map.value) return
  const all: L.LatLngTuple[] = []
  for (const s of props.sources) {
    for (const point of sourceFitPoints(s)) all.push(toGcjTuple(point))
  }
  for (const r of props.receptors) all.push(wgs84ToGcj02(r.latitude, r.longitude))
  if (all.length === 0) return
  const bounds = L.latLngBounds(all)
  map.value.fitBounds(bounds.pad(0.2), { animate: true })
}

function fitResultBounds() {
  if (!map.value || !props.result?.gridLat?.length || !props.result?.gridLon?.length) {
    fitBounds()
    return
  }
  const anchoredBounds = computeAnchoredBounds(props.result.gridLat, props.result.gridLon, resultAnchorPoint(), true) as [
    L.LatLngTuple,
    L.LatLngTuple,
  ]
  const bounds = L.latLngBounds(anchoredBounds[0], anchoredBounds[1])
  map.value.fitBounds(bounds.pad(0.08), { animate: true })
}

defineExpose({ fitBounds, fitResultBounds, clearSelection, fitSelection })

onMounted(() => {
  if (!mapEl.value) return
  map.value = L.map(mapEl.value, {
    center: props.initialCenter ?? [39.9, 116.4],
    zoom: props.initialZoom ?? 10,
    zoomControl: true,
    attributionControl: false,
  })
  setTileLayer(props.tileLayer ?? 'street')
  map.value.on('mousedown', startSelection)
  map.value.on('mousemove', updateSelection)
  map.value.on('mouseup', finishSelection)
  map.value.on('moveend zoomend', emitViewChange)
  window.addEventListener('mouseup', cancelSelectionDrag)
  renderMarkers()
  renderHeatmap()
  renderBoundaryLayer()
  // 初次加载若有数据则自适应
  if (!props.initialCenter && props.sources.length + props.receptors.length > 0) {
    setTimeout(fitBounds, 100)
  }
})

onUnmounted(() => {
  if (map.value) {
    map.value.off('mousedown', startSelection)
    map.value.off('mousemove', updateSelection)
    map.value.off('mouseup', finishSelection)
    map.value.off('moveend zoomend', emitViewChange)
  }
  window.removeEventListener('mouseup', cancelSelectionDrag)
  if (selectionOverlay.value) selectionOverlay.value.remove()
  if (heatmapOverlay.value) heatmapOverlay.value.remove()
  clearBoundaryLayer()
  clearEntityLayers()
  if (tileLayer.value) tileLayer.value.remove()
  if (map.value) map.value.remove()
})

watch(
  () => props.tileLayer,
  (v) => v && setTileLayer(v),
)
watch(
  () => [props.sources, props.receptors],
  () => renderMarkers(),
  { deep: true },
)
watch(
  () => [
    props.result,
    props.scale,
    props.opacity,
    props.heatmapDisplayMode,
    props.min,
    props.max,
    props.renderScale,
  ],
  () => renderHeatmap(),
  { deep: true },
)
watch(
  () => props.selectionEnabled,
  (enabled) => {
    if (!enabled) selectionStart.value = null
    if (!enabled && map.value) map.value.dragging.enable()
  },
)
watch(
  () => props.boundaryGeoJson,
  () => renderBoundaryLayer(),
)
</script>

<template>
  <div ref="mapEl" class="map-panel" />
</template>

<style scoped>
.map-panel {
  width: 100%;
  height: 100%;
  min-height: 400px;
  background: #f3f4f6;
}
</style>

<style>
.gnn-marker {
  background: transparent;
  border: none;
}
</style>
