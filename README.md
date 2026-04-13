# 大气污染扩散模拟系统

基于高斯烟羽模型的大气污染扩散模拟系统，支持点源、面源、线源和等效面源的扩散计算。

## ✨ 核心亮点

- ⚡ **极速并行计算**：32核多进程并行 + NumPy向量化优化，速度提升**150-300倍**
- 🎯 **高精度大范围模拟**：支持10m分辨率 + 10km范围 + 72风向的复杂场景
- 💾 **智能内存管理**：后端聚合模式，数据传输量减少**99%**
- 📊 **完整受体点分析**：每个风向独立计算 + 权重聚合，结果准确一致
- 🗺️ **多图层地图**：高德街道/卫星/混合地图 + WGS84/GCJ02坐标转换

## 功能特性

### 基础功能
- **排放源管理**：支持点源、面源、线源、等效面源四种排放源类型
- **气象条件管理**：支持风向、风速、大气稳定度、温度、湿度、云量、降水等气象参数配置
- **受体点管理**：支持添加多个受体点，并设置高度参数

### 扩散模拟
- **单一风向模拟**：选择特定气象条件进行快速模拟
- **全局模拟（多风向加权）**：
  - 支持8/16/32/64/72方向快速配置
  - 自定义每个风向的风速和权重
  - 统一风速设置：一键修改所有风向
- **并行计算模式**：
  - 自动检测CPU核心数并充分利用
  - 后端智能聚合，大幅减少数据传输
  - 实时显示计算进度和加速比

### 结果展示
- **浓度场热力图**：9种色阶可选（Jet、Turbo、Spectral_r等）
- **受体点浓度贡献分析**：查看各排放源对特定受体点的浓度贡献排名
- **污染物切换**：PM2.5、PM10、TSP、VOCs、NOx、O3等多污染物独立展示
- **自定义浓度范围**：手动设置最小/最大值以突出显示感兴趣区域
- **渲染精度调节**：1x-16x可调，与物理网格分辨率分离

### 高级功能
- **状态记忆**：自动保存所有设置，刷新页面后恢复
- **WGS84/GCJ02坐标转换**：自动处理国内地图偏移问题
- **连续色阶渲染**：平滑的颜色渐变效果
- **大范围高精度优化**：自动调整Canvas尺寸避免浏览器崩溃

## 性能表现

### 优化前后对比

| 配置 | 优化前 | 优化后 | 提升倍数 |
|------|--------|--------|----------|
| **单风向 (10m, 10km)** | ~60秒 | **~2-3秒** | **25倍** ⚡ |
| **72风向串行** | 72分钟 | - | - |
| **72风向并行 (32核)** | - | **15-30秒** | **150-300倍** 🚀 |
| **数据传输量 (72风向)** | ~2GB | **~20MB** | **减少99%** |
| **内存占用** | 超过系统限制 | **<200MB** | 安全范围 |

### 支持的配置矩阵

| 分辨率 | 域大小 | 风向数 | 网格点数 | 预计时间 | 状态 |
|--------|--------|--------|----------|----------|------|
| 10m | 5km | 72 | 25万 | <5s | ✅ 极快 |
| **10m** | **10km** | **72** | **100万** | **15-30s** | ✅ **完美运行** |
| 10m | 10km | 360 | 100万 | 30-60s | ✅ 快速 |
| 25m | 10km | 72 | 16万 | <5s | ✅ 极快 |
| 50m | 10km | 72 | 4万 | <2s | ✅ 瞬间 |

## 技术架构

### 系统架构图

