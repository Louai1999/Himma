import { t } from '../i18n/index.js';
import { get, put, post } from '../api.js';
import { formatDate } from '../dates.js';
import { h, fill, add, fmt } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { field, showErrors, clearErrors } from '../ui/forms.js';
import { confirmDialog } from '../ui/modal.js';
import { calmNotice, loadInto } from '../ui/notice.js';
import { showSaved } from '../ui/toast.js';
import { lastDayIso } from '../admin/coaches.js';

/** The coach's package, how clients join (code, link, WhatsApp), and the name clients see. */
export function mount(view) {
  const body = h('div', { class: 'stack' });
  add(view, h('header', { class: 'view-head' }, h('h1', { text: t('باقتي والدعوة') })), body);
  const load = () => loadInto(body, () => get('/coach/me'), (coach) => render(body, coach, load));
  load();
}

function render(body, coach, reload) {
  add(body, inviteCard(coach, reload), packageCard(coach), profileCard(coach, reload));
}

function inviteLink(code) {
  return `${location.origin}/?coach=${encodeURIComponent(code)}`;
}

async function copy(text, button) {
  try {
    await navigator.clipboard.writeText(text);
    showSaved(t('تم النسخ'));
  } catch {
    button.after(h('p', { class: 'small', text: t('انسخه يدوياً: {text}', { text }) }));
  }
}

function inviteCard(coach, reload) {
  const link = inviteLink(coach.inviteCode);
  const whatsappText = t('انضم لي على هِمّة عشان أتابع أكلك وتمارينك: {link} (كود المدرب: {code})', { link, code: coach.inviteCode });
  const copyCode = h('button', { type: 'button', class: 'btn btn-secondary btn-sm' }, icon('copy', 16), t('نسخ الكود'));
  const copyLink = h('button', { type: 'button', class: 'btn btn-secondary btn-sm' }, icon('copy', 16), t('نسخ الرابط'));
  copyCode.addEventListener('click', () => copy(coach.inviteCode, copyCode));
  copyLink.addEventListener('click', () => copy(link, copyLink));

  const renew = async () => {
    const ok = await confirmDialog({
      title: t('كود جديد'),
      message: t('سيتوقف الكود والرابط الحاليان عن العمل فوراً. متدربوك الحاليون يبقون معك.'),
      confirmText: t('أنشئ كوداً جديداً'),
    });
    if (!ok) return;
    try {
      await post('/coach/me/invite-code');
      showSaved(t('تم إنشاء كود جديد'));
      reload();
    } catch (error) {
      body.append(calmNotice(error));
    }
  };
  const body = h('section', { class: 'card stack invite-card' },
    h('h2', { text: t('ادعُ متدربيك') }),
    h('p', { class: 'muted small', text: t('المتدرب يفتح الرابط أو يكتب الكود في «حسابي ← مدربي»، ويوافق على مشاركة بياناته معك. يقدر يوقف المشاركة متى شاء.') }),
    h('p', { class: 'invite-code num', dir: 'ltr', text: coach.inviteCode }),
    h('p', { class: 'muted small invite-link', dir: 'ltr', text: link }),
    h('div', { class: 'row' },
      copyCode,
      copyLink,
      h('a', { class: 'btn btn-sm', href: `https://wa.me/?text=${encodeURIComponent(whatsappText)}`, target: '_blank', rel: 'noopener noreferrer' }, t('أرسل عبر واتساب')),
      h('button', { type: 'button', class: 'btn btn-ghost btn-sm', onclick: renew }, icon('refresh', 16), t('كود جديد'))));
  return body;
}

function packageCard(coach) {
  const free = Math.max(0, coach.maxClients - coach.activeClients);
  return h('section', { class: 'card stack' },
    h('h2', { text: t('باقتي') }),
    h('div', { class: 'stat-grid' },
      stat(coach.activeClients, t('متدربون الآن')),
      stat(free, t('مقاعد متاحة')),
      stat(coach.maxClients, t('حجم الباقة')),
      stat(formatDate(`${lastDayIso(coach.activeUntilUtc)}T12:00:00`), t('آخر يوم'))),
    !coach.isActive && h('p', { class: 'notice', text: t('الباقة منتهية. جدّدها مع إدارة هِمّة.') }),
    coach.isActive && free === 0 && h('p', { class: 'notice', text: t('امتلأت المقاعد. لزيادتها تواصل مع إدارة هِمّة.') }));
}

function stat(value, label) {
  return h('div', { class: 'stat' }, h('span', { class: 'value', text: typeof value === 'number' ? fmt(value) : value }), h('span', { class: 'label', text: label }));
}

function profileCard(coach, reload) {
  const name = h('input', { type: 'text', maxlength: 60, value: coach.businessName });
  const bio = h('textarea', { maxlength: 300, rows: 3, placeholder: t('مثال: مدرب معتمد، تخصصي خسارة الدهون وبناء العضل للمبتدئين.') });
  bio.value = coach.bio ?? '';
  const status = h('div');
  const save = h('button', { type: 'submit', class: 'btn' }, t('حفظ'));
  const form = h('form', { class: 'stack', novalidate: true },
    h('h2', { text: t('كيف يراك المتدربون') }),
    field(t('الاسم التجاري'), name, { name: 'businessName' }),
    field(t('نبذة قصيرة'), bio, { name: 'bio', hint: t('تظهر للمتدرب قبل أن يوافق على الانضمام.') }),
    status,
    save);
  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    clearErrors(form);
    fill(status);
    save.disabled = true;
    try {
      await put('/coach/me', { businessName: name.value, bio: bio.value });
      showSaved();
      reload();
    } catch (error) {
      const message = showErrors(form, error);
      if (message) fill(status, calmNotice(message));
    } finally {
      save.disabled = false;
    }
  });
  return h('section', { class: 'card' }, form);
}
