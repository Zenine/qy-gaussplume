"""
生成 Python 版高斯烟羽模型的黄金数值，供 C# 测试对齐使用。

用法：
    cd /Users/zeninexu/github/gnn
    python3 backend-dotnet/GnnSimulation.Tests/Data/golden/generate_golden.py

会在同目录下生成 golden_values.json。
"""
import json
import os
import sys

# 让 Python 能导入 backend 包
repo_root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
sys.path.insert(0, repo_root)

import numpy as np
from backend.core.gaussian_plume import GaussianPlumeModel, StabilityClassifier


def make_default_model(**overrides):
    defaults = dict(
        wind_speed=3.0,
        wind_direction=0.0,       # 北风（风来自北方，吹向南方）
        stability_class="D",
        temperature=293.15,
        boundary_layer_height=1000.0,
        humidity=50.0,
        cloud_cover=0.0,
        precipitation=0.0,
    )
    defaults.update(overrides)
    return GaussianPlumeModel(**defaults)


results = {}

# ---------- 1. 扩散参数 sigma ----------
sigma_cases = []
for stab in "ABCDEF":
    for dist in [100.0, 500.0, 1000.0, 5000.0]:
        m = make_default_model(stability_class=stab)
        sy, sz = m.calculate_sigma(dist)
        sigma_cases.append(dict(stability=stab, distance=dist, sigma_y=sy, sigma_z=sz))
results["sigma"] = sigma_cases

# ---------- 2. 干沉降速度 ----------
dry_cases = []
for pol in ["PM2.5", "PM10", "TSP", "VOCs", "NOx", "O3"]:
    m = make_default_model()
    dry_cases.append(dict(pollutant=pol, vd=m.calculate_dry_deposition_velocity(pol)))
# 再带上不同稳定度、风速、湿度
for stab, ws, hum in [("A", 2.0, 30), ("F", 5.0, 80)]:
    m = make_default_model(stability_class=stab, wind_speed=ws, humidity=hum)
    dry_cases.append(dict(pollutant="PM2.5", stability=stab, wind_speed=ws,
                          humidity=hum, vd=m.calculate_dry_deposition_velocity("PM2.5")))
results["dry_deposition"] = dry_cases

# ---------- 3. 湿沉降 ----------
wet_cases = []
for pol in ["PM2.5", "NOx", "VOCs"]:
    for prec in [0.0, 1.0, 5.0]:
        m = make_default_model(precipitation=prec, cloud_cover=3.0)
        wet_cases.append(dict(pollutant=pol, precipitation=prec, cloud_cover=3.0,
                              lambda_=m.calculate_wet_scavenging_coefficient(pol)))
results["wet_scavenging"] = wet_cases

# ---------- 4. 衰减系数 ----------
decay_cases = []
for dist in [100.0, 1000.0, 5000.0]:
    m = make_default_model()
    decay_cases.append(dict(distance=dist,
                            deposition=m.calculate_deposition_coefficient(dist, "PM2.5"),
                            chemical=m.calculate_chemical_decay(dist, "PM2.5"),
                            total=m.calculate_total_decay(dist, "PM2.5")))
results["decay"] = decay_cases

# ---------- 5. 有效烟囱高度（Briggs） ----------
eh_cases = []
m = make_default_model()
for (h, Q, T, v, d) in [
    (50, 1.0, 400, 15, 2),
    (80, 2.0, 450, 20, 3),
    (30, 0.5, 350, 10, 1.5),
]:
    eh_cases.append(dict(stack_height=h, emission_rate=Q, stack_temp=T,
                         velocity=v, diameter=d,
                         effective=m.calculate_effective_height(h, Q, T, v, d)))
results["effective_height"] = eh_cases

# ---------- 6. 最大扩散距离 ----------
maxd = []
for stab in "ABCDEF":
    m = make_default_model(stability_class=stab)
    maxd.append(dict(stability=stab, max_distance=m.calculate_max_diffusion_distance()))
results["max_distance"] = maxd

# ---------- 7. 单点浓度 calculate_concentration ----------
pt_cases = []
m = make_default_model()
for (x, y, z, H, Q) in [
    (500, 0, 0, 50, 1.0),
    (1000, 100, 10, 50, 1.0),
    (2000, -50, 5, 80, 2.0),
    (100, 0, 0, 30, 0.5),
]:
    pt_cases.append(dict(x=x, y=y, z=z, h=H, Q=Q,
                         concentration=m.calculate_concentration(x, y, z, H, Q)))
results["point_concentration"] = pt_cases

# ---------- 8. 受体点浓度 ----------
rc_cases = []
m = make_default_model(wind_speed=3.0, wind_direction=0.0)  # 北风 → 源南侧受影响
m_sdir = make_default_model(wind_speed=3.0, wind_direction=270.0) # 西风 → 源东侧受影响
stack_T, stack_v, stack_d = 400.0, 15.0, 2.0
for model, lbl, (src_lat, src_lon, h, Q), (rec_lat, rec_lon, rec_h) in [
    (m, "north-wind", (39.9, 116.4, 50, 1.0), (39.89, 116.4, 1.5)),
    (m, "far-downwind", (39.9, 116.4, 50, 1.0), (39.85, 116.4, 1.5)),
    (m_sdir, "west-wind", (39.9, 116.4, 50, 1.0), (39.9, 116.45, 1.5)),
]:
    rc_cases.append(dict(label=lbl, src_lat=src_lat, src_lon=src_lon, src_h=h, Q=Q,
                         rec_lat=rec_lat, rec_lon=rec_lon, rec_h=rec_h,
                         wind_direction=model.wind_direction,
                         stack_temperature=stack_T, velocity=stack_v, diameter=stack_d,
                         concentration=model.calculate_receptor_concentration(
                             src_lat, src_lon, h, Q,
                             rec_lat, rec_lon, rec_h,
                             temperature=stack_T, velocity=stack_v, diameter=stack_d)))
