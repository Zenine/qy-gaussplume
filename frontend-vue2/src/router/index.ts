import Vue from 'vue'
import Router from 'vue-router'
import DashboardView from '@/views/DashboardView.vue'
import SourcesView from '@/views/SourcesView.vue'
import ReceptorsView from '@/views/ReceptorsView.vue'
import MeteorologyView from '@/views/MeteorologyView.vue'

Vue.use(Router)

const router = new Router({
  mode: 'history',
  routes: [
    { path: '/', redirect: '/dashboard' },
    { path: '/dashboard', component: DashboardView, meta: { title: '主控台' } },
    { path: '/sources', component: SourcesView, meta: { title: '排放源' } },
    { path: '/receptors', component: ReceptorsView, meta: { title: '受体点' } },
    { path: '/meteorology', component: MeteorologyView, meta: { title: '气象场' } },
  ],
})

router.afterEach(() => {
  document.title = '长三院源贡献计算模拟平台'
})

export default router