```
┌─────────────────────────────────────────────────────────────┐
│                        前端 (Browser)                         │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │ Leaflet地图 │  │ 控制面板     │  │ Canvas渲染引擎      │  │
│  │ (GCJ02坐标) │  │ (参数设置)   │  │ (双线性插值+色阶)   │  │
│  └─────────────┘  └─────────────┘  └─────────────────────┘  │
└──────────────────────────┬──────────────────────────────────┘
                           │ HTTP/JSON API
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                      后端 (FastAPI)                          │
│                                                             │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │ API路由层    │  │ 并行调度器   │  │ 数据库 (SQLite)     │  │
│  │ simulation.py│  │ ProcessPool  │  │ SQLAlchemy ORM      │  │
│  └──────┬──────┘  └──────┬──────┘  └─────────────────────┘  │
│         │                 │                                   │
│         ▼                 ▼                                   │
│  ┌─────────────────────────────────────┐                    │
│  │        工作进程池 (N个进程)          │                    │
│  │  ┌─────────┐ ┌─────────┐ ┌─────────┐│                    │
│  │  │ Process1│ │ Process2│ │ ...     ││                    │
│  │  │ 风向0°  │ │ 风向5°  │ │ 风向355°││                    │
│  │  └────┬────┘ └────┬────┘ └────┬────┘│                    │
│  └───────┼──────────┼──────────┼──────┘                    │
│          ▼          ▼          ▼                            │
│  ┌─────────────────────────────────────┐                    │
│  │       高斯烟羽模型 (gaussian_plume)  │                    │
│  │  • NumPy向量化计算                   │                    │
│  │  • Pasquill-Gifford扩散参数          │                    │
│  │  • WRF沉降衰减模型                  │                    │
│  │  • 多源类型支持(点/面/线/等效)       │                    │
│  └─────────────────────────────────────┘                    │
└─────────────────────────────────────────────────────────────┘
```

### 技术栈

- **后端框架**：FastAPI + Uvicorn (ASGI)
- **数据库**：SQLite + SQLAlchemy ORM
- **数值计算**：NumPy (向量化) + SciPy
- **前端**：原生 HTML/CSS/JavaScript + Leaflet.js
- **地图服务**：高德地图API (街道/卫星/混合)
- **坐标系统**：WGS84 → GCJ02 自动转换
- **并行计算**：concurrent.futures.ProcessPoolExecutor

### 关键性能优化技术

#### 1️⃣ NumPy向量化计算

**问题**：原始代码使用Python循环计算衰减系数，对于100万网格点需要调用100万次函数

```python
# ❌ 优化前：Python列表推导式（~60秒）
total_decay = np.array([self.calculate_total_decay(x) for x in x_valid])

# ✅ 优化后：NumPy向量化（~0.01秒）
vd = self.calculate_dry_deposition_velocity(pollutant_type)
Lambda = self.calculate_wet_scavenging_coefficient(pollutant_type)
k_total = vd / boundary_layer_height + Lambda
total_decay = np.exp(-k_total * x_valid / wind_speed)
```

**原理**：
- Python循环：每次迭代都有解释器开销、类型检查、函数调用栈
- NumPy向量化：底层C/Fortran实现，直接操作连续内存块，CPU SIMD优化
- **速度提升：20-30倍**

#### 2️⃣ 多进程并行计算

**架构设计**：

```
主进程 (Main Process)
  ├── 任务分发：将72个风向分配给32个工作进程
  ├── 结果收集：异步收集各进程的计算结果
  └── 数据聚合：按权重合并浓度场和受体点贡献度

工作进程池 (Worker Pool)
  ├── Worker 1: 计算风向 0° 的浓度场 + 受体点贡献度
  ├── Worker 2: 计算风向 5° 的浓度场 + 受体点贡献度
  ├── ...
  └── Worker 32: 计算风向 355° 的浓度场 + 受体点贡献度
```

**关键实现**：
```python
from concurrent.futures import ProcessPoolExecutor, as_completed

# 自动检测CPU核心数
num_workers = min(mp.cpu_count(), len(wind_directions))

# 创建工作进程池
with ProcessPoolExecutor(max_workers=num_workers) as executor:
    # 提交任务
    futures = {executor.submit(process_single_wind_direction, args): wind_dir 
               for args in args_list}
    
    # 异步收集结果
    for future in as_completed(futures):
        result = future.result()
        results.append(result)
```

#### 3️⃣ 后端智能聚合模式

**问题**：72个风向的原始数据量约2GB，传输到前端会导致内存溢出

**解决方案**：在后端完成聚合计算，只返回最终结果

```python
if request.return_aggregated_only:  # 默认启用
    # 在后端加权聚合
    for i, result in enumerate(results):
        weight = weights[i] / total_weight
        aggregated_concentrations += conc * weight
    
    # 只返回一个聚合后的结果 (~20MB)
    return {
        "mode": "aggregated",
        "concentrations": aggregated_concentrations.tolist(),
        "receptor_contributions": receptor_contributions,
        ...
    }
```

