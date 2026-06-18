import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      redirect: '/dashboard',
    },
    {
      path: '/dashboard',
      name: 'dashboard',
      component: () => import('@/views/DashboardView.vue'),
      meta: { title: '主控台' },
    },
    {
      path: '/sources',
      name: 'sources',
      component: () => import('@/views/SourcesView.vue'),
      meta: { title: '排放源管理' },
    },
    {
      path: '/receptors',
      name: 'receptors',
      component: () => import('@/views/ReceptorsView.vue'),
      meta: { title: '受体点管理' },
    },
    {
      path: '/meteorology',
      name: 'meteorology',
      component: () => import('@/views/MeteorologyView.vue'),
      meta: { title: '气象场管理' },
    },
  ],
})

router.afterEach(() => {
  document.title = 'GNN-GaussPlume'
})

export default router
