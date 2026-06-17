const HTML_ESCAPE_MAP: Record<string, string> = {
  '&': '&amp;',
  '<': '&lt;',
  '>': '&gt;',
  '"': '&quot;',
  "'": '&#39;',
}

export function escapeHtml(value: unknown) {
  return String(value ?? '').replace(/[&<>"']/g, (char) => HTML_ESCAPE_MAP[char])
}

const SAFE_COLOR_PATTERNS = [
  /^#[0-9a-fA-F]{3,8}$/,
  /^rgba?\(\s*[\d.]+%?\s*,\s*[\d.]+%?\s*,\s*[\d.]+%?\s*(?:,\s*(?:0|1|0?\.\d+)\s*)?\)$/,
  /^hsla?\(\s*[\d.]+(?:deg)?\s*,\s*[\d.]+%\s*,\s*[\d.]+%\s*(?:,\s*(?:0|1|0?\.\d+)\s*)?\)$/,
  /^[a-zA-Z]+$/,
]

export function safeCssColor(value: unknown, fallback = '#64748b') {
  const color = String(value ?? '').trim()
  return SAFE_COLOR_PATTERNS.some((pattern) => pattern.test(color)) ? color : fallback
}
