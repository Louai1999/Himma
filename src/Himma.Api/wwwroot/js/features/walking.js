import { t } from '../i18n/index.js';
import { get, post } from '../api.js';
import { todayLocal } from '../dates.js';
import { h, fill, add, fmt } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { field, numberInput, readNumber, showErrors, clearErrors } from '../ui/forms.js';
import { calmNotice, loadInto } from '../ui/notice.js';
import { showSaved } from '../ui/toast.js';

/**
 * Today screen: today's steps (typed from a phone or watch) as distance and calories, the weekly average,
 * and "how much should I walk?" for a calorie goal. The server does every calculation.
 */
export function walkingCard() {
  const card = h('div', { class: 'card' });
  const date = todayLocal();
  load();
  return card;

  function load() {
    loadInto(card, () => get(`/steps?today=${date}`), render);
  }

  function render(data) {
    const today = data.entries.find((e) => e.date === date) ?? null;
    const steps = numberInput(today?.steps ?? null, { min: 0, max: 100000, step: 1, placeholder: t('مثال: 8000') });
    const status = h('div');
    const save = h('button', { type: 'submit', class: 'btn btn-sm' }, t('حفظ'));
    const form = h('form', { class: 'row', novalidate: true, style: 'align-items:flex-end' },
      h('div', { class: 'grow' }, field(t('خطوات اليوم'), steps, { name: 'steps' })),
      save);
    form.addEventListener('submit', async (event) => {
      event.preventDefault();
      clearErrors(form);
      fill(status);
      save.disabled = true;
      try {
        await post('/steps', { date, steps: readNumber(steps) });
        showSaved();
        load();
      } catch (error) {
        const message = showErrors(form, error);
        if (message) fill(status, calmNotice(message));
      } finally {
        save.disabled = false;
      }
    });

    add(card,
      h('h2', { class: 'row' }, icon('flame', 20), t('المشي')),
      form,
      status,
      today && today.distanceKm !== null && h('div', { class: 'grid-3 collapse', style: 'margin-top:10px' },
        stat(fmt(today.distanceKm), t('كم')),
        stat(fmt(today.calories), t('سعرة تقريباً')),
        stat(data.weekAverage === null ? '—' : fmt(data.weekAverage), t('متوسط الأسبوع'))),
      !data.hasProfile && h('p', { class: 'muted small' }, t('أكمل '), h('a', { href: '#profile', text: t('ملفك الشخصي') }), t(' لنحوّل الخطوات إلى مسافة وسعرات.')),
      data.hasProfile && planner());
  }
}

/** "How much should I walk to burn N calories?" */
function planner() {
  const calories = numberInput(null, { min: 10, max: 3000, step: 10, placeholder: t('مثال: 300') });
  const result = h('div');
  const button = h('button', { type: 'submit', class: 'btn btn-secondary btn-sm' }, t('احسب'));
  const form = h('form', { class: 'stack', novalidate: true },
    h('div', { class: 'row', style: 'align-items:flex-end' },
      h('div', { class: 'grow' }, field(t('كم سعرة تبي تحرق بالمشي؟'), calories, { name: 'calories' })),
      button),
    result);

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    clearErrors(form);
    fill(result);
    button.disabled = true;
    try {
      const plan = await get(`/steps/plan?calories=${encodeURIComponent(readNumber(calories) ?? '')}`);
      fill(result,
        h('div', { class: 'grid-3 collapse' },
          stat(fmt(plan.distanceKm), t('كم')),
          stat(fmt(plan.steps), t('خطوة')),
          stat(fmt(plan.minutes), t('دقيقة'))),
        h('p', { class: 'muted small', text: t('تقدير على سرعة مشي عادية (5 كم/ساعة). الحرق الفعلي يختلف حسب السرعة والطلعات.') }));
    } catch (error) {
      const message = showErrors(form, error);
      if (message) fill(result, calmNotice(message));
    } finally {
      button.disabled = false;
    }
  });

  return h('details', { class: 'inline-form', style: 'margin-top:10px' },
    h('summary', { text: t('كم أمشي؟') }),
    form);
}

function stat(value, label) {
  return h('div', { class: 'stat' }, h('span', { class: 'value', text: value }), h('span', { class: 'label', text: label }));
}
