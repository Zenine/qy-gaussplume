# 长三院源贡献计算模拟平台 Vue2 前端

这是从 `frontend-vue/` Vue3 版本迁移出的 Vue2 独立前端，供仍在 Vue2 技术栈上的平台集成使用。

## 技术栈

- Vue 2.7
- Vue Router 3
- Vuex 3
- Element UI 2
- Vite + `@vitejs/plugin-vue2`
- Leaflet + 高德瓦片

## 与 Vue3 版本的关系

- `frontend-vue/`：原 Vue3 + Element Plus 版本，继续保留。
- `frontend-vue2/`：Vue2 + Element UI 迁移版本，调用同一套后端 API。

Vue2 没有内置 Teleport，因此主控台工具条通过挂载后移动 DOM 的方式放入 App 顶部状态栏；如果单独嵌入其它 Vue2 平台，也可以保留页面内部 fallback 布局。

## 启动

```bash
cd frontend-vue2
npm install
npm run dev
```

默认端口：`5174`。开发环境会把 `/api` 代理到 `http://127.0.0.1:5207`。

## 构建

```bash
npm run build
```

## 迁移覆盖范围

已迁移：

- 主控台：地图点位、启用数据过滤、框选、单风向/多风向模拟、浓度场叠加、贡献抽屉入口。
- 排放源管理：列表、新增/编辑、删除、批量删除、全部启用/全部停用、类型筛选确认。
- 受体点管理：列表、新增/编辑、删除、批量删除、全部启用/全部停用。
- 气象场管理：列表、新增/编辑、删除、批量删除、全部启用。

后续如需做到与 Vue3 版本完全像素级一致，建议继续补齐：Excel 导入导出、公式抽屉精细排版、地图行政边界开关、更多自动化测试。
