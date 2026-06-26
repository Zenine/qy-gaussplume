<template>
  <el-container class="app-layout">
    <el-aside class="app-sidebar" :width="sidebarCollapsed ? '72px' : '248px'">
      <div class="brand" :class="{ collapsed: sidebarCollapsed }">
        <div class="brand-mark">GNN</div>
        <div v-if="!sidebarCollapsed" class="brand-copy">
          <strong>长三院源贡献计算模拟平台</strong>
          <span>源贡献计算模拟</span>
        </div>
      </div>
      <el-menu class="main-menu" :default-active="$route.path" :collapse="sidebarCollapsed" router background-color="transparent" text-color="#d9f2ef" active-text-color="#fff">
        <el-menu-item index="/dashboard"><i class="el-icon-data-analysis nav-icon" /><span slot="title">主控台</span></el-menu-item>
        <el-menu-item index="/sources"><i class="el-icon-office-building nav-icon" /><span slot="title">排放源</span></el-menu-item>
        <el-menu-item index="/receptors"><i class="el-icon-location-outline nav-icon" /><span slot="title">受体点</span></el-menu-item>
        <el-menu-item index="/meteorology"><i class="el-icon-sunny nav-icon" /><span slot="title">气象场</span></el-menu-item>
      </el-menu>
      <div v-if="!sidebarCollapsed" class="sidebar-footer"><span class="status-dot" /><span>匿名演示数据</span></div>
    </el-aside>

    <el-container>
      <el-header class="header">
        <el-button class="sidebar-toggle" circle :aria-label="sidebarCollapsed ? '展开侧边栏' : '收起侧边栏'" @click="$store.commit('toggleSidebar')">
          <i :class="sidebarCollapsed ? 'el-icon-s-unfold' : 'el-icon-s-fold'" />
        </el-button>
        <div class="page-heading">
          <span class="workspace-kicker">长三院源贡献计算模拟平台</span>
          <span class="title">{{ routeTitle }}</span>
        </div>
        <!-- Vue2 没有内置 Teleport，这里用普通 DOM 容器；DashboardView 挂载后把顶部工具条插入此处。 -->
        <div id="dashboard-header-actions" class="header-actions" />
        <span class="spacer" />
        <div class="header-badge"><span class="status-dot" /><span>本地运行</span></div>
      </el-header>
      <el-main><router-view /></el-main>
    </el-container>
  </el-container>
</template>

<script lang="ts">
import Vue from 'vue'

export default Vue.extend({
  name: 'App',
  computed: {
    sidebarCollapsed(): boolean {
      return this.$store.state.sidebarCollapsed
    },
    routeTitle(): string {
      return (this.$route.meta && (this.$route.meta.title as string)) || ''
    },
  },
})
</script>
