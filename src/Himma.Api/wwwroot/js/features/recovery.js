import { t } from '../i18n/index.js';
import { api, get, post, setSession } from '../api.js';
import { h, fill, add } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { field, showErrors, clearErrors } from '../ui/forms.js';
import { calmNotice, loadInto } from '../ui/notice.js';
import { showSaved } from '../ui/toast.js';

// Account recovery: a confirmed email (added from the account screen), "forgot password" by email,
// and the forced change after an admin gave a temporary password.

const BANNER_KEY = 'himma-email-banner-dismissed';

/** Whether the server can send email. Never throws: without an answer the app just hides email features. */
export async function emailEnabled() {
  try {
    return (await get('/auth/options')).emailEnabled;
  } catch {
    return false;
  }
}

/** A small form: runs submit() and shows field errors; returns the form element. */
function stepForm(fields, buttonText, submit) {
  const status = h('div');
  const button = h('button', { type: 'submit', class: 'btn btn-block' }, buttonText);
  const form = h('form', { class: 'stack', novalidate: true }, fields, status, button);
  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    clearErrors(form);
    fill(status);
    button.disabled = true;
    try {
      await submit();
    } catch (error) {
      const message = showErrors(form, error);
      if (message) fill(status, calmNotice(message));
    } finally {
      button.disabled = false;
    }
  });
  return form;
}

const emailInput = () => h('input', { type: 'email', autocomplete: 'email', required: true, dir: 'ltr', maxlength: 254 });
const codeInput = () => h('input', { type: 'text', inputmode: 'numeric', autocomplete: 'one-time-code', required: true, dir: 'ltr', maxlength: 6, pattern: '[0-9]{6}', placeholder: '123456' });
const passwordInput = (autocomplete) => h('input', { type: 'password', autocomplete, required: true, dir: 'ltr', minlength: 8 });

/**
 * "Forgot password or username": email → code + new password. onBack returns to sign-in;
 * onDone(message) returns to sign-in with a success message.
 */
export function forgotFlow(slot, { onBack, onDone }) {
  const back = h('button', { type: 'button', class: 'btn btn-ghost btn-sm', onclick: onBack }, t('رجوع لتسجيل الدخول'));
  fill(slot, h('p', { class: 'muted', text: t('جاري التحميل…') }));

  emailEnabled().then((enabled) => {
    if (!enabled) {
      fill(slot,
        h('h2', { text: t('نسيت كلمة المرور؟') }),
        h('p', { text: t('الاسترجاع بالبريد غير متاح حالياً. تواصل مع مدير التطبيق، وبعد التأكد منك يعطيك كلمة مرور مؤقتة.') }),
        back);
      return;
    }
    askEmail();
  });

  function askEmail() {
    const email = emailInput();
    fill(slot,
      h('h2', { text: t('نسيت كلمة المرور أو اسم المستخدم؟') }),
      h('p', { class: 'muted small', text: t('اكتب البريد المؤكد في حسابك. نرسل لك اسم المستخدم ورمزاً لتغيير كلمة المرور.') }),
      stepForm([field(t('البريد الإلكتروني'), email, { name: 'email' })], t('أرسل الرمز'), async () => {
        await post('/auth/forgot-password', { email: email.value.trim() });
        askCode(email.value.trim());
      }),
      back);
    email.focus();
  }

  function askCode(address) {
    const code = codeInput();
    const password = passwordInput('new-password');
    fill(slot,
      h('h2', { text: t('اكتب الرمز') }),
      h('p', { class: 'muted small', text: t('إذا كان البريد {email} مسجّلاً ومؤكداً، وصلك منه رمز من 6 أرقام صالح لمدة 10 دقائق. لا تنسَ مجلد الرسائل غير المرغوبة.', { email: address }) }),
      stepForm([
        field(t('الرمز'), code, { name: 'code' }),
        field(t('كلمة المرور الجديدة'), password, { name: 'newPassword', hint: t('8 أحرف على الأقل، وتجنب الكلمات الشائعة.') }),
      ], t('غيّر كلمة المرور'), async () => {
        await post('/auth/reset-password', { email: address, code: code.value.trim(), newPassword: password.value });
        onDone(t('تم تغيير كلمة المرور. سجّل دخولك بكلمة المرور الجديدة.'));
      }),
      h('div', { class: 'row' }, h('button', { type: 'button', class: 'btn btn-ghost btn-sm', onclick: askEmail }, t('ما وصلني رمز')), back));
    code.focus();
  }
}

/** After an admin reset: the temporary password must be replaced before using the app. */
export function forcedChange(slot, auth, onDone) {
  const current = passwordInput('current-password');
  const next = passwordInput('new-password');
  fill(slot,
    h('h2', { text: t('اختر كلمة مرور جديدة') }),
    h('p', { class: 'muted small', text: t('دخلت بكلمة مرور مؤقتة من مدير التطبيق. اختر كلمة مرور جديدة خاصة بك للمتابعة.') }),
    stepForm([
      field(t('كلمة المرور المؤقتة'), current, { name: 'currentPassword' }),
      field(t('كلمة المرور الجديدة'), next, { name: 'newPassword', hint: t('8 أحرف على الأقل، وتجنب الكلمات الشائعة.') }),
    ], t('حفظ والمتابعة'), async () => {
      setSession(auth);
      const fresh = await post('/auth/change-password', { currentPassword: current.value, newPassword: next.value });
      setSession(fresh);
      onDone(fresh);
    }));
  current.focus();
}