results["receptor_concentration"] = rc_cases

# ---------- 9. 浓度场（少数采样点） ----------
m = make_default_model(wind_direction=0.0)
grid_lat = np.linspace(39.85, 39.95, 11)  # 11 行
grid_lon = np.linspace(116.35, 116.45, 11) # 11 列
field = m.calculate_concentration_field(
    source_lat=39.9, source_lon=116.4,
    source_height=50, emission_rate=1.0,
    grid_lat=grid_lat, grid_lon=grid_lon,
    temperature=400.0, velocity=15.0, diameter=2.0,
    receptor_height=0.0)
field_samples = []
for (i, j) in [(0, 5), (3, 5), (5, 5), (7, 5), (10, 5), (5, 0), (5, 10)]:
    field_samples.append(dict(i=i, j=j, lat=float(grid_lat[i]), lon=float(grid_lon[j]),
                              concentration=float(field[i, j])))
results["concentration_field_samples"] = dict(
    grid_lat=grid_lat.tolist(),
    grid_lon=grid_lon.tolist(),
    samples=field_samples
)

# ---------- 10. 面源浓度场 ----------
m = make_default_model(wind_direction=0.0)
field_area = m.calculate_area_source_concentration_field(
    center_lat=39.9, center_lon=116.4,
    area_length=200, area_width=100, area_height=10,
    emission_rate=2.0,
    grid_lat=grid_lat, grid_lon=grid_lon,
    receptor_height=0.0)
area_samples = []
for (i, j) in [(0, 5), (3, 5), (5, 5), (7, 5), (10, 5)]:
    area_samples.append(dict(i=i, j=j, concentration=float(field_area[i, j])))
results["area_source_field_samples"] = dict(
    area=dict(center_lat=39.9, center_lon=116.4, length=200, width=100, height=10, Q=2.0),
    grid_lat=grid_lat.tolist(), grid_lon=grid_lon.tolist(),
    samples=area_samples)

# ---------- 11. 等效面源反算 ----------
m = make_default_model()
eq_rate = m.calculate_equivalent_emission_rate(100.0, 200.0, 100.0, 10.0)
results["equivalent_emission_rate"] = dict(concentration=100.0, length=200, width=100,
                                           height=10, rate=eq_rate)

# ---------- 12. 反推排放速率 ----------
m = make_default_model()
conc = m.calculate_concentration(x=1000, y=50, z=1.5, source_height=50, emission_rate=1.0)
# 反推时不带衰减的路径难以对齐：Python 反推不应用衰减，forward 带衰减 → 输入未衰减浓度
# 为保证对称性，我们用 forward（不带衰减）的值反推：这里手工构造无衰减浓度
x, y, z, H, Q = 1000.0, 50.0, 1.5, 50.0, 1.0
sy, sz = m.calculate_sigma(x)
Qug = Q * 1e6
t1 = Qug / (2 * np.pi * m.wind_speed * sy * sz)
t2 = np.exp(-y**2/(2*sy**2))
t3 = np.exp(-(z-H)**2/(2*sz**2)) + np.exp(-(z+H)**2/(2*sz**2))
C_no_decay = t1 * t2 * t3
Q_back = m.calculate_emission_rate_from_concentration(x, y, z, H, C_no_decay)
results["reverse_emission"] = dict(x=x, y=y, z=z, source_height=H,
                                   forward_no_decay=float(C_no_decay),
                                   Q_expected=Q, Q_reversed=Q_back)

# ---------- 13. 稳定度分类 ----------
sc = []
for (ws, sol, cc, day) in [
    (1.5, 800, None, True),   # 晴朗日，低风 → A
    (4.0, 400, None, True),   # 日间中等辐照 → B-C
    (6.5, 500, None, True),   # 日间高风 → D
    (2.0, None, 0.1, False),  # 夜间晴朗 → F
    (2.0, None, 0.8, False),  # 夜间多云 → D
]:
    sc.append(dict(wind_speed=ws, solar=sol, cloud=cc, daytime=day,
                   result=StabilityClassifier.classify(ws, sol, cc, day)))
results["stability_classifier"] = sc

# ---------- 14. 无/高湿/高温化学衰减 ----------
chem = []
for (pol, hum, temp, cc) in [
    ("PM2.5", 50, 293.15, 0.0),
    ("VOCs", 80, 308.15, 5.0),  # VOCs 是增强集合
    ("NOx", 30, 280, 2.0),
]:
    mm = make_default_model(humidity=hum, temperature=temp, cloud_cover=cc)
    chem.append(dict(pollutant=pol, humidity=hum, temperature=temp, cloud_cover=cc,
                     distance=1000, decay=mm.calculate_chemical_decay(1000, pol)))
results["chemical_decay_extras"] = chem

out = os.path.join(os.path.dirname(__file__), "golden_values.json")
with open(out, "w", encoding="utf-8") as f:
    json.dump(results, f, indent=2, ensure_ascii=False)
print(f"Wrote {out}")
print(f"Total scenarios: {sum(len(v) if isinstance(v, list) else 1 for v in results.values())}")
