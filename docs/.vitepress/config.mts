import { defineConfig } from 'vitepress'

const siteUrl = 'https://zenine.github.io/qy-gaussplume'
const title = 'QY-GaussPlume'
const description = '面向科研与工程评估的大气污染物扩散模拟平台'

const jsonLd = {
  '@context': 'https://schema.org',
  '@type': 'SoftwareApplication',
  name: title,
  applicationCategory: 'ScienceApplication',
  operatingSystem: 'macOS, Linux, Windows',
  description,
  url: siteUrl,
  codeRepository: 'https://github.com/Zenine/qy-gaussplume',
  license: 'https://github.com/Zenine/qy-gaussplume/blob/main/LICENSE',
}

export default defineConfig({
  base: '/qy-gaussplume/',
  title,
  titleTemplate: ':title | QY-GaussPlume',
  description,

  head: [
    ['link', { rel: 'icon', href: '/qy-gaussplume/hero.svg', type: 'image/svg+xml' }],
    ['link', { rel: 'canonical', href: siteUrl }],
    ['link', { rel: 'alternate', type: 'text/plain', href: '/qy-gaussplume/llms.txt', title: 'llms.txt' }],
    ['meta', { property: 'og:type', content: 'website' }],
    ['meta', { property: 'og:title', content: title }],
    ['meta', { property: 'og:description', content: description }],
    ['meta', { property: 'og:url', content: siteUrl }],
    ['meta', { property: 'og:image', content: `${siteUrl}/og.png` }],
    ['meta', { name: 'twitter:card', content: 'summary_large_image' }],
    ['meta', { name: 'twitter:title', content: title }],
    ['meta', { name: 'twitter:description', content: description }],
    ['meta', { name: 'twitter:image', content: `${siteUrl}/og.png` }],
    ['script', { type: 'application/ld+json' }, JSON.stringify(jsonLd)],
  ],

  markdown: {
    config: (md) => {
      md.core.ruler.push('escape_vue_interpolation', (state) => {
        for (const token of state.tokens) {
          if (token.type === 'inline' && token.children) {
            for (const child of token.children) {
              if (child.type === 'text' || child.type === 'html_inline') {
                child.content = child.content
                  .replace(/\{\{/g, '&#123;&#123;')
                  .replace(/\}\}/g, '&#125;&#125;')
              }
            }
          }
        }
      })
    },
  },

  ignoreDeadLinks: true,
  vite: {
    resolve: { preserveSymlinks: true },
    server: { fs: { strict: false } },
  },
  sitemap: {
    hostname: siteUrl,
  },

  locales: {
    root: {
      label: '简体中文',
      lang: 'zh-CN',
      themeConfig: {
        nav: [
          { text: '快速开始', link: '/quick-start' },
          { text: 'API', link: '/API' },
          { text: 'GitHub', link: 'https://github.com/Zenine/qy-gaussplume' },
        ],
        sidebar: {
          '/': [
            {
              text: '指南',
              items: [
                { text: '快速开始', link: '/quick-start' },
                { text: '架构', link: '/ARCHITECTURE' },
                { text: 'API', link: '/API' },
                { text: '工作流', link: '/WORKFLOW' },
                { text: 'FAQ', link: '/faq' },
              ],
            },
          ],
        },
      },
    },
    en: {
      label: 'English',
      lang: 'en-US',
      themeConfig: {
        nav: [
          { text: 'Quick Start', link: '/en/quick-start' },
          { text: 'FAQ', link: '/en/faq' },
        ],
        sidebar: {
          '/en/': [
            {
              text: 'Guide',
              items: [
                { text: 'Quick Start', link: '/en/quick-start' },
                { text: 'Architecture', link: '/en/ARCHITECTURE' },
                { text: 'API', link: '/en/API' },
                { text: 'Workflow', link: '/en/WORKFLOW' },
                { text: 'FAQ', link: '/en/faq' },
              ],
            },
          ],
        },
      },
    },
    ja: {
      label: '日本語',
      lang: 'ja',
      themeConfig: {
        nav: [
          { text: 'クイックスタート', link: '/ja/quick-start' },
          { text: 'FAQ', link: '/ja/faq' },
        ],
        sidebar: {
          '/ja/': [
            {
              text: 'ガイド',
              items: [
                { text: 'クイックスタート', link: '/ja/quick-start' },
                { text: 'アーキテクチャ', link: '/ja/ARCHITECTURE' },
                { text: 'API', link: '/ja/API' },
                { text: 'ワークフロー', link: '/ja/WORKFLOW' },
                { text: 'FAQ', link: '/ja/faq' },
              ],
            },
          ],
        },
      },
    },
    'zh-TW': {
      label: '繁體中文',
      lang: 'zh-TW',
      themeConfig: {
        nav: [
          { text: '快速開始', link: '/zh-TW/quick-start' },
          { text: 'FAQ', link: '/zh-TW/faq' },
        ],
        sidebar: {
          '/zh-TW/': [
            {
              text: '指南',
              items: [
                { text: '快速開始', link: '/zh-TW/quick-start' },
                { text: '架構', link: '/zh-TW/ARCHITECTURE' },
                { text: 'API', link: '/zh-TW/API' },
                { text: '工作流', link: '/zh-TW/WORKFLOW' },
                { text: 'FAQ', link: '/zh-TW/faq' },
              ],
            },
          ],
        },
      },
    },
  },

  themeConfig: {
    logo: '/hero.svg',
    socialLinks: [
      { icon: 'github', link: 'https://github.com/Zenine/qy-gaussplume' },
    ],
    search: { provider: 'local' },
    footer: {
      message: 'Built with <a href="https://github.com/lordmos/meridian" target="_blank">Meridian</a>',
    },
  },
})