/** Suggests adding a recovery email to accounts without one (dismissible for this browser session). */
export async function showEmailBanner(user) {
  if (user.hasEmail) return;
  try { if (sessionStorage.getItem(BANNER_KEY)) return; } catch { /* storage blocked: show it */ }
  if (!(await emailEnabled())) return;
  const slot = document.getElementById('banner-slot');
  if (!slot || slot.childElementCount > 0) return;
  const close = () => {
    try { sessionStorage.setItem(BANNER_KEY, '1'); } catch { /* fine */ }
    fill(slot);
  };
  fill(slot, h('div', { class: 'notice info banner', role: 'status' },
    h('span', { text: t('أضف بريدك الإلكتروني عشان تقدر تسترجع حسابك لو نسيت كلمة المرور.') }),
    h('span', { class: 'row' },
      h('a', { class: 'btn btn-sm', href: '#profile', onclick: () => fill(slot) }, t('أضف البريد')),
      h('button', { type: 'button', class: 'icon-btn', 'aria-label': t('إغلاق'), onclick: close }, icon('close', 18)))));
}

/** Account screen: the recovery email, with add / change (confirmed by a code) and remove. */
export function emailCard() {
  const card = h('div', { class: 'card stack', id: 'recovery-email' });
  load();
  return card;

  function load() {
    loadInto(card, () => get('/account/email'), render);
  }

  function render(data) {
    add(card, h('h2', { text: t('البريد الإلكتروني للاسترجاع') }));
    if (!data.emailEnabled) {
      add(card, h('p', { class: 'muted small', text: t('ميزة البريد ستتفعّل قريباً. لو نسيت كلمة المرور قبلها، يقدر مدير التطبيق يساعدك.') }));
      return;
    }
    if (data.email) {
      add(card,
        h('p', {}, '✅ ', h('span', { dir: 'ltr', text: data.email })),
        h('p', { class: 'muted small', text: t('لو نسيت كلمة المرور أو اسم المستخدم، اضغط «نسيت كلمة المرور؟» في صفحة الدخول.') }),
        h('div', { class: 'row' },
          h('button', { type: 'button', class: 'btn btn-secondary btn-sm', onclick: () => change(data.email) }, t('تغيير البريد')),
          h('button', { type: 'button', class: 'btn btn-ghost btn-sm', onclick: remove }, t('إزالة'))));
    } else {
      add(card,
        h('p', { class: 'muted small', text: t('بدون بريد مؤكد ما نقدر نساعدك لو نسيت كلمة المرور. نستخدمه للاسترجاع فقط، ولا يظهر لأحد.') }),
        h('button', { type: 'button', class: 'btn btn-sm', onclick: () => change(null) }, icon('plus', 16), t('أضف بريدك')));
    }
  }

  function change(current) {
    const email = emailInput();
    const password = passwordInput('current-password');
    fill(card,
      h('h2', { text: current ? t('تغيير البريد') : t('أضف بريدك') }),
      stepForm([
        field(t('البريد الإلكتروني'), email, { name: 'email' }),
        field(t('كلمة المرور الحالية'), password, { name: 'password', hint: t('للتأكد أنك صاحب الحساب.') }),
      ], t('أرسل رمز التأكيد'), async () => {
        await post('/account/email', { email: email.value.trim(), password: password.value });
        confirm(email.value.trim());
      }),
      h('button', { type: 'button', class: 'btn btn-ghost btn-sm', onclick: load }, t('إلغاء')));
    email.focus();
  }

  function confirm(address) {
    const code = codeInput();
    fill(card,
      h('h2', { text: t('اكتب رمز التأكيد') }),
      h('p', { class: 'muted small', text: t('أرسلنا رمزاً من 6 أرقام إلى {email}. صالح لمدة 10 دقائق.', { email: address }) }),
      stepForm([field(t('الرمز'), code, { name: 'code' })], t('تأكيد'), async () => {
        await post('/account/email/confirm', { email: address, code: code.value.trim() });
        showSaved(t('تم تأكيد بريدك'));
        document.getElementById('banner-slot')?.replaceChildren();
        load();
      }),
      h('button', { type: 'button', class: 'btn btn-ghost btn-sm', onclick: load }, t('إلغاء')));
    code.focus();
  }

  function remove() {
    const password = passwordInput('current-password');
    fill(card,
      h('h2', { text: t('إزالة البريد') }),
      h('p', { class: 'muted small', text: t('بدون بريد ما تقدر تسترجع حسابك لو نسيت كلمة المرور.') }),
      stepForm([field(t('كلمة المرور الحالية'), password, { name: 'password' })], t('إزالة البريد'), async () => {
        await api('/account/email', { method: 'DELETE', body: { password: password.value } });
        showSaved(t('تمت الإزالة'));
        load();
      }),
      h('button', { type: 'button', class: 'btn btn-ghost btn-sm', onclick: load }, t('إلغاء')));
    password.focus();
  }
}
