import { t } from '../i18n/index.js';
import { get } from '../api.js';
import { formatShort } from '../dates.js';
import { muscles } from '../labels.js';
import { h, fill, add, fmt } from '../ui/dom.js';
import { field, numberInput, readNumber, showErrors, clearErrors } from '../ui/forms.js';
import { calmNotice, loadInto } from '../ui/notice.js';

/** Personal records per exercise, and a one-rep-max calculator with a load chart. */
export function mount(view) {
  const listCard = h('div', { class: 'card' });
  add(view,
    h('header', { class: 'view-head' },
      h('h1', { text: t('أرقامي القياسية') }),
      h('a', { class: 'btn btn-ghost btn-sm', href: '#workouts' }, t('رجوع'))),
    calculatorCard(),
    listCard);

  loadInto(listCard, () => get('/workouts/records'), (records) => {
    add(listCard, h('h2', { text: t('أفضل أرقامك') }));
    if (records.length === 0) {
      add(listCard, h('p', { class: 'muted', text: t('سجّل جولات بأوزان في تمارينك، وتظهر أرقامك القياسية هنا.') }));
      return;
    }
    add(listCard, h('ul', { class: 'item-list' }, records.map((r) =>
      h('li', { class: 'item', style: 'align-items:flex-start' },
        h('div', { class: 'grow' },
          h('div', { class: 'title', text: r.exerciseName }),
          h('div', { class: 'muted small', text: t('{muscle} · {n} جلسة', { muscle: muscles[r.muscleGroup] ?? r.muscleGroup, n: r.sessions }) }),
          h('div', { class: 'small' }, t('أثقل وزن: '),
            h('span', { class: 'num', text: t('{kg} كغ × {reps}', { kg: fmt(r.heaviestWeightKg), reps: r.heaviestReps }) }),
            h('span', { class: 'muted', text: ` · ${formatShort(r.heaviestDate)}` })),
          h('div', { class: 'small' }, t('أفضل جولة: '),
            h('span', { class: 'num', text: t('{kg} كغ × {reps}', { kg: fmt(r.bestSetWeightKg), reps: r.bestSetReps }) }),
            h('span', { class: 'muted', text: ` · ${formatShort(r.bestSetDate)}` }))),
        h('div', { class: 'stat', style: 'min-width:84px' },
          h('span', { class: 'value', text: fmt(r.bestOneRepMaxKg) }),
          h('span', { class: 'label', text: t('1RM تقديري') }))))));
  });
}

function calculatorCard() {
  const weight = numberInput(null, { min: 1, max: 500, step: 0.5, placeholder: t('مثال: 100') });
  const reps = numberInput(null, { min: 1, max: 30, step: 1, placeholder: t('مثال: 5') });
  const result = h('div');
  const button = h('button', { type: 'submit', class: 'btn' }, t('احسب'));
  const form = h('form', { class: 'card stack', novalidate: true },
    h('h2', { text: t('حاسبة أقصى وزن (1RM)') }),
    h('p', { class: 'muted small', text: t('اكتب وزن جولة وعدد تكراراتها، ونقدّر أقصى وزن تقدر ترفعه مرة وحدة، بدون ما تخاطر بتجربته.') }),
    h('div', { class: 'grid-2' },
      field(t('الوزن (كغ)'), weight, { name: 'weightKg' }),
      field(t('التكرارات'), reps, { name: 'reps' })),
    button,
    result);

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    clearErrors(form);
    fill(result);
    button.disabled = true;
    try {
      const query = new URLSearchParams({ weightKg: readNumber(weight) ?? '', reps: readNumber(reps) ?? '' });
      const estimate = await get(`/strength/one-rep-max?${query}`);
      fill(result,
        h('div', { class: 'stat' },
          h('span', { class: 'value', text: t('{kg} كغ', { kg: fmt(estimate.oneRepMaxKg) }) }),
          h('span', { class: 'label', text: t('أقصى وزن لتكرار واحد (تقديري)') })),
        !estimate.reliable && calmNotice(t('التقدير أقل دقة فوق 12 تكراراً. جرّب جولة أثقل بتكرارات أقل.')),
        h('table', { class: 'set-table', style: 'margin-top:10px' },
          h('thead', {}, h('tr', {}, h('th', { text: '%' }), h('th', { text: t('الوزن (كغ)') }), h('th', { text: t('تكرارات تقريباً') }))),
          h('tbody', {}, estimate.table.map((row) => h('tr', {},
            h('td', {}, h('span', { class: 'num', text: `${row.percent}%` })),
            h('td', {}, h('span', { class: 'num', text: fmt(row.weightKg) })),
            h('td', {}, h('span', { class: 'num', text: String(row.reps) })))))),
        h('p', { class: 'muted small', text: t('معادلة Epley. استخدم الجدول لاختيار الوزن حسب التكرارات اللي تبيها.') }));
    } catch (error) {
      const message = showErrors(form, error);
      if (message) fill(result, calmNotice(message));
    } finally {
      button.disabled = false;
    }
  });
  return form;
}
