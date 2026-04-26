<script setup lang="ts">
import { onMounted, onUnmounted, ref, shallowRef, watch } from 'vue'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import type { EmissionSource, Receptor, SimulationResult } from '@/types'
import { wgs84ToGcj02 } from '@/utils/coords'
import {
  computeBounds,
  renderHeatmapToCanvas,
  type HeatmapOptions,
} from '@/composables/useHeatmapRenderer'
import type { ColorScale } from '@/utils/colorScale'

const props = defineProps<{
  sources: EmissionSource[]
  receptors: Receptor[]
  result?: SimulationResult | null
  scale?: ColorScale
  opacity?: number
  min?: number | null
  max?: number | null
  renderScale?: number
  tileLayer?: 'street' | 'satellite' | 'hybrid'
}>()

const mapEl = ref<HTMLDivElement | null>(null)
const map = shallowRef<L.Map | null>(null)
const tileLayer = shallowRef<L.TileLayer | null>(null)
const markers = shallowRef<L.Marker[]>([])
const heatmapOverlay = shallowRef<L.ImageOverlay | null>(null)

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

function clearMarkers() {
  for (const m of markers.value) m.remove()
  markers.value = []
}

function renderMarkers() {
  if (!map.value) return
  clearMarkers()

  // 源标记（红色圆点）
  for (const s of props.sources) {
    const [lat, lon] = wgs84ToGcj02(s.latitude, s.longitude)
    const icon = L.divIcon({
      className: 'gnn-marker',
      html: `<div style="width:14px;height:14px;border-radius:50%;background:${s.markerColor};border:2px solid #fff;box-shadow:0 0 3px rgba(0,0,0,0.4);"></div>`,
      iconSize: [14, 14],
      iconAnchor: [7, 7],
    })
    const marker = L.marker([lat, lon], { icon }).bindPopup(
      `<strong>${s.name}</strong><br>类型: ${s.sourceType}<br>高度: ${s.height} m`,
    )
    marker.addTo(map.value)
    markers.value.push(marker)
  }

  // 受体标记（蓝色方块）
  for (const r of props.receptors) {
    const [lat, lon] = wgs84ToGcj02(r.latitude, r.longitude)
    const icon = L.divIcon({
      className: 'gnn-marker',
      html: `<div style="width:12px;height:12px;background:${r.markerColor};border:2px solid #fff;box-shadow:0 0 3px rgba(0,0,0,0.4);"></div>`,
      iconSize: [12, 12],
      iconAnchor: [6, 6],
    })
    const marker = L.marker([lat, lon], { icon }).bindPopup(
      `<strong>${r.name}</strong><br>受体点<br>高度: ${r.height} m`,
    )
    marker.addTo(map.value)
    markers.value.push(marker)
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
    renderScale: props.renderScale ?? 2,
    useGcj02: true,
  }
  const canvas = renderHeatmapToCanvas(opts)
  const url = canvas.toDataURL('image/png')
  const bounds = computeBounds(gridLat, gridLon, true)
  heatmapOverlay.value = L.imageOverlay(url, bounds, {
    opacity: 1,
    interactive: false,
  }).addTo(map.value)
}

function fitBounds() {
  if (!map.value) return
  const all: L.LatLngTuple[] = []
  for (const s of props.sources) all.push(wgs84ToGcj02(s.latitude, s.longitude))
  for (const r of props.receptors) all.push(wgs84ToGcj02(r.latitude, r.longitude))
  if (all.length === 0) return
  const bounds = L.latLngBounds(all)
  map.value.fitBounds(bounds.pad(0.2), { animate: true })
}

defineExpose({ fitBounds })

onMounted(() => {
  if (!mapEl.value) return
  map.value = L.map(mapEl.value, {
    center: [39.9, 116.4],
    zoom: 10,
    zoomControl: true,
    attributionControl: false,
  })
  setTileLayer(props.tileLayer ?? 'street')
  renderMarkers()
  renderHeatmap()
  // 初次加载若有数据则自适应
  if (props.sources.length + props.receptors.length > 0) {
    setTimeout(fitBounds, 100)
  }
})

onUnmounted(() => {
  if (heatmapOverlay.value) heatmapOverlay.value.remove()
  clearMarkers()
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
  () => [props.result, props.scale, props.opacity, props.min, props.max, props.renderScale],
  () => renderHeatmap(),
  { deep: true },
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