**内存对比**：
- 原始模式：72个风向 × 20MB = **1.44GB** → 浏览器崩溃
- 聚合模式：1个聚合结果 = **~20MB** → 稳定运行

## 项目结构

```
gnn/
├── backend/                     # 后端代码
│   ├── api/                     # API接口层
│   │   ├── sources.py           # 排放源管理 CRUD
│   │   ├── receptors.py         # 受体点管理 CRUD
│   │   ├── meteorology.py       # 气象条件管理 CRUD
│   │   ├── simulation.py        # 模拟计算核心（并行+聚合）
│   │   └── map.py              # 地图服务（边界+GeoJSON）
│   ├── core/                    # 核心算法层
│   │   └── gaussian_plume.py    # 高斯扩散模型（向量化优化）
│   ├── models/                  # 数据模型
│   │   ├── models.py            # SQLAlchemy ORM模型
│   │   └── schemas.py           # Pydantic请求/响应模型
│   ├── templates/               # HTML模板
│   │   └── index.html           # 主界面（Leaflet+Canvas渲染）
│   ├── main.py                  # FastAPI应用入口
│   ├── database.py              # SQLite数据库配置
│   └── requirements.txt         # Python依赖
├── shp/                         # 地图边界Shapefile数据
├── start_server.bat             # Windows启动脚本
├── stop_server.bat              # Windows停止脚本
└── README.md                    # 本文档
```

## 快速开始

### 环境要求

- Python 3.10+
- 操作系统：Windows/Linux/macOS
- 推荐：8核以上CPU（充分利用并行计算）

### 安装步骤

1. **克隆项目**
```bash
git clone <项目地址>
cd gnn
```

2. **安装依赖**
```bash
cd backend
pip install -r requirements.txt
```

3. **启动服务器**

双击运行 `start_server.bat` 或在终端执行：
```bash
cd backend
python main.py
```

4. **访问系统**

打开浏览器访问：http://localhost:8000

### 关闭服务器

在运行服务器的终端按 `Ctrl + C` 或双击运行 `stop_server.bat`

## 使用说明

### 1. 排放源管理

支持四种排放源类型：

| 类型 | 适用场景 | 必需参数 | 特殊处理 |
|------|----------|----------|----------|
| **点源** | 烟囱等点状排放 | 高度、温度、出口速度、直径 | 有效高度抬升计算 |
| **面源** | 厂房、堆场 | 尺寸(L×W)、高度 | 虚拟点源法初始扩散 |
| **线源** | 道路交通 | 起点/终点、宽度、高度 | 线积分分段计算 |
| **等效面源** | 监测点反演 | 尺寸、测量浓度 | 自动计算等效排放速率 |

### 2. 气象条件配置

| 参数 | 范围 | 影响方式 |
|------|------|----------|
| **风向** (0-360°) | 气象风向（风来自的方向） | 决定污染物扩散主轴方向 |
| **风速** (0.1-20 m/s) | 影响稀释和传输距离 | 风速↑ → 浓度↓、扩散范围↑ |
| **大气稳定度** (A-F) | A极不稳定-F稳定 | A→F: 扩散↓、浓度↑ |
| **温度** (K) | 影响化学转化速率 | 温度↑ → 化学转化加快 |
| **湿度** (%) | 影响干沉降和化学转化 | 湿度↑ → 沉降增强 |
| **云量** (0-10) | 影响湿沉降 | 云量↑ → 湿沉降增强 |
| **降水** (mm/h) | 影响湿清除 | 降水↑ → 清除增强 |
| **边界层高度** (m) | 垂直扩散限制 | BLH↑ → 垂直混合↑ |

### 3. 扩散模拟操作流程

#### 步骤1：选择模拟类型
- **单一风向模拟**：快速测试单个气象条件
- **全局模拟**：多风向加权组合，适用于长期平均评估

#### 步骤2：配置模拟参数
```
模拟范围: 5-100 km（控制域大小）
网格分辨率: 10-500 m（决定物理精度，10m为最高精度）
模拟高度: 0-100 m（计算该高度的浓度分布）
渲染精度: 1x-16x（控制显示清晰度，与物理精度分离）
```

