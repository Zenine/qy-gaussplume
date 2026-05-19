<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { ReceptorContributionEntry, SimulationResult } from '@/types'

// 受体贡献度面板：侧拉抽屉，展示每受体×每污染物的源贡献排名。
const props = defineProps<{
  visible: boolean
  result: SimulationResult | null
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
}>()

const innerVisible = computed({
  get: () => props.visible,
  set: (v) => emit('update:visible', v),
})

const receptorNames = computed(() =>
  props.result ? Object.keys(props.result.receptorContributions) : [],
)
const selectedReceptor = ref<string>('')
const selectedPollutant = ref<string>('')

const pollutants = computed(() => {
  if (!props.result || !selectedReceptor.value) return []
  const entry = props.result.receptorContributions[selectedReceptor.value]
  return entry ? Object.keys(entry) : []
})

// 单次 watch 覆盖：结果到达即同步填充默认受体与默认污染物
watch(
  () => props.result,
  (res) => {
    if (!res) {
      selectedReceptor.value = ''
      selectedPollutant.value = ''
      return
    }
    const names = Object.keys(res.receptorContributions)
    if (names.length === 0) {
      selectedReceptor.value = ''
      selectedPollutant.value = ''
      return
    }
    if (!names.includes(selectedReceptor.value)) selectedReceptor.value = names[0]
    const pols = Object.keys(res.receptorContributions[selectedReceptor.value] ?? {})
    if (!pols.includes(selectedPollutant.value)) selectedPollutant.value = pols[0] ?? ''
  },
  { immediate: true },
)

watch(selectedReceptor, () => {
  const pols = pollutants.value
  if (pols.length > 0 && !pols.includes(selectedPollutant.value)) {
    selectedPollutant.value = pols[0]
  }
})

const rows = computed<ReceptorContributionEntry[]>(() => {
  if (!props.result || !selectedReceptor.value || !selectedPollutant.value) return []
  return props.result.receptorContributions[selectedReceptor.value]?.[selectedPollutant.value] ?? []
})

const totalConcentration = computed(() =>
  rows.value.reduce((acc, r) => acc + r.concentration, 0),
)
</script>

<template>
  <el-drawer
    v-model="innerVisible"
    title="受体点贡献分析"
    direction="rtl"
    size="500px"
  >
    <div v-if="!props.result" class="empty">运行模拟后会显示各受体点的源贡献排名</div>

    <template v-else>
      <div class="row">
        <label>受体点</label>
        <el-select v-model="selectedReceptor" size="small" style="width: 180px">
          <el-option v-for="n in receptorNames" :key="n" :value="n" :label="n" />
        </el-select>

        <label>污染物</label>
        <el-select v-model="selectedPollutant" size="small" style="width: 120px">
          <el-option v-for="p in pollutants" :key="p" :value="p" :label="p" />
        </el-select>
      </div>

      <div class="summary">
        <div class="metric">
          <div class="k">总浓度</div>
          <div class="v">{{ totalConcentration.toFixed(4) }} μg/m³</div>
        </div>
        <div class="metric">
          <div class="k">贡献源数</div>
          <div class="v">{{ rows.length }}</div>
        </div>
      </div>

      <el-table :data="rows" stripe size="small" :empty-text="'无贡献数据'">
        <el-table-column label="排名" width="60">
          <template #default="{ $index }">#{{ $index + 1 }}</template>
        </el-table-column>
        <el-table-column prop="sourceName" label="排放源" min-width="130" />
        <el-table-column label="浓度 (μg/m³)" width="140">
          <template #default="{ row }">{{ row.concentration.toFixed(4) }}</template>
        </el-table-column>
        <el-table-column label="占比" width="140">
          <template #default="{ row }">
            <el-progress
              :percentage="Math.min(100, +row.percentage.toFixed(1))"
              :text-inside="true"
              :stroke-width="16"
              :color="row.percentage > 50 ? '#f56c6c' : row.percentage > 20 ? '#e6a23c' : '#67c23a'"
            />
          </template>
        </el-table-column>
      </el-table>
    </template>
  </el-drawer>
</template>

<style scoped>
.empty {
  color: #9ca3af;
  text-align: center;
  padding: 40px 0;
}
.row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 16px;
}
.row label {
  font-size: 13px;
  color: #6b7280;
}
.summary {
  display: flex;
  gap: 24px;
  margin-bottom: 16px;
  padding: 12px;
  background: #f9fafb;
  border-radius: 6px;
}
.metric .k {
  font-size: 12px;
  color: #9ca3af;
}
.metric .v {
  font-size: 18px;
  font-weight: 600;
}
</style>
