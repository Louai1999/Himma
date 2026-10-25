import { t } from '../i18n/index.js';
import { get, post, put, del } from '../api.js';
import { emit } from '../state.js';
import { todayLocal, formatTime } from '../dates.js';
import { days as dayLabels } from '../labels.js';
import { h, fill, add, fmt } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { openModal, confirmDialog } from '../ui/modal.js';
import { field, selectOf, numberInput, readNumber, showErrors, clearErrors } from '../ui/forms.js';
import { calmNotice, loadInto } from '../ui/notice.js';
import { showSaved } from '../ui/toast.js';
import { pushControls } from '../push.js';

export const units = { gram: t('غرام'), scoop: t('سكوب'), capsule: t('كبسولة'), tablet: t('حبة'), ml: t('مل') };
const unitShort = { gram: t('غ'), scoop: t('سكوب'), capsule: t('كبسولة'), tablet: t('حبة'), ml: t('مل') };
const ALL_DAYS = Object.keys(dayLabels);
const MAX_TIMES = 6;

export const doseText = (s) => `${fmt(s.dose)} ${unitShort[s.unit] ?? s.unit}`;

function scheduleText(s) {
  const when = s.times.length ? s.times.join('، ') : t('بدون وقت محدد');
  const which = s.days.length === ALL_DAYS.length ? t('كل يوم') : s.days.map((d) => dayLabels[d]).join('، ');
  return `${when} · ${which}`;
}

/**
 * Logs one dose. If today's doses are already logged the server answers 409; the user then confirms
 * an extra dose instead of doubling it by accident.
 */
async function takeDose(item, date) {
  try {
    await post(`/supplements/${item.supplement.id}/intakes`, { date });
  } catch (error) {
    if (error.status !== 409) throw error;
    const at = error.problem?.takenAtUtc ? formatTime(error.problem.takenAtUtc) : '';
    const ok = await confirmDialog({
      title: t('سجّلت هذه الجرعة اليوم'),
      message: at
        ? t('سجّلت {name} اليوم الساعة {time}. تسجيل جرعة إضافية؟', { name: item.supplement.name, time: at })
        : t('جرعات {name} اليوم مسجّلة. تسجيل جرعة إضافية؟', { name: item.supplement.name }),
      confirmText: t('نعم، جرعة إضافية'),
    });
    if (!ok) return false;
    await post(`/supplements/${item.supplement.id}/intakes`, { date, force: true });
  }
  return true;
}

/** Today screen: today's supplements with "Taken", progress and the streak. */
export function todaySupplementsCard() {
  const card = h('div', { class: 'card' });
  const date = todayLocal();
  load();
  return card;

  function load() {
    loadInto(card, () => get(`/supplements/day/${date}`), render);
  }

  function render({ items }) {
    add(card, h('div', { class: 'card-head' },
      h('h2', { text: t('مكملاتي') }),
      h('a', { class: 'btn btn-ghost btn-sm', href: '#supplements' }, t('إدارة'))));
    if (items.length === 0) {
      add(card,
        h('p', { class: 'muted', text: t('أضف مكملاتك (كرياتين، بروتين، فيتامينات…) ليذكّرك هِمّة بمواعيدها.') }),
        h('a', { class: 'btn btn-secondary btn-sm', href: '#supplements' }, icon('plus', 16), t('إضافة مكمل')));
      return;
    }
    const due = items.filter((i) => i.dosesDue > 0 || i.intakes.length > 0);
    if (due.length === 0) {
      add(card, h('p', { class: 'muted', text: t('لا مكملات مجدولة اليوم.') }));
      return;
    }
    add(card, h('ul', { class: 'item-list' }, due.map(row)));
  }

  function row(item) {
    const s = item.supplement;
    const taken = item.intakes.length;
    const needed = Math.max(item.dosesDue, 1);
    const done = taken >= needed;
    const status = h('div');
    const takeButton = h('button', {
      type: 'button', class: done ? 'btn btn-ghost btn-sm' : 'btn btn-sm', onclick: async () => {
        takeButton.disabled = true;
        fill(status);
        try {
          if (await takeDose(item, date)) {
            showSaved(t('تم تسجيل الجرعة'));
            if (s.proteinG || s.calories) emit('meals-changed', { date });
            load();
          }
        } catch (error) {
          fill(status, calmNotice(error));
        } finally {
          takeButton.disabled = false;
        }
      },
    }, done ? t('جرعة إضافية') : [icon('check', 16), t('أخذتها')]);

    const last = item.intakes[item.intakes.length - 1];
    const undo = last && h('button', {
      type: 'button', class: 'btn btn-ghost btn-sm', onclick: async () => {
        try {
          await del(`/supplements/intakes/${last.id}`);
          showSaved(t('تم التراجع'));
          if (s.proteinG || s.calories) emit('meals-changed', { date });
          load();
        } catch (error) {
          fill(status, calmNotice(error));
        }
      },
    }, t('تراجع'));

    return h('li', { class: 'item', style: 'flex-wrap:wrap' },
      h('div', { class: 'grow' },
        h('div', { class: 'title' }, done ? '✅ ' : '⏳ ', s.name),
        h('div', { class: 'muted small' },
          doseText(s),
          s.times.length > 0 && ` · ${s.times.join('، ')}`,
          ' · ', h('span', { class: 'num', text: `${taken}/${needed}` }),
          item.streak > 1 && h('span', {}, ' · 🔥 ', t('{n} يوم متتالي', { n: item.streak })))),
      done && h('p', { class: 'small', style: 'flex-basis:100%;margin:0', text: t('✅ أخذت جرعتك اليومية من {name}', { name: s.name }) }),
      h('div', { class: 'row' }, undo, takeButton),
      status);
  }
}

