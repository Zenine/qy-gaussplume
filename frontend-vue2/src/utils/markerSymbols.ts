import type { MarkerSymbolInfo } from '@/types'

export const fallbackMarkerSymbols: MarkerSymbolInfo[] = [
  { symbol: 'factory', name: '工厂', icon: '🏭' },
  { symbol: 'monitor', name: '监测点', icon: '📍' },
  { symbol: 'industry', name: '工业', icon: '⚙️' },
  { symbol: 'power', name: '电厂', icon: '⚡' },
  { symbol: 'chemical', name: '化工厂', icon: '🧪' },
  { symbol: 'circle', name: '圆形', icon: '●' },
  { symbol: 'square', name: '方形', icon: '■' },
  { symbol: 'triangle', name: '三角形', icon: '▲' },
  { symbol: 'diamond', name: '菱形', icon: '◆' },
  { symbol: 'star', name: '星形', icon: '★' },
  { symbol: 'hexagon', name: '六边形', icon: '⬡' },
  { symbol: 'pentagon', name: '五边形', icon: '⬠' },
  { symbol: 'cross', name: '十字', icon: '✚' },
]

const fallbackBySymbol = new Map(fallbackMarkerSymbols.map((item) => [item.symbol, item]))

export function markerGlyph(symbol: string | null | undefined): string {
  return fallbackBySymbol.get(symbol || '')?.icon || '●'
}

export function markerLabel(symbol: string | null | undefined, catalog = fallbackMarkerSymbols): string {
  const item = catalog.find((entry) => entry.symbol === symbol) || fallbackBySymbol.get(symbol || '')
  return item ? `${item.icon} ${item.name}` : (symbol || '未设置')
}
