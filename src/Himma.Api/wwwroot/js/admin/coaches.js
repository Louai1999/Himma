import { t } from '../i18n/index.js';
import { get, put, api } from '../api.js';
import { formatDate, addDays, todayLocal } from '../dates.js';
import { h, fill, add, fmt } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { field, numberInput, readNumber, showErrors, clearErrors } from '../ui/forms.js';
import { confirmDialog } from '../ui/modal.js';
import { calmNotice, loadInto } from '../ui/notice.js';
import { showSaved } from '../ui/toast.js';
import { navigate } from '../router.js';

// Selling the coach package: the admin switches it on for a subscriber with the seats and end date
// that were paid for. The coach then manages their clients at /coach.html.

/** Ready-made packages, so a sale takes two clicks. The admin can still change any number. */
const PRESETS = [
  { label: t('مدرب فردي · 15 متدرب · شهر'), seats: 15, days: 30 },
  { label: t('مدرب محترف · 40 متدرب · شهر'), seats: 40, days: 30 },
  { label: t('نادي رياضي · 150 متدرب · سنة'), seats: 150, days: 365 },
];

/** Tab: every coach package, the ones ending soonest first. */
export function mount(view) {
  const body = h('div', { class: 'stack' });
  add(view,
    h('header', { class: 'view-head' }, h('h1', { text: t('المدربون') })),
    h('p', { class: 'muted small', text: t('لتفعيل باقة لمدرب جديد: افتح حسابه من قائمة المشتركين، ثم «باقة المدرب».') }),
    body);
  loadInto(body, () => get('/admin/coaches'), (coaches) => render(body, coaches));
}

function render(body, coaches) {
  if (coaches.length === 0) {
    add(body, h('div', { class: 'card empty', text: t('لا توجد باقات مدربين بعد.') }));
    return;
  }
  const active = coaches.filter((c) => c.isActive);
  const seats = active.reduce((sum, c) => sum + c.activeClients, 0);
  add(body,
    h('div', { class: 'stat-grid' },
      stat(active.length, t('باقات فعّالة')),
      stat(coaches.length - active.length, t('باقات منتهية')),
      stat(seats, t('متدربون عند المدربين')),
      stat(active.filter((c) => endsSoon(c)).length, t('تنتهي خلال 7 أيام'))),
    h('div', { class: 'card table-card' },
      h('div', { class: 'table-scroll' },
        h('table', { class: 'data-table' },
          h('thead', {}, h('tr', {},
            [t('المدرب'), t('الاسم التجاري'), t('المتدربون'), t('ينتهي في'), t('الحالة')].map((label) => h('th', { scope: 'col', text: label })))),
          h('tbody', {}, coaches.map(row))))));
}

function stat(value, label) {
  return h('div', { class: 'stat' }, h('span', { class: 'value', text: fmt(value) }), h('span', { class: 'label', text: label }));
}

function endsSoon(coach) {
  return new Date(coach.activeUntilUtc).getTime() - Date.now() < 7 * 24 * 60 * 60 * 1000;
}

function packageBadge(coach) {
  if (!coach.isActive) return h('span', { class: 'badge badge-pendingDeletion', text: t('منتهية') });
  if (endsSoon(coach)) return h('span', { class: 'badge badge-suspended', text: t('تنتهي قريباً') });
  return h('span', { class: 'badge badge-active', text: t('فعّالة') });
}

function row(coach) {
  const href = `#user/${encodeURIComponent(coach.userId)}`;
  const tr = h('tr', { class: 'clickable' },
    h('td', {},
      h('a', { href, class: 'user-link' }, h('strong', { text: coach.displayName })),
      h('div', { class: 'muted small', dir: 'ltr', text: `@${coach.username}` })),
    h('td', { text: coach.businessName }),
    h('td', { class: 'num', text: `${fmt(coach.activeClients)} / ${fmt(coach.maxClients)}` }),
    h('td', { text: lastDay(coach.activeUntilUtc) }),
    h('td', {}, packageBadge(coach)));
  tr.addEventListener('click', (event) => { if (!event.target.closest('a')) navigate(href); });
  return tr;
}

