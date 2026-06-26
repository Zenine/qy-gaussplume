export function errorMessage(e: unknown, fallback = '操作失败'): string {
  if (e instanceof Error) return e.message || fallback
  if (typeof e === 'string') return e
  return fallback
}
