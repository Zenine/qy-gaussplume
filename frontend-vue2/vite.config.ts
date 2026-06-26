import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue2'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5174,
    proxy: {
      '/api': {
        target: process.env.VITE_API_PROXY_TARGET || 'http://127.0.0.1:5207',
        changeOrigin: true,
      },
    },
  },
})
