<template>
  <el-dialog :visible.sync="innerVisible" title="多风向并行模拟" width="520px">
    <p class="muted">Vue2 版主控台已在顶部工具条内支持多风向直接运行；此弹窗保留为兼容入口。</p>
  </el-dialog>
</template>
<script lang="ts">import Vue from 'vue'; export default Vue.extend({ name:'ParallelSimulationDialog', props:{ visible:Boolean }, computed:{ innerVisible:{ get():boolean{return this.visible}, set(v:boolean){this.$emit('update:visible',v)} } } })</script>
