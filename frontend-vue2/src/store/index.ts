import Vue from 'vue'
import Vuex from 'vuex'

Vue.use(Vuex)

type TileLayer = 'street' | 'satellite' | 'hybrid'
type ColorScale = 'jet' | 'blue' | 'red' | 'green' | 'purple' | 'thermal' | 'rainbow' | 'turbo' | 'spectral_r'

const prefKey = 'gnn.vue2.prefs.v1'

function loadPrefs() {
  try {
    return JSON.parse(localStorage.getItem(prefKey) || '{}')
  } catch {
    return {}
  }
}

const saved = loadPrefs()

const store = new Vuex.Store({
  state: {
    sidebarCollapsed: false,
    currentRegionKey: saved.currentRegionKey || 'nanhu',
    regions: [
      { key: 'nanhu', name: '南湖区' },
      { key: 'xiuzhou', name: '秀洲区' },
      { key: 'jiashan', name: '嘉善县' },
      { key: 'tongxiang', name: '桐乡市' },
    ],
    scale: (saved.scale || 'jet') as ColorScale,
    opacity: saved.opacity ?? 0.85,
    renderScale: saved.renderScale ?? 4,
    heatmapDisplayMode: saved.heatmapDisplayMode || 'plume',
    tileLayer: (saved.tileLayer || 'street') as TileLayer,
    selectedPollutant: saved.selectedPollutant || '',
    gridResolution: saved.gridResolution ?? 100,
    domainSize: saved.domainSize ?? 5000,
    simulationHeight: saved.simulationHeight ?? 0,
    mapCenter: saved.mapCenter || [30.75, 120.75],
    mapZoom: saved.mapZoom || 10,
  },
  mutations: {
    toggleSidebar(state) {
      state.sidebarCollapsed = !state.sidebarCollapsed
    },
    setPref(state, payload: { key: string; value: any }) {
      Vue.set(state as any, payload.key, payload.value)
      const persisted: Record<string, any> = {}
      for (const key of [
        'currentRegionKey',
        'scale',
        'opacity',
        'renderScale',
        'heatmapDisplayMode',
        'tileLayer',
        'selectedPollutant',
        'gridResolution',
        'domainSize',
        'simulationHeight',
        'mapCenter',
        'mapZoom',
      ]) persisted[key] = (state as any)[key]
      localStorage.setItem(prefKey, JSON.stringify(persisted))
    },
    resetPrefs(state) {
      state.scale = 'jet'
      state.opacity = 0.85
      state.renderScale = 4
      state.heatmapDisplayMode = 'plume'
      state.tileLayer = 'street'
      state.selectedPollutant = ''
      state.gridResolution = 100
      state.domainSize = 5000
      state.simulationHeight = 0
      state.mapCenter = [30.75, 120.75]
      state.mapZoom = 10
      localStorage.removeItem(prefKey)
    },
  },
})

export default store
