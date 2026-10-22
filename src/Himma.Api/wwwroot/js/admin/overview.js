import { t } from '../i18n/index.js';
import { get } from '../api.js';
import { formatShort } from '../dates.js';
import { h, add } from '../ui/dom.js';
import { ring, barChart } from '../ui/charts.js';
import { loadInto } from '../ui/notice.js';
import { stat, percent } from './labels.js';

/** Overview: subscriber counts, retention, weekly sign-ups and average adherence. */
export function mount(view) {
  const body = h('div', { class: 'stack' });
  add(view, h('header', { class: 'view-head' }, h('h1', { text: t('نظرة عامة') })), body);

  loadInto(body, () => get('/admin/overview'), (o) => {
    add(body,
      h('div', { class: 'stat-grid' },
        stat(o.totalUsers, t('كل المشتركين')),
        stat(o.newLast7Days, t('جدد في 7 أيام')),
        stat(o.newLast30Days, t('جدد في 30 يوماً')),
        stat(o.activeLast7Days, t('نشطون في 7 أيام')),
        stat(o.suspended, t('موقوفون')),
        stat(o.pendingDeletion, t('بانتظار الحذف')),
        stat(percent(o.retentionPercent), t('الاستمرار'), t('من مضى على تسجيلهم أكثر من 30 يوماً وكانوا نشطين في آخر 7 أيام')),
        stat(percent(o.avgConsistencyPercent), t('متوسط الانتظام'))),

      h('section', { class: 'card' },
        h('h2', { text: t('المشتركون الجدد أسبوعياً') }),
        barChart({
          bars: o.weeklySignups.map((w) => ({ label: formatShort(w.weekStart), value: w.count, ok: true })),
          label: t('رسم بياني لعدد المشتركين الجدد في كل أسبوع خلال آخر 12 أسبوعاً'),
        })),

      h('section', { class: 'card' },
        h('h2', { text: t('متوسط الالتزام في آخر 30 يوماً') }),
        h('div', { class: 'rings rings-4' },
          ring({ value: o.avgCalorieAdherencePercent ?? 0, max: 100, label: t('السعرات'), sub: '%' }),
          ring({ value: o.avgProteinAdherencePercent ?? 0, max: 100, label: t('البروتين'), sub: '%', color: 'var(--gold)' }),
          ring({ value: o.avgWaterAdherencePercent ?? 0, max: 100, label: t('الماء'), sub: '%', color: 'var(--water)' }),
          ring({ value: o.avgConsistencyPercent ?? 0, max: 100, label: t('الانتظام'), sub: '%', color: 'var(--forest-light)' })),
        h('p', { class: 'muted small', text: t('متوسط نسبة الأيام التي حقق فيها المشتركون أهدافهم (لمن أعدّ ملفه الشخصي)، ونسبة الأيام التي سجّلوا فيها وجبات.') })));
  });
}
