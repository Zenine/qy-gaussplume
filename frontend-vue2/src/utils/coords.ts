// WGS84 ↔ GCJ02（国测局加密偏移）坐标转换
// 算法出处：GCJ02 公开逆向算法；范围判断避开中国大陆以外
//
// 用法：
//   const [lat, lon] = wgs84ToGcj02(39.9, 116.4)   // GPS → 高德/腾讯地图
//   const [lat, lon] = gcj02ToWgs84(39.9, 116.4)   // 高德 → GPS

const PI = Math.PI
const A = 6378245.0 // 椭球半长轴
const EE = 0.00669342162296594323 // 第一偏心率平方

export function isOutsideChina(lat: number, lon: number): boolean {
  return lon < 72.004 || lon > 137.8347 || lat < 0.8293 || lat > 55.8271
}

function transformLat(x: number, y: number): number {
  let ret = -100.0 + 2.0 * x + 3.0 * y + 0.2 * y * y + 0.1 * x * y + 0.2 * Math.sqrt(Math.abs(x))
  ret += ((20.0 * Math.sin(6.0 * x * PI) + 20.0 * Math.sin(2.0 * x * PI)) * 2.0) / 3.0
  ret += ((20.0 * Math.sin(y * PI) + 40.0 * Math.sin((y / 3.0) * PI)) * 2.0) / 3.0
  ret += ((160.0 * Math.sin((y / 12.0) * PI) + 320.0 * Math.sin((y * PI) / 30.0)) * 2.0) / 3.0
  return ret
}

function transformLon(x: number, y: number): number {
  let ret = 300.0 + x + 2.0 * y + 0.1 * x * x + 0.1 * x * y + 0.1 * Math.sqrt(Math.abs(x))
  ret += ((20.0 * Math.sin(6.0 * x * PI) + 20.0 * Math.sin(2.0 * x * PI)) * 2.0) / 3.0
  ret += ((20.0 * Math.sin(x * PI) + 40.0 * Math.sin((x / 3.0) * PI)) * 2.0) / 3.0
  ret += ((150.0 * Math.sin((x / 12.0) * PI) + 300.0 * Math.sin((x / 30.0) * PI)) * 2.0) / 3.0
  return ret
}

function delta(lat: number, lon: number): [number, number] {
  let dLat = transformLat(lon - 105.0, lat - 35.0)
  let dLon = transformLon(lon - 105.0, lat - 35.0)
  const radLat = (lat / 180.0) * PI
  let magic = Math.sin(radLat)
  magic = 1 - EE * magic * magic
  const sqrtMagic = Math.sqrt(magic)
  dLat = (dLat * 180.0) / (((A * (1 - EE)) / (magic * sqrtMagic)) * PI)
  dLon = (dLon * 180.0) / ((A / sqrtMagic) * Math.cos(radLat) * PI)
  return [dLat, dLon]
}

// WGS84 (GPS) → GCJ02 (高德/腾讯/Google 中国)
export function wgs84ToGcj02(lat: number, lon: number): [number, number] {
  if (isOutsideChina(lat, lon)) return [lat, lon]
  const [dLat, dLon] = delta(lat, lon)
  return [lat + dLat, lon + dLon]
}

// GCJ02 → WGS84（迭代收敛逆解）
export function gcj02ToWgs84(lat: number, lon: number): [number, number] {
  if (isOutsideChina(lat, lon)) return [lat, lon]
  // 粗略反算：正向偏移一次然后减
  let [wgsLat, wgsLon] = [lat, lon]
  // 迭代 2 次误差可控到亚米级
  for (let i = 0; i < 2; i++) {
    const [dLat, dLon] = delta(wgsLat, wgsLon)
    wgsLat = lat - dLat
    wgsLon = lon - dLon
  }
  return [wgsLat, wgsLon]
}
