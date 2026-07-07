<template><div ref="mapEl" class="map-panel" /></template>

<script lang="ts">
import Vue from 'vue'
import L from 'leaflet'
import type { EmissionSource, Receptor, SimulationResult } from '@/types'
import { wgs84ToGcj02 } from '@/utils/coords'
import { escapeHtml, safeCssColor } from '@/utils/html'
import { sourceFitPoints, sourceMapGeometry, type LatLngTuple } from '@/utils/sourceGeometry'
import { computeAnchoredBounds, renderHeatmapToCanvas } from '@/composables/useHeatmapRenderer'

const TILE_URLS: Record<string, string> = {
  street: 'https://webrd0{s}.is.autonavi.com/appmaptile?lang=zh_cn&size=1&scale=1&style=8&x={x}&y={y}&z={z}',
  satellite: 'https://webst0{s}.is.autonavi.com/appmaptile?style=6&x={x}&y={y}&z={z}',
  hybrid: 'https://webst0{s}.is.autonavi.com/appmaptile?style=7&x={x}&y={y}&z={z}',
}

export default Vue.extend({
  name: 'MapPanel',
  props: {
    sources: { type: Array, default: () => [] },
    heatmapSources: { type: Array, default: () => [] },
    receptors: { type: Array, default: () => [] },
    result: { type: Object, default: null },
    scale: { type: String, default: 'jet' },
    opacity: { type: Number, default: 0.85 },
    min: { type: Number, default: 0 },
    max: { type: Number, default: 0 },
    renderScale: { type: Number, default: 4 },
    heatmapDisplayMode: { type: String, default: 'plume' },
    boundaryGeoJson: { type: Object, default: null },
    tileLayer: { type: String, default: 'street' },
    selectionEnabled: { type: Boolean, default: false },
    initialCenter: { type: Array, default: () => [30.75, 120.75] },
    initialZoom: { type: Number, default: 10 },
  },
  data() {
    return {
      map: null as L.Map | null,
      baseLayer: null as L.TileLayer | null,
      entityLayers: [] as L.Layer[],
      heatmapOverlay: null as L.ImageOverlay | null,
      selectionOverlay: null as L.Rectangle | null,
      boundaryLayer: null as L.GeoJSON | null,
      selectionStart: null as L.LatLng | null,
    }
  },
  mounted() {
    this.map = L.map(this.$refs.mapEl as HTMLElement, { zoomControl: true }).setView(this.initialCenter as [number, number], this.initialZoom)
    this.setTileLayer(this.tileLayer)
    this.map.on('moveend zoomend', this.emitViewChange)
    this.map.on('mousedown', this.startSelection)
    this.map.on('mousemove', this.moveSelection)
    this.map.on('mouseup', this.finishSelection)
    this.renderMarkers()
    this.renderHeatmap()
    this.renderBoundaryLayer()
    setTimeout(() => this.map && this.map.invalidateSize(), 0)
  },
  beforeDestroy() {
    if (this.map) this.map.remove()
  },
  watch: {
    sources: 'renderMarkers',
    receptors: 'renderMarkers',
    result: 'renderHeatmap',
    min: 'renderHeatmap',
    max: 'renderHeatmap',
    scale: 'renderHeatmap',
    opacity: 'renderHeatmap',
    renderScale: 'renderHeatmap',
    heatmapDisplayMode: 'renderHeatmap',
    boundaryGeoJson: 'renderBoundaryLayer',
    tileLayer(value: string) { this.setTileLayer(value) },
  },
  methods: {
    setTileLayer(kind: string) {
      if (!this.map) return
      if (this.baseLayer) this.baseLayer.remove()
      this.baseLayer = L.tileLayer(TILE_URLS[kind] || TILE_URLS.street, { subdomains: ['1', '2', '3', '4'], maxZoom: 18, attribution: '© 高德地图' }).addTo(this.map)
    },
    toGcj(point: LatLngTuple): L.LatLngTuple { return wgs84ToGcj02(point[0], point[1]) },
    clearEntityLayers() { this.entityLayers.forEach((l) => l.remove()); this.entityLayers = [] },
    sourcePopup(s: EmissionSource) { return `<strong>${escapeHtml(s.name)}</strong><br>类型: ${escapeHtml(s.sourceType)}<br>高度: ${escapeHtml(s.height)} m` },
    sourcePointMarker(s: EmissionSource, point: LatLngTuple, size = 14) {
      const icon = L.divIcon({ className: 'gnn-marker', html: `<div style="width:${size}px;height:${size}px;border-radius:50%;background:${safeCssColor(s.markerColor)};border:2px solid #fff;box-shadow:0 0 3px rgba(0,0,0,.4);"></div>`, iconSize: [size, size], iconAnchor: [size / 2, size / 2] })
      return L.marker(this.toGcj(point), { icon }).bindPopup(this.sourcePopup(s))
    },
    resultAnchorPoint(): LatLngTuple | null {
      const anchorSources = (this.heatmapSources as EmissionSource[]).length
        ? this.heatmapSources as EmissionSource[]
        : this.sources as EmissionSource[]
      const points = anchorSources.flatMap((source) => sourceFitPoints(source))
      if (points.length === 0) return null
      const lats = points.map(([lat]) => lat)
      const lons = points.map(([, lon]) => lon)
      return [
        (Math.min(...lats) + Math.max(...lats)) / 2,
        (Math.min(...lons) + Math.max(...lons)) / 2,
      ]
    },
    renderMarkers() {
      if (!this.map) return
      this.clearEntityLayers()
      for (const s of this.sources as EmissionSource[]) {
        const geometry = sourceMapGeometry(s)
        if (geometry.kind === 'polygon') {
          const layer = L.polygon(geometry.corners.map(this.toGcj), { color: geometry.equivalent ? '#7c3aed' : safeCssColor(s.markerColor), weight: 2, dashArray: geometry.equivalent ? '6 4' : undefined, fillColor: safeCssColor(s.markerColor), fillOpacity: 0.18 }).bindPopup(this.sourcePopup(s))
          layer.addTo(this.map); this.entityLayers.push(layer)
        } else if (geometry.kind === 'polyline') {
          const line = L.polyline(geometry.points.map(this.toGcj), { color: safeCssColor(s.markerColor), weight: 4, opacity: 0.85 }).bindPopup(this.sourcePopup(s))
          line.addTo(this.map); this.entityLayers.push(line)
          geometry.points.forEach((p) => { const m = this.sourcePointMarker(s, p, 10); m.addTo(this.map!); this.entityLayers.push(m) })
        } else {
          const marker = this.sourcePointMarker(s, geometry.center); marker.addTo(this.map); this.entityLayers.push(marker)
        }
      }
      for (const r of this.receptors as Receptor[]) {
        const [lat, lon] = wgs84ToGcj02(r.latitude, r.longitude)
        const icon = L.divIcon({ className: 'gnn-marker', html: `<div style="width:12px;height:12px;background:${safeCssColor(r.markerColor)};border:2px solid #fff;box-shadow:0 0 3px rgba(0,0,0,.4);"></div>`, iconSize: [12, 12], iconAnchor: [6, 6] })
        const marker = L.marker([lat, lon], { icon }).bindPopup(`<strong>${escapeHtml(r.name)}</strong><br>受体点<br>高度: ${escapeHtml(r.height)} m`)
        marker.addTo(this.map); this.entityLayers.push(marker)
      }
    },
    renderHeatmap() {
      if (!this.map) return
      if (this.heatmapOverlay) { this.heatmapOverlay.remove(); this.heatmapOverlay = null }
      const result = this.result as SimulationResult | null
      if (!result || !result.concentrations?.length) return
      let max = this.max || 0
      if (!max) {
        for (const row of result.concentrations) {
          for (const value of row) {
            if (Number.isFinite(value) && value > max) max = value
          }
        }
      }
      if (max <= 0) return
      const canvas = renderHeatmapToCanvas({ concentrations: result.concentrations, gridLat: result.gridLat, gridLon: result.gridLon, min: this.min || 0, max, scale: this.scale as any, opacity: this.opacity, displayMode: this.heatmapDisplayMode as any, renderScale: this.renderScale, useGcj02: true })
      this.heatmapOverlay = L.imageOverlay(canvas.toDataURL('image/png'), computeAnchoredBounds(result.gridLat, result.gridLon, this.resultAnchorPoint(), true), { opacity: 1, interactive: false }).addTo(this.map)
    },
    clearBoundaryLayer() { if (this.boundaryLayer) { this.boundaryLayer.remove(); this.boundaryLayer = null } },
    renderBoundaryLayer() {
      if (!this.map) return
      this.clearBoundaryLayer()
      if (!this.boundaryGeoJson) return
      this.boundaryLayer = L.geoJSON(this.boundaryGeoJson as any, {
        style: { color: '#0f766e', weight: 2, fillOpacity: 0.04, dashArray: '6 4' },
        coordsToLatLng: (coords: any) => {
          const [lat, lon] = wgs84ToGcj02(coords[1], coords[0])
          return L.latLng(lat, lon, coords[2])
        },
      }).addTo(this.map)
    },
    clearSelection() { if (this.selectionOverlay) this.selectionOverlay.remove(); this.selectionOverlay = null; this.selectionStart = null; this.$emit('selection-change', null) },
    fitBounds() {
      if (!this.map) return
      const points: L.LatLngTuple[] = []
      ;(this.sources as EmissionSource[]).forEach((s) => sourceFitPoints(s).forEach((p) => points.push(this.toGcj(p))))
      ;(this.receptors as Receptor[]).forEach((r) => points.push(wgs84ToGcj02(r.latitude, r.longitude)))
      if (points.length) this.map.fitBounds(L.latLngBounds(points).pad(0.15), { animate: true })
    },
    fitResultBounds() {
      if (!this.map) return
      const result = this.result as SimulationResult | null
      if (!result?.gridLat?.length || !result?.gridLon?.length) {
        this.fitBounds()
        return
      }
      const bounds = computeAnchoredBounds(result.gridLat, result.gridLon, this.resultAnchorPoint(), true) as [L.LatLngTuple, L.LatLngTuple]
      this.map.fitBounds(L.latLngBounds(bounds[0], bounds[1]).pad(0.08), { animate: true })
    },
    emitViewChange() { if (!this.map) return; const c = this.map.getCenter(); this.$emit('view-change', { center: [c.lat, c.lng], zoom: this.map.getZoom() }) },
    startSelection(e: L.LeafletMouseEvent) { if (!this.selectionEnabled || !this.map) return; this.selectionStart = e.latlng; if (this.selectionOverlay) this.selectionOverlay.remove(); this.selectionOverlay = L.rectangle(L.latLngBounds(e.latlng, e.latlng), { color: '#2563eb', weight: 2, dashArray: '6 4', fillOpacity: 0.16 }).addTo(this.map); this.map.dragging.disable() },
    moveSelection(e: L.LeafletMouseEvent) { if (!this.selectionStart || !this.selectionOverlay) return; this.selectionOverlay.setBounds(L.latLngBounds(this.selectionStart, e.latlng)) },
    finishSelection() { if (!this.selectionStart || !this.selectionOverlay || !this.map) return; const b = this.selectionOverlay.getBounds(); this.selectionStart = null; this.map.dragging.enable(); this.$emit('selection-change', { north: b.getNorth(), south: b.getSouth(), east: b.getEast(), west: b.getWest() }) },
  },
})
</script>

<style scoped>
.map-panel { width: 100%; height: 100%; }
</style>