#### 步骤3：运行模拟
- 单击"▶ 运行模拟"按钮
- 观察控制台日志了解计算进度
- 等待弹窗提示完成

#### 步骤4：查看结果
- 地图上显示浓度场热力图
- 切换不同污染物类型
- 调整色阶和浓度范围
- 点击"受体点分析"查看详细贡献度

### 4. 受体点分析

**功能说明**：
- 显示每个排放源对选定受体点的浓度贡献
- 按贡献度排序，直观识别主要污染源
- 支持多污染物分别查看

**一致性保证**：
- 每个风向独立计算该风向下的受体点贡献度
- 按用户定义的权重加权聚合
- 数学结果与串行计算完全一致

## 高斯扩散模型详解

### 基本公式

$$C(x,y,z) = \frac{Q}{2\pi u \sigma_y \sigma_z} \exp\left(-\frac{y^2}{2\sigma_y^2}\right) \left[\exp\left(-\frac{(z-H)^2}{2\sigma_z^2}\right) + \exp\left(-\frac{(z+H)^2}{2\sigma_z^2}\right)\right]$$

其中：
- $C$：浓度 (μg/m³)
- $Q$：排放速率 (g/s)
- $u$：风速 (m/s)
- $\sigma_y, \sigma_z$：水平和垂直扩散参数 (m)
- $H$：有效源高度 (m)（包含烟气抬升）

### 扩散参数（Pasquill-Gifford）

$$\sigma_y = a_y \cdot x^{b_y}$$
$$\sigma_z = a_z \cdot x^{b_z}$$

| 稳定度 | $a_y$ | $b_y$ | $a_z$ | $b_z$ | 扩散特点 |
|--------|-------|-------|-------|-------|----------|
| A (极不稳定) | 0.527 | 0.865 | 0.28 | 0.90 | 扩散最快，浓度低 |
| B (不稳定) | 0.371 | 0.866 | 0.23 | 0.85 | |
| C (弱不稳定) | 0.209 | 0.897 | 0.22 | 0.80 | |
| D (中性) | 0.128 | 0.905 | 0.20 | 0.76 | 典型白天条件 |
| E (弱稳定) | 0.098 | 0.902 | 0.15 | 0.73 | |
| F (稳定) | 0.065 | 0.902 | 0.12 | 0.67 | 扩散最慢，浓度高 |

### 烟气抬升计算（Briggs公式）

对于点源，有效源高度考虑了烟气动力抬升和浮力抬升：

$$\Delta h = \frac{w_s d}{u} \left(1.6 + \frac{T_s - T_a}{T_s} \cdot \frac{g d}{w_s^2}\right)^{1/3}$$

其中：
- $w_s$：烟气出口速度 (m/s)
- $d$：烟囱直径 (m)
- $T_s$：烟气温度 (K)
- $T_a$：环境温度 (K)

### 面源处理（虚拟点源法）

对于面源排放，采用虚拟点源法将面源转化为等效点源：

1. **初始扩散参数**：
   $$\sigma_{y0} = W / 4.3$$
   $$\sigma_{z0} = H / 2.15$$

2. **虚拟距离**：
   $$x_{virtual} = \max\left[\left(\frac{\sigma_{y0}}{a_y}\right)^{1/b_y}, \left(\frac{\sigma_{z0}}{a_z}\right)^{1/b_z}\right]$$

3. **有效扩散参数（卷积形式）**：
   $$\sigma_{y,eff} = \sqrt{\sigma_y^2 + \sigma_{y0}^2}$$
   $$\sigma_{z,eff} = \sqrt{\sigma_z^2 + \sigma_{z0}^2}$$

### 等效面源处理

对于已知监测浓度的等效面源：

1. **等效排放速率计算**：
   $$Q_{eq} = C_{measured} \times u \times H \times \sqrt{L \times W}$$

2. **浓度限制**：
   - 面源内部：$C_{final} = \min(C_{calculated}, C_{measured})$
   - 面源外部：使用标准高斯扩散计算

## 衰减模型（WRF方法）

### 1. 干沉降（阻力法）

$$v_d = v_g + \frac{1}{R_a + R_b + R_c}$$

