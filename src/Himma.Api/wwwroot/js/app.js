import { t } from './i18n/index.js';
import { languageSwitch } from './ui/language.js';
import { refreshSession, onSessionExpired, clearSession } from './api.js';
import { renderAuth } from './auth.js';
import { startRouter, stopRouter } from './router.js';
import { state, on } from './state.js';
import { h, fill, add } from './ui/dom.js';
import { calmNotice } from './ui/notice.js';
import { logo } from './ui/logo.js';
import { startWaterReminder, stopWaterReminder } from './features/water.js';
import * as today from './features/today.js';
import * as workouts from './features/workouts.js';
import * as session from './features/session.js';
import * as weight from './features/weight.js';
import * as historyScreen from './features/history.js';
import * as profile from './features/profile.js';
import * as supplements from './features/supplements.js';
import * as records from './features/records.js';
import * as volume from './features/volume.js';
import { syncPush } from './push.js';
import { showEmailBanner } from './features/recovery.js';

const root = document.getElementById('app');
document.title = t('هِمّة');

const TABS = [
  { key: 'today', label: t('اليوم'), icon: 'today', mount: today.mount },
  { key: 'workouts', label: t('التمارين'), icon: 'workouts', mount: workouts.mount },
  { key: 'weight', label: t('الجسم'), icon: 'weight', mount: weight.mount },
  { key: 'history', label: t('السجل'), icon: 'history', mount: historyScreen.mount },
  { key: 'profile', label: t('حسابي'), icon: 'profile', mount: profile.mount },
];

onSessionExpired(() => showAuth(t('انتهت الجلسة. سجّل الدخول مرة أخرى.')));
on('signed-out', (message) => showAuth(message));

registerServiceWorker();
start();

/** On page load the access token is gone (it is only kept in memory), so try the refresh cookie. */
async function start() {
  try {
    const auth = await refreshSession();
    if (auth?.user.mustChangePassword) renderAuth(root, showApp, null, { forceChange: auth });
    else if (auth) showApp(auth);
    else showAuth();
  } catch (error) {
    fill(root, h('div', { class: 'auth' }, calmNotice(error, start)));
  } finally {
    root.removeAttribute('aria-busy');
  }
}

function showAuth(message) {
  stopRouter();
  stopWaterReminder();
  clearSession();
  state.user = null;
  if (location.hash) window.history.replaceState(null, '', location.pathname);
  renderAuth(root, showApp, message);
}

function showApp(auth) {
  state.user = auth.user;
  const main = h('main', { id: 'main' });
  const nav = h('nav', { class: 'tabs', 'aria-label': t('الأقسام') });

  fill(root,
    h('header', { class: 'app-header' },
      logo(48),
      h('div', {},
        h('p', { class: 'brand', text: t('هِمّة') }),
        h('p', { class: 'greeting', text: t('أهلاً {name}', { name: auth.user.displayName }) })),
      languageSwitch()),
    h('div', { id: 'banner-slot' }),
    nav,
    main);

  startRouter({ mainEl: main, navEl: nav, tabs: TABS, hidden: { session: session.mount, supplements: supplements.mount, records: records.mount, volume: volume.mount }, parentTabs: { session: 'workouts', supplements: 'today', records: 'workouts', volume: 'workouts' } });
  startWaterReminder();
  syncPush();
  showEmailBanner(auth.user);
}

function registerServiceWorker() {
  if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => navigator.serviceWorker.register('/sw.js').catch(() => {}));
  }
}
