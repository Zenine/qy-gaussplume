<template>
  <el-drawer :visible.sync="innerVisible" title="空气站点污染源贡献明细" size="640px" @close="$emit('update:visible', false)">
    <div v-if="!result" class="empty">运行模拟后会显示各空气站点的污染源贡献排名</div>
    <template v-else>
      <div class="panel-toolbar">
        <label>污染物指标</label>
        <el-select v-model="selectedPollutant" data-test="panel-pollutant-select" size="small" clearable placeholder="全部污染物" style="width:180px">
          <el-option v-for="p in pollutants" :key="p" :value="p" :label="p" />
        </el-select>
      </div>
      <div class="station-list">
        <section v-for="card in stationContributionCards" :key="card.receptorName" class="station-card">
          <header class="station-header"><strong>{{ card.receptorName }}</strong><span>总贡献浓度：{{ card.total.toFixed(4) }} µg/m³</span></header>
          <div v-for="pollutant in card.pollutants" :key="pollutant.pollutant" class="pollutant-block">
            <div class="pollutant-header"><strong>{{ pollutant.pollutant }}</strong><span>总贡献浓度：{{ pollutant.total.toFixed(4) }} µg/m³</span></div>
            <div class="table-header"><span>排名</span><span>污染源名称</span><span>贡献浓度 (µg/m³)</span><span>贡献占比</span></div>
            <div v-for="(row, index) in pollutant.rows" :key="`${pollutant.pollutant}-${row.sourceId}`" class="source-row">
              <span>#{{ index + 1 }}</span><strong>{{ row.sourceName }}</strong><span>{{ row.concentration.toFixed(4) }}</span><span>{{ row.percentage.toFixed(1) }}%</span>
            </div>
          </div>
        </section>
        <div v-if="stationContributionCards.length === 0" class="empty">无贡献数据</div>
      </div>
    </template>
  </el-drawer>
</template>
<script lang="ts">
import Vue from 'vue'
export default Vue.extend({
  name: 'ContributionPanel',
  props: { visible: Boolean, result: Object },
  data: () => ({ selectedPollutant: '' }),
  computed: {
    innerVisible: { get(): boolean { return this.visible }, set(v: boolean) { this.$emit('update:visible', v) } },
    pollutants(): string[] {
      if (!this.result) return []
      const values = new Set<string>()
      Object.values((this.result as any).receptorContributions || {}).forEach((byPollutant: any) => Object.keys(byPollutant).forEach((p) => values.add(p)))
      return [...values]
    },
    stationContributionCards(): any[] {
      if (!this.result) return []
      const pollutantFilter = this.selectedPollutant
      return Object.entries((this.result as any).receptorContributions || {}).map(([receptorName, byPollutant]: [string, any]) => {
        const pollutantCards = Object.entries(byPollutant).filter(([p]) => !pollutantFilter || p === pollutantFilter).map(([pollutant, rows]) => {
          const sortedRows = this.sortedRows(rows as any[])
          const total = sortedRows.reduce((sum: number, row: any) => sum + row.concentration, 0)
          return { pollutant, total, rows: sortedRows.slice(0, 10).map((row: any) => ({ ...row, percentage: total > 0 ? row.concentration / total * 100 : 0 })) }
        }).filter((card: any) => card.total > 0).sort((a: any, b: any) => b.total - a.total)
        const total = pollutantCards.reduce((sum: number, card: any) => sum + card.total, 0)
        return { receptorName, total, pollutants: pollutantCards }
      }).filter((card: any) => card.total > 0).sort((a: any, b: any) => b.total - a.total)
    },
  },
  watch: {
    result() { if (!this.pollutants.includes(this.selectedPollutant)) this.selectedPollutant = '' },
  },
  methods: {
    sortedRows(rows: any[]) { return [...(rows || [])].filter((r) => r.concentration > 0).sort((a, b) => b.concentration - a.concentration) },
  },
})
</script>
<style scoped>.empty{padding:20px;color:#9ca3af}.panel-toolbar{display:flex;align-items:center;justify-content:space-between;padding:0 0 12px;border-bottom:1px solid #e5e7eb}.station-list{display:flex;flex-direction:column;gap:14px}.station-card{padding:14px;border:1px solid #e5e7eb;border-radius:10px;background:#f8fafc}.station-header,.pollutant-header{display:flex;justify-content:space-between;gap:8px}.pollutant-block{margin-top:12px}.table-header,.source-row{display:grid;grid-template-columns:56px minmax(0,1fr) 132px 84px;gap:8px;align-items:center;padding:6px 0;font-size:12px}.table-header{color:#64748b;border-bottom:1px solid #e5e7eb}.source-row strong{overflow:hidden;text-overflow:ellipsis;white-space:nowrap}</style>
