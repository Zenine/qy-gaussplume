import { describe, expect, it } from 'vitest'
import { escapeHtml, safeCssColor } from '@/utils/html'

describe('html utils', () => {
  it('转义地图弹窗中的用户输入文本', () => {
    expect(escapeHtml('<img src=x onerror=alert(1)> & "工地"')).toBe(
      '&lt;img src=x onerror=alert(1)&gt; &amp; &quot;工地&quot;',
    )
  })

  it('只允许安全的地图标记颜色值', () => {
    expect(safeCssColor('#22C55E')).toBe('#22C55E')
    expect(safeCssColor('rgb(255, 0, 0)')).toBe('rgb(255, 0, 0)')
    expect(safeCssColor('red')).toBe('red')
    expect(safeCssColor('red;background:url(javascript:alert(1))')).toBe('#64748b')
  })
})
