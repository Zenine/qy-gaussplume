<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useAppStore } from '@/stores/app'
import {
  DataBoard,
  Expand,
  Fold,
  Location,
  OfficeBuilding,
  Sunny,
} from '@element-plus/icons-vue'

const route = useRoute()
const app = useAppStore()

const menuItems = [
  { path: '/dashboard', title: '主控台', icon: DataBoard },
  { path: '/sources', title: '排放源', icon: OfficeBuilding },
  { path: '/receptors', title: '受体点', icon: Location },
  { path: '/meteorology', title: '气象场', icon: Sunny },
]

const activeMenu = computed(() => route.path)
const routeTitle = computed(() => (route.meta?.title as string) ?? '')
const sidebarToggleLabel = computed(() => (app.sidebarCollapsed ? '展开侧边栏' : '收起侧边栏'))
const sidebarToggleIcon = computed(() => (app.sidebarCollapsed ? Expand : Fold))
</script>

<template>
  <el-container class="app-layout">
    <el-aside class="app-sidebar" :width="app.sidebarCollapsed ? '72px' : '248px'">
      <div class="brand" :class="{ collapsed: app.sidebarCollapsed }">
        <div class="brand-mark">QY</div>
        <div v-if="!app.sidebarCollapsed" class="brand-copy">
          <strong>QY-GaussPlume</strong>
          <span>清源扩散模拟</span>
        </div>
      </div>
      <el-menu
        class="main-menu"
        :default-active="activeMenu"
        :collapse="app.sidebarCollapsed"
        :router="true"
        background-color="transparent"
        text-color="#d9f2ef"
        active-text-color="#ffffff"
      >
        <el-menu-item v-for="m in menuItems" :key="m.path" :index="m.path">
          <el-icon class="nav-icon"><component :is="m.icon" /></el-icon>
          <template #title>{{ m.title }}</template>
        </el-menu-item>
      </el-menu>
      <div v-if="!app.sidebarCollapsed" class="sidebar-footer">
        <span class="status-dot" />
        <span>匿名演示数据</span>
      </div>
    </el-aside>

    <el-container>
      <el-header class="header">
        <el-button
          class="sidebar-toggle"
          circle
          :aria-label="sidebarToggleLabel"
          @click="app.toggleSidebar"
        >
          <el-icon><component :is="sidebarToggleIcon" /></el-icon>
        </el-button>
        <div class="page-heading">
          <span class="workspace-kicker">科研扩散模拟平台</span>
          <span class="title">{{ routeTitle }}</span>
        </div>
        <span class="spacer" />
        <div class="header-badge">
          <span class="status-dot" />
          <span>本地运行</span>
        </div>
      </el-header>
      <el-main>
        <router-view v-slot="{ Component }">
          <transition name="fade" mode="out-in">
            <component :is="Component" />
          </transition>
        </router-view>
      </el-main>
    </el-container>
  </el-container>
</template>

<style scoped>
.app-layout {
  height: 100vh;
}
.app-sidebar {
  display: flex;
  flex-direction: column;
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.08), rgba(255, 255, 255, 0)),
    #113236;
  overflow: hidden;
  transition: width 0.2s;
  border-right: 1px solid rgba(255, 255, 255, 0.1);
}
.brand {
  display: flex;
  align-items: center;
  gap: 12px;
  height: 72px;
  padding: 0 18px;
  color: #fff;
}
.brand.collapsed {
  justify-content: center;
  padding: 0;
}
.brand-mark {
  display: grid;
  place-items: center;
  width: 38px;
  height: 38px;
  flex: 0 0 38px;
  border-radius: 8px;
  background: linear-gradient(135deg, #19c2a8, #2e90fa);
  color: #fff;
  font-weight: 800;
  letter-spacing: 0;
  box-shadow: 0 12px 26px rgba(5, 150, 105, 0.28);
}
.brand-copy {
  display: flex;
  min-width: 0;
  flex-direction: column;
  gap: 3px;
}
.brand-copy strong {
  font-size: 15px;
  line-height: 1.2;
  white-space: nowrap;
}
.brand-copy span {
  color: #a7d9d2;
  font-size: 12px;
}
.main-menu {
  flex: 1;
  border-right: 0;
  padding: 8px 10px;
}
.main-menu :deep(.el-menu-item) {
  height: 44px;
  margin: 4px 0;
  border-radius: 8px;
  color: #d9f2ef;
}
.main-menu:not(.el-menu--collapse) :deep(.el-menu-item) {
  padding-left: 14px !important;
}
.main-menu :deep(.el-menu-item:hover) {
  background: rgba(255, 255, 255, 0.08);
}
.main-menu :deep(.el-menu-item.is-active) {
  background: rgba(25, 194, 168, 0.2);
  box-shadow: inset 3px 0 0 #19c2a8;
}
.nav-icon {
  color: inherit;
}
.sidebar-footer {
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 0 16px 18px;
  padding: 10px 12px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  color: #c7e7e2;
  font-size: 12px;
  background: rgba(255, 255, 255, 0.06);
}
.header {
  display: flex;
  align-items: center;
  gap: 14px;
  height: 64px;
  border-bottom: 1px solid #dfe7ee;
  background: rgba(255, 255, 255, 0.92);
  backdrop-filter: blur(10px);
}
.sidebar-toggle {
  border-color: #d6e1e8;
}
.page-heading {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.workspace-kicker {
  color: #64748b;
  font-size: 12px;
  line-height: 1;
}
.header .title {
  color: #102a43;
  font-size: 16px;
  font-weight: 700;
}
.spacer {
  flex: 1;
}
.header-badge {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  height: 30px;
  padding: 0 10px;
  border: 1px solid #d9e7e4;
  border-radius: 999px;
  color: #31545c;
  background: #f4fbf9;
  font-size: 12px;
}
.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 999px;
  background: #16a34a;
  box-shadow: 0 0 0 3px rgba(22, 163, 74, 0.14);
}
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.15s;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
