<template>
  <div class="dashboard-map" data-test="dashboard-map" :class="{ 'parallel-toolbar-expanded': simulationMode === 'parallel' }">
    <div ref="headerToolbar" class="dashboard-top-toolbar" data-test="floating-toolbar">
      <div class="dashboard-toolbar-row toolbar-primary-row" data-test="toolbar-primary-row">
      <div class="simulation-primary-actions">
        <el-radio-group v-model="simulationMode" data-test="simulation-mode-select" size="small">
          <el-radio-button label="single">单风向</el-radio-button>
          <el-radio-button label="parallel">多风向</el-radio-button>
        </el-radio-group>
        <el-button
          v-if="simulationMode === 'parallel'"
          data-test="wind-profile-import"
          size="small"
          :type="parallelWindProfile ? 'success' : 'primary'"
          plain
          icon="el-icon-upload2"
          @click="showWindProfileDialog = true"
        >{{ parallelWindProfile ? `已导入 ${parallelWindProfile.directionCount} 方位` : '导入风频 XLSX' }}</el-button>
      </div>
      <el-radio-group v-model="currentRegionKey" data-test="region-selector" size="small" class="toolbar-region">
        <el-radio-button v-for="r in regions" :key="r.key" :label="r.key">{{ r.name }}</el-radio-button>
      </el-radio-group>
      <el-select v-model="tileLayer" size="small" class="toolbar-select">
        <el-option value="street" label="高德街道" />
        <el-option value="satellite" label="高德卫星" />
        <el-option value="hybrid" label="高德混合" />
      </el-select>
      <el-select v-model="selectedMeteorologyId" size="small" class="toolbar-wind" placeholder="选择气象场">
        <el-option
          v-for="m in meteorologies"
          :key="m.id"
          :value="m.id"
          :label="`${m.name} - 风速:${m.windSpeed} 风向:${m.windDirection}°`"
        />
      </el-select>
      </div>
      <div class="dashboard-toolbar-row toolbar-secondary-row" data-test="toolbar-secondary-row">
      <template v-if="simulationMode === 'parallel'">
        <span class="parallel-note" data-test="parallel-mode-note">多风向按来风方向聚合</span>
        <el-select v-model="parallelDirectionCount" data-test="parallel-direction-count" size="small" class="toolbar-compact" :disabled="Boolean(parallelWindProfile)" @change="clearParallelWindProfile">
          <el-option v-if="parallelWindProfile && ![8, 16, 32, 64, 72].includes(parallelWindProfile.directionCount)" :value="parallelWindProfile.directionCount" :label="`${parallelWindProfile.directionCount} 导入`" />
          <el-option v-for="n in [8, 16, 32, 64, 72]" :key="n" :value="n" :label="`${n} 风向`" />
        </el-select>
        <el-input-number
          v-model="parallelWindSpeed"
          data-test="parallel-wind-speed"
          size="small"
          class="toolbar-speed"
          :min="0.1"
          :max="20"
          :step="0.1"
          :disabled="Boolean(parallelWindProfile)"
          @change="clearParallelWindProfile"
        />
        <el-tag v-if="parallelWindProfile" data-test="wind-profile-summary" size="small" type="success" closable @close="clearParallelWindProfile">
          已导入 {{ parallelWindProfile.directionCount }} 方位 · 权重和 {{ parallelWindProfile.weightSum.toFixed(4) }}
        </el-tag>
      </template>
      <el-select v-model="calculationPollutant" data-test="calculation-pollutant-select" size="small" clearable placeholder="计算全部污染物" class="toolbar-select">
        <el-option v-for="p in pollutantOptions" :key="p" :value="p" :label="p" />
      </el-select>
      <el-button
        data-test="run-simulation"
        type="primary"
        size="small"
        icon="el-icon-video-play"
        :loading="running"
        :disabled="running || !selectedMeteorologyId"
        @click="runCurrentSimulation"
      >
        {{ simulationMode === 'parallel' ? '运行全局模拟' : '运行模拟' }}
      </el-button>
      <el-button data-test="clear-result" size="small" icon="el-icon-delete" @click="clearResult">清除结果</el-button>
      <el-button data-test="formula-info" size="small" icon="el-icon-document" @click="showFormula = true">公式说明</el-button>
      </div>
    </div>

    <MapPanel
      ref="map"
      :sources="activeSources"
      :heatmap-sources="result ? resultSources : activeSources"
      :heatmap-wind-direction="lastSimulationInputs && lastSimulationInputs.mode === 'single' ? lastSimulationInputs.windDirection : null"
      :receptors="activeReceptors"
      :result="displayedResult"
      :scale="scale"
      :opacity="opacity"
      :min="effectiveMin"
      :max="effectiveMax"
      :render-scale="renderScale"
      :heatmap-display-mode="heatmapDisplayMode"
      :boundary-geo-json="boundaryGeoJson"
      :tile-layer="tileLayer"
      :selection-enabled="selectionEnabled"
      :initial-center="mapCenter"
      :initial-zoom="mapZoom"
      @selection-change="selectionBounds = $event"
      @view-change="onMapViewChange"
    />

    <div class="range-panel floating-card" data-test="range-panel">
      <div class="range-row"><span>模拟范围</span><strong>{{ domainSizeKm }} km</strong></div>
      <el-slider v-model="domainSizeKm" :min="5" :max="100" :step="5" />
      <div class="range-row"><span>网格分辨率</span><strong>{{ gridResolution }} m</strong></div>
      <el-slider v-model="gridResolution" :min="10" :max="500" :step="10" />
      <div class="range-row"><span>模拟高度</span><strong>{{ simulationHeight }} m</strong></div>
      <el-slider v-model="simulationHeight" data-test="simulation-height-slider" :min="0" :max="100" :step="1" />
      <p v-if="resultGridOutdated" class="hint warning">模拟范围、网格或高度已修改，当前结果未更新。</p>
    </div>

    <aside class="right-stack">

      <section class="floating-card" data-test="map-layer-card">
        <div class="card-title"><span>地图图层</span></div>
        <div class="switch-row">
          <span>行政边界</span>
          <el-switch v-model="boundaryEnabled" data-test="boundary-layer-switch" :loading="boundaryLoading" @change="onBoundaryEnabledChange" />
        </div>
        <p class="hint">按需加载后端 GeoJSON，并按高德底图坐标叠加显示。</p>
      </section>

      <section v-if="!result" class="floating-card" data-test="draw-card">
        <div class="card-title">
          <span>绘制选择区域</span>
          <el-button size="small" type="primary" icon="el-icon-brush" @click="startSelection">绘制</el-button>
        </div>
        <p class="hint">拖拽矩形区域，仅模拟区域内排放源影响。</p>
        <div v-if="selectionBounds" class="selection-summary">
          已选择 {{ effectiveSources.length }} 个排放源，{{ effectiveReceptors.length }} 个受体点
          <el-button type="text" size="small" @click="clearSelection">清除</el-button>
        </div>
      </section>

      <section class="floating-card" data-test="weather-card">
        <div class="card-title"><span>气象控制</span><span class="card-icon">⌖</span></div>
        <div class="wind-rose">
          <svg viewBox="0 0 150 150" role="img" aria-label="风向指示" data-test="wind-control-dial" @click="updateWindFromDial">
            <circle class="wind-ring" cx="75" cy="75" r="74" />
            <circle class="wind-ring" cx="75" cy="75" r="50" />
            <circle class="wind-ring" cx="75" cy="75" r="26" />
            <line class="wind-axis" x1="75" y1="1" x2="75" y2="149" />
            <line class="wind-axis" x1="1" y1="75" x2="149" y2="75" />
            <text x="75" y="20" text-anchor="middle">N</text>
            <text x="128" y="79" text-anchor="middle">E</text>
            <text x="75" y="136" text-anchor="middle">S</text>
            <text x="22" y="79" text-anchor="middle">W</text>
            <line
              class="wind-pointer"
              data-test="wind-direction-pointer"
              x1="75"
              y1="75"
              :x2="windPointer.x"
              :y2="windPointer.y"
            />
            <circle class="wind-pointer-tip" data-test="wind-direction-pointer-tip" cx="75" cy="75" r="6" />
          </svg>
        </div>
        <div class="field-grid weather-fields">
          <label>
            来风方向 (°)
            <el-input-number
              v-model="draftWindDirection"
              data-test="wind-direction-input"
              size="small"
              :min="0"
              :max="360"
              :step="1"
            />
          </label>
          <label>
            风速 (m/s)
            <el-input-number
              v-model="draftWindSpeed"
              data-test="wind-speed-input"
              size="small"
              :min="0.1"
              :max="20"
              :step="0.1"
            />
          </label>
        </div>
        <p v-if="resultWeatherOutdated" class="hint warning">气象参数已修改，当前结果未更新，请点击运行模拟。</p>
        <p v-else-if="weatherDirty" class="hint warning">将使用当前临时风速和来风方向运行，不会覆盖已保存气象场。</p>
        <p v-else class="hint">点击圆盘可调整来风方向和风速；外端指向风吹来的方向。</p>
      </section>

      <section class="floating-card" data-test="stats-card">
        <div class="card-title"><span>数据统计</span></div>
        <div class="stats-grid">
          <div><strong>{{ effectiveSources.length }}</strong><span>排放源</span></div>
          <div><strong>{{ effectiveReceptors.length }}</strong><span>受体点</span></div>
        </div>
      </section>

      <template v-if="result">
        <section class="floating-card" data-test="result-card">
          <div class="card-title"><span>模拟结果</span><span class="complete">完成</span></div>
          <p v-if="resultParametersOutdated" class="hint warning">页面参数已变化，当前结果未更新，请重新模拟。</p>
          <label class="full-field">
            显示污染物
            <el-select v-model="selectedPollutant" clearable placeholder="全部污染物" style="width:100%;margin-top:6px">
              <el-option v-for="p in resultPollutantOptions" :key="p" :value="p" :label="p" />
            </el-select>
          </label>
        </section>

        <section class="floating-card" data-test="color-scale-card">
          <div class="card-title"><span>扩散浓度色阶</span></div>
          <label class="full-field">
            色阶类型
            <el-select v-model="scale" size="small" style="width:100%;margin-top:6px">
              <el-option value="jet" label="热力图" />
              <el-option value="turbo" label="Turbo" />
              <el-option value="viridis" label="Viridis" />
              <el-option value="grayscale" label="灰度" />
              <el-option value="blue" label="蓝色" />
              <el-option value="red" label="红色" />
              <el-option value="green" label="绿色" />
              <el-option value="purple" label="紫色" />
              <el-option value="thermal" label="Thermal" />
              <el-option value="rainbow" label="Rainbow" />
              <el-option value="spectral_r" label="Spectral R" />
            </el-select>
          </label>
          <div class="range-header">
            <span>色阶范围 <small>自动：{{ autoRange.min.toFixed(3) }} - {{ autoRange.max.toFixed(3) }} μg/m³</small></span>
            <el-button type="text" size="small" data-test="reset-color-range" :disabled="!colorRangeCustomized" @click="resetColorRange">恢复自动</el-button>
          </div>
          <div class="field-grid color-fields">
            <label>最小值<el-input-number v-model="customMin" data-test="color-range-min" size="small" :controls="false" :placeholder="autoRange.min.toFixed(3)" /></label>
            <label>最大值<el-input-number v-model="customMax" data-test="color-range-max" size="small" :controls="false" :placeholder="autoRange.max.toFixed(3)" /></label>
          </div>
          <p v-if="colorRangeInvalid" class="hint warning">色阶最大值必须大于最小值，否则图层会显示为最低色。</p>
          <label class="full-field">透明度<el-slider v-model="opacity" :min="0" :max="1.2" :step="0.05" /></label>
          <div class="field-grid color-fields">
            <label>扩散显示<el-select v-model="heatmapDisplayMode" size="small"><el-option value="plume" label="羽流突出" /><el-option value="continuous" label="连续低值" /></el-select></label>
            <label>渲染精度<el-select v-model="renderScale" size="small"><el-option v-for="n in [1,2,4,8,12,16]" :key="n" :value="n" :label="`${n}x`" /></el-select></label>
          </div>
          <ColorLegend v-if="effectiveMax > 0" :min="effectiveMin" :max="effectiveMax" :scale="scale" />
        </section>

        <section class="floating-card" data-test="ranking-card">
          <div class="card-title"><span>空气站点污染源贡献排名</span><el-button type="text" @click="showContribution = true">详情</el-button></div>
          <div v-if="receptorContributionNames.length" class="ranking-controls">
            <label>污染物指标
              <el-select v-model="selectedRankingPollutant" data-test="ranking-pollutant-select" clearable size="small" placeholder="全部污染物" style="width:100%;margin-top:6px">
                <el-option v-for="p in rankingPollutants" :key="p" :value="p" :label="p" />
              </el-select>
            </label>
          </div>
          <div v-if="isAllPollutantRanking" class="ranking-summary">全部污染物：按空气站点展示污染物与污染源贡献</div>
          <div v-else-if="selectedRankingPollutant" class="ranking-summary">{{ selectedRankingPollutant }}：按空气站点展示污染源贡献排名</div>
          <div class="station-summary-list">
            <div v-for="card in stationContributionCards" :key="card.receptorName" class="station-summary-card" data-test="station-contribution-card">
              <div class="station-summary-title"><strong>{{ card.receptorName }}</strong><span>总贡献浓度 {{ card.total.toFixed(4) }} µg/m³</span></div>
              <div v-for="pollutant in card.pollutants" :key="pollutant.pollutant" class="station-pollutant-block">
                <div class="pollutant-summary-row"><span class="pollutant-name">{{ pollutant.pollutant }}</span><div class="ranking-bar"><span :style="{ width: `${Math.min(100, pollutant.percentage)}%` }" /></div><span class="ranking-percent">{{ pollutant.percentage.toFixed(1) }}%</span><strong>{{ pollutant.total.toFixed(4) }} µg/m³</strong></div>
                <div class="ranking-list source-ranking-list">
                  <div v-for="(item, index) in pollutant.rows" :key="`${pollutant.pollutant}-${item.sourceId}`" class="ranking-item">
                    <span class="ranking-index">{{ index + 1 }}</span><div class="ranking-main"><div class="ranking-name">{{ item.sourceName }}</div><div class="ranking-bar"><span :style="{ width: `${Math.min(100, item.percentage)}%` }" /></div></div><span class="ranking-percent">{{ item.percentage.toFixed(1) }}%</span><strong>{{ item.concentration.toFixed(4) }} µg/m³</strong>
                  </div>
                </div>
              </div>
            </div>
            <p v-if="stationContributionCards.length === 0" class="hint">暂无空气站点污染源贡献数据</p>
          </div>
        </section>
      </template>
    </aside>

    <div class="quick-actions">
      <el-button circle icon="el-icon-aim" @click="fitBounds" />
      <el-button circle icon="el-icon-refresh" @click="$store.commit('resetPrefs')" />
    </div>

    <ContributionPanel :visible.sync="showContribution" :result="displayedResult" />
    <FormulaDrawer :visible.sync="showFormula" />

    <el-dialog
      :visible.sync="showWindProfileDialog"
      title="导入多风向风频数据"
      width="min(760px, 94vw)"
      :close-on-click-modal="false"
      data-test="wind-profile-dialog"
    >
      <el-alert
        title="按照模板填写风向中心角度、平均风速(m/s)、加权值；导入后，全局模拟会逐行使用对应风速和权重。"
        type="info"
        :closable="false"
        show-icon
      />
      <div class="wind-profile-actions">
        <el-upload
          data-test="wind-profile-file"
          action="#"
          :auto-upload="true"
          :show-file-list="false"
          accept=".xlsx"
          :before-upload="importWindProfile"
        >
          <el-button type="primary" icon="el-icon-upload2" :loading="windProfileUploading">选择 XLSX 文件并导入</el-button>
        </el-upload>
        <el-button data-test="wind-profile-template" icon="el-icon-download" @click="downloadWindProfileTemplate">下载 72 方位模板</el-button>
        <el-button v-if="parallelWindProfile" type="danger" plain icon="el-icon-delete" @click="clearParallelWindProfile">清除已导入数据</el-button>
      </div>
      <div v-if="parallelWindProfile" class="wind-profile-loaded" data-test="wind-profile-loaded">
        <el-alert
          :title="`导入成功：${parallelWindProfile.directionCount} 个方位，权重和 ${parallelWindProfile.weightSum.toFixed(4)}。以下数据将用于下一次全局模拟。`"
          type="success"
          :closable="false"
          show-icon
        />
        <el-table :data="windProfileRows" height="360" border size="mini" class="wind-profile-table">
          <el-table-column prop="windDirection" label="风向中心角度" width="150" />
          <el-table-column prop="windSpeed" label="平均风速(m/s)" width="150" />
          <el-table-column prop="weight" label="加权值" />
        </el-table>
      </div>
      <el-empty v-else description="尚未导入风频数据，请先选择 XLSX 文件" :image-size="80" />
      <span slot="footer">
        <el-button @click="showWindProfileDialog = false">关闭</el-button>
        <el-button v-if="parallelWindProfile" type="primary" @click="showWindProfileDialog = false">使用此数据</el-button>
      </span>
    </el-dialog>
  </div>
