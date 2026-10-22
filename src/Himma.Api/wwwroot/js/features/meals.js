import { t } from '../i18n/index.js';
import { get, post, del } from '../api.js';
import { mealTypes } from '../labels.js';
import { h, fill, add, debounce, fmt } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { openModal, confirmDialog } from '../ui/modal.js';
import { field, selectOf, numberInput, readNumber, showErrors, clearErrors } from '../ui/forms.js';
import { calmNotice, skeleton } from '../ui/notice.js';
import { showSaved } from '../ui/toast.js';

/**
 * "Add meal" dialog: search system foods and the user's own foods, choose grams, add.
 * Calls onAdd(request, preview). The preview is only for instant display; the server
 * calculates the real values and its response replaces the preview.
 */
export function openAddMeal({ mealType, onAdd }) {
  let selected = null;
  let searchToken = 0;

  const typeSelect = selectOf(mealTypes, mealType);
  const search = h('input', { type: 'search', placeholder: t('ابحث: دجاج، أرز، تمر...'), autocomplete: 'off' });
  const results = h('ul', { class: 'pick-list', 'aria-label': t('نتائج البحث') });
  const grams = numberInput(100, { min: 1, max: 3000, step: 1 });
  const preview = h('p', { class: 'muted small' });
  const chosenName = h('strong');
  const chosen = h('div', { class: 'chosen stack', hidden: true },
    h('div', { class: 'row spread' }, chosenName, h('button', { type: 'button', class: 'btn btn-ghost btn-sm', onclick: () => choose(null) }, t('تغيير'))),
    field(t('الكمية (غرام)'), grams, { name: 'grams' }),
    preview);
  const status = h('div');
  const addButton = h('button', { type: 'submit', class: 'btn btn-block', disabled: true }, icon('plus', 18), t('إضافة'));

  const form = h('form', { class: 'stack', novalidate: true },
    field(t('الوجبة'), typeSelect, { name: 'mealType' }),
    field(t('ابحث عن طعام'), search),
    results,
    chosen,
    customFoodForm((food) => { runSearch(); choose(food); }),
    status,
    addButton);

  const modal = openModal({ title: t('إضافة وجبة'), content: form });

  search.addEventListener('input', debounce(runSearch, 250));
  grams.addEventListener('input', updatePreview);
  form.addEventListener('submit', (event) => {
    event.preventDefault();
    const amount = readNumber(grams);
    if (!selected || !amount || amount < 1 || amount > 3000) {
      fill(status, calmNotice(t('اختر طعاماً وأدخل كمية بين 1 و 3000 غرام.')));
      return;
    }
    const request = {
      mealType: typeSelect.value,
      source: selected.source === 'custom' ? 'custom' : 'manual',
      grams: amount,
      ...(selected.source === 'custom' ? { customFoodId: selected.id } : { foodId: selected.id }),
    };
    onAdd(request, {
      mealType: typeSelect.value,
      name: selected.name,
      grams: amount,
      calories: (selected.kcalPer100g * amount) / 100,
      proteinG: (selected.proteinPer100g * amount) / 100,
    });
    modal.close();
  });

  runSearch();
  search.focus();

  async function runSearch() {
    const token = ++searchToken;
    fill(results, h('li', {}, skeleton(2)));
    try {
      const term = search.value.trim();
      const foods = await get(`/foods${term ? `?search=${encodeURIComponent(term)}` : ''}`);
      if (token !== searchToken) return;
      fill(results, ...(foods.length
        ? foods.map(foodRow)
        : [h('li', { class: 'muted small', text: t('لا نتائج. جرّب كلمة أخرى أو أضف طعاماً خاصاً.') })]));
    } catch (error) {
      if (token === searchToken) fill(results, h('li', {}, calmNotice(error, runSearch)));
    }
  }

  function foodRow(food) {
    const serving = food.servingGrams
      ? ` · ${food.servingLabel ?? ''} ${t('{grams} غ', { grams: fmt(food.servingGrams) })}`
      : '';
    return h('li', { class: 'item' },
      h('button', { type: 'button', class: 'item-main', onclick: () => choose(food) },
        h('span', { class: 'title', text: food.name }),
        h('span', { class: 'meta', text: t('{kcal} سعرة · {protein} غ بروتين لكل 100 غ', { kcal: fmt(food.kcalPer100g), protein: fmt(food.proteinPer100g) }) + serving })),
      food.source === 'custom' && h('span', { class: 'tag', text: t('خاص') }),
      food.source === 'custom' && h('button', {
        type: 'button', class: 'icon-btn danger', 'aria-label': t('حذف {name}', { name: food.name }),
        onclick: () => removeCustomFood(food),
      }, icon('trash', 18)));
  }

  function choose(food) {
    selected = food;
    chosen.hidden = !food;
    addButton.disabled = !food;
    if (food) {
      chosenName.textContent = food.name;
      grams.value = String(food.servingGrams ?? 100);
      updatePreview();
      grams.focus();
    }
  }

  function updatePreview() {
    const amount = readNumber(grams) ?? 0;
    if (!selected) return;
    preview.textContent = t('تقريباً {kcal} سعرة و {protein} غ بروتين', {
      kcal: fmt((selected.kcalPer100g * amount) / 100),
      protein: fmt((selected.proteinPer100g * amount) / 100),
    });
  }

  async function removeCustomFood(food) {
    const ok = await confirmDialog({ title: t('حذف طعام خاص'), message: t('حذف "{name}" من أطعمتك؟ الوجبات المسجلة سابقاً تبقى كما هي.', { name: food.name }), confirmText: t('حذف'), danger: true });
    if (!ok) return;
    try {
      await del(`/customfoods/${food.id}`);
      if (selected?.source === 'custom' && selected.id === food.id) choose(null);
      showSaved();
      runSearch();
    } catch (error) {
      fill(status, calmNotice(error));
    }
  }
}

/** Collapsible form to add a food that is not in the list. */
function customFoodForm(onCreated) {
  const name = h('input', { type: 'text', maxlength: 60 });
  const kcal = numberInput(null, { min: 0, max: 900, step: 'any', placeholder: t('مثال: 250') });
  const protein = numberInput(null, { min: 0, max: 100, step: 'any', placeholder: t('مثال: 8') });
  const status = h('div');
  const box = h('div', { class: 'stack' },
    field(t('اسم الطعام'), name, { name: 'name' }),
    h('div', { class: 'grid-2' },
      field(t('سعرات لكل 100 غ'), kcal, { name: 'kcalPer100g' }),
      field(t('بروتين لكل 100 غ'), protein, { name: 'proteinPer100g' })),
    status,
    h('button', { type: 'button', class: 'btn btn-secondary', onclick: save }, t('حفظ في أطعمتي')));

  async function save() {
    clearErrors(box);
    fill(status);
    try {
      const food = await post('/customfoods', { name: name.value.trim(), kcalPer100g: readNumber(kcal), proteinPer100g: readNumber(protein) });
      showSaved();
      name.value = '';
      kcal.value = '';
      protein.value = '';
      details.open = false;
      onCreated(food);
    } catch (error) {
      const message = showErrors(box, error);
      if (message) fill(status, calmNotice(message));
    }
  }

  const details = h('details', { class: 'inline-form' }, h('summary', { text: t('لم تجد طعامك؟ أضف طعاماً خاصاً') }), box);
  return details;
}