/** Supplements screen: the list, add/edit/delete, and notifications for this device. */
export function mount(view) {
  const listCard = h('div', { class: 'card' });
  add(view,
    h('header', { class: 'view-head' },
      h('h1', { text: t('مكملاتي') }),
      h('a', { class: 'btn btn-ghost btn-sm', href: '#today' }, t('رجوع لليوم'))),
    h('div', { class: 'card stack' }, h('h2', { text: t('التنبيهات') }), pushControls()),
    listCard,
    h('p', { class: 'muted small', text: t('هِمّة لا يقترح جرعات. اكتب الجرعة التي تستخدمها، واستشر مختصاً قبل أي مكمل.') }));
  load();

  function load() {
    loadInto(listCard, () => get('/supplements'), render);
  }

  function render(list) {
    add(listCard,
      h('div', { class: 'card-head' },
        h('h2', { text: t('القائمة') }),
        h('button', { type: 'button', class: 'btn btn-sm', onclick: () => openSupplementForm({ onSaved: load }) }, icon('plus', 16), t('إضافة مكمل'))),
      list.length === 0
        ? h('p', { class: 'muted', text: t('لا مكملات بعد.') })
        : h('ul', { class: 'item-list' }, list.map((s) =>
          h('li', { class: 'item' },
            h('div', { class: 'grow' },
              h('div', { class: 'title', text: s.name }),
              h('div', { class: 'muted small', text: `${doseText(s)} · ${scheduleText(s)}` }),
              s.notes && h('div', { class: 'muted small', text: s.notes }),
              (s.proteinG || s.calories) && h('div', { class: 'muted small', text: t('يُضاف لأكلك: {p} غ بروتين، {k} سعرة', { p: fmt(s.proteinG ?? 0), k: fmt(s.calories ?? 0) }) })),
            h('button', { type: 'button', class: 'icon-btn', 'aria-label': t('تعديل {name}', { name: s.name }), onclick: () => openSupplementForm({ supplement: s, onSaved: load }) }, icon('edit', 18)),
            h('button', { type: 'button', class: 'icon-btn', 'aria-label': t('حذف {name}', { name: s.name }), onclick: () => remove(s) }, icon('trash', 18))))));
  }

  async function remove(s) {
    const ok = await confirmDialog({ title: t('حذف مكمل'), message: t('حذف "{name}" وسجل جرعاته؟ الوجبات المسجلة تبقى.', { name: s.name }), confirmText: t('حذف'), danger: true });
    if (!ok) return;
    try {
      await del(`/supplements/${s.id}`);
      showSaved(t('تم الحذف'));
      load();
    } catch (error) {
      fill(listCard, calmNotice(error, load));
    }
  }
}

