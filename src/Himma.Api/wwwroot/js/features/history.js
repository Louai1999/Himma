import { t } from '../i18n/index.js';
import { get } from '../api.js';
import { todayLocal, addDays, formatLong, formatShort, formatTime, parseIso } from '../dates.js';
import { mealTypes } from '../labels.js';
import { h, fill, add, fmt } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { ring, lineChart, barChart } from '../ui/charts.js';
import { loadInto } from '../ui/notice.js';

const DAY_MS = 24 * 60 * 60 * 1000;

/** History: browse any past day, 30-day adherence, weight chart and 14-day calories. */
export function mount(view) {
  const today = todayLocal();
  let selected = today;

  const dayInput = h('input', { type: 'date', value: today, max: today, 'aria-label': t('اختر يوماً') });
  const prevButton = h('button', { type: 'button', class: 'icon-btn', 'aria-label': t('اليوم السابق'), onclick: () => select(addDays(selected, -1)) }, icon('prev', 20, 'flip-ltr'));
  const nextButton = h('button', { type: 'button', class: 'icon-btn', 'aria-label': t('اليوم التالي'), onclick: () => select(addDays(selected, 1)) }, icon('next', 20, 'flip-ltr'));
  const dayTitle = h('h2');
  const dayBody = h('div');
  const summaryCard = h('div', { class: 'card' });
  const weightCard = h('div', { class: 'card' });
  const caloriesCard = h('div', { class: 'card' });

  dayInput.addEventListener('change', () => { if (dayInput.value) select(dayInput.value); });

  add(view,
    h('header', { class: 'view-head' }, h('h1', { text: t('السجل') })),
    h('div', { class: 'card stack' },
      h('div', { class: 'row spread' }, dayTitle, h('div', { class: 'row' }, prevButton, dayInput, nextButton)),
      dayBody),
    summaryCard,
    weightCard,
    caloriesCard);

  select(today);
  loadSummary();

  function select(date) {
    if (date > today) return;
    selected = date;
    dayInput.value = date;
    nextButton.disabled = date >= today;
    dayTitle.textContent = date === today ? t('اليوم') : formatLong(date);
    loadInto(dayBody, () => Promise.all([get(`/meals/${date}`), get(`/water/${date}`)]), renderDay);
  }

  function renderDay([meals, water]) {
    if (meals.items.length === 0 && water.entries.length === 0) {
      add(dayBody, h('p', { class: 'muted', text: t('لا يوجد تسجيل في هذا اليوم.') }));
      return;
    }
    add(dayBody,
      h('div', { class: 'grid-3 collapse' },
        stat(meals.totals.calories, t('سعرة')),
        stat(meals.totals.proteinG, t('غ بروتين')),
        stat(water.totalMl, t('مل ماء'))),
      Object.entries(mealTypes).map(([type, label]) => {
        const items = meals.items.filter((m) => m.mealType === type);
        return items.length > 0 && h('section', { class: 'meal-section' },
          h('h3', { text: label }),
          h('ul', { class: 'item-list' }, items.map((m) =>
            h('li', { class: 'item' },
              h('span', { class: 'grow title', text: m.name }),
              h('span', { class: 'meta' }, h('span', { class: 'num', text: fmt(m.calories) }), t(' سعرة'))))));
      }),
      water.entries.length > 0 && h('p', { class: 'muted small', style: 'margin-top:8px' },
        t('الماء: '), water.entries.map((w) => t('{ml} مل ({time})', { ml: w.ml, time: formatTime(w.createdAtUtc) })).join(t('، '))));
  }

  function loadSummary() {
    loadInto(summaryCard, () => Promise.all([
      get(`/history/summary?days=30&today=${today}`),
      get(`/history/summary?days=14&today=${today}`),
    ]), ([month, twoWeeks]) => {
      renderAdherence(month);
      renderWeights(month);
      renderCalories(twoWeeks);
    });
  }

  function renderAdherence(summary) {
    const a = summary.adherence;
    add(summaryCard,
      h('h2', { text: t('التزامك في آخر 30 يوماً') }),
      h('div', { class: 'grid-2', style: 'margin-bottom:12px' },
        stat(summary.streak, summary.streak === 1 ? t('يوم متتالٍ') : t('أيام متتالية')),
        stat(`${fmt(a.consistencyPercent)}%`, t('أيام سجّلت فيها وجبات'))),
      a.calorieAdherencePercent === null
        ? h('p', { class: 'muted small' }, t('أكمل '), h('a', { href: '#profile', text: t('ملفك الشخصي') }), t(' لترى نسب الالتزام بالأهداف.'))
        : h('div', { class: 'rings' },
          ring({ value: a.calorieAdherencePercent, max: 100, label: t('السعرات'), sub: '%' }),
          ring({ value: a.proteinAdherencePercent, max: 100, label: t('البروتين'), sub: '%', color: 'var(--gold)' }),
          ring({ value: a.waterAdherencePercent, max: 100, label: t('الماء'), sub: '%', color: 'var(--water)' })),
      h('p', { class: 'muted small', style: 'margin-top:8px', text: t('نسبة الأيام التي حققت فيها الهدف: السعرات ضمن ±10%، البروتين 90% فأكثر، الماء 80% فأكثر.') }));
  }

  function renderWeights(summary) {
    fill(weightCard,
      h('h2', { text: t('الوزن في آخر 30 يوماً') }),
      lineChart({
        points: summary.weights.map((w) => ({ x: parseIso(w.date).getTime() / DAY_MS, y: w.weightKg, label: formatShort(w.date) })),
        label: t('رسم بياني لوزنك في آخر 30 يوماً'),
      }));
  }

  function renderCalories(summary) {
    const target = summary.targets?.targetCalories ?? null;
    fill(caloriesCard,
      h('h2', { text: t('السعرات في آخر 14 يوماً') }),
      barChart({
        bars: summary.daily.map((d) => ({ label: formatShort(d.date), value: d.calories, ok: d.calorieAdherent })),
        target,
        label: t('رسم بياني للسعرات اليومية في آخر 14 يوماً مقارنة بالهدف'),
      }),
      h('div', { class: 'legend' },
        h('span', {}, h('i', { style: 'background:var(--forest)' }), t('ضمن الهدف')),
        h('span', {}, h('i', { style: 'background:var(--gold)' }), t('خارج الهدف')),
        target !== null && h('span', {}, h('i', { style: 'background:transparent;border-top:2px dashed var(--gold)' }), t('الهدف'))));
  }
}

function stat(value, label) {
  return h('div', { class: 'stat' },
    h('span', { class: 'value', text: typeof value === 'number' ? fmt(value) : value }),
    h('span', { class: 'label', text: label }));
}
