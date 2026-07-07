import { beforeAll, describe, expect, it, vi } from 'vitest'
import {
  computeAnchoredBounds,
  computeBounds,
  renderHeatmapToCanvas,
} from '@/composables/useHeatmapRenderer'
import { wgs84ToGcj02 } from '@/utils/coords'

let lastImageData: ImageData | null = null

// jsdom 不原生支持 Canvas 2D。用最小 stub 让逻辑跑通
beforeAll(() => {
  HTMLCanvasElement.prototype.getContext = vi.fn(() => ({
    createImageData: (w: number, h: number) => ({
      data: new Uint8ClampedArray(w * h * 4),
      width: w,
      height: h,
      colorSpace: 'srgb',
    }),
    putImageData: vi.fn((img: ImageData) => {
      lastImageData = img
    }),
  })) as unknown as HTMLCanvasElement['getContext']
})

describe('renderHeatmapToCanvas', () => {
  // jsdom 不支持 Canvas 2D context；用最小 stub 实现验证逻辑
  // 这里只测非零 pixel 不为空 transparent 的基本流程
  it('生成 Canvas 元素，尺寸=网格*renderScale', () => {
    const concentrations = [
      [0, 1, 2],
      [1, 5, 2],
      [0, 1, 0],
    ]
    const canvas = renderHeatmapToCanvas({
      concentrations,
      gridLat: [39.9, 39.91, 39.92],
      gridLon: [116.4, 116.41, 116.42],
      min: 1,
      max: 5,
      scale: 'jet',
      opacity: 0.7,
      renderScale: 2,
      useGcj02: false,
    })
    expect(canvas.tagName).toBe('CANVAS')
    expect(canvas.width).toBe(3 * 2)
    expect(canvas.height).toBe(3 * 2)
  })

  it('网格超 4096 自动降 renderScale', () => {
    const n = 2050
    const row = new Array(n).fill(0.5)
    const conc = new Array(n).fill(row)
    const gridLat = new Array(n).fill(0).map((_, i) => 39 + i * 1e-5)
    const gridLon = new Array(n).fill(0).map((_, i) => 116 + i * 1e-5)
    const canvas = renderHeatmapToCanvas({
      concentrations: conc,
      gridLat,
      gridLon,
      min: 0,
      max: 1,
      scale: 'jet',
      opacity: 1,
      renderScale: 4, // 4x * 2050 = 8200 → 会被降到 1x = 2050
      useGcj02: false,
    })
    expect(Math.max(canvas.width, canvas.height)).toBeLessThanOrEqual(4096)
  })

  it('按扩散羽流显示：低浓度透明，高浓度按色阶增强', () => {
    lastImageData = null
    renderHeatmapToCanvas({
      concentrations: [
        [1, 50, 100],
        [1, 50, 100],
      ],
      gridLat: [39.9, 39.91],
      gridLon: [116.4, 116.41, 116.42],
      min: 0,
      max: 100,
      scale: 'jet',
      opacity: 0.8,
      renderScale: 1,
      useGcj02: false,
    })

    expect(lastImageData).not.toBeNull()
    const data = lastImageData!.data
    const lowAlpha = data[3]
    const midAlpha = data[4 * 1 + 3]
    const highAlpha = data[4 * 2 + 3]

    expect(lowAlpha).toBe(0)
    expect(midAlpha).toBeGreaterThan(0)
    expect(highAlpha).toBeGreaterThan(midAlpha)
  })

  it('网格纬度升序时，最高纬度绘制在画布顶部，最低纬度绘制在底部', () => {
    lastImageData = null
    renderHeatmapToCanvas({
      concentrations: [
        [100, 0, 0],
        [0, 0, 0],
        [0, 0, 50],
      ],
      gridLat: [39.9, 39.91, 39.92],
      gridLon: [116.4, 116.41, 116.42],
      min: 0,
      max: 100,
      scale: 'jet',
      opacity: 1,
      renderScale: 1,
      useGcj02: false,
    })

    expect(lastImageData).not.toBeNull()
    const data = lastImageData!.data
    const width = lastImageData!.width
    const topRightAlpha = data[(0 * width + 2) * 4 + 3]
    const bottomLeftAlpha = data[(2 * width + 0) * 4 + 3]
    const topLeftAlpha = data[3]
    const bottomRightAlpha = data[(2 * width + 2) * 4 + 3]

    expect(topRightAlpha).toBeGreaterThan(0)
    expect(bottomLeftAlpha).toBeGreaterThan(topRightAlpha)
    expect(topLeftAlpha).toBe(0)
    expect(bottomRightAlpha).toBe(0)
  })

  it('连续低值模式显示所有正浓度格点', () => {
    lastImageData = null
    renderHeatmapToCanvas({
      concentrations: [
        [1, 50, 100],
        [1, 50, 100],
      ],
      gridLat: [39.9, 39.91],
      gridLon: [116.4, 116.41, 116.42],
      min: 0,
      max: 100,
      scale: 'jet',
      opacity: 0.8,
      renderScale: 1,
      useGcj02: false,
      displayMode: 'continuous',
    })

    expect(lastImageData).not.toBeNull()
    const data = lastImageData!.data
    const lowAlpha = data[3]
    const midAlpha = data[4 * 1 + 3]
    expect(lowAlpha).toBeGreaterThan(0)
    expect(midAlpha).toBeGreaterThan(lowAlpha)
  })

  it('不透明度允许超过 1 以增强中高浓度，但最终 alpha 安全封顶', () => {
    lastImageData = null
    renderHeatmapToCanvas({
      concentrations: [
        [50, 100],
        [50, 100],
      ],
      gridLat: [39.9, 39.91],
      gridLon: [116.4, 116.41],
      min: 0,
      max: 100,
      scale: 'jet',
      opacity: 1.2,
      renderScale: 1,
      useGcj02: false,
    })

    expect(lastImageData).not.toBeNull()
    const data = lastImageData!.data
    const midAlpha = data[3]
    const highAlpha = data[4 + 3]
    expect(midAlpha).toBeGreaterThan(180)
    expect(highAlpha).toBe(255)
  })
})

