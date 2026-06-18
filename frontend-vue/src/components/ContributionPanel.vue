<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { ReceptorContributionEntry, SimulationResult } from '@/types'

// 空气站点贡献面板：侧拉抽屉，展示每空气站点×每污染物的污染源贡献排名。
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

const selectedPollutant = ref<string>('')

const pollutants = computed(() => {
  if (!props.result) return []
  const values = new Set<string>()
  for (const byPollutant of Object.values(props.result.receptorContributions)) {
    for (const pollutant of Object.keys(byPollutant)) values.add(pollutant)
  }
  return [...values]
})

watch(
  () => props.result,
  (res) => {
    if (!res) {
      selectedPollutant.value = ''
      return
    }
    if (!pollutants.value.includes(selectedPollutant.value)) {
      selectedPollutant.value = ''
    }
  },
  { immediate: true },
)

function sortedPositiveContributions(rows: ReceptorContributionEntry[]) {
  return rows
    .filter((row) => row.concentration > 0)
    .sort((a, b) => b.concentration - a.concentration)
}

const stationContributionCards = computed(() => {
  if (!props.result) return []
  const pollutantFilter = selectedPollutant.value
  return Object.entries(props.result.receptorContributions)
    .map(([receptorName, byPollutant]) => {
      const pollutantCards = Object.entries(byPollutant)
        .filter(([pollutant]) => !pollutantFilter || pollutant === pollutantFilter)
        .map(([pollutant, contributions]) => {
          const rows = sortedPositiveContributions(contributions)
          const total = rows.reduce((acc, row) => acc + row.concentration, 0)
          return {
            pollutant,
            total,
            rows: rows.slice(0, 10),
          }
        })
        .filter((card) => card.total > 0)
        .sort((a, b) => b.total - a.total)
      const total = pollutantCards.reduce((acc, card) => acc + card.total, 0)
      return {
        receptorName,
        total,
        pollutants: pollutantCards,
      }
    })
    .filter((card) => card.total > 0)
    .sort((a, b) => b.total - a.total)
})
</script>

<template>
  <el-drawer
    v-model="innerVisible"
    title="空气站点污染源贡献明细"
    direction="rtl"
    size="640px"
  >
    <div v-if="!props.result" class="empty">运行模拟后会显示各空气站点的污染源贡献排名</div>

    <template v-else>
      <div class="row">
        <label>污染物指标</label>
        <el-select
          v-model="selectedPollutant"
          data-test="panel-pollutant-select"
          size="small"
          clearable
          placeholder="全部污染物"
          style="width: 180px"
        >
          <el-option v-for="p in pollutants" :key="p" :value="p" :label="p" />
        </el-select>
      </div>

      <div class="station-list">
        <section
          v-for="card in stationContributionCards"
          :key="card.receptorName"
          class="station-card"
        >
          <header class="station-header">
            <strong>{{ card.receptorName }}</strong>
            <span>总贡献浓度：{{ card.total.toFixed(4) }} µg/m³</span>
          </header>
          <div
            v-for="pollutant in card.pollutants"
            :key="pollutant.pollutant"
            class="pollutant-block"
          >
            <div class="pollutant-header">
              <strong>{{ pollutant.pollutant }}</strong>
              <span>总贡献浓度：{{ pollutant.total.toFixed(4) }} µg/m³</span>
            </div>
            <div class="table-header">
              <span>排名</span>
              <span>污染源名称</span>
              <span>贡献浓度 (µg/m³)</span>
              <span>贡献占比</span>
            </div>
            <div
              v-for="(row, index) in pollutant.rows"
              :key="`${pollutant.pollutant}-${row.sourceId}`"
              class="source-row"
            >
              <span>#{{ index + 1 }}</span>
              <strong>{{ row.sourceName }}</strong>
              <span>{{ row.concentration.toFixed(4) }}</span>
              <span>{{ row.percentage.toFixed(1) }}%</span>
            </div>
          </div>
        </section>
        <div v-if="stationContributionCards.length === 0" class="empty">无贡献数据</div>
      </div>
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

.station-list {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.station-card {
  padding: 14px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  background: #f9fafb;
}

.station-header,
.pollutant-header {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 12px;
}

.station-header {
  padding-bottom: 10px;
  border-bottom: 1px solid #e5e7eb;
}

.station-header strong {
  color: #1677ff;
  font-size: 15px;
}

.station-header span,
.pollutant-header span {
  color: #64748b;
  font-size: 12px;
}

.pollutant-block {
  padding-top: 12px;
}

.pollutant-block + .pollutant-block {
  margin-top: 12px;
  border-top: 1px solid #e5e7eb;
}

.pollutant-header strong {
  color: #111827;
  font-size: 14px;
}

.table-header,
.source-row {
  display: grid;
  grid-template-columns: 56px minmax(0, 1fr) 132px 84px;
  gap: 10px;
  align-items: center;
}

.table-header {
  margin-top: 10px;
  padding: 8px 0;
  color: #6b7280;
  font-size: 12px;
  border-bottom: 1px solid #e5e7eb;
}

.source-row {
  padding: 8px 0;
  color: #374151;
  font-size: 13px;
  border-bottom: 1px solid #eef2f7;
}

.source-row strong {
  min-width: 0;
  overflow: hidden;
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