</template>

<script lang="ts">
import Vue from 'vue'
import MapPanel from '@/components/MapPanel.vue'
import ColorLegend from '@/components/ColorLegend.vue'
import ContributionPanel from '@/components/ContributionPanel.vue'
import FormulaDrawer from '@/components/FormulaDrawer.vue'
import { sourcesApi, receptorsApi, meteorologyApi, simulationApi, mapApi } from '@/api'
import { filterEntitiesByBounds } from '@/utils/selection'
import { wgs84ToGcj02 } from '@/utils/coords'
import { downloadBlob } from '@/utils/download'
import { errorMessage } from '@/utils/error'
import type { EmissionSource, Receptor, Meteorology, SimulationResult, ReceptorContributionEntry, WindProfileImportResult } from '@/types'

const defaultCalculationPollutants = ['PM2.5', 'PM10', 'TSP', 'VOCs', 'NOx', 'SO2', 'O3']

type LastSimulationInputs =
  | {
      mode: 'single'
      meteorologyId: number
      windSpeed: number
      windDirection: number
      gridResolution: number
      domainSize: number
      receptorHeight: number
      calculationPollutant: string
    }
  | {
      mode: 'parallel'
      meteorologyId: number
      windSpeed: number
      windDirections: number[]
      windSpeeds: number[]
      weights: number[]
      gridResolution: number
      domainSize: number
      receptorHeight: number
      calculationPollutant: string
    }

