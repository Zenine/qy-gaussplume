#!/usr/bin/env node
const fs = require('fs')
const path = require('path')
const root = path.resolve(__dirname, '..')
const read = (file) => fs.readFileSync(path.join(root, file), 'utf8')
const sources = read('src/views/SourcesView.vue')
const receptors = read('src/views/ReceptorsView.vue')
const meteorology = read('src/views/MeteorologyView.vue')

function assertIncludes(source, needle, message) {
  if (!source.includes(needle)) throw new Error(`${message}：缺少 ${needle}`)
}

// 排放源管理页应对齐 Vue3 的核心功能。
assertIncludes(sources, 'data-test="source-type-filter"', '排放源页必须提供源类型筛选下拉框')
assertIncludes(sources, '全部类型', '排放源页类型筛选必须包含全部类型')
assertIncludes(sources, 'data-test="apply-source-type-filter"', '排放源页类型筛选必须点击确定后才生效')
assertIncludes(sources, 'downloadTemplate', '排放源页必须支持按源类型下载导入模板')
assertIncludes(sources, 'importFile', '排放源页必须支持按源类型批量导入')
assertIncludes(sources, '全部启用', '排放源页必须提供全部启用')
assertIncludes(sources, '全部停用', '排放源页必须提供全部停用')
assertIncludes(sources, 'Promise.allSettled', '排放源页批量操作应允许部分失败并刷新列表')
assertIncludes(sources, "form.sourceType === 'point'", '排放源表单必须有点源专属字段')
assertIncludes(sources, "form.sourceType === 'area' || form.sourceType === 'equivalent_area'", '排放源表单必须有面源/等效面源专属字段')
assertIncludes(sources, "form.sourceType === 'line'", '排放源表单必须有线源专属字段')
assertIncludes(sources, 'FLSI 积分步长 (m)', '线源参数必须明确表示数值积分步长，不得误导为分段点源')
assertIncludes(sources, 'data-test="pollutant-emission-rate-input"', '普通源污染物必须使用排放速率输入')
assertIncludes(sources, 'data-test="pollutant-concentration-input"', '等效面源污染物必须使用测量浓度输入')
assertIncludes(sources, 'v-else', '等效面源污染物数值框必须与普通源排放速率框严格互斥，不能同时显示两列数值输入')
assertIncludes(sources, 'downloadBlob', '排放源模板下载必须落盘为 Excel 文件')
assertIncludes(sources, 'data-test="source-marker-symbol-select"', '排放源页标记图标必须使用可选目录，不能只提供 factory 文本框')
assertIncludes(sources, 'sourcesApi.markerSymbols()', '排放源页必须从后端加载标记图标目录')

// 受体点管理页对齐 Vue3 核心功能。
assertIncludes(receptors, 'downloadTemplate', '受体点页必须支持下载模板')
assertIncludes(receptors, 'importFile', '受体点页必须支持批量导入')
assertIncludes(receptors, 'exportSelected', '受体点页必须支持导出已选')
assertIncludes(receptors, 'downloadBlob', '受体点页导入导出必须复用下载工具')
assertIncludes(receptors, '全部启用', '受体点页必须提供全部启用')
assertIncludes(receptors, '全部停用', '受体点页必须提供全部停用')
assertIncludes(receptors, 'Promise.allSettled', '受体点页批量操作应允许部分失败并刷新列表')
assertIncludes(receptors, '删除确认', '受体点页删除必须有确认框')
assertIncludes(receptors, '批量删除确认', '受体点页批量删除必须有确认框')
assertIncludes(receptors, 'data-test="receptor-marker-symbol-select"', '受体点页标记图标必须使用可选目录，不能只提供 monitor 文本框')
assertIncludes(receptors, 'sourcesApi.markerSymbols()', '受体点页必须复用后端标记图标目录')

// 气象场管理页对齐 Vue3 核心功能。
assertIncludes(meteorology, 'boundaryLayerHeight', '气象场页必须包含边界层高度字段')
assertIncludes(meteorology, 'temperature', '气象场页必须包含温度字段')
assertIncludes(meteorology, 'humidity', '气象场页必须包含湿度字段')
assertIncludes(meteorology, 'cloudCover', '气象场页必须包含云量字段')
assertIncludes(meteorology, 'precipitation', '气象场页必须包含降水字段')
assertIncludes(meteorology, '全部启用', '气象场页必须提供全部启用')
assertIncludes(meteorology, 'Promise.allSettled', '气象场页批量操作应允许部分失败并刷新列表')
assertIncludes(meteorology, '删除确认', '气象场页删除必须有确认框')
assertIncludes(meteorology, '批量删除确认', '气象场页批量删除必须有确认框')

console.log('Vue2 management parity checks passed')
