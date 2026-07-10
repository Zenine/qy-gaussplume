#!/usr/bin/env node
const fs = require('fs')
const path = require('path')

const root = path.resolve(__dirname, '..')
const dashboard = fs.readFileSync(path.join(root, 'src/views/DashboardView.vue'), 'utf8')
const client = fs.readFileSync(path.join(root, 'src/api/client.ts'), 'utf8')
const router = fs.readFileSync(path.join(root, 'src/router/index.ts'), 'utf8')
const mapPanel = fs.readFileSync(path.join(root, 'src/components/MapPanel.vue'), 'utf8')
const sourceGeometry = fs.readFileSync(path.join(root, 'src/utils/sourceGeometry.ts'), 'utf8')
const globalStyle = fs.readFileSync(path.join(root, 'src/style.css'), 'utf8')

function assertIncludes(source, needle, message) {
  if (!source.includes(needle)) {
    throw new Error(message + `：缺少 ${needle}`)
  }
}

assertIncludes(dashboard, 'data-test="weather-card"', 'Vue2 主控台必须保留右侧气象控制窗口')
assertIncludes(dashboard, 'data-test="wind-control-dial"', 'Vue2 主控台必须提供可点击风向/风速圆盘')
assertIncludes(dashboard, 'data-test="wind-direction-input"', 'Vue2 主控台必须提供来风方向输入框')
assertIncludes(dashboard, 'data-test="wind-speed-input"', 'Vue2 主控台必须提供风速输入框')
assertIncludes(dashboard, 'resultWeatherOutdated', 'Vue2 主控台必须提示气象参数变更导致结果过期')
assertIncludes(dashboard, 'lastSimulationInputs', 'Vue2 主控台必须记录上次模拟的气象参数')
assertIncludes(dashboard, 'data-test="calculation-pollutant-select"', 'Vue2 主控台必须保留计算污染物下拉测试锚点')
assertIncludes(dashboard, 'data-test="run-simulation"', 'Vue2 主控台必须保留运行模拟按钮测试锚点')
assertIncludes(dashboard, 'data-test="simulation-mode-select"', 'Vue2 主控台必须保留单/多风向切换测试锚点')
assertIncludes(dashboard, "defaultCalculationPollutants = ['PM2.5', 'PM10', 'TSP', 'VOCs', 'NOx', 'O3']", 'Vue2 主控台计算污染物下拉必须默认包含全量业务污染物')

assertIncludes(dashboard, 'data-test="color-scale-card"', 'Vue2 主控台必须提供扩散浓度色阶控制卡片')
assertIncludes(dashboard, 'data-test="color-range-min"', 'Vue2 主控台必须提供色阶最小值输入')
assertIncludes(dashboard, 'data-test="color-range-max"', 'Vue2 主控台必须提供色阶最大值输入')
assertIncludes(dashboard, 'data-test="ranking-card"', 'Vue2 主控台必须提供空气站点污染源贡献排名卡片')
assertIncludes(dashboard, '空气站点污染源贡献排名', 'Vue2 主控台必须显示空气站点污染源贡献排名标题')
assertIncludes(dashboard, 'stationContributionCards', 'Vue2 主控台必须计算按空气站点分组的贡献排名')
assertIncludes(dashboard, 'chooseDisplayPollutant', 'Vue2 主控台模拟完成后必须按计算污染物优先选择结果分场')
assertIncludes(dashboard, 'syncRankingPollutant', 'Vue2 主控台显示污染物与排名污染物必须联动')
assertIncludes(dashboard, 'this.chooseDisplayPollutant(r.availablePollutants, this.calculationPollutant)', 'Vue2 单风向和多风向结果都必须复用同一污染物选择逻辑')
assertIncludes(dashboard, 'this.$nextTick(() => this.fitResultBounds())', 'Vue2 模拟完成后必须优先定位到热力图结果范围，避免受体点拉偏扩散源头')
assertIncludes(dashboard, ':heatmap-sources="result ? resultSources : activeSources"', 'Vue2 热力图锚点必须使用本次模拟源集合，不能被未参与本次计算的源拉偏')
assertIncludes(dashboard, ':heatmap-wind-direction="lastSimulationInputs && lastSimulationInputs.mode === \'single\' ? lastSimulationInputs.windDirection : null"', 'Vue2 热力图源头可视段必须使用本次单风向结果对应的风向，不能被后续输入值带偏')
assertIncludes(dashboard, 'this.resultSources = simulationSources', 'Vue2 运行模拟后必须保存本次参与计算的源集合')
if (dashboard.includes('concentrations || []) as number[][]).flat()')) {
  throw new Error('Vue2 主控台禁止用 flat()+Math.min/max 计算浓度范围，大网格会触发 Maximum call stack size exceeded')
}

