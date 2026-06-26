<template>
  <el-drawer :visible.sync="innerVisible" title="公式说明" size="460px" @open="load" @close="$emit('update:visible', false)">
    <div v-loading="loading" class="formula-body">
      <pre v-if="info">{{ JSON.stringify(info, null, 2) }}</pre>
      <p v-else class="muted">打开后从后端加载公式、源类型和污染因子参数。</p>
    </div>
  </el-drawer>
</template>
<script lang="ts">
import Vue from 'vue'
import { simulationApi } from '@/api'
export default Vue.extend({
  name: 'FormulaDrawer',
  props: { visible: Boolean },
  data: () => ({ loading: false, info: null as any }),
  computed: { innerVisible: { get(): boolean { return this.visible }, set(v: boolean) { this.$emit('update:visible', v) } } },
  methods: { async load() { if (this.info) return; this.loading = true; try { this.info = await simulationApi.formulas() } finally { this.loading = false } } },
})
</script>
<style scoped>.formula-body{padding:16px}pre{white-space:pre-wrap;font-size:12px}.muted{color:#64748b}</style>
