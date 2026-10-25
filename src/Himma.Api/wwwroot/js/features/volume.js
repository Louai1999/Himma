import { t } from '../i18n/index.js';
import { get } from '../api.js';
import { todayLocal, formatShort } from '../dates.js';
import { muscles } from '../labels.js';
import { h, add, fmt } from '../ui/dom.js';
import { loadInto } from '../ui/notice.js';

const statusText = {
  low: t('قليل'),
  good: t('مناسب'),
  high: t('عالي'),
};
const statusColor = { low: 'var(--gold)', good: 'var(--forest)', high: 'var(--rust)' };

/** Hard sets per muscle this week against the 10–20 range, plus the last weeks. Counted by the server. */
export function mount(view) {
  const weekCard = h('div', { class: 'card' });
  const historyCard = h('div', { class: 'card' });
  add(view,
    h('header', { class: 'view-head' },
      h('h1', { text: t('الحجم التدريبي') }),
      h('a', { class: 'btn btn-ghost btn-sm', href: '#workouts' }, t('رجوع'))),
    weekCard,
    historyCard);

  loadInto(weekCard, () => get(`/workouts/volume?weeks=4&today=${todayLocal()}`), (data) => {
    const current = data.weeks[data.weeks.length - 1];
    add(weekCard,
      h('h2', { text: t('هذا الأسبوع') }),
      h('p', { class: 'muted small', text: t('عدد الجولات المكتملة لكل عضلة من السبت. المدى الشائع للضخامة {min} إلى {max} جولة أسبوعياً.', { min: data.targetMinSets, max: data.targetMaxSets }) }));

    if (current.muscles.length === 0) {
      add(weekCard, h('p', { class: 'muted', text: t('ما سجّلت جولات هالأسبوع بعد.') }));
    } else {
      add(weekCard, h('ul', { class: 'item-list' }, current.muscles.map((m) => muscleRow(m, data.targetMaxSets))));
    }

    const trained = new Set(current.muscles.map((m) => m.muscle));
    const untouched = Object.keys(muscles).filter((m) => !trained.has(m) && m !== 'cardio' && m !== 'fullBody');
    if (untouched.length > 0) {
      add(weekCard, h('p', { class: 'small', style: 'margin-top:10px' },
        h('strong', { text: t('ما تمرّنت هالأسبوع: ') }), untouched.map((m) => muscles[m]).join('، ')));
    }

    add(historyCard,
      h('h2', { text: t('آخر 4 أسابيع') }),
      h('table', { class: 'set-table' },
        h('thead', {}, h('tr', {}, h('th', { text: t('الأسبوع') }), h('th', { text: t('الجولات') }), h('th', { text: t('أكثر عضلة') }))),
        h('tbody', {}, data.weeks.slice().reverse().map((w) => h('tr', {},
          h('td', { text: t('من {date}', { date: formatShort(w.weekStart) }) }),
          h('td', {}, h('span', { class: 'num', text: String(w.muscles.reduce((sum, m) => sum + m.sets, 0)) })),
          h('td', { text: w.muscles[0] ? muscles[w.muscles[0].muscle] ?? w.muscles[0].muscle : '—' }))))));
  });
}

function muscleRow(m, maxSets) {
  const bar = h('span');
  bar.style.width = `${Math.min(100, Math.round((m.sets / (maxSets * 1.25)) * 100))}%`;
  bar.style.background = statusColor[m.status];
  return h('li', { class: 'item', style: 'flex-wrap:wrap' },
    h('div', { class: 'grow' },
      h('div', { class: 'title', text: muscles[m.muscle] ?? m.muscle }),
      h('div', { class: 'muted small', text: t('{sets} جولة · {kg} كغ حجم', { sets: m.sets, kg: fmt(m.volumeKg) }) })),
    h('span', { class: 'tag', style: `color:${statusColor[m.status]}`, text: statusText[m.status] }),
    h('div', { class: 'progress', role: 'progressbar', 'aria-valuemin': 0, 'aria-valuemax': maxSets, 'aria-valuenow': m.sets, 'aria-label': muscles[m.muscle], style: 'flex-basis:100%' }, bar));
}
