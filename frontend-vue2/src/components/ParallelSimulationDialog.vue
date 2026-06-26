<template>
  <el-dialog :visible.sync="innerVisible" title="全局模拟 (多风向加权)" width="640px" :close-on-click-modal="false">
    <el-form label-width="120px">
      <el-form-item label="气象场">
        <el-select v-model="metId" style="width:100%">
          <el-option v-for="m in meteorologies" :key="m.id" :value="m.id" :label="`${m.name} (${m.stabilityClass})`" />
        </el-select>
      </el-form-item>
      <el-form-item label="统一风速 (m/s)">
        <el-input-number v-model="windSpeed" :min="0.1" :max="20" :step="0.1" />
        <span class="hint">覆盖气象场中的风速</span>
      </el-form-item>
      <el-form-item label="风向数">
        <el-radio-group v-model="dirCount">
          <el-radio-button :label="8">8</el-radio-button>
          <el-radio-button :label="16">16</el-radio-button>
          <el-radio-button :label="32">32</el-radio-button>
          <el-radio-button :label="64">64</el-radio-button>
          <el-radio-button :label="72">72</el-radio-button>
        </el-radio-group>
      </el-form-item>
      <el-form-item label="权重">
        <el-radio-group v-model="weightMode">
          <el-radio label="uniform">等权</el-radio>
          <el-radio label="custom">自定义</el-radio>
        </el-radio-group>
        <el-input v-if="weightMode === 'custom'" v-model="customWeights" type="textarea" :rows="3" :placeholder="`请输入 ${dirCount} 个权重，以逗号或空格分隔`" style="margin-top:8px" />
      </el-form-item>
      <el-form-item label="返回模式">
        <el-switch v-model="returnAggregated" active-text="聚合 (推荐)" inactive-text="每风向明细" />
      </el-form-item>
      <el-alert v-if="result" :title="`${result.mode === 'aggregated' ? '聚合' : '明细'} · 成功 ${result.successfulSimulations} · 耗时 ${result.computationTimeSeconds}s · 加速 ${result.speedupFactor}×`" type="success" :closable="false" />
    </el-form>
    <span slot="footer">
      <el-button :disabled="running" @click="innerVisible = false">关闭</el-button>
      <el-button type="primary" :loading="running" @click="run">▶ 运行并行模拟</el-button>
    </span>
  </el-dialog>
</template>

<script lang="ts">
import Vue from 'vue'
import { simulationApi } from '@/api'
import { errorMessage } from '@/utils/error'
import type { Meteorology, ParallelSimulationRequest, ParallelSimulationResult } from '@/types'

export default Vue.extend({
  name: 'ParallelSimulationDialog',
  props: {
    visible: Boolean,
    meteorologies: { type: Array, default: () => [] },
    selectedMeteorologyId: { type: Number, default: null },
    gridResolution: { type: Number, default: 100 },
    domainSize: { type: Number, default: 5000 },
    pollutantType: { type: String, default: '' },
    receptorHeight: { type: Number, default: 0 },
  },
  data: () => ({
    metId: null as number | null,
    windSpeed: 3.0,
    dirCount: 16 as 8 | 16 | 32 | 64 | 72,
    weightMode: 'uniform' as 'uniform' | 'custom',
    customWeights: '',
    returnAggregated: true,
    running: false,
    result: null as ParallelSimulationResult | null,
  }),
  computed: {
    innerVisible: {
      get(): boolean { return this.visible },
      set(v: boolean) { this.$emit('update:visible', v) },
    },
    directions(): number[] { return Array.from({ length: this.dirCount }, (_, i) => (360 / this.dirCount) * i) },
  },
  watch: {
    selectedMeteorologyId: { immediate: true, handler(v: number | null) { this.metId = v } },
  },
  methods: {
    parseCustomWeights(): number[] | undefined {
      if (this.weightMode !== 'custom') return undefined
      const parts = this.customWeights.split(/[,，\s]+/).filter(Boolean).map(Number)
      if (parts.some(Number.isNaN)) throw new Error('权重必须是用逗号或空格分隔的数字')
      if (parts.length !== this.dirCount) throw new Error(`权重数量 (${parts.length}) 与风向数 (${this.dirCount}) 不匹配`)
      return parts
    },
    async run() {
      if (!this.metId) { this.$message.warning('请选择气象场'); return }
      this.running = true
      this.result = null
      try {
        const request: ParallelSimulationRequest = {
          meteorologyId: this.metId,
          windSpeed: this.windSpeed,
          windDirections: this.directions,
          weights: this.parseCustomWeights(),
          gridResolution: this.gridResolution,
          domainSize: this.domainSize,
          pollutantType: this.pollutantType || undefined,
          receptorHeight: this.receptorHeight,
          returnAggregatedOnly: this.returnAggregated,
        }
        const r = await simulationApi.runParallel(request)
        this.result = r
        this.$emit('completed', r, request)
        this.$message.success(`成功 ${r.successfulSimulations}/${r.totalWindDirections} 个风向，耗时 ${r.computationTimeSeconds}s`)
      } catch (e) {
        this.$message.error(errorMessage(e, '并行模拟失败'))
      } finally {
        this.running = false
      }
    },
  },
})
</script>

<style scoped>.hint{margin-left:12px;color:#9ca3af;font-size:12px}</style>
