import { t } from '../i18n/index.js';
import { get, post, api } from '../api.js';
import { formatDate, formatDateTime, formatShort, parseIso, todayLocal } from '../dates.js';
import { genders, activities, goals, mealTypes } from '../labels.js';
import { h, fill, add, fmt } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { ring, lineChart, barChart } from '../ui/charts.js';
import { confirmDialog } from '../ui/modal.js';
import { calmNotice, loadInto } from '../ui/notice.js';
import { showSaved } from '../ui/toast.js';
import { downloadJson } from '../ui/download.js';
import { navigate } from '../router.js';
import { statusBadge, stat, percent } from './labels.js';
import { openPasswordAction } from './actions.js';

const DAY_MS = 24 * 60 * 60 * 1000;

/** One subscriber: account, profile, adherence, weight, last 14 days, workouts, meals and the danger zone. */
export function mount(view, [id]) {
  const body = h('div', { class: 'stack' });
  add(view, h('a', { class: 'btn btn-ghost btn-sm back-link', href: '#users' }, icon('next', 16, 'flip-ltr'), t('كل المشتركين')), body);
  const load = () => loadInto(body, () => get(`/admin/users/${encodeURIComponent(id)}`), (user) => render(body, user, load));
  load();
}

function render(body, user, reload) {
  add(body,
    accountCard(user),
    profileCard(user),
    adherenceCard(user),
    h('div', { class: 'grid-2 wide-only' }, weightCard(user), workoutCard(user)),
    lastDaysCard(user),
    mealsCard(user),
    dangerZone(user, reload));
}

function accountCard(user) {
  return h('section', { class: 'card stack' },
    h('div', { class: 'row spread' },
      h('div', {},
        h('h1', { text: user.displayName }),
        h('p', { class: 'muted', dir: 'ltr', text: `@${user.username}` })),
      statusBadge(user.status)),
    h('dl', { class: 'facts' },
      fact(t('تاريخ الاشتراك'), formatDate(user.createdAtUtc)),
      fact(t('آخر دخول'), formatDateTime(user.lastLoginAtUtc)),
      fact(t('آخر نشاط'), formatDateTime(user.lastActivityAtUtc)),
      user.deletionScheduledForUtc && fact(t('الحذف النهائي في'), formatDateTime(user.deletionScheduledForUtc))),
    h('div', { class: 'row' },
      h('button', {
        type: 'button', class: 'btn btn-secondary btn-sm', onclick: async (event) => {
          const button = event.currentTarget;
          button.disabled = true;
          try {
            const data = await get(`/admin/users/${encodeURIComponent(user.id)}/export`);
            downloadJson(data, `himma-export-${user.username}-${todayLocal()}.json`);
            showSaved(t('تم التصدير'));
          } catch (error) {
            button.after(calmNotice(error));
          } finally {
            button.disabled = false;
          }
        },
      }, icon('copy', 16), t('تصدير بياناته (JSON)'))));
}

function fact(label, value) {
  return h('div', { class: 'fact' }, h('dt', { text: label }), h('dd', { text: value }));
}

function profileCard(user) {
  const card = h('section', { class: 'card' }, h('h2', { text: t('الملف الشخصي والأهداف') }));
  if (!user.profile) {
    add(card, h('p', { class: 'muted', text: t('لم يُعدّ الملف الشخصي بعد.') }));
    return card;
  }
  const p = user.profile;
  add(card,
    h('dl', { class: 'facts' },
      fact(t('الجنس'), genders[p.gender]),
      fact(t('العمر'), fmt(p.age)),
      fact(t('الطول (سم)'), fmt(p.heightCm)),
      fact(t('الوزن الحالي (كغ)'), fmt(p.weightKg)),
      fact(t('الوزن المستهدف (اختياري)'), p.targetWeightKg === null ? '—' : fmt(p.targetWeightKg)),
      fact(t('هدفك'), goals[p.goal]),
      fact(t('مستوى النشاط'), activities[p.activityLevel])),
    h('div', { class: 'grid-3' },
      stat(user.targets.targetCalories, t('سعرة يومياً')),
      stat(user.targets.proteinTargetG, t('غ بروتين')),
      stat(user.targets.waterTargetMl, t('مل ماء'))));
  return card;
}

