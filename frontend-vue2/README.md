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

## 集成环境变量

复制 `.env.example` 为 `.env.local` 后按平台网关调整：

| 变量 | 默认值 | 说明 |
|---|---|---|
| `VITE_API_BASE_URL` | 空 | 后端域名；留空表示同源或 Vite proxy。 |
| `VITE_API_PATH_PREFIX` | `/api` | 接口路径前缀；如果同事平台已经把后端挂到根路径，可设为空字符串。 |
| `VITE_API_KEY` | 空 | 需要 `x-api-key` 时由环境注入，禁止硬编码到源码。 |
| `VITE_ROUTER_MODE` | `history` | 独立部署用 `history`；嵌入 Vue2 老平台或静态目录可改为 `hash`。 |

## 构建

```bash
npm run build
```

## 迁移覆盖范围

详细逐项对齐状态见 [PARITY.md](PARITY.md)。


已迁移：

- 主控台：地图点位、启用数据过滤、框选、单风向/多风向模拟、右侧气象控制圆盘、风速/来风方向临时调整、浓度场叠加、扩散浓度色阶控制、空气站点污染源贡献排名、贡献抽屉入口。
- 排放源管理：列表、新增/编辑、删除、批量删除、全部启用/全部停用、类型筛选确认、按类型模板下载/批量导入、源类型专属字段和污染物子表。
- 受体点管理：列表、新增/编辑、删除、批量删除、下载模板、批量导入、导出已选、全部启用/全部停用。
- 气象场管理：列表、新增/编辑、删除、批量删除、全部启用，以及边界层高度、温度、湿度、云量、降水等完整字段。

后续如需做到与 Vue3 版本完全像素级一致，建议继续补齐：Excel 导入导出、公式抽屉精细排版、地图行政边界开关、更多组件级自动化测试。
