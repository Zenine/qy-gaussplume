<template>
  <div class="dashboard-map" :class="{ 'parallel-toolbar-expanded': simulationMode === 'parallel' }">
    <div ref="headerToolbar" class="dashboard-top-toolbar">
      <el-radio-group v-model="currentRegionKey" size="small" class="toolbar-region">
        <el-radio-button v-for="r in regions" :key="r.key" :label="r.key">{{ r.name }}</el-radio-button>
      </el-radio-group>
      <el-select v-model="tileLayer" size="small" class="toolbar-select"><el-option value="street" label="高德街道"/><el-option value="satellite" label="高德卫星"/><el-option value="hybrid" label="高德混合"/></el-select>
      <el-select v-model="selectedMeteorologyId" size="small" class="toolbar-wind"><el-option v-for="m in meteorologies" :key="m.id" :value="m.id" :label="`${m.name} - 风速:${m.windSpeed} 风向:${m.windDirection}°`" /></el-select>
      <el-radio-group v-model="simulationMode" size="small"><el-radio-button label="single">单风向</el-radio-button><el-radio-button label="parallel">多风向</el-radio-button></el-radio-group>
      <template v-if="simulationMode === 'parallel'"><span class="parallel-note">多风向按来风方向聚合</span><el-select v-model="parallelDirectionCount" size="small" class="toolbar-compact"><el-option v-for="n in [8,16,32,64,72]" :key="n" :value="n" :label="`${n} 风向`" /></el-select><el-input-number v-model="parallelWindSpeed" size="small" class="toolbar-speed" :min="0.1" :max="20" :step="0.1" /></template>
      <el-select v-model="calculationPollutant" size="small" clearable placeholder="计算全部污染物" class="toolbar-select"><el-option v-for="p in pollutantOptions" :key="p" :value="p" :label="p" /></el-select>
      <el-button type="primary" size="small" icon="el-icon-video-play" :loading="running" :disabled="running || !selectedMeteorologyId" @click="runCurrentSimulation">{{ simulationMode === 'parallel' ? '运行全局模拟' : '运行模拟' }}</el-button>
      <el-button size="small" icon="el-icon-delete" @click="clearResult">清除结果</el-button>
      <el-button size="small" icon="el-icon-document" @click="showFormula = true">公式说明</el-button>
    </div>

    <MapPanel ref="map" :sources="activeSources" :receptors="activeReceptors" :result="displayedResult" :scale="scale" :opacity="opacity" :min="0" :max="autoMax" :render-scale="renderScale" :tile-layer="tileLayer" :selection-enabled="selectionEnabled" :initial-center="mapCenter" :initial-zoom="mapZoom" @selection-change="selectionBounds = $event" @view-change="onMapViewChange" />

    <div class="range-panel floating-card">
      <div class="range-row"><span>模拟范围</span><strong>{{ domainSizeKm }} km</strong></div><el-slider v-model="domainSizeKm" :min="5" :max="100" :step="5" />
      <div class="range-row"><span>网格分辨率</span><strong>{{ gridResolution }} m</strong></div><el-slider v-model="gridResolution" :min="10" :max="500" :step="10" />
      <div class="range-row"><span>模拟高度</span><strong>{{ simulationHeight }} m</strong></div><el-slider v-model="simulationHeight" :min="0" :max="100" :step="1" />
    </div>

    <aside class="right-stack">
      <section v-if="!result" class="floating-card"><div class="card-title"><span>绘制选择区域</span><el-button size="small" type="primary" icon="el-icon-brush" @click="startSelection">绘制</el-button></div><p class="hint">拖拽矩形区域，仅模拟区域内排放源影响。</p><div v-if="selectionBounds" class="selection-summary">已选择 {{ effectiveSources.length }} 个排放源，{{ effectiveReceptors.length }} 个受体点 <el-button type="text" size="small" @click="clearSelection">清除</el-button></div></section>
      <section class="floating-card"><div class="card-title"><span>气象控制</span></div><div class="field-grid"><label>风速 m/s</label><el-input-number v-model="draftWindSpeed" size="small" :min="0.1" :max="20" :step="0.1"/><label>来风方向 °</label><el-input-number v-model="draftWindDirection" size="small" :min="0" :max="359" :step="1"/></div></section>
      <section class="floating-card"><div class="card-title"><span>数据统计</span></div><div class="stats-grid"><div><strong>{{ effectiveSources.length }}</strong><span>排放源</span></div><div><strong>{{ effectiveReceptors.length }}</strong><span>受体点</span></div></div></section>
      <section v-if="result" class="floating-card"><div class="card-title"><span>模拟结果</span><el-button type="text" @click="showContribution = true">贡献分析</el-button></div><ColorLegend :min="0" :max="autoMax"/><el-select v-model="selectedPollutant" clearable placeholder="全部污染物" style="width:100%;margin-top:10px"><el-option v-for="p in pollutantOptions" :key="p" :value="p" :label="p" /></el-select></section>
    </aside>
    <div class="quick-actions"><el-button circle icon="el-icon-aim" @click="fitBounds"/><el-button circle icon="el-icon-refresh" @click="$store.commit('resetPrefs')"/></div>
    <ContributionPanel :visible.sync="showContribution" :result="displayedResult" />
    <FormulaDrawer :visible.sync="showFormula" />
  </div>
