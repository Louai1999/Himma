import { t } from './i18n/index.js';
import { languageSwitch } from './ui/language.js';
import { post, setSession } from './api.js';
import { h, fill, add } from './ui/dom.js';
import { field, showErrors, clearErrors } from './ui/forms.js';
import { calmNotice } from './ui/notice.js';
import { logo } from './ui/logo.js';

/**
 * The sign-in / sign-up gate. Calls onSignedIn(authResponse) when done.
 * The admin page passes allowRegister: false (admins are never created through sign-up).
 */
export function renderAuth(root, onSignedIn, message, { allowRegister = true, subtitle } = {}) {
  let mode = 'login';
  const formSlot = h('div', { class: 'card' });
  const loginTab = h('button', { type: 'button', onclick: () => switchTo('login') }, t('تسجيل الدخول'));
  const registerTab = h('button', { type: 'button', onclick: () => switchTo('register') }, t('حساب جديد'));

  fill(root, h('main', { class: 'auth' },
    h('div', { class: 'auth-top' }, languageSwitch()),
    h('div', { class: 'brand-block' }, logo(110), h('h1', { text: t('هِمّة') }), h('p', { class: 'muted', text: subtitle ?? t('أكلك وماؤك ووزنك وتمارينك في مكان واحد.') })),
    message && calmNotice(message),
    allowRegister && h('div', { class: 'segmented', role: 'group', 'aria-label': t('نوع الدخول') }, loginTab, registerTab),
    formSlot));

  switchTo('login');

  function switchTo(next) {
    mode = next;
    loginTab.setAttribute('aria-pressed', String(mode === 'login'));
    registerTab.setAttribute('aria-pressed', String(mode === 'register'));
    fill(formSlot, mode === 'login' ? loginForm() : registerForm());
    formSlot.querySelector('input')?.focus();
  }

  function loginForm() {
    const username = h('input', { type: 'text', autocomplete: 'username', required: true, dir: 'ltr' });
    const password = h('input', { type: 'password', autocomplete: 'current-password', required: true, dir: 'ltr' });
    return authForm(t('دخول'), [
      field(t('اسم المستخدم'), username, { name: 'username' }),
      field(t('كلمة المرور'), password, { name: 'password' }),
    ], () => post('/auth/login', { username: username.value.trim(), password: password.value }));
  }

  function registerForm() {
    const username = h('input', { type: 'text', autocomplete: 'username', required: true, dir: 'ltr', minlength: 3, maxlength: 30 });
    const displayName = h('input', { type: 'text', autocomplete: 'nickname', required: true, maxlength: 60 });
    const password = h('input', { type: 'password', autocomplete: 'new-password', required: true, minlength: 8, dir: 'ltr' });
    const consent = h('input', { type: 'checkbox', id: 'privacy-consent', required: true });
    return authForm(t('إنشاء الحساب'), [
      field(t('اسم المستخدم (بالإنجليزي)'), username, { name: 'username', hint: t('حروف إنجليزية وأرقام و . _ - فقط.') }),
      field(t('اسمك الظاهر'), displayName, { name: 'displayName' }),
      field(t('كلمة المرور'), password, { name: 'password', hint: t('8 أحرف على الأقل، وتجنب الكلمات الشائعة.') }),
      h('p', { class: 'privacy', text: t('إشعار الخصوصية: بياناتك محفوظة في حسابك. صاحب الخدمة يستطيع الاطلاع على بيانات الاستخدام (مثل الوجبات والتمارين ومدى الالتزام) لأغراض الدعم وتحسين الخدمة فقط، ولا تُشارك مع أي طرف آخر. تستطيع تصدير بياناتك أو حذف حسابك في أي وقت.') }),
      h('div', { class: 'field', dataset: { field: 'acceptPrivacy' } },
        h('label', { class: 'check', for: 'privacy-consent' }, consent, h('span', { text: t('قرأت إشعار الخصوصية وأوافق عليه.') })),
        h('span', { class: 'field-error' })),
    ], () => post('/auth/register', {
      username: username.value.trim(),
      displayName: displayName.value.trim(),
      password: password.value,
      acceptPrivacy: consent.checked,
    }));
  }

  function authForm(submitText, fields, submit) {
    const status = h('div');
    const button = h('button', { type: 'submit', class: 'btn btn-block' }, submitText);
    const form = h('form', { class: 'stack', novalidate: true }, fields, status, button);
    form.addEventListener('submit', async (event) => {
      event.preventDefault();
      clearErrors(form);
      fill(status);
      button.disabled = true;
      try {
        const auth = await submit();
        setSession(auth);
        onSignedIn(auth);
      } catch (error) {
        const message = showErrors(form, error);
        if (message) fill(status, calmNotice(message));
      } finally {
        button.disabled = false;
      }
    });
    return form;
  }
}