function sameNumberArray(a: number[], b: number[]) {
  return a.length === b.length && a.every((value, index) => value === b[index])
}

export default Vue.extend({
  name: 'DashboardView',
  components: { MapPanel, ColorLegend, ContributionPanel, FormulaDrawer },
  data: () => ({
    sources: [] as EmissionSource[],
    resultSources: [] as EmissionSource[],
    receptors: [] as Receptor[],
    meteorologies: [] as Meteorology[],
    selectedMeteorologyId: null as number | null,
    running: false,
    result: null as SimulationResult | null,
    lastSimulationInputs: null as LastSimulationInputs | null,
    selectionEnabled: false,
    selectionBounds: null as any,
    showContribution: false,
    showFormula: false,
    showWindProfileDialog: false,
    windProfileUploading: false,
    customMin: null as number | null,
    customMax: null as number | null,
    selectedRankingPollutant: '',
    boundaryEnabled: false,
    boundaryLoading: false,
    boundaryGeoJson: null as any,
    simulationMode: 'single' as 'single' | 'parallel',
    parallelDirectionCount: 16,
    parallelWindSpeed: 3,
    parallelWindProfile: null as WindProfileImportResult | null,
    draftWindDirection: 0,
    draftWindSpeed: 0.1,
    calculationPollutant: '',
  }),
  computed: {
    regions(): any[] { return this.$store.state.regions },
    currentRegionKey: {
      get(): string { return this.$store.state.currentRegionKey },
      set(v: string) {
        this.$store.commit('setPref', { key: 'currentRegionKey', value: v })
        this.clearResult()
        this.loadAll()
      },
    },
    tileLayer: { get(): string { return this.$store.state.tileLayer }, set(v: string) { this.$store.commit('setPref', { key: 'tileLayer', value: v }) } },
    scale: { get(): string { return this.$store.state.scale }, set(v: string) { this.$store.commit('setPref', { key: 'scale', value: v }) } },
    opacity: { get(): number { return this.$store.state.opacity }, set(v: number) { this.$store.commit('setPref', { key: 'opacity', value: v }) } },
    renderScale: { get(): number { return this.$store.state.renderScale }, set(v: number) { this.$store.commit('setPref', { key: 'renderScale', value: v }) } },
    heatmapDisplayMode: { get(): string { return this.$store.state.heatmapDisplayMode }, set(v: string) { this.$store.commit('setPref', { key: 'heatmapDisplayMode', value: v }) } },
    mapCenter(): any { return this.$store.state.mapCenter },
    mapZoom(): number { return this.$store.state.mapZoom },
    gridResolution: { get(): number { return this.$store.state.gridResolution }, set(v: number) { this.$store.commit('setPref', { key: 'gridResolution', value: v }) } },
    domainSize: { get(): number { return this.$store.state.domainSize }, set(v: number) { this.$store.commit('setPref', { key: 'domainSize', value: v }) } },
    simulationHeight: { get(): number { return this.$store.state.simulationHeight }, set(v: number) { this.$store.commit('setPref', { key: 'simulationHeight', value: v }) } },
    selectedPollutant: { get(): string { return this.$store.state.selectedPollutant }, set(v: string) { this.$store.commit('setPref', { key: 'selectedPollutant', value: v }) } },
    domainSizeKm: { get(): number { return Math.round(this.domainSize / 1000) }, set(v: number) { this.domainSize = v * 1000 } },
    selectedMeteorology(): Meteorology | null { return this.meteorologies.find((m) => m.id === this.selectedMeteorologyId) || null },
    windPointer(): { x: string; y: string } {
      const center = 75
      const radius = 44
      const radians = (this.draftWindDirection * Math.PI) / 180
      const x = center + Math.sin(radians) * radius
      const y = center - Math.cos(radians) * radius
      return { x: x.toFixed(2), y: y.toFixed(2) }
    },
    weatherDirty(): boolean {
      const met = this.selectedMeteorology
      if (!met) return false
      return this.draftWindDirection !== met.windDirection || this.draftWindSpeed !== met.windSpeed
    },
    parallelWindDirections(): number[] {
      if (this.parallelWindProfile) return this.parallelWindProfile.windDirections
      return Array.from({ length: this.parallelDirectionCount }, (_, i) => (360 / this.parallelDirectionCount) * i)
    },
    parallelWindSpeeds(): number[] {
      return this.parallelWindProfile?.windSpeeds || this.parallelWindDirections.map(() => this.parallelWindSpeed)
    },
    parallelWeights(): number[] | undefined {
      return this.parallelWindProfile?.weights
    },
    windProfileRows(): Array<{ windDirection: number; windSpeed: number; weight: number }> {
      if (!this.parallelWindProfile) return []
      return this.parallelWindProfile.windDirections.map((windDirection, index) => ({
        windDirection,
        windSpeed: this.parallelWindProfile!.windSpeeds[index],
        weight: this.parallelWindProfile!.weights[index],
      }))
    },
    resultWeatherOutdated(): boolean {
      const last = this.lastSimulationInputs
      if (!this.result || !last || !this.selectedMeteorologyId) return false
      if (last.mode === 'parallel') {
        return this.selectedMeteorologyId !== last.meteorologyId
          || !sameNumberArray(this.parallelWindDirections, last.windDirections)
          || !sameNumberArray(this.parallelWindSpeeds, last.windSpeeds)
          || !sameNumberArray(this.parallelWeights || [], last.weights)
      }
      return this.selectedMeteorologyId !== last.meteorologyId || this.draftWindSpeed !== last.windSpeed || this.draftWindDirection !== last.windDirection
    },
    resultGridOutdated(): boolean {
      const last = this.lastSimulationInputs
      if (!this.result || !last) return false
      return this.gridResolution !== last.gridResolution || this.domainSize !== last.domainSize || this.simulationHeight !== last.receptorHeight || this.calculationPollutant !== last.calculationPollutant
    },
    activeSources(): EmissionSource[] { return this.sources.filter((s) => s.isActive) },
    activeReceptors(): Receptor[] { return this.receptors.filter((r) => r.isActive) },
    effectiveSources(): EmissionSource[] {
      return filterEntitiesByBounds(this.activeSources, this.selectionBounds, (e: any) => {
        const [latitude, longitude] = wgs84ToGcj02(e.latitude, e.longitude)
        return { latitude, longitude }
      })
    },
    effectiveReceptors(): Receptor[] {
      return filterEntitiesByBounds(this.activeReceptors, this.selectionBounds, (e: any) => {
        const [latitude, longitude] = wgs84ToGcj02(e.latitude, e.longitude)
        return { latitude, longitude }
      })
    },
    pollutantOptions(): string[] {
      const s = new Set<string>(defaultCalculationPollutants)
      ;(this.result?.availablePollutants || []).forEach((p) => s.add(p))
      this.activeSources.forEach((src) => (src.pollutants || []).forEach((p) => s.add(p.pollutantType)))
      return [...s]
    },
    resultPollutantOptions(): string[] {
      if (!this.result) return []
      const fields = Object.keys(this.result.pollutantConcentrations || {})
      return fields.length ? fields : (this.result.availablePollutants || [])
    },
    displayedResult(): SimulationResult | null {
      if (!this.result || !this.selectedPollutant || !this.result.pollutantConcentrations?.[this.selectedPollutant]) return this.result
      return { ...this.result, concentrations: this.result.pollutantConcentrations[this.selectedPollutant] }
    },
    autoRange(): { min: number; max: number } {
      const concentrations = (this.displayedResult?.concentrations || []) as number[][]
      let min = Number.POSITIVE_INFINITY
      let max = Number.NEGATIVE_INFINITY
      for (const row of concentrations) {
        for (const value of row) {
          if (!Number.isFinite(value)) continue
          if (value < min) min = value
          if (value > max) max = value
        }
      }
      if (min === Number.POSITIVE_INFINITY) return { min: 0, max: 0 }
      return { min, max }
    },
    effectiveMin(): number { return this.customMin ?? this.autoRange.min },
    effectiveMax(): number { return this.customMax ?? this.autoRange.max },
    colorRangeCustomized(): boolean { return this.customMin !== null || this.customMax !== null },
    colorRangeInvalid(): boolean { return this.effectiveMax <= this.effectiveMin },
    autoMax(): number { return this.autoRange.max },
    resultParametersOutdated(): boolean { return this.resultWeatherOutdated || this.resultGridOutdated },
    isAllPollutantRanking(): boolean { return !this.selectedRankingPollutant },
    receptorContributionNames(): string[] { return this.result ? Object.keys(this.result.receptorContributions || {}) : [] },
    rankingPollutants(): string[] {
      if (!this.result) return []
      const values = new Set<string>()
      Object.values(this.result.receptorContributions || {}).forEach((byPollutant: any) => Object.keys(byPollutant).forEach((p) => values.add(p)))
      return [...values]
    },
    stationContributionCards(): any[] {
      if (!this.result) return []
      const pollutantFilter = this.selectedRankingPollutant
      return Object.entries(this.result.receptorContributions || {}).map(([receptorName, byPollutant]: [string, any]) => {
        const pollutants = Object.entries(byPollutant)
          .filter(([pollutant]) => !pollutantFilter || pollutant === pollutantFilter)
          .map(([pollutant, contributions]) => {
            const sortedRows = this.sortedPositiveContributions(contributions as ReceptorContributionEntry[])
            const total = sortedRows.reduce((sum, row) => sum + row.concentration, 0)
            return { pollutant, total, rows: sortedRows.slice(0, 10).map((row) => ({ ...row, percentage: total > 0 ? row.concentration / total * 100 : 0 })) }
          })
          .filter((row) => row.total > 0)
          .sort((a, b) => b.total - a.total)
        const total = pollutants.reduce((sum, row) => sum + row.total, 0)
        return { receptorName, total, pollutants: pollutants.map((row) => ({ ...row, percentage: total > 0 ? row.total / total * 100 : 0 })) }
      }).filter((card) => card.total > 0).sort((a, b) => b.total - a.total)
    },
  },
  watch: {
    selectedMeteorologyId() { this.syncMeteorology() },
    result(value: SimulationResult | null) {
      if (!value) {
        this.selectedRankingPollutant = ''
        return
      }
      this.syncRankingPollutant()
    },
    selectedPollutant() { this.syncRankingPollutant() },
    selectedRankingPollutant(pollutant: string) {
      if (this.selectedPollutant !== pollutant) this.selectedPollutant = pollutant
    },
  },
  mounted() {
    this.mountToolbar()
    this.loadAll()
  },
  beforeDestroy() { this.unmountToolbar() },
  methods: {
    mountToolbar() {
      const target = document.getElementById('dashboard-header-actions')
      const el = this.$refs.headerToolbar as HTMLElement
      if (target && el) target.appendChild(el)
    },
    unmountToolbar() {
      const el = this.$refs.headerToolbar as HTMLElement
      if (el && this.$el.contains(el) === false) this.$el.insertBefore(el, this.$el.firstChild)
    },
    async loadAll() {
      const [srcs, recs, mets] = await Promise.all([
        sourcesApi.list(0, 1000, this.currentRegionKey),
        receptorsApi.list(0, 1000, this.currentRegionKey),
        meteorologyApi.list(0, 1000, this.currentRegionKey),
      ])
      this.sources = srcs
      this.receptors = recs
      this.meteorologies = mets.filter((m: any) => m.isActive)
      if (!this.meteorologies.some((m) => m.id === this.selectedMeteorologyId)) {
        this.selectedMeteorologyId = this.meteorologies[0]?.id ?? null
      }
      this.syncMeteorology()
    },
    syncMeteorology() {
      const m = this.selectedMeteorology
      if (!m) return
      const last = this.lastSimulationInputs
      if (this.result && last?.meteorologyId === m.id) {
        if (last.mode === 'single') {
          this.draftWindSpeed = last.windSpeed
          this.draftWindDirection = last.windDirection
        } else {
          this.parallelWindSpeed = last.windSpeed
          this.parallelDirectionCount = last.windDirections.length
        }
        return
      }
      this.draftWindSpeed = m.windSpeed
      this.draftWindDirection = m.windDirection
      this.parallelWindSpeed = m.windSpeed
    },
    resetColorRange() { this.customMin = null; this.customMax = null },
    clearParallelWindProfile() { this.parallelWindProfile = null },
    async downloadWindProfileTemplate() {
      try {
        const blob = await simulationApi.downloadWindProfileTemplate()
        downloadBlob(blob, 'wind_profile_72_template.xlsx')
      } catch (e) {
        this.$message.error(errorMessage(e, '下载风频模板失败'))
      }
    },
    importWindProfile(file: File) {
      void this.processWindProfile(file)
      return false
    },
    async processWindProfile(file: File) {
      this.windProfileUploading = true
      try {
        const profile = await simulationApi.importWindProfile(file)
        this.parallelWindProfile = profile
        this.parallelDirectionCount = profile.directionCount
        const weightedSpeed = profile.windSpeeds.reduce(
          (sum, speed, index) => sum + speed * profile.weights[index],
          0,
        ) / profile.weightSum
        this.parallelWindSpeed = Number(weightedSpeed.toFixed(3))
        this.$message.success(`已导入 ${profile.directionCount} 个风向的平均风速与权重`)
      } catch (e) {
        this.$message.error(errorMessage(e, '导入风频数据失败'))
      } finally {
        this.windProfileUploading = false
      }
    },
    async onBoundaryEnabledChange() {
      if (!this.boundaryEnabled) return
      if (this.boundaryGeoJson) return
      this.boundaryLoading = true
      try { this.boundaryGeoJson = await mapApi.getGeoJson(true) }
      catch (e) { this.boundaryEnabled = false; this.$message.error('加载行政边界失败') }
      finally { this.boundaryLoading = false }
    },
    sortedPositiveContributions(rows: ReceptorContributionEntry[]) {
      return rows.filter((row) => row.concentration > 0).sort((a, b) => b.concentration - a.concentration).map((row) => ({ ...row, percentage: 0 }))
    },
    chooseDisplayPollutant(available: string[] | null | undefined, requested?: string) {
      if (!available?.length) return
      this.selectedPollutant = requested && available.includes(requested)
        ? requested
        : this.selectedPollutant && available.includes(this.selectedPollutant)
          ? this.selectedPollutant
          : available[0]
    },
    syncRankingPollutant() {
      if (!this.result || !this.selectedPollutant) {
        this.selectedRankingPollutant = ''
        return
      }
      this.selectedRankingPollutant = this.rankingPollutants.includes(this.selectedPollutant) ? this.selectedPollutant : ''
    },
    updateWindFromDial(event: MouseEvent) {
      const target = event.currentTarget as SVGElement | null
      if (!target) return
      const rect = target.getBoundingClientRect()
      if (!rect.width || !rect.height) return
      const centerX = rect.left + rect.width / 2
      const centerY = rect.top + rect.height / 2
      const dx = event.clientX - centerX
      const dy = centerY - event.clientY
      const maxRadius = Math.min(rect.width, rect.height) / 2
      const distance = Math.min(maxRadius, Math.hypot(dx, dy))
      const degrees = (Math.atan2(dx, dy) * 180) / Math.PI
      this.draftWindDirection = Math.round((degrees + 360) % 360)
      this.draftWindSpeed = Number((0.1 + (distance / maxRadius) * 19.9).toFixed(1))
    },
    async runCurrentSimulation() {
      this.simulationMode === 'parallel' ? await this.runParallel() : await this.runSimulation()
    },
    async runSimulation() {
      if (!this.selectedMeteorologyId || !this.effectiveSources.length) return
      this.running = true
      try {
        const simulationSources = [...this.effectiveSources]
        const request = {
          meteorologyId: this.selectedMeteorologyId,
          sourceIds: this.selectionBounds ? simulationSources.map((s) => s.id) : undefined,
          receptorIds: this.selectionBounds ? this.effectiveReceptors.map((r) => r.id) : undefined,
          pollutantType: this.calculationPollutant || undefined,
          windSpeed: this.draftWindSpeed,
          windDirection: this.draftWindDirection,
          gridResolution: this.gridResolution,
          domainSize: this.domainSize,
          receptorHeight: this.simulationHeight,
        }
        const r = await simulationApi.run(request)
        this.result = r
        this.resultSources = simulationSources
        this.lastSimulationInputs = {
          mode: 'single',
          meteorologyId: request.meteorologyId,
          windSpeed: request.windSpeed,
          windDirection: request.windDirection,
          gridResolution: request.gridResolution,
          domainSize: request.domainSize,
          receptorHeight: request.receptorHeight,
          calculationPollutant: this.calculationPollutant,
        }
        this.chooseDisplayPollutant(r.availablePollutants, this.calculationPollutant)
        this.$nextTick(() => this.fitResultBounds())
      } finally {
        this.running = false
      }
    },
    async runParallel() {
      if (!this.selectedMeteorologyId || !this.effectiveSources.length) return
      this.running = true
      try {
        const simulationSources = [...this.effectiveSources]
        const request = {
          meteorologyId: this.selectedMeteorologyId,
          sourceIds: this.selectionBounds ? simulationSources.map((s) => s.id) : undefined,
          receptorIds: this.selectionBounds ? this.effectiveReceptors.map((r) => r.id) : undefined,
          pollutantType: this.calculationPollutant || undefined,
          windSpeed: this.parallelWindSpeed,
          windDirections: this.parallelWindDirections,
          windSpeeds: this.parallelWindSpeeds,
          weights: this.parallelWeights,
          gridResolution: this.gridResolution,
          domainSize: this.domainSize,
          receptorHeight: this.simulationHeight,
        }
        const r = await simulationApi.runParallel(request as any)
        this.result = {
          concentrations: r.concentrations,
          gridLat: r.gridLat,
          gridLon: r.gridLon,
          contributions: [],
          receptorContributions: r.receptorContributions || {},
          pollutantConcentrations: r.pollutantConcentrations || null,
          availablePollutants: r.availablePollutants || null,
        } as any
        this.resultSources = simulationSources
        this.chooseDisplayPollutant(r.availablePollutants, this.calculationPollutant)
        this.lastSimulationInputs = {
          mode: 'parallel',
          meteorologyId: request.meteorologyId,
          windSpeed: request.windSpeed,
          windDirections: request.windDirections,
          windSpeeds: request.windSpeeds,
          weights: request.weights || [],
          gridResolution: request.gridResolution,
          domainSize: request.domainSize,
          receptorHeight: request.receptorHeight,
          calculationPollutant: this.calculationPollutant,
        }
        this.$nextTick(() => this.fitResultBounds())
      } finally {
        this.running = false
      }
    },
    startSelection() { this.selectionEnabled = true },
    clearSelection() {
      ;(this.$refs.map as any).clearSelection()
      this.selectionEnabled = false
      this.selectionBounds = null
    },
    clearResult() {
      this.result = null
      this.resultSources = []
      this.lastSimulationInputs = null
      this.customMin = null
      this.customMax = null
      this.selectedRankingPollutant = ''
      if (this.$refs.map) this.clearSelection()
    },
    fitBounds() { ;(this.$refs.map as any).fitBounds() },
    fitResultBounds() { ;(this.$refs.map as any).fitResultBounds() },
    onMapViewChange(p: any) {
      this.$store.commit('setPref', { key: 'mapCenter', value: p.center })
      this.$store.commit('setPref', { key: 'mapZoom', value: p.zoom })
    },
  },
})
</script>