function openSupplementForm({ supplement = null, onSaved }) {
  const name = h('input', { type: 'text', maxlength: 60, value: supplement?.name ?? '', placeholder: t('مثال: كرياتين') });
  const dose = numberInput(supplement?.dose, { min: 0.01, max: 5000, step: 'any', placeholder: '5' });
  const unit = selectOf(units, supplement?.unit ?? 'gram');
  const protein = numberInput(supplement?.proteinG, { min: 0, max: 200, step: 0.1, placeholder: t('اختياري') });
  const calories = numberInput(supplement?.calories, { min: 0, max: 2000, step: 1, placeholder: t('اختياري') });
  const notes = h('input', { type: 'text', maxlength: 120, value: supplement?.notes ?? '', placeholder: t('مثال: مع الفطور') });

  const selectedDays = new Set(supplement?.days ?? ALL_DAYS);
  const dayChips = h('div', { class: 'chips', role: 'group', 'aria-label': t('الأيام') }, ALL_DAYS.map((d) => {
    const chip = h('button', { type: 'button', class: 'chip', 'aria-pressed': String(selectedDays.has(d)), text: dayLabels[d] });
    chip.addEventListener('click', () => {
      if (selectedDays.has(d)) selectedDays.delete(d); else selectedDays.add(d);
      chip.setAttribute('aria-pressed', String(selectedDays.has(d)));
    });
    return chip;
  }));

  const timesBox = h('div', { class: 'stack' });
  const addTimeButton = h('button', { type: 'button', class: 'btn btn-ghost btn-sm', onclick: () => addTime('') }, icon('plus', 16), t('إضافة وقت'));
  (supplement ? supplement.times : ['08:00']).forEach(addTime);

  function addTime(value) {
    const input = h('input', { type: 'time', value, required: true, 'aria-label': t('وقت التذكير') });
    const rowEl = h('div', { class: 'row' }, input,
      h('button', { type: 'button', class: 'icon-btn', 'aria-label': t('حذف الوقت'), onclick: () => { rowEl.remove(); updateAddButton(); } }, icon('trash', 18)));
    add(timesBox, rowEl);
    updateAddButton();
  }
  function updateAddButton() { addTimeButton.hidden = timesBox.children.length >= MAX_TIMES; }

  const status = h('div');
  const button = h('button', { type: 'submit', class: 'btn' }, supplement ? t('حفظ التعديل') : t('إضافة'));
  const form = h('form', { class: 'stack', novalidate: true },
    field(t('الاسم'), name, { name: 'name' }),
    h('div', { class: 'grid-2' }, field(t('الجرعة'), dose, { name: 'dose' }), field(t('الوحدة'), unit, { name: 'unit' })),
    h('div', { class: 'field', dataset: { field: 'times' } },
      h('span', { class: 'label', text: t('أوقات التذكير') }),
      h('span', { class: 'hint', text: t('كل وقت = جرعة. اتركها فارغة إذا ما لها وقت محدد.') }),
      timesBox, addTimeButton,
      h('span', { class: 'field-error' })),
    h('div', { class: 'field', dataset: { field: 'days' } },
      h('span', { class: 'label', text: t('الأيام') }),
      dayChips,
      h('span', { class: 'field-error' })),
    h('details', { class: 'inline-form', open: Boolean(supplement?.proteinG || supplement?.calories) },
      h('summary', { text: t('يحتوي بروتين أو سعرات؟ (مثل البروتين باودر)') }),
      h('p', { class: 'muted small', text: t('عند تسجيل الجرعة تُضاف هذه القيم لأكلك اليوم تلقائياً.') }),
      h('div', { class: 'grid-2' },
        field(t('بروتين لكل جرعة (غ)'), protein, { name: 'proteinG' }),
        field(t('سعرات لكل جرعة'), calories, { name: 'calories' }))),
    field(t('ملاحظة'), notes, { name: 'notes' }),
    status,
    button);

  const modal = openModal({ title: supplement ? t('تعديل مكمل') : t('إضافة مكمل'), content: form });

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    clearErrors(form);
    fill(status);
    const times = [...timesBox.querySelectorAll('input')].map((i) => i.value).filter(Boolean);
    const body = {
      name: name.value,
      dose: readNumber(dose),
      unit: unit.value,
      times,
      days: ALL_DAYS.filter((d) => selectedDays.has(d)),
      proteinG: readNumber(protein),
      calories: readNumber(calories),
      notes: notes.value.trim() || null,
    };
    button.disabled = true;
    try {
      if (supplement) await put(`/supplements/${supplement.id}`, body);
      else await post('/supplements', body);
      showSaved();
      modal.close();
      onSaved();
    } catch (error) {
      const message = showErrors(form, error);
      if (message) fill(status, calmNotice(message));
    } finally {
      button.disabled = false;
    }
  });
}
