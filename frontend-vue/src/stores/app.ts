import { defineStore } from 'pinia'
import { ref } from 'vue'

// 全局 UI/配置状态（侧边栏折叠等）。运行时数据各页面自行用 composables 管理。
export const useAppStore = defineStore('app', () => {
  const sidebarCollapsed = ref(false)
  function toggleSidebar() {
    sidebarCollapsed.value = !sidebarCollapsed.value
  }
  return { sidebarCollapsed, toggleSidebar }
})