describe('computeBounds', () => {
  it('非 GCJ02 直接取网格范围', () => {
    const b = computeBounds([39.9, 39.95], [116.4, 116.5], false) as [
      [number, number],
      [number, number],
    ]
    expect(b[0][0]).toBe(39.9) // south
    expect(b[0][1]).toBe(116.4) // west
    expect(b[1][0]).toBe(39.95)
    expect(b[1][1]).toBe(116.5)
  })



  it('GCJ02 边界使用四角转换后的外包框，避免浓度图层相对高德底图偏移', () => {
    const gridLat = [31.1, 31.3]
    const gridLon = [120.2, 120.6]
    const b = computeBounds(gridLat, gridLon, true) as [
      [number, number],
      [number, number],
    ]
    const converted = [
      wgs84ToGcj02(31.1, 120.2),
      wgs84ToGcj02(31.1, 120.6),
      wgs84ToGcj02(31.3, 120.2),
      wgs84ToGcj02(31.3, 120.6),
    ]
    const lats = converted.map(([lat]) => lat)
    const lons = converted.map(([, lon]) => lon)

    expect(b[0][0]).toBe(Math.min(...lats))
    expect(b[0][1]).toBe(Math.min(...lons))
    expect(b[1][0]).toBe(Math.max(...lats))
    expect(b[1][1]).toBe(Math.max(...lons))
  })

  it('GCJ02 边界相对原始偏移（国内）', () => {
    const b = computeBounds([39.9, 39.95], [116.4, 116.5], true) as [
      [number, number],
      [number, number],
    ]
    expect(b[0][0]).not.toBe(39.9)
    expect(b[1][0]).not.toBe(39.95)
    // 但数量级应接近（不超过 0.02 度）
    expect(Math.abs(b[0][0] - 39.9)).toBeLessThan(0.02)
  })

  it('GCJ02 锚定边界保证污染源在热力图内的归一化位置与 marker 对齐', () => {
    const gridLat = [30.72, 30.78]
    const gridLon = [120.68, 120.82]
    const source: [number, number] = [30.75, 120.73]
    const b = computeAnchoredBounds(gridLat, gridLon, source, true) as [
      [number, number],
      [number, number],
    ]
    const [sourceLatGcj, sourceLonGcj] = wgs84ToGcj02(source[0], source[1])
    const latRatio = (source[0] - Math.min(...gridLat)) / (Math.max(...gridLat) - Math.min(...gridLat))
    const lonRatio = (source[1] - Math.min(...gridLon)) / (Math.max(...gridLon) - Math.min(...gridLon))
    const anchoredLat = b[0][0] + latRatio * (b[1][0] - b[0][0])
    const anchoredLon = b[0][1] + lonRatio * (b[1][1] - b[0][1])

    expect(anchoredLat).toBeCloseTo(sourceLatGcj, 12)
    expect(anchoredLon).toBeCloseTo(sourceLonGcj, 12)
  })
})
