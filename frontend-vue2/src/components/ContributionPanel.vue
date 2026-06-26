<template>
  <el-drawer :visible.sync="innerVisible" title="受体点源贡献分析" size="420px" @close="$emit('update:visible', false)">
    <div v-if="!result" class="empty">暂无模拟结果</div>
    <div v-else class="contribution-list">
      <div v-for="(byPollutant, receptorName) in result.receptorContributions" :key="receptorName" class="station-card">
        <h4>{{ receptorName }}</h4>
        <div v-for="(rows, pollutant) in byPollutant" :key="pollutant" class="pollutant-block">
          <strong>{{ pollutant }}</strong>
          <div v-for="row in sortedRows(rows)" :key="row.sourceId + '-' + pollutant" class="row">
            <span>{{ row.sourceName }}</span><b>{{ row.concentration.toFixed(3) }}</b>
          </div>
        </div>
      </div>
    </div>
  </el-drawer>
</template>
<script lang="ts">
import Vue from 'vue'
export default Vue.extend({
  name: 'ContributionPanel',
  props: { visible: Boolean, result: Object },
  computed: {
    innerVisible: {
      get(): boolean { return this.visible },
      set(v: boolean) { this.$emit('update:visible', v) },
    },
  },
  methods: { sortedRows(rows: any[]) { return [...(rows || [])].filter((r) => r.concentration > 0).sort((a, b) => b.concentration - a.concentration).slice(0, 10) } },
})
</script>
<style scoped>.empty{padding:20px;color:#64748b}.station-card{padding:12px;border-bottom:1px solid #e5e7eb}.pollutant-block{margin-top:8px}.row{display:flex;justify-content:space-between;padding:4px 0;color:#475569}</style>
