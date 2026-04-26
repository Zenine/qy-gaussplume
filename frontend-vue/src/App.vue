<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useAppStore } from '@/stores/app'

const route = useRoute()
const app = useAppStore()

const menuItems = [
  { path: '/dashboard', title: '主控台', icon: 'DataBoard' },
  { path: '/sources', title: '排放源', icon: 'Factory' },
  { path: '/receptors', title: '受体点', icon: 'Location' },
  { path: '/meteorology', title: '气象场', icon: 'Sunny' },
]

const activeMenu = computed(() => route.path)
</script>

<template>
  <el-container class="app-layout">
    <el-aside :width="app.sidebarCollapsed ? '64px' : '220px'">
      <div class="logo">{{ app.sidebarCollapsed ? 'QY' : 'QY-GaussPlume' }}</div>
      <el-menu
        :default-active="activeMenu"
        :collapse="app.sidebarCollapsed"
        :router="true"
        background-color="#001529"
        text-color="#fff"
        active-text-color="#409EFF"
      >
        <el-menu-item v-for="m in menuItems" :key="m.path" :index="m.path">
          <span>{{ m.title }}</span>
        </el-menu-item>
      </el-menu>
    </el-aside>

    <el-container>
      <el-header class="header">
        <el-button link @click="app.toggleSidebar">
          {{ app.sidebarCollapsed ? '展开' : '收起' }}
        </el-button>
        <span class="title">{{ (route.meta?.title as string) ?? '' }}</span>
        <span class="spacer" />
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
.logo {
  height: 60px;
  line-height: 60px;
  text-align: center;
  color: #fff;
  background: #001529;
  font-weight: 600;
  letter-spacing: 1px;
}
.el-aside {
  background: #001529;
  overflow: hidden;
  transition: width 0.2s;
}
.header {
  display: flex;
  align-items: center;
  gap: 16px;
  border-bottom: 1px solid #e5e7eb;
  background: #fff;
}
.header .title {
  font-size: 16px;
  font-weight: 600;
}
.spacer {
  flex: 1;
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
