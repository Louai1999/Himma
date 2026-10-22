import { h, fill, add } from './ui/dom.js';
import { icon } from './ui/icons.js';

// Hash-based navigation: #today, #workouts/templates, #session/12 ...
// Each screen module exports mount(view, params) and may return a cleanup function.

let routes = new Map();
let main = null;
let nav = null;
let cleanup = null;

export function startRouter({ mainEl, navEl, tabs, hidden = {} }) {
  main = mainEl;
  nav = navEl;
  routes = new Map([...tabs.map((t) => [t.key, t.mount]), ...Object.entries(hidden)]);

  fill(nav, ...tabs.map((tab) =>
    h('a', { class: 'tab', href: `#${tab.key}`, dataset: { tab: tab.key } }, icon(tab.icon, 18), h('span', { text: tab.label }))));

  window.addEventListener('hashchange', render);
  render();
}

export function stopRouter() {
  window.removeEventListener('hashchange', render);
  runCleanup();
}

export function navigate(hash) {
  if (location.hash === hash) render();
  else location.hash = hash;
}

function runCleanup() {
  if (typeof cleanup === 'function') cleanup();
  cleanup = null;
}

function render() {
  const [name = 'today', ...params] = location.hash.replace(/^#/, '').split('/').map(decodeURIComponent);
  const key = routes.has(name) ? name : 'today';
  const mount = routes.get(key);

  runCleanup();
  const view = h('section', { class: 'view', 'aria-live': 'polite' });
  fill(main, view);
  cleanup = mount(view, params) ?? null;

  nav.querySelectorAll('.tab').forEach((tab) => {
    const active = tab.dataset.tab === key || (key === 'session' && tab.dataset.tab === 'workouts');
    if (active) tab.setAttribute('aria-current', 'page');
    else tab.removeAttribute('aria-current');
  });
  window.scrollTo({ top: 0 });
}