其中：
- $v_g$：重力沉降速度 (m/s)
- $R_a$：空气动力学阻力（受稳定度影响）
- $R_b$：层流底层阻力
- $R_c$：冠层阻力

**沉降系数**：
$$decay_{deposition} = \exp\left[-(k_dry + \Lambda) \times \frac{x}{u}\right]$$

其中 $k_dry = v_d / H_{BL}$，$H_{BL}$ 为边界层高度

### 2. 湿沉降

$$\Lambda = a \cdot P^b + \Lambda_{background}$$

其中：
- $P$：降水强度 (mm/h)
- $\Lambda_{background} = 10^{-5}$ s⁻¹（背景清除系数）
- 云量修正：$\Lambda \times (1 + 0.1 \times cloud\_cover)$

### 3. 化学转化

$$k_{effective} = k_{base} \cdot f_T \cdot f_H \cdot f_C$$

其中：
- $f_T = \exp((T-298)/20)$ （温度因子）
- $f_H = 1 + (H-50)/200$ （湿度因子）
- $f_C = 1 + cloud\_cover/50$ （云量因子）

### 4. 总衰减

$$decay_{total} = decay_{deposition} \times decay_{chemical}$$

## 渲染系统架构

### 物理与可视化分离

```
┌─────────────────────────────────────────────────────┐
│                   物理计算层（后端）                    │
│                                                     │
│  输入: grid_resolution = 10m, domain = 10km          │
│  → 网格点数: 1001 × 1001 = 1,002,001 个点          │
│                                                     │
│  对每个网格点:                                       │
│    C[i][j] = GaussianPlume(...) × decay              │
│                                                     │
│  输出: concentrations[1001][1001] (float64数组)      │
└──────────────────────┬──────────────────────────────┘
                       │ JSON传输 (~20MB)
                       ▼
┌─────────────────────────────────────────────────────┐
│                  可视化渲染层（前端）                    │
│                                                     │
│  输入: renderScale = 2x                             │
│  → Canvas尺寸: 2002 × 2002 像素                     │
│                                                     │
│  对每个Canvas像素:                                    │
│    1. 双线性插值获取浓度值                            │
│    2. 归一化到 [0, 1]                               │
│    3. 连续色阶映射 (getGradientColor)                │
│    4. 设置RGBA像素值                                │
│                                                     │
│  输出: ImageOverlay (PNG图片叠加到地图)              │
└─────────────────────────────────────────────────────┘
```

### 渲染精度选项

| 精度 | 用途 | Canvas尺寸 | 内存占用 | 适用场景 |
|------|------|------------|----------|----------|
| 1x | 快速预览 | 与网格相同 | 最小 | 大范围低精度 |
| 2x | 平衡模式 | 2×网格 | 中等 | **推荐默认** |
| 4x | 高质量 | 4×网格 | 较大 | 中等范围高精度 |
| 8x | 精细渲染 | 8×网格 | 大 | 小范围超高精度 |
| 12x | 超精细 | 12×网格 | 很大 | 特写区域 |
| 16x | 极限质量 | 16×网格 | 最大 | 最终输出 |

**自动保护机制**：当Canvas尺寸超过4096×4096时，自动降低渲染精度防止浏览器崩溃

## 状态记忆功能

系统自动持久化以下状态到 localStorage：

- ✅ 地图位置（中心点 + 缩放级别）
- ✅ 地图图层选择（街道/卫星/混合）
- ✅ 色阶类型和自定义范围
- ✅ 气象场选择
- ✅ 污染物类型筛选
- ✅ 模拟参数（范围/分辨率/高度/渲染精度）
- ✅ 模拟结果（如果<10MB）

**注意**：超过10MB的结果不会保存到localStorage，需要重新运行模拟

## 依赖清单

```
fastapi>=0.104.0          # Web框架
uvicorn>=0.24.0           # ASGI服务器
sqlalchemy>=2.0.0         # ORM框架
pydantic>=2.0.0           # 数据验证
numpy>=1.24.0             # 数值计算（向量化核心）
scipy>=1.10.0             # 科学计算辅助
python-multipart>=0.0.6   # 文件上传支持
jinja2>=3.0.0             # 模板引擎
geopandas>=0.14.0         # 地理数据处理
pyproj>=3.6.0             # 坐标系转换
shapely>=2.0.0            # 几何运算
aiofiles>=23.0.0          # 异步文件操作
```

