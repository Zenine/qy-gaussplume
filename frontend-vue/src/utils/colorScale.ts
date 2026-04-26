// 连续色阶：把 [0,1] 归一化浓度映射到 RGBA。
// 常用于高斯扩散浓度场的热力可视化。

export type RGBA = [number, number, number, number]

export type ColorScale = 'jet' | 'turbo' | 'viridis' | 'grayscale'

// 简化的 Jet 色阶（蓝→青→绿→黄→红）
function jet(t: number): RGBA {
  t = Math.max(0, Math.min(1, t))
  let r = 0,
    g = 0,
    b = 0
  if (t < 0.125) {
    r = 0; g = 0; b = 0.5 + 4 * t
  } else if (t < 0.375) {
    r = 0; g = 4 * (t - 0.125); b = 1
  } else if (t < 0.625) {
    r = 4 * (t - 0.375); g = 1; b = 1 - 4 * (t - 0.375)
  } else if (t < 0.875) {
    r = 1; g = 1 - 4 * (t - 0.625); b = 0
  } else {
    r = 1 - 4 * (t - 0.875); g = 0; b = 0
  }
  return [r * 255, g * 255, b * 255, 255]
}

// 简化的 Turbo（更明亮的现代色阶）
function turbo(t: number): RGBA {
  t = Math.max(0, Math.min(1, t))
  // 多项式拟合近似 turbo
  const r = 0.13572138 + t * (4.61539260 - t * (42.66032258 - t * (132.13108234 - t * (152.94239396 - t * 59.28637943))))
  const g = 0.09140261 + t * (2.19418839 + t * (4.84296658 - t * (14.18503333 - t * (4.27729857 + t * 2.82956604))))
  const b = 0.10667330 + t * (12.64194608 - t * (60.58204836 - t * (110.36276771 - t * (89.90310912 - t * 27.34824973))))
  return [
    Math.max(0, Math.min(255, r * 255)),
    Math.max(0, Math.min(255, g * 255)),
    Math.max(0, Math.min(255, b * 255)),
    255,
  ]
}

// Viridis（色盲友好）
function viridis(t: number): RGBA {
  t = Math.max(0, Math.min(1, t))
  const r = 0.267004 + t * (0.127568 + t * (0.229739 - t * 0.016214))
  const g = 0.004874 + t * (0.566949 + t * (0.485793 - t * 0.124207))
  const b = 0.329415 + t * (0.850535 - t * (1.040259 - t * 0.143040))
  return [
    Math.max(0, Math.min(255, r * 255)),
    Math.max(0, Math.min(255, g * 255)),
    Math.max(0, Math.min(255, b * 255)),
    255,
  ]
}

function grayscale(t: number): RGBA {
  const v = Math.max(0, Math.min(1, t)) * 255
  return [v, v, v, 255]
}

export function gradientColor(t: number, scale: ColorScale = 'jet'): RGBA {
  switch (scale) {
    case 'jet': return jet(t)
    case 'turbo': return turbo(t)
    case 'viridis': return viridis(t)
    case 'grayscale': return grayscale(t)
  }
}

// 线性归一化 [min, max] → [0, 1]，支持对数刻度
export function normalize(value: number, min: number, max: number, log = false): number {
  if (max <= min) return 0
  if (log) {
    const lmin = Math.log(Math.max(min, 1e-12))
    const lmax = Math.log(Math.max(max, 1e-12))
    const lv = Math.log(Math.max(value, 1e-12))
    return Math.max(0, Math.min(1, (lv - lmin) / (lmax - lmin)))
  }
  return Math.max(0, Math.min(1, (value - min) / (max - min)))
}

// 计算二维浓度场的最小/最大非零值
export function concentrationRange(field: number[][]): { min: number; max: number } {
  let min = Infinity
  let max = -Infinity
  for (const row of field) {
    for (const v of row) {
      if (v > 0) {
        if (v < min) min = v
        if (v > max) max = v
      }
    }
  }
  if (!Number.isFinite(min)) min = 0
  if (!Number.isFinite(max)) max = 0
  return { min, max }
}
