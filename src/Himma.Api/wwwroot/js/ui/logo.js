import { t } from '../i18n/index.js';
import { h } from './dom.js';

/**
 * The Himma logo. Uses assets/himma-logo.png when the developer has added it,
 * otherwise falls back to the bundled SVG version.
 */
export function logo(size = 48) {
  const img = h('img', { src: 'assets/himma-logo.png', alt: t('شعار هِمّة'), width: size, height: size, class: 'logo' });
  img.addEventListener('error', () => { img.src = 'assets/himma-logo.svg'; }, { once: true });
  return img;
}
