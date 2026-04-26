import { beforeAll, describe, expect, it, vi } from 'vitest'
import {
  computeBounds,
  renderHeatmapToCanvas,
} from '@/composables/useHeatmapRenderer'

// jsdom 不原生支持 Canvas 2D。用最小 stub 让逻辑跑通
beforeAll(() => {
  HTMLCanvasElement.prototype.getContext = vi.fn(() => ({
    createImageData: (w: number, h: number) => ({
      data: new Uint8ClampedArray(w * h * 4),
      width: w,
      height: h,
      colorSpace: 'srgb',
    }),
    putImageData: vi.fn(),
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
})
