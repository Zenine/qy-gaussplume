import { describe, expect, it } from 'vitest'
import { wgs84ToGcj02, gcj02ToWgs84, isOutsideChina } from '@/utils/coords'

describe('isOutsideChina', () => {
  it('北京在国内', () => {
    expect(isOutsideChina(39.9, 116.4)).toBe(false)
  })
  it('东京在国外', () => {
    expect(isOutsideChina(35.68, 139.69)).toBe(true)
  })
  it('南极洲在国外', () => {
    expect(isOutsideChina(-90, 0)).toBe(true)
  })
})

describe('wgs84ToGcj02', () => {
  it('国外坐标不做偏移', () => {
    const [lat, lon] = wgs84ToGcj02(35.68, 139.69)
    expect(lat).toBe(35.68)
    expect(lon).toBe(139.69)
  })

  it('北京偏移量应在合理范围（百米量级）', () => {
    // GCJ02 加密在中国大陆产生约 300-800m 的偏移
    const [lat, lon] = wgs84ToGcj02(39.9, 116.4)
    const dLat = Math.abs(lat - 39.9)
    const dLon = Math.abs(lon - 116.4)
    // 约 0.001~0.01 度 = 110~1100m
    expect(dLat).toBeGreaterThan(0.001)
    expect(dLat).toBeLessThan(0.02)
    expect(dLon).toBeGreaterThan(0.001)
    expect(dLon).toBeLessThan(0.02)
  })
})

describe('往返转换', () => {
  it('WGS84 → GCJ02 → WGS84 应在亚米级精度（~1e-5 度）', () => {
    const cases = [
      [39.9, 116.4],
      [31.2, 121.5], // 上海
      [23.1, 113.3], // 广州
      [45.8, 126.5], // 哈尔滨
    ]
    for (const [lat, lon] of cases) {
      const [gLat, gLon] = wgs84ToGcj02(lat, lon)
      const [back, back2] = gcj02ToWgs84(gLat, gLon)
      expect(Math.abs(back - lat)).toBeLessThan(1e-5)
      expect(Math.abs(back2 - lon)).toBeLessThan(1e-5)
    }
  })

  it('国外往返保持恒等', () => {
    const [lat, lon] = gcj02ToWgs84(35.68, 139.69)
    expect(lat).toBe(35.68)
    expect(lon).toBe(139.69)
  })
})
