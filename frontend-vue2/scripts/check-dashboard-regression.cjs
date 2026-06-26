#!/usr/bin/env node
const fs = require('fs')
const path = require('path')

const root = path.resolve(__dirname, '..')
const dashboard = fs.readFileSync(path.join(root, 'src/views/DashboardView.vue'), 'utf8')
const client = fs.readFileSync(path.join(root, 'src/api/client.ts'), 'utf8')
const router = fs.readFileSync(path.join(root, 'src/router/index.ts'), 'utf8')

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

assertIncludes(dashboard, 'data-test="boundary-layer-switch"', 'Vue2 主控台必须提供行政边界开关')
assertIncludes(dashboard, 'boundaryGeoJson', 'Vue2 主控台必须把行政边界 GeoJSON 传给地图')
assertIncludes(fs.readFileSync(path.join(root, 'src/components/ContributionPanel.vue'), 'utf8'), 'data-test="panel-pollutant-select"', 'Vue2 贡献分析抽屉必须提供污染物筛选')
assertIncludes(fs.readFileSync(path.join(root, 'src/components/FormulaDrawer.vue'), 'utf8'), '<el-tabs', 'Vue2 公式说明必须按公式/污染因子/源类型分组展示')
assertIncludes(client, 'VITE_API_KEY', 'Vue2 接口客户端必须通过环境变量读取 API Key，禁止硬编码')
assertIncludes(client, 'VITE_API_PATH_PREFIX', 'Vue2 接口客户端必须支持导出集成时配置接口路径前缀')
if (/x-api-key['"]\s*:\s*['"][0-9a-f-]{16,}/i.test(client)) {
  throw new Error('Vue2 接口客户端禁止硬编码 x-api-key')
}
assertIncludes(router, "mode: import.meta.env.VITE_ROUTER_MODE", 'Vue2 路由模式必须支持导出集成时从环境变量切换')

console.log('Vue2 dashboard regression checks passed')

const colorLegend = fs.readFileSync(path.join(root, 'src/components/ColorLegend.vue'), 'utf8')
assertIncludes(colorLegend, 'steppedGradientColor', 'Vue2 色阶图例必须使用与 Vue3 一致的分段色阶')
const parallelDialog = fs.readFileSync(path.join(root, 'src/components/ParallelSimulationDialog.vue'), 'utf8')
assertIncludes(parallelDialog, 'simulationApi.runParallel', 'Vue2 多风向对话框必须保留运行并行模拟能力')
assertIncludes(parallelDialog, 'customWeights', 'Vue2 多风向对话框必须支持自定义权重')
