import { mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { EmissionSource } from '@/types'
import MapPanel from '@/components/MapPanel.vue'

const leaflet = vi.hoisted(() => {
  const layer = () => ({
    addTo: vi.fn().mockReturnThis(),
    bindPopup: vi.fn().mockReturnThis(),
    remove: vi.fn(),
  })
  const mapInstance = {
    on: vi.fn(),
    off: vi.fn(),
    remove: vi.fn(),
    getCenter: vi.fn(() => ({ lat: 39.9, lng: 116.4 })),
    getZoom: vi.fn(() => 10),
    dragging: { enable: vi.fn(), disable: vi.fn() },
  }
  return {
    map: vi.fn(() => mapInstance),
    tileLayer: vi.fn(() => layer()),
    polyline: vi.fn(() => layer()),
    polygon: vi.fn(() => layer()),
    marker: vi.fn(() => layer()),
    divIcon: vi.fn((options) => options),
  }
})

vi.mock('leaflet', () => ({ default: leaflet }))

function makeLineSource(): EmissionSource {
  return {
    id: 1,
    name: '测试线源',
    sourceType: 'line',
    latitude: 39.9,
    longitude: 116.4,
    height: 0,
    temperature: null,
    velocity: null,
    diameter: null,
    areaShape: null,
    areaLength: null,
    areaWidth: null,
    areaHeight: null,
    areaTemperature: null,
    sigmaZ0Area: null,
    lineType: 'straight',
    startLon: 116.39,
    startLat: 39.9,
    endLon: 116.41,
    endLat: 39.9,
    lineWidth: 6,
    lineHeight: 0,
    lineTemperature: null,
    sigmaZ0Line: null,
    lineSegmentLength: 10,
    markerSymbol: 'circle',
    markerColor: '#ef4444',
    isActive: true,
    pollutants: [],
    createdAt: '',
    updatedAt: '',
  }
}

describe('MapPanel 线源图形', () => {
  beforeEach(() => vi.clearAllMocks())

  it('线源只渲染一条连续圆角线带，不创建点源式端点标记', () => {
    const wrapper = mount(MapPanel, {
      props: {
        sources: [makeLineSource()],
        receptors: [],
        initialCenter: [39.9, 116.4],
      },
    })

    expect(leaflet.polyline).toHaveBeenCalledTimes(1)
    expect(leaflet.polyline.mock.calls[0][1]).toMatchObject({
      weight: 6,
      lineCap: 'round',
      lineJoin: 'round',
    })
    expect(leaflet.marker).not.toHaveBeenCalled()

    wrapper.unmount()
  })
})
