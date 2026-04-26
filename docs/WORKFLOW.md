# 开发工作流

日常写代码常用命令与标准步骤。

## 起停

```bash
# 一键起（前后端同时）
./scripts/start.sh

# 一键停
./scripts/stop.sh

# 手动起（两个终端）
cd backend-dotnet/GnnSimulation.Api && dotnet run
cd frontend-vue && npm run dev
```

- 后端: <http://localhost:5207>
- 前端: <http://localhost:5173>（自动代理 `/api` 到后端）

## 跑测试

```bash
# 完整验证（提交前推荐）
./scripts/verify.sh

# 后端（136 用例，~30s）
cd backend-dotnet
dotnet test --nologo

# 单个类
dotnet test --filter "FullyQualifiedName~SourcesControllerTests"

# 前端（60 用例，~7s）
cd frontend-vue
npm test

# 前端 watch
npm run test:watch
```

## 常见变更模板

### 加一个新的数据实体

1. **写实体类** `GnnSimulation.Data/Entities/Foo.cs`（继承 `EntityBase` 获得 id + 时间戳）
2. **加 DbSet** `GnnSimulation.Data/GnnDbContext.cs`：
   ```csharp
   public DbSet<Foo> Foos => Set<Foo>();
   ```
3. **加 Fluent 映射**（在 `OnModelCreating` 里）：
   ```csharp
   e.ToTable("foos");
   e.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
   // ⚠️ 不要用 HasDefaultValue(...)，用 CLR 属性初始化器代替
   ```
4. **生成迁移**：
   ```bash
   export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
   export DOTNET_ROOT="$HOME/.dotnet"
   dotnet ef migrations add AddFoo --project GnnSimulation.Data --startup-project GnnSimulation.Data
   ```
5. **写实体测试** `GnnSimulation.Tests/Data/FooTests.cs`（默认值、约束、关联）

### 加一个新 API

1. **DTO**：`GnnSimulation.Api/Dtos/FooDtos.cs`（Create/Update/Response 三件套）
2. **映射**：`GnnSimulation.Api/Mapping/EntityMapping.cs` 补 `ToEntity/ToDto/ApplyUpdate` 扩展方法
3. **Controller**：`GnnSimulation.Api/Controllers/FoosController.cs`
4. **集成测试**：`GnnSimulation.Tests/Api/FoosControllerTests.cs`（用 `GnnWebApplicationFactory`）
5. **前端类型**：`frontend-vue/src/types/index.ts` 加 `Foo / FooCreate / FooUpdate`
6. **前端 API**：`frontend-vue/src/api/foos.ts` 导出 `foosApi`
7. **前端 API 测试**：`frontend-vue/tests/api.spec.ts` 补用例

### 改后端算法

1. 先动 `GnnSimulation.Core/Atmosphere/*.cs`
2. 跑单测：`dotnet test --filter "FullyQualifiedName~GaussianPlumeModelTests"`
3. 如改了公式 → 重新生成黄金值：
   ```bash
   cd backend-dotnet/GnnSimulation.Tests/Data/golden
   python3 generate_golden.py
   ```
4. 跑黄金值测试：`dotnet test --filter "FullyQualifiedName~GoldenValueTests"`

### 改前端页面

1. 动 `.vue` 文件
2. `npm run dev` 自带 HMR，保存即刷新
3. 写/跑测试：`npm test -- tests/views/FooView.spec.ts`
4. 发布前：`npm run build`（会跑 `vue-tsc -b` 做类型检查）

## 已知陷阱

### EF Core `HasDefaultValue` 不能乱用

EF 如果检测到属性值等于 `HasDefaultValue(x)` 里的 `x`，会**跳过 INSERT 这列**，让 DB 层 DEFAULT 生效。但如果 DB schema 没有 DEFAULT，列会被写成 NULL → 读回非空属性时崩。

**规则**：
- ✅ `HasDefaultValueSql("CURRENT_TIMESTAMP")` 用于 DB 层真的有默认的列（时间戳）
- ❌ 不要用 `HasDefaultValue("point")` 之类
- ✅ 在 C# 实体类用初始化器：`public string SourceType { get; set; } = "point";`

### 老 Python DB 的历史 NULL 值

Python SQLAlchemy 的 `Column(Boolean, default=True)` 是 **Python 层默认**，不是 DB 层 NOT NULL。历史行可能存在 `is_active = NULL`。

**处理**：`Program.cs` 启动时已加 `UPDATE ... WHERE is_active IS NULL` 自愈。新加字段若有类似情况，在同一位置加 SQL。

### Vitest 对 jsdom 的 Canvas 限制

`<canvas>` 2D context 在 jsdom 里是 null。`tests/heatmap.spec.ts` 用 `vi.fn(() => ({ createImageData, putImageData }))` 做最小 stub。要测真实像素渲染，需改用 `@vitest/browser` 或引入 `canvas` node 模块（重）。

### 并发 npm install 会相互死锁

不要开多个 `npm install` 终端。如遇到僵尸进程：

```bash
pkill -9 -f "npm install"
rm -rf frontend-vue/node_modules frontend-vue/package-lock.json
cd frontend-vue && npm install --registry=https://registry.npmmirror.com
```

### 后端监听端口修改

`backend-dotnet/GnnSimulation.Api/Properties/launchSettings.json` 有 http 和 https 两个 profile，默认 `5207`/`7067`。改了要同时更新：
- `backend-dotnet/GnnSimulation.Api/Program.cs` 的 CORS 白名单
- `frontend-vue/.env` 的 `VITE_API_PROXY_TARGET`

## 验证全栈正常

```bash
# 1. 后端测试绿
cd backend-dotnet && dotnet test --nologo | tail -3
# 预期：已通过! - 失败: 0，通过: 136

# 2. 前端测试绿
cd frontend-vue && npm test 2>&1 | tail -3
# 预期：Test Files 15 passed, Tests 60 passed

# 3. 构建成功
(cd backend-dotnet && dotnet build --nologo) && (cd frontend-vue && npm run build)

# 4. 跑起来并敲端到端
./scripts/start.sh
sleep 5
curl -s http://localhost:5207/api/map/bounds | head -c 100
curl -s http://localhost:5173/api/sources/pollutant-types | head -c 100   # 经 Vite proxy
./scripts/stop.sh
```

## 其他常用命令

```bash
# 后端 OpenAPI 导出（启动后）
curl http://localhost:5207/openapi/v1.json > docs/openapi.json

# 前端 bundle 体积分析
cd frontend-vue && npm run build  # 查看 dist/assets/ 目录的 .js 大小

# SQLite 查表
sqlite3 backend/air_pollution.db ".tables"
sqlite3 backend/air_pollution.db "SELECT count(*) FROM emission_sources;"

# 查看运行时日志
tail -f .run/backend.log
tail -f .run/frontend.log
```

## 运行时日志

`scripts/start.sh` 会把前后端输出分别写到：

- `.run/backend.log` — 后端 ASP.NET Core 输出（含 HTTP 请求日志、EF SQL、异常堆栈）
- `.run/frontend.log` — 前端 Vite 输出（启动、HMR、代理转发错误）

每次 `start.sh` 启动会把旧日志轮转为 `*.log.1`。后端 HTTP 请求日志默认已开启 `Information` 级别（见 `backend-dotnet/GnnSimulation.Api/appsettings.json`）。

## 问题反馈

有 bug 报告、功能建议或疑问，联系 **Zenine Xu** <zeninexu@gmail.com> 或在仓库提 issue。
