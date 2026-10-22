import { t } from '../i18n/index.js';
import { refreshSession, onSessionExpired, clearSession, post } from '../api.js';
import { renderAuth } from '../auth.js';
import { startRouter, stopRouter } from '../router.js';
import { h, fill } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { calmNotice } from '../ui/notice.js';
import { logo } from '../ui/logo.js';
import { languageSwitch } from '../ui/language.js';
import * as overview from './overview.js';
import * as users from './users.js';
import * as userDetail from './userDetail.js';
import * as audit from './audit.js';

// Admin dashboard. Same sign-in as the app, but only accounts with the Admin role get in;
// the server enforces the role on every /api/admin call as well.

const root = document.getElementById('app');
document.title = `${t('هِمّة')} - ${t('لوحة المدير')}`;

const TABS = [
  { key: 'overview', label: t('نظرة عامة'), icon: 'history', mount: overview.mount },
  { key: 'users', label: t('المشتركون'), icon: 'profile', mount: users.mount },
  { key: 'audit', label: t('سجل المراجعة'), icon: 'calendar', mount: audit.mount },
];

onSessionExpired(() => showAuth(t('انتهت الجلسة. سجّل الدخول مرة أخرى.')));
start();

async function start() {
  try {
    const auth = await refreshSession();
    if (auth) enter(auth);
    else showAuth();
  } catch (error) {
    fill(root, h('div', { class: 'auth' }, calmNotice(error, start)));
  } finally {
    root.removeAttribute('aria-busy');
  }
}

function enter(auth) {
  if (auth.user.roles.includes('Admin')) showDashboard(auth);
  else showNotAdmin(auth);
}

function showAuth(message) {
  stopRouter();
  clearSession();
  renderAuth(root, enter, message, { allowRegister: false, subtitle: t('لوحة المدير') });
}

function showNotAdmin(auth) {
  fill(root, h('main', { class: 'auth' },
    h('div', { class: 'card stack' },
      h('h1', { text: t('هذه الصفحة للمدير فقط') }),
      h('p', { class: 'muted', text: t('أنت مسجّل الدخول باسم {name}، وهذا الحساب ليس حساب مدير.', { name: auth.user.username }) }),
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

function showDashboard(auth) {
  const main = h('main', { id: 'main' });
  const nav = h('nav', { class: 'tabs admin-tabs', 'aria-label': t('الأقسام') });

  fill(root,
    h('header', { class: 'app-header' },
      logo(44),
      h('div', {},
        h('p', { class: 'brand', text: t('هِمّة') }),
        h('p', { class: 'greeting', text: t('لوحة المدير · {name}', { name: auth.user.displayName }) })),
      languageSwitch(),
      h('button', { type: 'button', class: 'icon-btn', 'aria-label': t('تسجيل الخروج'), title: t('تسجيل الخروج'), onclick: signOut }, icon('logout'))),
    nav,
    main);

  startRouter({ mainEl: main, navEl: nav, tabs: TABS, hidden: { user: userDetail.mount }, parentTabs: { user: 'users' } });
}
