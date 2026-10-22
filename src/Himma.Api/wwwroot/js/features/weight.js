import { get, post } from '../api.js';
import { todayLocal, addDays, formatShort, formatLong, parseIso } from '../dates.js';
import { h, fill, add, fmt } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { lineChart } from '../ui/charts.js';
import { field, numberInput, readNumber, showErrors, clearErrors } from '../ui/forms.js';
import { calmNotice, loadInto } from '../ui/notice.js';
import { showSaved } from '../ui/toast.js';

const DAY_MS = 24 * 60 * 60 * 1000;

/** Weight: log a weight, see progress towards the target and the last 30 days. */
export function mount(view) {
  const progressCard = h('div', { class: 'card' });
  const chartCard = h('div', { class: 'card' });
  const listCard = h('div', { class: 'card' });

  add(view, 
    h('header', { class: 'view-head' }, h('h1', { text: 'الوزن' })),
    logForm(load),
    progressCard,
    chartCard,
    listCard);

  load();

  function load() {
    loadInto(progressCard, () => get('/weight'), (data) => {
      renderProgress(data);
      renderChart(data);
      renderList(data);
    });
  }

  function renderProgress({ startWeightKg: start, latestWeightKg: latest, targetWeightKg: target }) {
    add(progressCard, h('h2', { text: 'تقدمك' }));
    if (latest === null) {
      add(progressCard, h('p', { class: 'muted', text: 'سجّل وزنك الأول لتبدأ متابعة التقدم.' }));
      return;
    }
    add(progressCard, h('div', { class: 'grid-3 collapse' },
      stat(start, 'البداية'), stat(latest, 'الآن'), stat(target, 'الهدف')));
    if (target === null) {
      add(progressCard, h('p', { class: 'muted small', style: 'margin-top:8px' }, 'حدد وزناً مستهدفاً من ', h('a', { href: '#profile', text: 'حسابي' }), ' لترى نسبة التقدم.'));
      return;
    }
    // Share of the way from the starting weight to the target (moving the wrong way counts as 0).
    const done = start === target ? 1 : Math.min(Math.max((start - latest) / (start - target), 0), 1);
    const bar = h('span');
    bar.style.width = '0%';
    requestAnimationFrame(() => requestAnimationFrame(() => { bar.style.width = `${Math.round(done * 100)}%`; }));
    const left = Math.abs(latest - target);
    add(progressCard, 
      h('div', { class: 'progress', role: 'progressbar', 'aria-valuemin': 0, 'aria-valuemax': 100, 'aria-valuenow': Math.round(done * 100), 'aria-label': 'التقدم نحو الوزن المستهدف', style: 'margin-top:12px' }, bar),
      h('p', { class: 'muted small', style: 'margin-top:6px' },
        left < 0.05 ? 'وصلت إلى هدفك. أحسنت!' : [`باقي `, h('span', { class: 'num', text: fmt(left) }), ' كغ على هدفك.']));
  }

  function renderChart({ entries, targetWeightKg }) {
    const since = addDays(todayLocal(), -29);
    const recent = entries.filter((e) => e.date >= since);
    fill(chartCard, 
      h('h2', { text: 'آخر 30 يوماً' }),
      lineChart({
        points: recent.map((e) => ({ x: parseIso(e.date).getTime() / DAY_MS, y: e.weightKg, label: formatShort(e.date) })),
        target: targetWeightKg,
        label: 'رسم بياني لوزنك في آخر 30 يوماً',
      }),
      targetWeightKg !== null && h('div', { class: 'legend' }, h('span', {}, h('i', { style: 'background:var(--gold)' }), 'الوزن المستهدف')));
  }

  function renderList({ entries }) {
    const recent = entries.slice(-10).reverse();
    fill(listCard, 
      h('h2', { text: 'آخر التسجيلات' }),
      recent.length === 0
        ? h('p', { class: 'muted', text: 'لا توجد تسجيلات بعد.' })
        : h('ul', { class: 'item-list' }, recent.map((e) =>
          h('li', { class: 'item' },
            h('span', { class: 'grow', text: formatLong(e.date) }),
            h('span', {}, h('span', { class: 'num', text: fmt(e.weightKg) }), ' كغ')))));
  }
}

function stat(value, label) {
  return h('div', { class: 'stat' },
    h('span', { class: 'value', text: value === null ? '—' : fmt(value) }),
    h('span', { class: 'label', text: value === null ? label : `${label} (كغ)` }));
}

function logForm(onSaved) {
  const date = h('input', { type: 'date', value: todayLocal(), max: todayLocal(), required: true });
  const weight = numberInput(null, { min: 30, max: 300, step: 0.1, placeholder: 'مثال: 78.5' });
  const status = h('div');
  const button = h('button', { type: 'submit', class: 'btn' }, icon('plus', 16), 'حفظ الوزن');
  const form = h('form', { class: 'card stack', novalidate: true },
    h('h2', { text: 'سجّل وزنك' }),
    h('div', { class: 'grid-2' }, field('التاريخ', date, { name: 'date' }), field('الوزن (كغ)', weight, { name: 'weightKg' })),
    h('p', { class: 'muted small', text: 'تسجيل نفس اليوم مرة أخرى يستبدل القيمة السابقة.' }),
    status,
    button);

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    clearErrors(form);
    fill(status);
    button.disabled = true;
    try {
      await post('/weight', { date: date.value, weightKg: readNumber(weight) });
      weight.value = '';
      showSaved();
      onSaved();
    } catch (error) {
      const message = showErrors(form, error);
      if (message) fill(status, calmNotice(message));
    } finally {
      button.disabled = false;
    }
  });
  return form;
}