function adherenceCard(user) {
  const a = user.adherence30;
  return h('section', { class: 'card' },
    h('h2', { text: t('الالتزام في آخر 30 يوماً') }),
    h('div', { class: 'grid-3', style: 'margin-bottom:12px' },
      stat(user.streak, t('أيام متتالية')),
      stat(percent(a.consistencyPercent), t('الانتظام')),
      stat(a.loggedDays, t('أيام سجّل فيها وجبات'))),
    a.calorieAdherencePercent === null
      ? h('p', { class: 'muted small', text: t('لا توجد أهداف لأنه لم يُعدّ ملفه الشخصي.') })
      : h('div', { class: 'rings' },
        ring({ value: a.calorieAdherencePercent, max: 100, label: t('السعرات'), sub: '%' }),
        ring({ value: a.proteinAdherencePercent, max: 100, label: t('البروتين'), sub: '%', color: 'var(--gold)' }),
        ring({ value: a.waterAdherencePercent, max: 100, label: t('الماء'), sub: '%', color: 'var(--water)' })));
}

function weightCard(user) {
  const change = user.weightChange30Kg;
  return h('section', { class: 'card' },
    h('h2', { text: t('الوزن في آخر 30 يوماً') }),
    change !== null && h('p', { class: 'muted small' },
      t('التغير: '), h('span', { class: 'num', text: `${change > 0 ? '+' : ''}${fmt(change)}` }), t(' كغ')),
    lineChart({
      points: user.weights30.map((w) => ({ x: parseIso(w.date).getTime() / DAY_MS, y: w.weightKg, label: formatShort(w.date) })),
      target: user.profile?.targetWeightKg ?? null,
      label: t('رسم بياني لوزن المشترك في آخر 30 يوماً'),
    }));
}

function workoutCard(user) {
  const w = user.workouts;
  return h('section', { class: 'card' },
    h('h2', { text: t('الالتزام بالتمارين') }),
    h('dl', { class: 'facts' },
      fact(t('الخطة النشطة'), w.activePlanName ?? '—'),
      fact(t('أيام التمرين في الأسبوع'), fmt(w.trainingDaysPerWeek)),
      fact(t('تمارين مكتملة (30 يوماً)'), fmt(w.completedSessions30)),
      fact(t('المخطط لها (30 يوماً)'), fmt(w.plannedSessions30))),
    w.compliancePercent !== null && ring({ value: w.compliancePercent, max: 100, label: t('نسبة الالتزام'), sub: '%', color: 'var(--rust)' }));
}

function lastDaysCard(user) {
  const target = user.targets?.targetCalories ?? null;
  return h('section', { class: 'card' },
    h('h2', { text: t('السعرات في آخر 14 يوماً') }),
    barChart({
      bars: user.last14Days.map((d) => ({ label: formatShort(d.date), value: d.calories, ok: d.calorieAdherent })),
      target,
      label: t('رسم بياني للسعرات اليومية في آخر 14 يوماً مقارنة بالهدف'),
    }),
    h('div', { class: 'table-scroll' },
      h('table', { class: 'data-table compact' },
        h('thead', {}, h('tr', {}, [t('التاريخ'), t('السعرات'), t('البروتين'), t('الماء'), t('الوجبات')].map((label) => h('th', { scope: 'col', text: label })))),
        h('tbody', {}, user.last14Days.slice().reverse().map((d) => h('tr', {},
          h('td', { text: formatShort(d.date) }),
          h('td', { class: 'num', text: fmt(d.calories) }),
          h('td', { class: 'num', text: fmt(d.proteinG) }),
          h('td', { class: 'num', text: fmt(d.waterMl) }),
          h('td', { class: 'num', text: fmt(d.mealCount) })))))));
}

/** Reading a subscriber's meals is audit-logged on the server. */
function mealsCard(user) {
  const date = h('input', { type: 'date', value: todayLocal(), max: todayLocal(), 'aria-label': t('اختر يوماً') });
  const result = h('div');
  const show = () => loadInto(result, () => get(`/admin/users/${encodeURIComponent(user.id)}/meals?date=${date.value}`), (day) => {
    if (day.items.length === 0) {
      add(result, h('p', { class: 'muted', text: t('لا يوجد تسجيل في هذا اليوم.') }));
      return;
    }
    add(result,
      h('p', { class: 'muted small' }, h('span', { class: 'num', text: fmt(day.totals.calories) }), t(' سعرة · '),
        h('span', { class: 'num', text: fmt(day.totals.proteinG) }), t(' غ بروتين')),
      h('ul', { class: 'item-list' }, day.items.map((m) => h('li', { class: 'item' },
        h('div', { class: 'grow' },
          h('div', { class: 'title', text: m.name }),
          h('div', { class: 'meta', text: `${mealTypes[m.mealType]} · ${t('{grams} غ', { grams: fmt(m.grams) })}` })),
        h('span', { class: 'num', text: fmt(m.calories) })))));
  });

  return h('section', { class: 'card stack' },
    h('h2', { text: t('وجبات يوم معيّن') }),
    h('p', { class: 'muted small', text: t('فتح وجبات المشترك يُسجَّل في سجل المراجعة.') }),
    h('div', { class: 'row' }, date, h('button', { type: 'button', class: 'btn btn-secondary btn-sm', onclick: show }, t('عرض'))),
    result);
}

