<template>
  <el-drawer :visible.sync="innerVisible" title="计算公式说明" size="min(720px, 100vw)" @open="load" @close="$emit('update:visible', false)">
    <div v-loading="loading" class="formula-body">
      <el-empty v-if="!loading && !info" description="暂无公式说明" />
      <el-tabs v-else v-model="activeTab">
        <el-tab-pane label="高斯烟羽" name="plume">
          <div class="formula-section"><h3>基础扩散公式</h3><pre>{{ info.gaussianPlumeFormula }}</pre></div>
          <div class="formula-section"><h3>衰减与沉降修正</h3><pre>{{ info.decayFormula }}</pre></div>
          <div class="formula-section"><h3>多风向聚合</h3><pre>{{ info.windAggregationFormula }}</pre></div>
        </el-tab-pane>
        <el-tab-pane label="污染因子" name="pollutants">
          <el-table :data="info.pollutants" stripe size="small">
            <el-table-column label="污染物" min-width="120"><template slot-scope="s"><strong>{{ s.row.type }}</strong><span class="muted"> {{ s.row.name }}</span></template></el-table-column>
            <el-table-column label="重力沉降" min-width="100"><template slot-scope="s">{{ formatNumber(s.row.gravitationalSettlingVelocity) }}</template></el-table-column>
            <el-table-column label="干沉降阻力" min-width="130"><template slot-scope="s">Rb {{ formatNumber(s.row.dryResistanceRb) }} / Rc {{ formatNumber(s.row.dryResistanceRc) }}</template></el-table-column>
            <el-table-column label="湿清除系数" min-width="120"><template slot-scope="s">a {{ formatNumber(s.row.wetScavengingA) }} / b {{ formatNumber(s.row.wetScavengingB) }}</template></el-table-column>
            <el-table-column label="化学衰减" min-width="100"><template slot-scope="s">{{ formatNumber(s.row.chemicalRate) }}</template></el-table-column>
            <el-table-column label="化学增强" min-width="150"><template slot-scope="s"><span v-if="s.row.chemicalEnhanced">温度 ×{{ s.row.chemicalTemperatureMultiplier }} / 湿度 ×{{ s.row.chemicalHumidityMultiplier }}</span><span v-else class="muted">无</span></template></el-table-column>
          </el-table>
        </el-tab-pane>
        <el-tab-pane label="源类型" name="sources">
          <section v-for="source in info.sourceTypes" :key="source.type" class="source-formula">
            <h3>{{ source.name }} <span class="muted">{{ source.type }}</span></h3><pre>{{ source.formula }}</pre><p>{{ source.notes }}</p>
          </section>
        </el-tab-pane>
      </el-tabs>
    </div>
  </el-drawer>
</template>
<script lang="ts">
import Vue from 'vue'
import { simulationApi } from '@/api'
import { errorMessage } from '@/utils/error'
export default Vue.extend({
  name: 'FormulaDrawer',
  props: { visible: Boolean },
  data: () => ({ loading: false, info: null as any, activeTab: 'plume' }),
  computed: { innerVisible: { get(): boolean { return this.visible }, set(v: boolean) { this.$emit('update:visible', v) } } },
  methods: {
    formatNumber(value: number) { if (value === 0) return '0'; if (Math.abs(value) < 0.001) return value.toExponential(2); return Number(value.toFixed(6)).toString() },
    async load() { if (this.info || this.loading) return; this.loading = true; try { this.info = await simulationApi.formulas() } catch (e) { this.$message.error(errorMessage(e, '加载公式说明失败')) } finally { this.loading = false } },
  },
})
</script>
<style scoped>.formula-body{padding:16px}.formula-section,.source-formula{margin-bottom:16px;padding:12px;border:1px solid #e5e7eb;border-radius:10px;background:#f8fafc}pre{white-space:pre-wrap;font-size:12px}.muted{color:#64748b;font-weight:400}</style>
