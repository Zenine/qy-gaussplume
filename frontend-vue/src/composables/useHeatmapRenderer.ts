import L from 'leaflet'
import { gradientColor, normalize, type ColorScale } from '@/utils/colorScale'
import { wgs84ToGcj02 } from '@/utils/coords'

export interface HeatmapOptions {
  concentrations: number[][] // [row=lat][col=lon]
  gridLat: number[] // 原始 WGS84 纬度
  gridLon: number[] // 原始 WGS84 经度
  min: number
  max: number
  scale: ColorScale
  opacity: number
  renderScale: number // 渲染分辨率倍数（1=与网格相同，2=双倍像素）
  useGcj02: boolean // 国内瓦片需要 WGS84→GCJ02 偏移
}

// 把浓度场绘制到 Canvas，然后作为 L.ImageOverlay 贴到地图的 lat/lon 边界框。
// 通过 renderScale 提升像素密度（避免马赛克），同时克制峰值以保护浏览器内存。
export function renderHeatmapToCanvas(opts: HeatmapOptions): HTMLCanvasElement {
  const { concentrations, gridLat, gridLon, min, max, scale, opacity, renderScale } = opts

  const nLat = gridLat.length
  const nLon = gridLon.length

  // 防浏览器崩：Canvas 上限 4096x4096
  let rs = Math.max(1, Math.min(16, Math.floor(renderScale)))
  while (Math.max(nLat, nLon) * rs > 4096 && rs > 1) rs--

  const w = nLon * rs
  const h = nLat * rs
  const canvas = document.createElement('canvas')
  canvas.width = w
  canvas.height = h
  const ctx = canvas.getContext('2d')!
  const img = ctx.createImageData(w, h)

  // 双线性插值：对 Canvas 每个像素从网格采样
  for (let py = 0; py < h; py++) {
    // 注意：Canvas 的 y=0 在顶部，地理 y 在 Leaflet overlay 坐标体系下
    // 由 ImageOverlay 按 bounds [[south,west],[north,east]] 拉伸，所以这里
    // 让 py=0 对应最高纬度（北边），py=h-1 对应最低纬度（南边）。
    const fy = ((h - 1 - py) / (h - 1)) * (nLat - 1)
    const y0 = Math.floor(fy)
    const y1 = Math.min(nLat - 1, y0 + 1)
    const ty = fy - y0

    for (let px = 0; px < w; px++) {
      const fx = (px / (w - 1)) * (nLon - 1)
      const x0 = Math.floor(fx)
      const x1 = Math.min(nLon - 1, x0 + 1)
      const tx = fx - x0

      const v00 = concentrations[y0][x0]
      const v01 = concentrations[y0][x1]
      const v10 = concentrations[y1][x0]
      const v11 = concentrations[y1][x1]
      const v0 = v00 * (1 - tx) + v01 * tx
      const v1 = v10 * (1 - tx) + v11 * tx
      const v = v0 * (1 - ty) + v1 * ty

      const idx = (py * w + px) * 4
      if (v <= 0) {
        img.data[idx + 3] = 0 // 透明
        continue
      }
      const t = normalize(v, min, max)
      const [r, g, b] = gradientColor(t, scale)
      img.data[idx] = r
      img.data[idx + 1] = g
      img.data[idx + 2] = b
      img.data[idx + 3] = Math.round(255 * Math.max(0, Math.min(1, opacity)))
    }
  }

  ctx.putImageData(img, 0, 0)
  return canvas
}

// 为网格计算 Leaflet bounds（WGS84 → GCJ02 可选）
export function computeBounds(
  gridLat: number[],
  gridLon: number[],
  useGcj02: boolean,
): L.LatLngBoundsExpression {
  const south = Math.min(...gridLat)
  const north = Math.max(...gridLat)
  const west = Math.min(...gridLon)
  const east = Math.max(...gridLon)

  if (!useGcj02) {
    return [
      [south, west],
      [north, east],
    ]
  }
  const [s, w] = wgs84ToGcj02(south, west)
  const [n, e] = wgs84ToGcj02(north, east)
  return [
    [s, w],
    [n, e],
  ]
}