</template>
<script lang="ts">
import Vue from 'vue'
import MapPanel from '@/components/MapPanel.vue'
import ColorLegend from '@/components/ColorLegend.vue'
import ContributionPanel from '@/components/ContributionPanel.vue'
import FormulaDrawer from '@/components/FormulaDrawer.vue'
import { sourcesApi, receptorsApi, meteorologyApi, simulationApi } from '@/api'
import { filterEntitiesByBounds } from '@/utils/selection'
import { wgs84ToGcj02 } from '@/utils/coords'
import type { EmissionSource, Receptor, Meteorology, SimulationResult } from '@/types'

export default Vue.extend({
  name: 'DashboardView', components: { MapPanel, ColorLegend, ContributionPanel, FormulaDrawer },
  data: () => ({ sources: [] as EmissionSource[], receptors: [] as Receptor[], meteorologies: [] as Meteorology[], selectedMeteorologyId: null as number | null, running: false, result: null as SimulationResult | null, selectionEnabled: false, selectionBounds: null as any, showContribution: false, showFormula: false, simulationMode: 'single', parallelDirectionCount: 16, parallelWindSpeed: 3, draftWindDirection: 0, draftWindSpeed: 0.1, calculationPollutant: '' }),
  computed: {
    regions(): any[] { return this.$store.state.regions },
    currentRegionKey: { get(): string { return this.$store.state.currentRegionKey }, set(v: string) { this.$store.commit('setPref', { key: 'currentRegionKey', value: v }); this.loadAll() } },
    tileLayer: { get(): string { return this.$store.state.tileLayer }, set(v: string) { this.$store.commit('setPref', { key: 'tileLayer', value: v }) } },
    scale(): string { return this.$store.state.scale }, opacity(): number { return this.$store.state.opacity }, renderScale(): number { return this.$store.state.renderScale }, mapCenter(): any { return this.$store.state.mapCenter }, mapZoom(): number { return this.$store.state.mapZoom },
    gridResolution: { get(): number { return this.$store.state.gridResolution }, set(v: number) { this.$store.commit('setPref', { key: 'gridResolution', value: v }) } },
    domainSize: { get(): number { return this.$store.state.domainSize }, set(v: number) { this.$store.commit('setPref', { key: 'domainSize', value: v }) } },
    simulationHeight: { get(): number { return this.$store.state.simulationHeight }, set(v: number) { this.$store.commit('setPref', { key: 'simulationHeight', value: v }) } },
    selectedPollutant: { get(): string { return this.$store.state.selectedPollutant }, set(v: string) { this.$store.commit('setPref', { key: 'selectedPollutant', value: v }) } },
    domainSizeKm: { get(): number { return Math.round(this.domainSize / 1000) }, set(v: number) { this.domainSize = v * 1000 } },
    activeSources(): EmissionSource[] { return this.sources.filter((s) => s.isActive) }, activeReceptors(): Receptor[] { return this.receptors.filter((r) => r.isActive) },
    effectiveSources(): EmissionSource[] { return filterEntitiesByBounds(this.activeSources, this.selectionBounds, (e:any) => { const [latitude, longitude] = wgs84ToGcj02(e.latitude, e.longitude); return { latitude, longitude } }) },
    effectiveReceptors(): Receptor[] { return filterEntitiesByBounds(this.activeReceptors, this.selectionBounds, (e:any) => { const [latitude, longitude] = wgs84ToGcj02(e.latitude, e.longitude); return { latitude, longitude } }) },
    pollutantOptions(): string[] { const s = new Set<string>(this.result?.availablePollutants || []); this.activeSources.forEach((src) => (src.pollutants || []).forEach((p) => s.add(p.pollutantType))); return [...s] },
    displayedResult(): SimulationResult | null { if (!this.result || !this.selectedPollutant || !this.result.pollutantConcentrations?.[this.selectedPollutant]) return this.result; return { ...this.result, concentrations: this.result.pollutantConcentrations[this.selectedPollutant] } },
    autoMax(): number { return Math.max(0, ...((this.displayedResult?.concentrations || []) as number[][]).flat()) },
  },
  watch: { selectedMeteorologyId() { this.syncMeteorology() } },
  mounted() { this.mountToolbar(); this.loadAll() }, beforeDestroy() { this.unmountToolbar() },
  methods: {
    mountToolbar() { const target = document.getElementById('dashboard-header-actions'); const el = this.$refs.headerToolbar as HTMLElement; if (target && el) target.appendChild(el) }, unmountToolbar() { const el = this.$refs.headerToolbar as HTMLElement; if (el && this.$el.contains(el) === false) this.$el.insertBefore(el, this.$el.firstChild) },
    async loadAll() { const [srcs, recs, mets] = await Promise.all([sourcesApi.list(0,1000,this.currentRegionKey), receptorsApi.list(0,1000,this.currentRegionKey), meteorologyApi.list(0,1000,this.currentRegionKey)]); this.sources=srcs; this.receptors=recs; this.meteorologies=mets.filter((m:any)=>m.isActive); this.selectedMeteorologyId=this.meteorologies[0]?.id ?? null; this.syncMeteorology() },
    syncMeteorology() { const m = this.meteorologies.find((x) => x.id === this.selectedMeteorologyId); if (m) { this.draftWindSpeed = m.windSpeed; this.draftWindDirection = m.windDirection; this.parallelWindSpeed = m.windSpeed } },
    async runCurrentSimulation() { this.simulationMode === 'parallel' ? await this.runParallel() : await this.runSimulation() },
    async runSimulation() { if (!this.selectedMeteorologyId || !this.effectiveSources.length) return; this.running=true; try { const r = await simulationApi.run({ meteorologyId:this.selectedMeteorologyId, sourceIds:this.selectionBounds ? this.effectiveSources.map(s=>s.id) : undefined, receptorIds:this.selectionBounds ? this.effectiveReceptors.map(r=>r.id) : undefined, pollutantType:this.calculationPollutant || undefined, windSpeed:this.draftWindSpeed, windDirection:this.draftWindDirection, gridResolution:this.gridResolution, domainSize:this.domainSize, receptorHeight:this.simulationHeight }); this.result=r; this.selectedPollutant = r.availablePollutants?.[0] || this.selectedPollutant; this.$nextTick(()=>this.fitBounds()) } finally { this.running=false } },
    async runParallel() { if (!this.selectedMeteorologyId || !this.effectiveSources.length) return; this.running=true; try { const dirs = Array.from({length:this.parallelDirectionCount},(_,i)=>360/this.parallelDirectionCount*i); const r = await simulationApi.runParallel({ meteorologyId:this.selectedMeteorologyId, sourceIds:this.selectionBounds ? this.effectiveSources.map(s=>s.id) : undefined, receptorIds:this.selectionBounds ? this.effectiveReceptors.map(r=>r.id) : undefined, pollutantType:this.calculationPollutant || undefined, windSpeed:this.parallelWindSpeed, windDirections:dirs, gridResolution:this.gridResolution, domainSize:this.domainSize, receptorHeight:this.simulationHeight } as any); this.result={ concentrations:r.concentrations, gridLat:r.gridLat, gridLon:r.gridLon, contributions:[], receptorContributions:r.receptorContributions || {}, pollutantConcentrations:r.pollutantConcentrations || null, availablePollutants:r.availablePollutants || null } as any } finally { this.running=false } },
    startSelection() { this.selectionEnabled = true }, clearSelection() { (this.$refs.map as any).clearSelection(); this.selectionEnabled=false }, clearResult() { this.result=null; this.clearSelection() }, fitBounds() { (this.$refs.map as any).fitBounds() }, onMapViewChange(p:any) { this.$store.commit('setPref', { key:'mapCenter', value:p.center }); this.$store.commit('setPref', { key:'mapZoom', value:p.zoom }) },
  },
})
</script>
<style scoped>
.dashboard-map{position:relative;height:calc(100vh - 104px);min-height:620px;overflow:hidden;border:1px solid #cfdde4;border-radius:8px;background:#eef3f4}.dashboard-top-toolbar{display:flex;align-items:center;justify-content:flex-end;flex:1 1 auto;flex-wrap:wrap;gap:8px;min-width:0}.toolbar-select{width:128px}.toolbar-wind{width:230px}.toolbar-compact{width:96px}.toolbar-speed{width:108px}.parallel-note{font-size:12px;color:#64748b}.floating-card{border:1px solid #dfe7ee;border-radius:8px;background:rgba(255,255,255,.96);box-shadow:0 10px 28px rgba(15,46,60,.12)}.range-panel{position:absolute;left:18px;bottom:18px;z-index:1000;width:230px;padding:16px 16px 10px}.range-row{display:flex;justify-content:space-between;color:#64748b;font-size:12px}.range-row strong{color:#1677ff}.right-stack{position:absolute;right:14px;top:76px;bottom:14px;z-index:1000;display:flex;width:300px;flex-direction:column;gap:10px;overflow-y:auto}.right-stack .floating-card{padding:14px}.card-title{display:flex;align-items:center;justify-content:space-between;gap:10px;padding-bottom:10px;border-bottom:1px solid #edf2f5;font-weight:800}.hint{color:#64748b}.field-grid{display:grid;grid-template-columns:80px 1fr;gap:10px;align-items:center;margin-top:12px}.stats-grid{display:grid;grid-template-columns:1fr 1fr;gap:10px}.stats-grid div{padding:12px;border-radius:8px;background:#f8fafc}.stats-grid strong{display:block;font-size:22px;color:#1677ff}.stats-grid span{font-size:12px;color:#64748b}.quick-actions{position:absolute;left:18px;top:92px;z-index:1000;display:flex;flex-direction:column;gap:8px}.selection-summary{margin-top:8px;color:#475569;font-size:12px}
</style>
