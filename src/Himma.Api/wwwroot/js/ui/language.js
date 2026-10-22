import { h } from './dom.js';
import { otherLang, otherLanguageName, setLang } from '../i18n/index.js';

/** Small button that switches between Arabic and English (the page reloads in the new language). */
export function languageSwitch() {
  return h('button', {
    type: 'button',
    class: 'btn btn-ghost btn-sm lang-switch',
    lang: otherLang,
    onclick: () => setLang(otherLang),
  }, otherLanguageName);
}
