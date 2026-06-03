import DefaultTheme from 'vitepress/theme'
import './style.css'
import { startInlineIconsWatcher } from './inline-svg'

export default {
  extends: DefaultTheme,
  enhanceApp() {
    if (typeof window !== 'undefined') {
      startInlineIconsWatcher()
    }
  },
}
