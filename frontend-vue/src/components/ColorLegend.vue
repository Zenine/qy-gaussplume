<script setup lang="ts">
import { computed } from 'vue'
import { gradientColor, type ColorScale } from '@/utils/colorScale'

// 浮在地图右下角的色阶图例条
const props = defineProps<{
  min: number
  max: number
  scale: ColorScale
  unit?: string
}>()

const STEPS = 24
const stops = computed(() =>
  Array.from({ length: STEPS }, (_, i) => {
    const t = i / (STEPS - 1)
    const [r, g, b] = gradientColor(t, props.scale)
    return { t, css: `rgb(${r},${g},${b})` }
  }),
)

const midValue = computed(() => (props.min + props.max) / 2)
</script>

<template>
  <div class="legend">
    <div class="title">浓度 ({{ props.unit ?? 'μg/m³' }})</div>
    <div class="bar">
      <div
        v-for="(s, i) in stops"
        :key="i"
        :style="{ background: s.css }"
        class="seg"
      />
    </div>
    <div class="ticks">
      <span>{{ props.min.toFixed(3) }}</span>
      <span>{{ midValue.toFixed(3) }}</span>
      <span>{{ props.max.toFixed(3) }}</span>
    </div>
  </div>
</template>

<style scoped>
.legend {
  background: rgba(255, 255, 255, 0.92);
  border: 1px solid #d1d5db;
  border-radius: 6px;
  padding: 8px 10px;
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.08);
  font-size: 12px;
  min-width: 220px;
}
.title {
  color: #374151;
  font-weight: 600;
  margin-bottom: 4px;
}
.bar {
  display: flex;
  height: 12px;
  border-radius: 2px;
  overflow: hidden;
}
.seg {
  flex: 1;
}
.ticks {
  display: flex;
  justify-content: space-between;
  color: #6b7280;
  margin-top: 4px;
}
</style>