assertIncludes(dashboard, 'data-test="boundary-layer-switch"', 'Vue2 主控台必须提供行政边界开关')
assertIncludes(dashboard, 'boundaryGeoJson', 'Vue2 主控台必须把行政边界 GeoJSON 传给地图')
assertIncludes(dashboard, 'if (!this.boundaryEnabled) return', 'Vue2 行政边界关闭后应保留已加载 GeoJSON，避免反复请求和图层闪烁')
assertIncludes(fs.readFileSync(path.join(root, 'src/components/ContributionPanel.vue'), 'utf8'), 'data-test="panel-pollutant-select"', 'Vue2 贡献分析抽屉必须提供污染物筛选')
assertIncludes(fs.readFileSync(path.join(root, 'src/components/FormulaDrawer.vue'), 'utf8'), '<el-tabs', 'Vue2 公式说明必须按公式/污染因子/源类型分组展示')
assertIncludes(client, 'VITE_API_KEY', 'Vue2 接口客户端必须通过环境变量读取 API Key，禁止硬编码')
assertIncludes(client, 'VITE_API_PATH_PREFIX', 'Vue2 接口客户端必须支持导出集成时配置接口路径前缀')
if (/x-api-key['"]\s*:\s*['"][0-9a-f-]{16,}/i.test(client)) {
  throw new Error('Vue2 接口客户端禁止硬编码 x-api-key')
}
assertIncludes(router, "mode: import.meta.env.VITE_ROUTER_MODE", 'Vue2 路由模式必须支持导出集成时从环境变量切换')
assertIncludes(sourceGeometry, 'const halfLat = metersToLatitudeDegrees(source.areaLength / 2)', 'Vue2 面源地图几何必须与后端一致：AreaLength 控制纬向跨度')
assertIncludes(sourceGeometry, 'const halfLon = metersToLongitudeDegrees(source.areaWidth / 2, source.latitude)', 'Vue2 面源地图几何必须与后端一致：AreaWidth 控制经向跨度')
assertIncludes(mapPanel, 'computeAnchoredBounds(result.gridLat, result.gridLon, this.resultAnchorPoint(), true)', 'Vue2 热力图必须按排放源锚定 WGS84→GCJ02 后的 bounds 叠加到高德底图')
assertIncludes(mapPanel, 'resultAnchorPoint()', 'Vue2 热力图必须以参与排放源几何中心作为图层锚点，避免扩散源头偏移')
assertIncludes(mapPanel, 'heatmapSources', 'Vue2 地图组件必须支持独立热力图锚点源集合')
assertIncludes(mapPanel, 'sourceOrigins: this.resultSourceOrigins()', 'Vue2 羽流突出模式必须从每个参与污染源补可见源头段')
assertIncludes(mapPanel, ".filter((source) => source.sourceType === 'point')", 'Vue2 源头可见段只能用于点源，避免线源被显示成中心点扩散')
assertIncludes(fs.readFileSync(path.join(root, 'src/composables/useHeatmapRenderer.ts'), 'utf8'), 'drawSourceOriginPlumes', 'Vue2 热力图渲染器必须保留污染源源头可见段绘制')
if (mapPanel.includes('Math.max(...result.concentrations.flat())')) {
  throw new Error('Vue2 地图禁止用 Math.max(...concentrations.flat()) 计算最大浓度，大网格会触发 Maximum call stack size exceeded')
}
assertIncludes(mapPanel, 'coordsToLatLng', 'Vue2 行政边界必须通过 coordsToLatLng 做 WGS84→GCJ02 转换后叠加到高德底图')
assertIncludes(mapPanel, 'fitResultBounds()', 'Vue2 地图必须提供结果范围定位，确保扩散图从污染源网格范围展开')
assertIncludes(globalStyle, '@media (max-width: 760px)', 'Vue2 全局布局必须保留小屏侧栏和页头响应式规则')
assertIncludes(globalStyle, 'width: 72px !important', 'Vue2 小屏侧栏必须强制收窄，避免挤压主控台地图')
assertIncludes(dashboard, '@media (max-width: 1100px)', 'Vue2 主控台必须保留小屏地图和控制面板流式布局')
assertIncludes(dashboard, '.dashboard-map>.map-panel{height:460px', 'Vue2 小屏主控台地图必须有稳定高度，避免被右侧面板压缩')

console.log('Vue2 dashboard regression checks passed')

const colorLegend = fs.readFileSync(path.join(root, 'src/components/ColorLegend.vue'), 'utf8')
assertIncludes(colorLegend, 'steppedGradientColor', 'Vue2 色阶图例必须使用与 Vue3 一致的分段色阶')
const parallelDialog = fs.readFileSync(path.join(root, 'src/components/ParallelSimulationDialog.vue'), 'utf8')
assertIncludes(parallelDialog, 'simulationApi.runParallel', 'Vue2 多风向对话框必须保留运行并行模拟能力')
assertIncludes(parallelDialog, 'customWeights', 'Vue2 多风向对话框必须支持自定义权重')
