import { t } from '../i18n/index.js';
import { h, fill, add } from './dom.js';
import { icon } from './icons.js';

let hideTimer;

/** The single, quiet green confirmation shown after every successful save. */
export function showSaved(text = t('تم الحفظ')) {
  const toast = document.getElementById('toast');
  if (!toast) return;
  clearTimeout(hideTimer);
  toast.classList.remove('show');
  fill(toast, icon('check', 18), h('span', { text }));
  // Restart the animation even when two saves happen in a row.
  void toast.offsetWidth;
  toast.classList.add('show');
  hideTimer = setTimeout(() => toast.classList.remove('show'), 1800);
}
