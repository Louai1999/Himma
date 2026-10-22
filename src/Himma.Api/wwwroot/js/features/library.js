import { get, post, put, del } from '../api.js';
import { muscles, regions, places, exerciseTarget } from '../labels.js';
import { h, fill, add, debounce } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { muscleFigure } from '../ui/body.js';
import { openModal, confirmDialog } from '../ui/modal.js';
import { field, selectOf, numberInput, readNumber, showErrors, clearErrors } from '../ui/forms.js';
import { calmNotice, skeleton } from '../ui/notice.js';
import { showSaved } from '../ui/toast.js';

/**
 * Exercise library picker with search and filters (place, muscle, region).
 * System exercises can be added or cloned; the user's own can also be edited or deleted.
 * onPick(exercise) is called for every exercise added; the dialog stays open to add more.
 */
export function openLibrary({ place = '', muscle = '', onPick }) {
  const search = h('input', { type: 'search', placeholder: 'ابحث باسم التمرين', autocomplete: 'off' });
  const placeSelect = selectOf({ '': 'كل الأماكن', ...places }, place);
  const muscleSelect = selectOf({ '': 'كل العضلات', ...muscles }, muscle);
  const regionSelect = selectOf({ '': 'كل المناطق', ...regions }, '');
  const mineOnly = h('input', { type: 'checkbox', id: 'mine-only' });
  const list = h('ul', { class: 'exercise-list', 'aria-label': 'التمارين', style: 'max-height:55vh;overflow-y:auto' });
  let token = 0;

  const content = h('div', { class: 'stack' },
    field('بحث', search),
    h('div', { class: 'grid-3' }, field('المكان', placeSelect), field('العضلة', muscleSelect), field('المنطقة', regionSelect)),
    h('div', { class: 'row spread' },
      h('label', { class: 'check', for: 'mine-only' }, mineOnly, h('span', { text: 'تماريني فقط' })),
      h('button', { type: 'button', class: 'btn btn-secondary btn-sm', onclick: () => openExerciseForm({ onSaved: refresh }) }, icon('plus', 16), 'تمرين جديد')),
    list);

  openModal({ title: 'مكتبة التمارين', content });

  [placeSelect, muscleSelect, regionSelect, mineOnly].forEach((el) => el.addEventListener('change', refresh));
  search.addEventListener('input', debounce(refresh, 250));
  refresh();

  async function refresh() {
    const current = ++token;
    fill(list, h('li', {}, skeleton(3)));
    const query = new URLSearchParams();
    if (placeSelect.value) query.set('place', placeSelect.value);
    if (muscleSelect.value) query.set('muscle', muscleSelect.value);
    if (regionSelect.value) query.set('region', regionSelect.value);
    if (search.value.trim()) query.set('search', search.value.trim());
    if (mineOnly.checked) query.set('mine', 'true');
    try {
      const exercises = await get(`/exercises?${query}`);
      if (current !== token) return;
      fill(list, ...(exercises.length
        ? exercises.map(row)
        : [h('li', { class: 'muted small', text: 'لا توجد تمارين بهذه الفلاتر.' })]));
    } catch (error) {
      if (current === token) fill(list, h('li', {}, calmNotice(error, refresh)));
    }
  }

  function row(exercise) {
    const addButton = h('button', { type: 'button', class: 'btn btn-sm' }, icon('plus', 16), 'إضافة');
    addButton.addEventListener('click', () => {
      onPick(exercise);
      addButton.disabled = true;
      fill(addButton, icon('check', 16), 'أُضيف');
    });

    const actions = exercise.isSystem
      ? [h('button', { type: 'button', class: 'btn btn-ghost btn-sm', onclick: () => clone(exercise) }, icon('copy', 16), 'نسخ وتعديل')]
      : [
        h('button', { type: 'button', class: 'icon-btn', 'aria-label': `تعديل ${exercise.name}`, onclick: () => openExerciseForm({ exercise, onSaved: refresh }) }, icon('edit', 18)),
        h('button', { type: 'button', class: 'icon-btn danger', 'aria-label': `حذف ${exercise.name}`, onclick: () => remove(exercise) }, icon('trash', 18)),
      ];

    return h('li', { class: 'exercise' },
      muscleFigure(exercise),
      h('div', { class: 'stack', style: 'gap:4px' },
        h('div', { class: 'row spread' },
          h('span', { class: 'name', text: exercise.name }),
          h('span', { class: exercise.isSystem ? 'tag muted' : 'tag gold', text: exercise.isSystem ? 'جاهز' : 'تمريني' })),
        h('span', { class: 'muted small', text: `${exerciseTarget(exercise)} · ${places[exercise.place]}${exercise.equipment ? ` · ${exercise.equipment}` : ''}` }),
        exercise.instructions && h('span', { class: 'small', text: exercise.instructions }),
        h('div', { class: 'row' }, onPick && addButton, actions)));
  }

  async function clone(exercise) {
    try {
      const copy = await post(`/exercises/${exercise.id}/clone`);
      showSaved('تم النسخ');
      openExerciseForm({ exercise: copy, onSaved: refresh });
      refresh();
    } catch (error) {
      list.prepend(h('li', {}, calmNotice(error)));
    }
  }

  async function remove(exercise) {
    const ok = await confirmDialog({
      title: 'حذف تمرين',
      message: `حذف "${exercise.name}"؟ إن كان مستخدماً في خطة أو في سجلك سيبقى ظاهراً هناك.`,
      confirmText: 'حذف',
      danger: true,
    });
    if (!ok) return;
    try {
      await del(`/exercises/${exercise.id}`);
      showSaved('تم الحذف');
      refresh();
    } catch (error) {
      list.prepend(h('li', {}, calmNotice(error)));
    }
  }
}

