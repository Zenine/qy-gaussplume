<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { simulationApi } from '@/api'
import type { SimulationFormulaInfo } from '@/types'
import { errorMessage } from '@/utils/error'

const props = defineProps<{
  visible: boolean
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
}>()

const innerVisible = computed({
  get: () => props.visible,
  set: (v) => emit('update:visible', v),
})

const loading = ref(false)
const info = ref<SimulationFormulaInfo | null>(null)
const activeTab = ref('plume')

function formatNumber(value: number) {
  if (value === 0) return '0'
  if (Math.abs(value) < 0.001) return value.toExponential(2)
  return Number(value.toFixed(6)).toString()
}

async function loadFormulas() {
  if (info.value || loading.value) return
  loading.value = true
  try {
    info.value = await simulationApi.formulas()
  } catch (e) {
    ElMessage.error(errorMessage(e, '加载公式说明失败'))
  } finally {
    loading.value = false
  }
}

watch(
  () => props.visible,
  (visible) => {
    if (visible) void loadFormulas()
  },
  { immediate: true },
)
</script>

<template>
  <el-drawer
    v-model="innerVisible"
    title="计算公式说明"
    direction="rtl"
    size="min(720px, 100vw)"
    class="formula-drawer"
  >
    <div v-loading="loading" class="formula-body">
      <el-empty v-if="!loading && !info" description="暂无公式说明" />

      <template v-else-if="info">
        <el-tabs v-model="activeTab">
          <el-tab-pane label="高斯烟羽" name="plume">
            <div class="formula-section">
              <h3>基础扩散公式</h3>
              <pre>{{ info.gaussianPlumeFormula }}</pre>
            </div>
            <div class="formula-section">
              <h3>衰减与沉降修正</h3>
              <pre>{{ info.decayFormula }}</pre>
            </div>
            <div class="formula-section">
              <h3>多风向聚合</h3>
              <pre>{{ info.windAggregationFormula }}</pre>
            </div>
          </el-tab-pane>

          <el-tab-pane label="污染因子" name="pollutants">
            <el-table :data="info.pollutants" stripe size="small" class="formula-table">
              <el-table-column label="污染物" min-width="120">
                <template #default="{ row }">
                  <strong>{{ row.type }}</strong>
                  <span class="muted">{{ row.name }}</span>
                </template>
              </el-table-column>
              <el-table-column label="重力沉降 (m/s)" min-width="120">
                <template #default="{ row }">
                  {{ formatNumber(row.gravitationalSettlingVelocity) }}
                </template>
              </el-table-column>
              <el-table-column label="干沉降阻力" min-width="130">
                <template #default="{ row }">
                  Rb {{ formatNumber(row.dryResistanceRb) }} / Rc {{ formatNumber(row.dryResistanceRc) }}
                </template>
              </el-table-column>
              <el-table-column label="湿清除系数" min-width="120">
                <template #default="{ row }">
                  a {{ formatNumber(row.wetScavengingA) }} / b {{ formatNumber(row.wetScavengingB) }}
                </template>
              </el-table-column>
              <el-table-column label="化学衰减" min-width="100">
                <template #default="{ row }">
                  {{ formatNumber(row.chemicalRate) }}
                </template>
              </el-table-column>
              <el-table-column label="修正" min-width="130">
                <template #default="{ row }">
                  <el-tag v-if="row.chemicalEnhanced" size="small" type="warning">化学增强</el-tag>
                  <el-tag v-if="row.temperatureCorrected" size="small" type="success">温度修正</el-tag>
                  <span v-if="!row.chemicalEnhanced && !row.temperatureCorrected" class="muted">常规</span>
                </template>
              </el-table-column>
            </el-table>
          </el-tab-pane>

          <el-tab-pane label="源类型" name="sources">
            <div class="source-formulas">
              <section v-for="source in info.sourceTypes" :key="source.type" class="source-formula">
                <div>
                  <h3>{{ source.name }}</h3>
                  <span class="muted">{{ source.type }}</span>
                </div>
                <pre>{{ source.formula }}</pre>
                <p>{{ source.notes }}</p>
              </section>
            </div>
          </el-tab-pane>
        </el-tabs>
      </template>
    </div>
  </el-drawer>
</template>

<style scoped>
.formula-body {
  min-height: 360px;
}

.formula-section {
  margin-bottom: 18px;
}

.formula-section h3,
.source-formula h3 {
  margin: 0 0 8px;
  color: #1f2937;
  font-size: 14px;
  font-weight: 600;
}

pre {
  margin: 0;
  overflow-x: auto;
  border: 1px solid #dce6ec;
  border-radius: 6px;
  background: #f8fafc;
  color: #334155;
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 12px;
  line-height: 1.55;
  padding: 10px 12px;
  white-space: pre-wrap;
}

.formula-table :deep(.cell) {
  line-height: 1.45;
}

.muted {
  display: block;
  color: #6b7280;
  font-size: 12px;
}

.source-formulas {
  display: grid;
  gap: 14px;
}

.source-formula {
  border-bottom: 1px solid #e5edf3;
  padding-bottom: 14px;
}

.source-formula p {
  margin: 8px 0 0;
  color: #4b5563;
  font-size: 13px;
  line-height: 1.6;
}
</style>
