import { mount } from '@vue/test-utils'
import ElementPlus from 'element-plus'
import { createPinia } from 'pinia'
import { createRouter, createWebHistory } from 'vue-router'
import { describe, expect, it } from 'vitest'
import App from '@/App.vue'

function mountApp() {
  const router = createRouter({
    history: createWebHistory(),
    routes: [
      {
        path: '/',
        component: { template: '<div />' },
        meta: { title: '主控台' },
      },
    ],
  })

  return mount(App, {
    global: {
      plugins: [ElementPlus, createPinia(), router],
    },
  })
}

describe('App shell', () => {
  it('渲染带图标的应用导航和可访问的折叠按钮', () => {
    const wrapper = mountApp()

    expect(wrapper.find('.brand-mark').exists()).toBe(true)
    expect(wrapper.find('.workspace-kicker').text()).toBe('科研扩散模拟平台')
    expect(wrapper.findAll('.nav-icon')).toHaveLength(4)

    const collapseButton = wrapper.find('button[aria-label="收起侧边栏"]')
    expect(collapseButton.exists()).toBe(true)
    expect(collapseButton.find('.el-icon').exists()).toBe(true)
  })
})