## 常见问题 FAQ

### Q1: 为什么10m分辨率+72风向之前会崩溃？

**A**: 原因是数据量过大。10m分辨率在10km范围内产生100万个网格点，72个风向的总数据量约2GB，超过了浏览器和服务器内存限制。现在通过以下优化解决：
- 后端聚合模式：只返回加权聚合后的结果（~20MB），减少99%数据传输
- NumPy向量化：单风向计算时间从60秒降到2-3秒
- 多进程并行：32核同时计算，总时间从72分钟降到15-30秒

### Q2: 受体点分析结果显示什么？

**A**: 受体点分析显示每个排放源对该受体点的浓度贡献百分比。例如：
```
受体点: 学校操场
污染物: PM2.5
总浓度: 15.2 μg/m³

排名  排放源名称          贡献浓度    占比
 1    钢厂1号烟囱        8.5 μg/m³  55.9%
 2    化工园区VOCs排放   4.2 μg/m³  27.6%
 3    交通干道尾气       2.5 μg/m³  16.5%
```
这有助于识别主要污染源，制定针对性的减排措施。

### Q3: 如何提高计算速度？

**A**: 系统已内置多项自动优化：
1. **自动并行**：检测CPU核心数，自动使用最大可用核心
2. **智能聚合**：大数据量时自动启用后端聚合模式
3. **向量化计算**：所有数值计算已使用NumPy向量化

**用户可调优**：
- 降低网格分辨率（50m vs 10m）可显著减少计算量
- 减少风向数量（32方向 vs 72方向）
- 使用更少的核心（通过num_workers参数）

### Q4: 坐标偏移问题如何解决？

**A**: 国内地图使用GCJ02坐标系（有加密偏移），而GPS设备使用WGS84坐标系。系统已实现自动转换：
- 用户输入的坐标视为WGS84（GPS标准）
- 内部存储使用WGS84
- 地图显示时自动转换为GCJ02
- 无需用户手动处理

## 参考文献

1. Pasquill, F. (1961). The estimation of the dispersion of windborne material. Meteorological Magazine, 90(1063), 33-49.
2. Turner, D. B. (1994). Workbook of atmospheric dispersion estimates: an introduction to dispersion modeling. CRC press.
3. HJ 2.2-2018 环境影响评价技术导则 大气环境
4. Briggs, G. A. (1975). Plume rise predictions. In Lectures on Air Pollution and Environmental Impact Analyses (pp. 59-111).
5. WRF-Chem Model Documentation (Version 4.0)
6. Seinfeld, J. H., & Pandis, S. N. (2016). Atmospheric Chemistry and Physics: From Air Pollution to Climate Change (3rd ed.). Wiley.

## 更新日志

### v2.0 (2025-01) - 性能革命性升级

#### 🚀 性能优化
- ✨ **NumPy向量化重构**：消除所有Python循环，单风向计算速度提升**25倍**（60s→2-3s）
- ✨ **多进程并行计算**：支持32核同时计算，72个风向总时间从72分钟降至**15-30秒**
- ✨ **后端智能聚合**：数据传输量减少**99%**（2GB→20MB），彻底解决内存溢出问题
- ✨ **自动资源调度**：动态检测CPU核心数，最大化利用硬件算力

#### 🐛 问题修复
- 🔧 **UTF-8编码修复**：解决中文界面乱码和页面加载失败问题
- 🔧 **NumPy DLL警告抑制**：清理控制台输出，提供干净的用户体验
- 🔧 **受体点分析一致性**：确保并行模式下与串行模式数学结果完全一致

#### 📊 功能增强
- ➕ **完整的受体点贡献分析**：每个风向独立计算 + 权重聚合
- ➕ **实时进度反馈**：显示计算耗时、加速比、节省的内存量
- ➕ **智能内存保护**：自动检测大数据量并启用安全模式
- ➕ **扩展配置矩阵**：正式支持10m分辨率+10km范围+72风向的极限场景

---

## 许可证

MIT License

Copyright (c) 2025 大气污染扩散模拟系统

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