<style scoped>
.dashboard-map{position:relative;height:calc(100vh - 104px);min-height:620px;overflow:hidden;border:1px solid #cfdde4;border-radius:8px;background:#eef3f4}.dashboard-top-toolbar{display:flex;flex:1 1 auto;min-width:0;width:100%;flex-direction:column;align-items:stretch;gap:8px;white-space:normal}.dashboard-top-toolbar>*{flex:0 0 auto}.toolbar-select{width:128px}.toolbar-wind{width:230px}.toolbar-compact{width:96px}.toolbar-speed{width:108px}.parallel-note{font-size:12px;color:#64748b}.floating-card{border:1px solid #dfe7ee;border-radius:8px;background:rgba(255,255,255,.96);box-shadow:0 10px 28px rgba(15,46,60,.12)}.range-panel{position:absolute;left:18px;bottom:18px;z-index:1000;width:230px;padding:16px 16px 10px}.range-row{display:flex;justify-content:space-between;color:#64748b;font-size:12px}.range-row strong{color:#1677ff}.right-stack{position:absolute;right:14px;top:76px;bottom:14px;z-index:1000;display:flex;width:320px;flex-direction:column;gap:10px;overflow-y:auto}.right-stack .floating-card{padding:14px}.card-title{display:flex;align-items:center;justify-content:space-between;gap:10px;padding-bottom:10px;border-bottom:1px solid #edf2f5;font-weight:800}.card-icon{color:#1677ff}.hint{color:#64748b;font-size:12px;line-height:1.6}.warning{color:#d97706}.stats-grid{display:grid;grid-template-columns:1fr 1fr;gap:10px}.stats-grid div{padding:12px;border-radius:8px;background:#f8fafc}.stats-grid strong{display:block;font-size:22px;color:#1677ff}.stats-grid span{font-size:12px;color:#64748b}.quick-actions{position:absolute;left:18px;top:92px;z-index:1000;display:flex;flex-direction:column;gap:8px}.selection-summary{margin-top:8px;color:#475569;font-size:12px}.wind-rose{position:relative;width:150px;height:150px;margin:16px auto 10px}.wind-rose svg{display:block;width:100%;height:100%;cursor:crosshair}.wind-rose text{fill:#64748b;font-size:11px}.wind-ring,.wind-axis{fill:none;stroke:#dbe6ec;stroke-width:1}.wind-ring:nth-child(2),.wind-ring:nth-child(3){stroke:#e8eef2;stroke-width:3}.wind-pointer{stroke:#1677ff;stroke-linecap:round;stroke-width:4}.wind-pointer-tip{fill:#1677ff}.field-grid{display:grid;gap:10px;margin-top:12px}.weather-fields{grid-template-columns:repeat(2,minmax(0,1fr))}.weather-fields label{display:flex;flex-direction:column;gap:6px;color:#31545c;font-size:12px}.weather-fields .el-input-number{width:100%}.full-field{display:flex;flex-direction:column;gap:6px;margin-top:10px;color:#31545c;font-size:12px}.complete{color:#16a34a;font-size:12px}.range-header{display:flex;align-items:flex-start;justify-content:space-between;gap:8px;margin-top:10px;color:#31545c;font-size:12px}.range-header small{display:block;color:#64748b;margin-top:3px}.color-fields{grid-template-columns:repeat(2,minmax(0,1fr))}.color-fields label{display:flex;flex-direction:column;gap:6px;color:#31545c;font-size:12px}.color-fields .el-input-number,.color-fields .el-select{width:100%}.ranking-controls{margin-top:10px}.ranking-summary{margin-top:10px;padding:8px 10px;border-radius:8px;background:#f0f7ff;color:#31545c;font-size:12px}.station-summary-list{display:flex;flex-direction:column;gap:10px;margin-top:10px}.station-summary-card{padding:10px;border:1px solid #edf2f5;border-radius:10px;background:#f8fafc}.station-summary-title{display:flex;justify-content:space-between;gap:8px;color:#31545c;font-size:12px}.station-summary-title strong{font-size:13px;color:#0f172a}.station-pollutant-block{margin-top:8px}.pollutant-summary-row,.ranking-item{display:grid;grid-template-columns:52px minmax(0,1fr) 48px 86px;gap:6px;align-items:center;font-size:12px;color:#475569}.ranking-item{grid-template-columns:20px minmax(0,1fr) 48px 86px;margin-top:6px}.ranking-main{min-width:0}.ranking-name{overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.ranking-bar{height:6px;overflow:hidden;border-radius:999px;background:#e2e8f0}.ranking-bar span{display:block;height:100%;border-radius:inherit;background:linear-gradient(90deg,#38bdf8,#1677ff)}.ranking-index,.ranking-percent{color:#64748b;text-align:right}.ranking-item strong,.pollutant-summary-row strong{text-align:right;color:#0f172a;font-size:11px}
@media (max-width: 1100px){.dashboard-map{display:flex;height:auto;min-height:0;flex-direction:column;gap:10px;overflow:visible;padding:10px}.dashboard-map>.map-panel{height:460px;min-height:460px;flex:0 0 auto;border-radius:8px}.range-panel,.right-stack,.quick-actions{position:static;z-index:auto;width:auto}.range-panel{order:2;padding:14px}.right-stack{order:3;display:flex;max-height:none;flex-direction:column;overflow:visible}.quick-actions{order:1;flex-direction:row;align-self:flex-start}.dashboard-top-toolbar{justify-content:flex-start}.toolbar-region,.toolbar-select,.toolbar-wind,.toolbar-compact,.toolbar-speed{width:100%}.weather-fields,.color-fields{grid-template-columns:1fr}.pollutant-summary-row,.ranking-item{grid-template-columns:28px minmax(0,1fr) 44px}.pollutant-summary-row strong,.ranking-item strong{grid-column:2 / 4;text-align:left}}
.wind-profile-actions{display:flex;align-items:center;gap:10px;flex-wrap:wrap;margin:16px 0}.wind-profile-loaded{display:flex;flex-direction:column;gap:12px}.wind-profile-table{width:100%}
.dashboard-toolbar-row{display:flex;align-items:center;gap:8px;flex-wrap:wrap;min-width:0}.simulation-primary-actions{display:flex;align-items:center;gap:8px}
@media (max-width:1100px){.toolbar-region{width:auto!important}.toolbar-select{width:128px!important}.toolbar-wind{width:230px!important}.toolbar-compact{width:96px!important}.toolbar-speed{width:108px!important}}
</style>