function dangerZone(user, reload) {
  const status = h('div');
  const base = `/admin/users/${encodeURIComponent(user.id)}`;
  const done = (message) => { showSaved(message); reload(); };
  const fail = (error) => fill(status, calmNotice(error));

  const suspend = () => openPasswordAction({
    title: t('إيقاف الحساب'),
    message: t('لن يستطيع {name} الدخول، وسيُسجَّل خروجه من كل الأجهزة فوراً. يمكنك إعادة تفعيله لاحقاً.', { name: user.username }),
    confirmText: t('إيقاف الحساب'),
    onConfirm: async (body) => { await post(`${base}/suspend`, body); done(t('تم إيقاف الحساب')); },
  });

  const softDelete = () => openPasswordAction({
    title: t('حذف الحساب'),
    message: t('سيُخفى الحساب ويُسجَّل خروجه فوراً، ثم يُحذف نهائياً بعد 30 يوماً. يمكنك استرجاعه خلال هذه المدة.'),
    confirmText: t('حذف الحساب'),
    onConfirm: async (body) => { await api(base, { method: 'DELETE', body }); done(t('جُدول الحساب للحذف')); },
  });

  const purge = () => openPasswordAction({
    title: t('حذف نهائي'),
    message: t('سيُمسح حساب {name} وكل بياناته الآن، ولا يمكن استرجاعها:', { name: user.username }),
    details: [
      t('الحساب والملف الشخصي'),
      t('كل الوجبات والماء والوزن'),
      t('الأطعمة والتمارين الخاصة'),
      t('خطط التمرين وسجل التمارين'),
    ],
    irreversible: true,
    requireUsername: true,
    username: user.username,
    confirmText: t('احذف نهائياً'),
    onConfirm: async (body) => {
      await post(`${base}/purge`, body);
      showSaved(t('تم الحذف النهائي'));
      navigate('#users');
    },
  });

  const simple = async (path, title, message, saved) => {
    if (!(await confirmDialog({ title, message, confirmText: title }))) return;
    try {
      await post(`${base}/${path}`);
      done(saved);
    } catch (error) {
      fail(error);
    }
  };

  const buttons = [];
  if (user.status === 'active') {
    buttons.push(h('button', { type: 'button', class: 'btn btn-secondary', onclick: suspend }, t('إيقاف الحساب')));
  }
  if (user.status === 'suspended') {
    buttons.push(h('button', {
      type: 'button', class: 'btn btn-secondary',
      onclick: () => simple('reactivate', t('إعادة التفعيل'), t('سيتمكن المشترك من الدخول مرة أخرى.'), t('تمت إعادة التفعيل')),
    }, t('إعادة التفعيل')));
  }
  if (user.status === 'pendingDeletion') {
    buttons.push(h('button', {
      type: 'button', class: 'btn btn-secondary',
      onclick: () => simple('restore', t('استرجاع الحساب'), t('سيُلغى الحذف ويعود الحساب نشطاً.'), t('تم استرجاع الحساب')),
    }, t('استرجاع الحساب')));
  } else {
    buttons.push(h('button', { type: 'button', class: 'btn btn-danger', onclick: softDelete }, t('حذف الحساب')));
  }
  buttons.push(h('button', { type: 'button', class: 'btn btn-danger strong', onclick: purge }, icon('trash', 16), t('حذف نهائي')));

  return h('section', { class: 'card danger-zone stack' },
    h('h2', { text: t('منطقة الخطر') }),
    h('p', { class: 'muted small', text: t('كل إجراء هنا يُسجَّل في سجل المراجعة. الإيقاف والحذف يطلبان كلمة مرورك.') }),
    h('div', { class: 'row' }, buttons),
    status);
}
