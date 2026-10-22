import { get, post, del } from '../api.js';
import { on } from '../state.js';
import { todayLocal, formatLong, formatTime, dayKeyOf } from '../dates.js';
import { mealTypes, muscles } from '../labels.js';
import { h, fill, add, fmt } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { ring } from '../ui/charts.js';
import { calmNotice, skeleton } from '../ui/notice.js';
import { showSaved } from '../ui/toast.js';
import { openAddMeal } from './meals.js';
import { fetchProfile } from './profile.js';
import { startAndOpenSession } from './session.js';

const QUICK_WATER = [250, 500, 750];

/** Today: targets as rings, meals, water and today's workout. */
export function mount(view) {
  const date = todayLocal();
  const model = { targets: null, meals: [], water: [] };
  let tempId = 0;

  const tipBox = h('div');
  const ringsCard = h('div', { class: 'card' });
  const waterCard = h('div', { class: 'card' });
  const mealsCard = h('div', { class: 'card' });
  const workoutCard = h('div', { class: 'card' });
  const mealsNotice = h('div');
  const waterNotice = h('div');

  add(view, 
    h('header', { class: 'view-head' }, h('h1', { text: 'اليوم' }), h('p', { class: 'muted', text: formatLong(date) })),
    tipBox, ringsCard, mealsCard, waterCard, workoutCard);

  load();
  loadTip();
  loadWorkout();
  return on('water-changed', (event) => { if (event?.date === date) reloadWater(); });

  async function load() {
    fill(ringsCard, skeleton());
    fill(mealsCard, skeleton());
    fill(waterCard, skeleton());
    try {
      const [profile, meals, water] = await Promise.all([fetchProfile(), get(`/meals/${date}`), get(`/water/${date}`)]);
      model.targets = profile?.targets ?? null;
      model.meals = meals.items;
      model.water = water.entries;
      renderRings();
      renderMeals();
      renderWater();
    } catch (error) {
      if (error.status === 401) return;
      fill(ringsCard, calmNotice(error, load));
      fill(mealsCard);
      fill(waterCard);
    }
  }

  async function reloadWater() {
    try {
      model.water = (await get(`/water/${date}`)).entries;
      renderWater();
      renderRings();
    } catch { /* the next full load will catch up */ }
  }

  async function loadTip() {
    try {
      const tip = await get(`/tips/today?date=${date}`);
      fill(tipBox, h('div', { class: 'card tip' }, icon('flame', 20), h('p', { text: tip.text })));
    } catch { /* the tip is optional */ }
  }

  // ---------- Rings ----------
  function totals() {
    const sum = (key) => model.meals.reduce((acc, m) => acc + (m[key] ?? 0), 0);
    return { calories: sum('calories'), protein: sum('proteinG'), water: model.water.reduce((acc, w) => acc + w.ml, 0) };
  }

  function renderRings() {
    const t = totals();
    if (!model.targets) {
      fill(ringsCard, 
        h('h2', { text: 'ابدأ بملفك الشخصي' }),
        h('p', { class: 'muted', text: 'أدخل طولك ووزنك وهدفك، وسنحسب لك السعرات والبروتين والماء يومياً.' }),
        h('a', { class: 'btn', href: '#profile' }, 'إعداد الملف الشخصي'));
      return;
    }
    const { targetCalories, proteinTargetG, waterTargetMl } = model.targets;
    const left = (target, value, unit) => (value <= target ? `متبقي ${fmt(target - value)} ${unit}` : `زيادة ${fmt(value - target)} ${unit}`);
    fill(ringsCard, h('div', { class: 'rings' },
      ring({ value: t.calories, max: targetCalories, label: 'السعرات', sub: 'سعرة', color: t.calories > targetCalories * 1.1 ? 'var(--rust)' : 'var(--forest)', remaining: left(targetCalories, t.calories, 'سعرة') }),
      ring({ value: t.protein, max: proteinTargetG, label: 'البروتين', sub: 'غ', color: 'var(--gold)', remaining: left(proteinTargetG, t.protein, 'غ') }),
      ring({ value: t.water, max: waterTargetMl, label: 'الماء', sub: 'مل', color: 'var(--water)', remaining: left(waterTargetMl, t.water, 'مل') })));
  }

  // ---------- Meals ----------
  function renderMeals() {
    fill(mealsCard, 
      h('div', { class: 'card-head' }, h('h2', { text: 'وجبات اليوم' })),
      mealsNotice,
      Object.entries(mealTypes).map(([type, label]) => {
        const items = model.meals.filter((m) => m.mealType === type);
        const kcal = items.reduce((acc, m) => acc + m.calories, 0);
        return h('section', { class: 'meal-section' },
          h('div', { class: 'card-head' },
            h('h3', {}, label, items.length > 0 && h('span', { class: 'muted small' }, ' · ', h('span', { class: 'num', text: fmt(kcal) }), ' سعرة')),
            h('button', { type: 'button', class: 'btn btn-secondary btn-sm', onclick: () => openAddMeal({ mealType: type, onAdd: addMeal }) },
              icon('plus', 16), h('span', { text: 'إضافة' }), h('span', { class: 'visually-hidden', text: label }))),
          items.length > 0 && h('ul', { class: 'item-list' }, items.map(mealRow)));
      }));
  }

  function mealRow(meal) {
    return h('li', { class: meal.pending ? 'item pending' : 'item' },
      h('div', { class: 'grow' },
        h('div', { class: 'title', text: meal.name }),
        h('div', { class: 'meta' },
          h('span', { class: 'num', text: fmt(meal.grams) }), ' غ · ',
          h('span', { class: 'num', text: fmt(meal.calories) }), ' سعرة · ',
          h('span', { class: 'num', text: fmt(meal.proteinG) }), ' غ بروتين')),
      !meal.pending && h('button', {
        type: 'button', class: 'icon-btn danger', 'aria-label': `حذف ${meal.name}`, onclick: () => removeMeal(meal),
      }, icon('trash', 18)));
  }

  // Optimistic: show the meal at once, then replace it with the server's version (or roll back).
  function addMeal(request, preview) {
    const temp = { ...preview, id: `temp-${++tempId}`, pending: true };
    model.meals.push(temp);
    fill(mealsNotice);
    renderMeals();
    renderRings();
    post('/meals', { ...request, date })
      .then((saved) => {
        model.meals = model.meals.map((m) => (m === temp ? saved : m));
        showSaved();
      })
      .catch((error) => {
        model.meals = model.meals.filter((m) => m !== temp);
        if (error.status !== 401) fill(mealsNotice, calmNotice(error, () => addMeal(request, preview)));
      })
      .finally(() => { renderMeals(); renderRings(); });
  }

  function removeMeal(meal) {
    const index = model.meals.indexOf(meal);
    model.meals = model.meals.filter((m) => m !== meal);
    fill(mealsNotice);
    renderMeals();
    renderRings();
    del(`/meals/${meal.id}`)
      .then(() => showSaved('تم الحذف'))
      .catch((error) => {
        if (error.status === 404) return; // already gone
        model.meals.splice(index, 0, meal);
        renderMeals();
        renderRings();
        if (error.status !== 401) fill(mealsNotice, calmNotice(error, () => removeMeal(meal)));
      });
  }

  // ---------- Water ----------
  function renderWater() {
    const total = model.water.reduce((acc, w) => acc + w.ml, 0);
    fill(waterCard, 
      h('div', { class: 'card-head' },
        h('h2', { class: 'row' }, icon('drop', 20), 'الماء'),
        h('span', { class: 'muted' }, h('span', { class: 'num', text: fmt(total) }), ' مل')),
      waterNotice,
      h('div', { class: 'water-buttons' }, QUICK_WATER.map((ml) =>
        h('button', { type: 'button', class: 'btn', onclick: () => addWaterEntry(ml) }, icon('plus', 16), h('span', { class: 'num', text: String(ml) }), ' مل'))),
      model.water.length > 0 && h('ul', { class: 'item-list', style: 'margin-top:10px' }, model.water.slice().reverse().map((entry) =>
        h('li', { class: entry.pending ? 'item pending' : 'item' },
          h('div', { class: 'grow' }, h('span', { class: 'num', text: String(entry.ml) }), ' مل',
            entry.createdAtUtc && h('span', { class: 'meta' }, ' · ', formatTime(entry.createdAtUtc))),
          !entry.pending && h('button', {
            type: 'button', class: 'icon-btn danger', 'aria-label': `حذف ${entry.ml} مل`, onclick: () => removeWater(entry),
          }, icon('trash', 18))))));
  }

  function addWaterEntry(ml) {
    const temp = { id: `temp-${++tempId}`, ml, pending: true };
    model.water.push(temp);
    fill(waterNotice);
    renderWater();
    renderRings();
    post('/water', { date, ml })
      .then((saved) => {
        model.water = model.water.map((w) => (w === temp ? saved : w));
        showSaved();
      })
      .catch((error) => {
        model.water = model.water.filter((w) => w !== temp);
        if (error.status !== 401) fill(waterNotice, calmNotice(error, () => addWaterEntry(ml)));
      })
      .finally(() => { renderWater(); renderRings(); });
  }

  function removeWater(entry) {
    const index = model.water.indexOf(entry);
    model.water = model.water.filter((w) => w !== entry);
    renderWater();
    renderRings();
    del(`/water/${entry.id}`)
      .then(() => showSaved('تم الحذف'))
      .catch((error) => {
        if (error.status === 404) return;
        model.water.splice(index, 0, entry);
        renderWater();
        renderRings();
        if (error.status !== 401) fill(waterNotice, calmNotice(error, () => removeWater(entry)));
      });
  }

  // ---------- Today's workout ----------
  async function loadWorkout() {
    fill(workoutCard, skeleton(2));
    try {
      const plans = await get('/plans');
      const active = plans.find((p) => p.isActive);
      if (!active) {
        fill(workoutCard, 
          h('h2', { class: 'row' }, icon('workouts', 20), 'تمرين اليوم'),
          h('p', { class: 'muted', text: 'لا توجد خطة تمرين نشطة بعد.' }),
          h('a', { class: 'btn btn-secondary', href: '#workouts/templates' }, 'اختر خطة جاهزة'));
        return;
      }
      const plan = await get(`/plans/${active.id}`);
      const day = plan.days.find((d) => d.dayKey === dayKeyOf(date));
      if (!day || day.isRest || day.items.length === 0) {
        fill(workoutCard, 
          h('h2', { class: 'row' }, icon('workouts', 20), 'تمرين اليوم'),
          h('p', { class: 'muted', text: 'اليوم يوم راحة في خطتك. الاستشفاء جزء من التقدم.' }));
        return;
      }
      const start = h('button', { type: 'button', class: 'btn' }, icon('play', 16), 'ابدأ التمرين');
      start.addEventListener('click', async () => {
        start.disabled = true;
        try {
          await startAndOpenSession(date, day.id);
        } catch (error) {
          start.disabled = false;
          add(workoutCard, calmNotice(error));
        }
      });
      fill(workoutCard, 
        h('div', { class: 'card-head' }, h('h2', { class: 'row' }, icon('workouts', 20), 'تمرين اليوم'), h('span', { class: 'tag', text: plan.name })),
        h('div', { class: 'chips' }, day.focus.map((m) => h('span', { class: 'tag', text: muscles[m] }))),
        h('ul', { class: 'item-list', style: 'margin:10px 0' }, day.items.map((item) =>
          h('li', { class: 'item' },
            h('span', { class: 'grow title', text: item.exercise.name }),
            h('span', { class: 'meta' }, h('span', { class: 'num', text: `${item.sets} × ${item.reps}` }))))),
        start);
    } catch (error) {
      if (error.status !== 401) fill(workoutCard, calmNotice(error, loadWorkout));
    }
  }
}
