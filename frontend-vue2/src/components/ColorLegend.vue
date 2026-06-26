<template><div class="legend"><span>{{ min }}</span><div class="bar" /><span>{{ max }}</span></div></template>
<script lang="ts">import Vue from 'vue'; export default Vue.extend({ name:'ColorLegend', props:{ min:{default:0}, max:{default:0} } })</script>
<style scoped>.legend{display:flex;align-items:center;gap:8px;font-size:12px;color:#64748b}.bar{width:120px;height:10px;border-radius:999px;background:linear-gradient(90deg,#313695,#74add1,#ffffbf,#f46d43,#a50026)}</style>
