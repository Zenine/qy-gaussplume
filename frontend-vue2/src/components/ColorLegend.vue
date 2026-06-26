<template>
  <div class="legend">
    <div class="title">{{ title || '扩散浓度色阶' }} ({{ unit || 'μg/m³' }})</div>
    <div class="bar">
      <div v-for="(s, i) in stops" :key="i" :style="{ background: s.css }" class="seg" />
    </div>
    <div class="ticks">
      <span>{{ Number(min).toFixed(3) }}</span>
      <span>{{ midValue.toFixed(3) }}</span>
      <span>{{ Number(max).toFixed(3) }}</span>
    </div>
  </div>
</template>

<script lang="ts">
import Vue from 'vue'
import { steppedGradientColor, type ColorScale } from '@/utils/colorScale'

const STEPS = 7

export default Vue.extend({
  name: 'ColorLegend',
  props: {
    min: { type: Number, default: 0 },
    max: { type: Number, default: 0 },
    scale: { type: String, default: 'jet' },
    unit: { type: String, default: 'μg/m³' },
    title: { type: String, default: '扩散浓度色阶' },
  },
  computed: {
    stops(): Array<{ t: number; css: string }> {
      return Array.from({ length: STEPS }, (_, i) => {
        const t = i / (STEPS - 1)
        const [r, g, b] = steppedGradientColor(t, this.scale as ColorScale, STEPS)
        return { t, css: `rgb(${r},${g},${b})` }
      })
    },
    midValue(): number {
      return (Number(this.min) + Number(this.max)) / 2
    },
  },
})
</script>

<style scoped>
.legend{background:rgba(255,255,255,.92);border:1px solid #d1d5db;border-radius:6px;padding:8px 10px;box-shadow:0 2px 6px rgba(0,0,0,.08);font-size:12px;min-width:220px}.title{color:#374151;font-weight:600;margin-bottom:4px}.bar{display:flex;height:12px;border-radius:2px;overflow:hidden}.seg{flex:1}.ticks{display:flex;justify-content:space-between;color:#6b7280;margin-top:4px}
</style>
