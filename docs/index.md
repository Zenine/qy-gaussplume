---
titleTemplate: ':title'
layout: home
hero:
  name: QY-GaussPlume
  text: 让大气扩散评估成为可操作的地图工作流
  tagline: 面向科研与工程评估团队，把排放源、受体点、气象场和贡献分析整合到一个可验证的模拟平台。
  image:
    src: /hero.svg
    alt: QY-GaussPlume
  actions:
    - theme: brand
      text: 快速开始
      link: /quick-start
    - theme: alt
      text: 查看架构
      link: /ARCHITECTURE
features:
  - icon:
      src: /icons/globe.svg
    title: 地图上完成扩散判断
    details: 在同一工作台里查看排放源、受体点、模拟范围和浓度热力图，减少在脚本、表格和地图之间来回切换。
  - icon:
      src: /icons/settings.svg
    title: 临时风场快速试算
    details: 在主控台直接调整风速和风向运行单风向模拟，不覆盖已保存气象场，适合现场讨论和方案推演。
  - icon:
      src: /icons/bar-chart.svg
    title: 解释每个受体点的来源
    details: 按污染物输出来源贡献排名和百分比，让评估结论不仅有浓度结果，也能说明影响来自哪里。
  - icon:
      src: /icons/file-text.svg
    title: 批量数据管理
    details: 通过 Excel 模板维护排放源和受体点，支持批量导入、导出和受体点批量删除。
---

# QY-GaussPlume

QY-GaussPlume 是面向科研和工程评估的大气污染物扩散模拟平台。它用 ASP.NET Core 9、Vue 3、Leaflet 和高斯烟羽模型，把排放源管理、气象场选择、地图模拟和贡献分析串成一个可运行、可验证、可交付的工作流。

## 适用场景

- 环评项目中快速比较不同排放源和风场条件。
- 工程方案讨论时查看污染物扩散范围和受体点影响。
- 科研或教学中复现实验数据、测试模型参数和解释贡献排名。

## 继续阅读

- [快速开始](quick-start.md)
- [架构与演进](ARCHITECTURE.md)
- [API 参考](API.md)
- [开发工作流](WORKFLOW.md)
- [FAQ](faq.md)
