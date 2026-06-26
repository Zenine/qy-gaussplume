import axios, { type AxiosInstance, type InternalAxiosRequestConfig } from 'axios'

// Vue2 集成说明：
// - VITE_API_BASE_URL：后端域名；为空时走当前站点/Vite proxy。
// - VITE_API_PATH_PREFIX：接口路径前缀，默认 /api；同事平台若已在网关层挂载到根路径，可设置为空字符串。
// - VITE_API_KEY：需要 x-api-key 鉴权时设置；不要把真实 key 写进仓库。
const baseURL = import.meta.env.VITE_API_BASE_URL || '/'
const apiPathPrefix = (import.meta.env.VITE_API_PATH_PREFIX ?? '/api').replace(/\/$/, '')
const apiKey = import.meta.env.VITE_API_KEY

function withConfiguredApiPrefix(url?: string) {
  if (!url || !url.startsWith('/api')) return url
  const suffix = url.slice('/api'.length) || '/'
  return `${apiPathPrefix}${suffix}` || suffix
}

export const http: AxiosInstance = axios.create({
  baseURL,
  timeout: 120000,
  headers: { 'Content-Type': 'application/json' },
})

http.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  config.url = withConfiguredApiPrefix(config.url)
  if (apiKey) {
    config.headers.set('x-api-key', apiKey)
  }
  return config
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