/** Create or edit one of the user's own exercises. */
export function openExerciseForm({ exercise = null, onSaved }) {
  const name = h('input', { type: 'text', maxlength: 60, value: exercise?.name ?? '' });
  const muscle = selectOf(muscles, exercise?.muscleGroup ?? 'chest');
  const region = selectOf(regions, exercise?.region ?? 'whole');
  const place = selectOf(places, exercise?.place ?? 'gym');
  const equipment = h('input', { type: 'text', maxlength: 60, value: exercise?.equipment ?? '' });
  const sets = numberInput(exercise?.defaultSets ?? 3, { min: 1, max: 20, step: 1 });
  const reps = numberInput(exercise?.defaultReps ?? 10, { min: 1, max: 100, step: 1 });
  const instructions = h('textarea', { maxlength: 1000 });
  instructions.value = exercise?.instructions ?? '';
  const status = h('div');
  const button = h('button', { type: 'submit', class: 'btn' }, 'حفظ');

  const form = h('form', { class: 'stack', novalidate: true },
    field('اسم التمرين', name, { name: 'name' }),
    h('div', { class: 'grid-3' }, field('العضلة', muscle, { name: 'muscleGroup' }), field('المنطقة', region, { name: 'region' }), field('المكان', place, { name: 'place' })),
    field('الأدوات', equipment, { name: 'equipment' }),
    h('div', { class: 'grid-2' }, field('الجولات', sets, { name: 'defaultSets' }), field('التكرارات', reps, { name: 'defaultReps' })),
    field('طريقة الأداء (اختياري)', instructions, { name: 'instructions' }),
    status,
    button);

  const modal = openModal({ title: exercise ? 'تعديل تمرين' : 'تمرين جديد', content: form });

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    clearErrors(form);
    fill(status);
    button.disabled = true;
    const body = {
      name: name.value.trim(),
      muscleGroup: muscle.value,
      region: region.value,
      place: place.value,
      equipment: equipment.value.trim(),
      iconKey: exercise?.iconKey,
      defaultSets: readNumber(sets),
      defaultReps: readNumber(reps),
      instructions: instructions.value.trim() || null,
    };
    try {
      const saved = exercise ? await put(`/exercises/${exercise.id}`, body) : await post('/exercises', body);
      showSaved();
      modal.close();
      onSaved?.(saved);
    } catch (error) {
      const message = showErrors(form, error);
      if (message) fill(status, calmNotice(message));
    } finally {
      button.disabled = false;
    }
  });
}