/** The package ends at midnight UTC after its last day; this is that last day as yyyy-MM-dd. */
export function lastDayIso(activeUntilUtc) {
  return new Date(new Date(activeUntilUtc).getTime() - 1).toISOString().slice(0, 10);
}

function lastDay(activeUntilUtc) {
  return formatDate(`${lastDayIso(activeUntilUtc)}T12:00:00`);
}

/** Card on a subscriber's page: switch the package on, extend it, change seats, or remove it. */
export function coachPackageCard(user) {
  const card = h('section', { class: 'card stack' });
  const load = () => {
    fill(card, h('h2', { text: t('باقة المدرب') }));
    const slot = h('div');
    add(card, slot);
    get(`/admin/users/${encodeURIComponent(user.id)}/coach`)
      .then((coach) => { fill(slot); renderPackage(slot, user, coach, load); })
      .catch((error) => {
        fill(slot);
        if (error.status === 404) renderPackage(slot, user, null, load);
        else add(slot, calmNotice(error, load));
      });
  };
  load();
  return card;
}

function renderPackage(slot, user, coach, reload) {
  const base = `/admin/users/${encodeURIComponent(user.id)}/coach`;
  const name = h('input', { type: 'text', maxlength: 60, value: coach?.businessName ?? user.displayName });
  const seats = numberInput(coach?.maxClients ?? 15, { min: 1, max: 500, step: 1 });
  const until = h('input', { type: 'date', value: coach ? lastDayIso(coach.activeUntilUtc) : addDays(todayLocal(), 30) });
  const status = h('div');

  const presetButtons = PRESETS.map((p) => h('button', {
    type: 'button', class: 'btn btn-secondary btn-sm', onclick: () => {
      seats.value = String(p.seats);
      // Extending an active package adds the time to its current end, so no paid day is lost.
      const from = coach?.isActive ? until.value : todayLocal();
      until.value = addDays(from, p.days);
    },
  }, p.label));

  const save = h('button', { type: 'submit', class: 'btn' }, coach ? t('حفظ التعديلات') : t('تفعيل الباقة'));
  const form = h('form', { class: 'stack', novalidate: true },
    coach && h('p', { class: 'muted small' },
      t('الحالة: '), packageBadge(coach), ' · ',
      t('{used} من {max} متدرب', { used: fmt(coach.activeClients), max: fmt(coach.maxClients) }), ' · ',
      t('كود الدعوة: '), h('span', { class: 'num', dir: 'ltr', text: coach.inviteCode })),
    !coach && h('p', { class: 'muted small', text: t('فعّل الباقة بعد أن يدفع المدرب. سيظهر له رابط «لوحة المدرب» في حسابه.') }),
    h('div', { class: 'row wrap' }, presetButtons),
    h('div', { class: 'grid-3' },
      field(t('الاسم التجاري'), name, { name: 'businessName' }),
      field(t('عدد المتدربين'), seats, { name: 'maxClients' }),
      field(t('آخر يوم في الباقة'), until, { name: 'activeUntil' })),
    status,
    h('div', { class: 'row' },
      save,
      coach && h('button', { type: 'button', class: 'btn btn-danger', onclick: remove }, icon('trash', 16), t('إلغاء الباقة'))));

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    clearErrors(form);
    fill(status);
    save.disabled = true;
    try {
      await put(base, { businessName: name.value.trim(), maxClients: readNumber(seats) ?? 0, activeUntil: until.value });
      showSaved(coach ? t('تم تحديث الباقة') : t('تم تفعيل باقة المدرب'));
      reload();
    } catch (error) {
      const message = showErrors(form, error);
      if (message) fill(status, calmNotice(message));
    } finally {
      save.disabled = false;
    }
  });

  async function remove() {
    const ok = await confirmDialog({
      title: t('إلغاء باقة المدرب'),
      message: t('سيفقد المدرب الوصول إلى لوحة المدرب، وينتهي ارتباطه بكل متدربيه ({count}). بيانات المتدربين تبقى لهم.', { count: fmt(coach.activeClients) }),
      confirmText: t('إلغاء الباقة'),
      danger: true,
    });
    if (!ok) return;
    try {
      await api(base, { method: 'DELETE' });
      showSaved(t('أُلغيت الباقة'));
      reload();
    } catch (error) {
      fill(status, calmNotice(error));
    }
  }

  add(slot, form);
}
