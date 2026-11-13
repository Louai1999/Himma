import { t } from '../i18n/index.js';
import { refreshSession, onSessionExpired, clearSession, post, get } from '../api.js';
import { renderAuth } from '../auth.js';
import { startRouter, stopRouter } from '../router.js';
import { h, fill } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { calmNotice } from '../ui/notice.js';
import { logo } from '../ui/logo.js';
import { languageSwitch } from '../ui/language.js';
import * as clients from './clients.js';
import * as clientDetail from './clientDetail.js';
import * as settings from './settings.js';

// Coach dashboard. Same sign-in as the app; the server checks the coach package on every /api/coach call.

const root = document.getElementById('app');
document.title = `${t('هِمّة')} - ${t('لوحة المدرب')}`;

const TABS = [
  { key: 'clients', label: t('متدربيني'), icon: 'profile', mount: clients.mount },
  { key: 'settings', label: t('باقتي والدعوة'), icon: 'copy', mount: settings.mount },
];

onSessionExpired(() => showAuth(t('انتهت الجلسة. سجّل الدخول مرة أخرى.')));
start();

async function start() {
  try {
    const auth = await refreshSession();
    if (auth) await enter(auth);
    else showAuth();
  } catch (error) {
    fill(root, h('div', { class: 'auth' }, calmNotice(error, start)));
  } finally {
    root.removeAttribute('aria-busy');
  }
}

async function enter(auth) {
  try {
    const coach = await get('/coach/me');
    showDashboard(auth, coach);
  } catch (error) {
    if (error.status === 404) showNoPackage(auth);
    else fill(root, h('div', { class: 'auth' }, calmNotice(error, start)));
  }
}

function showAuth(message) {
  stopRouter();
  clearSession();
  renderAuth(root, enter, message, { allowRegister: false, subtitle: t('لوحة المدرب') });
}

function showNoPackage(auth) {
  fill(root, h('main', { class: 'auth' },
    h('div', { class: 'card stack' },
      h('h1', { text: t('لوحة المدرب') }),
      h('p', { text: t('حساب {name} ليس عليه باقة المدربين بعد.', { name: auth.user.username }) }),
      h('p', { class: 'muted', text: t('مع الباقة: تتابع متدربيك من مكان واحد، تعرف من يحتاجك اليوم، ترسل لهم رسائل وخطط تمرين بضغطة، ويرون اسمك داخل التطبيق.') }),
      h('p', { class: 'muted small', text: t('لتفعيلها تواصل مع إدارة هِمّة.') }),
      h('div', { class: 'row' },
        h('a', { class: 'btn', href: '/' }, t('الذهاب إلى التطبيق')),
        h('button', { type: 'button', class: 'btn btn-secondary', onclick: signOut }, t('تسجيل الخروج'))))));
}

async function signOut() {
  try {
    await post('/auth/logout');
  } catch {
    // Signing out locally is enough.
  }
  showAuth();
}

function showDashboard(auth, coach) {
  const main = h('main', { id: 'main' });
  const nav = h('nav', { class: 'tabs admin-tabs coach-tabs', 'aria-label': t('الأقسام') });

  fill(root,
    h('header', { class: 'app-header' },
      logo(44),
      h('div', {},
        h('p', { class: 'brand', text: coach.businessName }),
        h('p', { class: 'greeting', text: t('لوحة المدرب · {name}', { name: auth.user.displayName }) })),
      h('a', { class: 'icon-btn', href: '/', 'aria-label': t('الذهاب إلى التطبيق'), title: t('الذهاب إلى التطبيق') }, icon('today')),
      languageSwitch(),
      h('button', { type: 'button', class: 'icon-btn', 'aria-label': t('تسجيل الخروج'), title: t('تسجيل الخروج'), onclick: signOut }, icon('logout'))),
    !coach.isActive && h('div', { class: 'notice', role: 'status', text: t('انتهت مدة باقتك. جدّدها مع إدارة هِمّة لتعود إلى متدربيك؛ روابطهم محفوظة.') }),
    nav,
    main);

  startRouter({ mainEl: main, navEl: nav, tabs: TABS, hidden: { client: clientDetail.mount }, parentTabs: { client: 'clients' } });
}
