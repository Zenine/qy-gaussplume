<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import type {
  Meteorology,
  ParallelSimulationRequest,
  ParallelSimulationResult,
} from '@/types'
import { simulationApi } from '@/api'
import { errorMessage } from '@/utils/error'

// 并行模拟对话框：预设 8/16/32/72 风向 + 统一风速 + 支持自定义权重
const props = defineProps<{
  visible: boolean
  meteorologies: Meteorology[]
  selectedMeteorologyId: number | null
  gridResolution: number
  domainSize: number
  pollutantType: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  (e: 'completed', result: ParallelSimulationResult): void
}>()

const innerVisible = computed({
  get: () => props.visible,
  set: (v) => emit('update:visible', v),
})

const metId = ref<number | null>(null)
const windSpeed = ref(3.0)
const dirCount = ref<8 | 16 | 32 | 72>(16)
const weightMode = ref<'uniform' | 'custom'>('uniform')
const customWeights = ref<string>('')
const returnAggregated = ref(true)
const running = ref(false)
const result = ref<ParallelSimulationResult | null>(null)

watch(
  () => props.selectedMeteorologyId,
  (v) => (metId.value = v),
  { immediate: true },
)

const directions = computed(() =>
  Array.from({ length: dirCount.value }, (_, i) => (360 / dirCount.value) * i),
)

function parseCustomWeights(): number[] | null {
  if (weightMode.value !== 'custom') return null
  const parts = customWeights.value
    .split(/[,，\s]+/)
    .filter((x) => x.length > 0)
    .map((x) => Number(x))
  if (parts.some(Number.isNaN)) {
    ElMessage.error('权重必须是用逗号或空格分隔的数字')
    throw new Error('invalid weights')
  }
  if (parts.length !== dirCount.value) {
    ElMessage.error(`权重数量 (${parts.length}) 与风向数 (${dirCount.value}) 不匹配`)
    throw new Error('weights count mismatch')
  }
  return parts
}

async function run() {
  if (!metId.value) {
    ElMessage.warning('请选择气象场')
    return
  }
  running.value = true
  result.value = null
  try {
    const weights = parseCustomWeights()
    const request: ParallelSimulationRequest = {
      meteorologyId: metId.value,
      windSpeed: windSpeed.value,
      windDirections: directions.value,
      weights: weights ?? undefined,
      gridResolution: props.gridResolution,
      domainSize: props.domainSize,
      pollutantType: props.pollutantType || undefined,
      returnAggregatedOnly: returnAggregated.value,
    }
    const r = await simulationApi.runParallel(request)
    result.value = r
    emit('completed', r)
    ElMessage.success(
      `成功 ${r.successfulSimulations}/${r.totalWindDirections} 个风向，耗时 ${r.computationTimeSeconds}s`,
    )
  } catch (e) {
    ElMessage.error(errorMessage(e, '并行模拟失败'))
  } finally {
    running.value = false
  }
}
</script>

<template>
  <el-dialog
    v-model="innerVisible"
    title="全局模拟 (多风向加权)"
    width="640px"
    :close-on-click-modal="false"
  >
    <el-form label-width="120px">
      <el-form-item label="气象场">
        <el-select v-model="metId" style="width: 100%">
          <el-option
            v-for="m in meteorologies"
            :key="m.id"
            :value="m.id"
            :label="`${m.name} (${m.stabilityClass})`"
          />
        </el-select>
      </el-form-item>

      <el-form-item label="统一风速 (m/s)">
        <el-input-number v-model="windSpeed" :min="0.1" :max="20" :step="0.1" />
        <span class="hint">覆盖气象场中的风速</span>
      </el-form-item>

      <el-form-item label="风向数">
        <el-radio-group v-model="dirCount">
          <el-radio-button :value="8">8</el-radio-button>
          <el-radio-button :value="16">16</el-radio-button>
          <el-radio-button :value="32">32</el-radio-button>
          <el-radio-button :value="72">72</el-radio-button>
        </el-radio-group>
      </el-form-item>

      <el-form-item label="权重">
        <el-radio-group v-model="weightMode">
          <el-radio value="uniform">等权</el-radio>
          <el-radio value="custom">自定义</el-radio>
        </el-radio-group>
        <el-input
          v-if="weightMode === 'custom'"
          v-model="customWeights"
          type="textarea"
          :rows="3"
          :placeholder="`请输入 ${dirCount} 个权重，以逗号或空格分隔`"
          style="margin-top: 8px"
        />
      </el-form-item>

      <el-form-item label="返回模式">
        <el-switch
          v-model="returnAggregated"
          active-text="聚合 (推荐)"
          inactive-text="每风向明细"
        />
      </el-form-item>

      <el-alert
        v-if="result"
        :title="`${result.mode === 'aggregated' ? '聚合' : '明细'} · 成功 ${result.successfulSimulations} · 耗时 ${result.computationTimeSeconds}s · 加速 ${result.speedupFactor}×`"
        type="success"
        :closable="false"
      />
    </el-form>

    <template #footer>
      <el-button @click="innerVisible = false" :disabled="running">关闭</el-button>
      <el-button type="primary" :loading="running" @click="run">
        ▶ 运行并行模拟
      </el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.hint {
  margin-left: 12px;
  color: #9ca3af;
  font-size: 12px;
}
</style>
