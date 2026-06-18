import { defineStore } from 'pinia'
import { ref, watch } from 'vue'

export const FIXED_REGIONS = [
  { key: 'nanhu', name: '南湖区' },
  { key: 'xiuzhou', name: '秀洲区' },
  { key: 'jiashan', name: '嘉善县' },
  { key: 'tongxiang', name: '桐乡市' },
] as const

export type RegionKey = (typeof FIXED_REGIONS)[number]['key']

const STORAGE_KEY = 'gnn.currentRegion.v1'
const DEFAULT_REGION: RegionKey = 'nanhu'

function loadRegion(): RegionKey {
  const raw = localStorage.getItem(STORAGE_KEY)
  return FIXED_REGIONS.some((r) => r.key === raw) ? raw as RegionKey : DEFAULT_REGION
}

export const useRegionStore = defineStore('region', () => {
  const currentRegionKey = ref<RegionKey>(loadRegion())

  watch(currentRegionKey, (value) => localStorage.setItem(STORAGE_KEY, value), { immediate: true })

  function setRegion(key: RegionKey) {
    currentRegionKey.value = key
  }

  return { regions: FIXED_REGIONS, currentRegionKey, setRegion }
})
