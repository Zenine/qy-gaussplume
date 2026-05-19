import { describe, expect, it } from 'vitest'
import {
  concentrationRange,
  gradientColor,
  normalize,
} from '@/utils/colorScale'

describe('normalize', () => {
  it('线性映射到 [0,1]', () => {
    expect(normalize(0, 0, 10)).toBe(0)
    expect(normalize(5, 0, 10)).toBe(0.5)
    expect(normalize(10, 0, 10)).toBe(1)
  })
  it('越界被夹紧', () => {
    expect(normalize(-5, 0, 10)).toBe(0)
    expect(normalize(20, 0, 10)).toBe(1)
  })
  it('退化 max==min 返回 0', () => {
    expect(normalize(5, 5, 5)).toBe(0)
  })
  it('对数刻度', () => {
    const mid = normalize(10, 1, 100, true)
    expect(mid).toBeCloseTo(0.5, 1)
  })
})

describe('gradientColor', () => {
  it('Jet: t=0 起于深蓝', () => {
    const [r, g, b, a] = gradientColor(0, 'jet')
    expect(r).toBeLessThan(20)
    expect(g).toBeLessThan(20)
    expect(b).toBeGreaterThan(120) // 约 0.5 * 255
    expect(a).toBe(255)
  })
  it('Jet: t=1 终于深红', () => {
    const [r, g, b] = gradientColor(1, 'jet')
    expect(r).toBeGreaterThan(120)
    expect(g).toBeLessThan(20)
    expect(b).toBeLessThan(20)
  })
  it('grayscale: 灰度线性', () => {
    const [r, g, b] = gradientColor(0.5, 'grayscale')
    expect(r).toBe(g)
    expect(g).toBe(b)
    expect(r).toBeCloseTo(127.5, 0)
  })
})

describe('concentrationRange', () => {
  it('忽略 0 与负数，找正值范围', () => {
    const field = [
      [0, 1, 5],
      [0, 10, 0],
      [2, 0, 8],
    ]
    const { min, max } = concentrationRange(field)
    expect(min).toBe(1)
    expect(max).toBe(10)
  })
  it('全零返回 (0, 0)', () => {
    const { min, max } = concentrationRange([
      [0, 0],
      [0, 0],
    ])
    expect(min).toBe(0)
    expect(max).toBe(0)
  })
})
