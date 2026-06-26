import axios, { type AxiosInstance } from 'axios'

// 开发环境走 Vite proxy（见 vite.config.ts），生产环境读 VITE_API_BASE_URL
const baseURL = import.meta.env.VITE_API_BASE_URL || '/'

export const http: AxiosInstance = axios.create({
  baseURL,
  timeout: 120000,
  headers: { 'Content-Type': 'application/json' },
})

// 统一错误：把后端 { detail } 结构透传到 axios error.message
http.interceptors.response.use(
  (response) => response,
  (error) => {
    const detail = error?.response?.data?.detail
    if (typeof detail === 'string' && detail.length > 0) {
      error.message = detail
    }
    return Promise.reject(error)
  },
)
