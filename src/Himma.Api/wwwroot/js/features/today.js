import { t } from '../i18n/index.js';
import { get, post, del } from '../api.js';
import { on } from '../state.js';
import { todayLocal, formatLong, formatTime, dayKeyOf } from '../dates.js';
import { mealTypes, muscles } from '../labels.js';
import { h, fill, add, fmt } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { exerciseInfoButton } from '../ui/exerciseInfo.js';
import { ring } from '../ui/charts.js';
import { calmNotice, skeleton } from '../ui/notice.js';
import { showSaved } from '../ui/toast.js';
import { openAddMeal } from './meals.js';
import { fetchProfile } from './profile.js';
import { startAndOpenSession } from './session.js';
import { todaySupplementsCard } from './supplements.js';
import { walkingCard } from './walking.js';

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
    h('header', { class: 'view-head' }, h('h1', { text: t('اليوم') }), h('p', { class: 'muted', text: formatLong(date) })),
    tipBox, ringsCard, mealsCard, todaySupplementsCard(), waterCard, walkingCard(), workoutCard);

  load();
  loadTip();
  loadWorkout();
  const offWater = on('water-changed', (event) => { if (event?.date === date) reloadWater(); });
  // A supplement with protein or calories adds a meal entry when taken.
  const offMeals = on('meals-changed', (event) => { if (event?.date === date) load(); });
  return () => { offWater(); offMeals(); };

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
    const sum = totals();
    if (!model.targets) {
      fill(ringsCard,
        h('h2', { text: t('ابدأ بملفك الشخصي') }),
        h('p', { class: 'muted', text: t('أدخل طولك ووزنك وهدفك، وسنحسب لك السعرات والبروتين والماء يومياً.') }),
        h('a', { class: 'btn', href: '#profile' }, t('إعداد الملف الشخصي')));
      return;
    }
    const { targetCalories, proteinTargetG, waterTargetMl } = model.targets;
    const left = (target, value, unit) => (value <= target
      ? t('متبقي {n} {unit}', { n: fmt(target - value), unit })
      : t('زيادة {n} {unit}', { n: fmt(value - target), unit }));
    fill(ringsCard, h('div', { class: 'rings' },
      ring({ value: sum.calories, max: targetCalories, label: t('السعرات'), sub: t('سعرة'), color: sum.calories > targetCalories * 1.1 ? 'var(--rust)' : 'var(--forest)', remaining: left(targetCalories, sum.calories, t('سعرة')) }),
      ring({ value: sum.protein, max: proteinTargetG, label: t('البروتين'), sub: t('غ'), color: 'var(--gold)', remaining: left(proteinTargetG, sum.protein, t('غ')) }),
      ring({ value: sum.water, max: waterTargetMl, label: t('الماء'), sub: t('مل'), color: 'var(--water)', remaining: left(waterTargetMl, sum.water, t('مل')) })));
  }

  // ---------- Meals ----------
  function renderMeals() {
    fill(mealsCard,
      h('div', { class: 'card-head' }, h('h2', { text: t('وجبات اليوم') })),
      mealsNotice,
      Object.entries(mealTypes).map(([type, label]) => {
        const items = model.meals.filter((m) => m.mealType === type);
        const kcal = items.reduce((acc, m) => acc + m.calories, 0);
        return h('section', { class: 'meal-section' },
          h('div', { class: 'card-head' },
            h('h3', {}, label, items.length > 0 && h('span', { class: 'muted small' }, ' · ', h('span', { class: 'num', text: fmt(kcal) }), t(' سعرة'))),
            h('button', { type: 'button', class: 'btn btn-secondary btn-sm', onclick: () => openAddMeal({ mealType: type, onAdd: addMeal }) },
              icon('plus', 16), h('span', { text: t('إضافة') }), h('span', { class: 'visually-hidden', text: label }))),
          items.length > 0 && h('ul', { class: 'item-list' }, items.map(mealRow)));
      }));
  }

  function mealRow(meal) {
    return h('li', { class: meal.pending ? 'item pending' : 'item' },
      h('div', { class: 'grow' },
        h('div', { class: 'title', text: meal.name }),
        h('div', { class: 'meta' },
          // Supplements measured in scoops or capsules have no grams.
          meal.grams > 0 && [h('span', { class: 'num', text: fmt(meal.grams) }), t(' غ · ')],
          h('span', { class: 'num', text: fmt(meal.calories) }), t(' سعرة · '),
          h('span', { class: 'num', text: fmt(meal.proteinG) }), t(' غ بروتين'))),
      !meal.pending && h('button', {
        type: 'button', class: 'icon-btn danger', 'aria-label': t('حذف {name}', { name: meal.name }), onclick: () => removeMeal(meal),
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
      .then(() => showSaved(t('تم الحذف')))
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
        h('h2', { class: 'row' }, icon('drop', 20), t('الماء')),
        h('span', { class: 'muted' }, h('span', { class: 'num', text: fmt(total) }), t(' مل'))),
      waterNotice,
      h('div', { class: 'water-buttons' }, QUICK_WATER.map((ml) =>
        h('button', { type: 'button', class: 'btn', onclick: () => addWaterEntry(ml) }, icon('plus', 16), h('span', { class: 'num', text: String(ml) }), t(' مل')))),
      model.water.length > 0 && h('ul', { class: 'item-list', style: 'margin-top:10px' }, model.water.slice().reverse().map((entry) =>
        h('li', { class: entry.pending ? 'item pending' : 'item' },
          h('div', { class: 'grow' }, h('span', { class: 'num', text: String(entry.ml) }), t(' مل'),
            entry.createdAtUtc && h('span', { class: 'meta' }, ' · ', formatTime(entry.createdAtUtc))),
          !entry.pending && h('button', {
            type: 'button', class: 'icon-btn danger', 'aria-label': t('حذف {ml} مل', { ml: entry.ml }), onclick: () => removeWater(entry),
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
      .then(() => showSaved(t('تم الحذف')))
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
          h('h2', { class: 'row' }, icon('workouts', 20), t('تمرين اليوم')),
          h('p', { class: 'muted', text: t('لا توجد خطة تمرين نشطة بعد.') }),
          h('a', { class: 'btn btn-secondary', href: '#workouts/templates' }, t('اختر خطة جاهزة')));
        return;
      }
      const plan = await get(`/plans/${active.id}`);
      const day = plan.days.find((d) => d.dayKey === dayKeyOf(date));
      if (!day || day.isRest || day.items.length === 0) {
        fill(workoutCard,
          h('h2', { class: 'row' }, icon('workouts', 20), t('تمرين اليوم')),
          h('p', { class: 'muted', text: t('اليوم يوم راحة في خطتك. الاستشفاء جزء من التقدم.') }));
        return;
      }
      const start = h('button', { type: 'button', class: 'btn' }, icon('play', 16), t('ابدأ التمرين'));
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
        h('div', { class: 'card-head' }, h('h2', { class: 'row' }, icon('workouts', 20), t('تمرين اليوم')), h('span', { class: 'tag', text: plan.name })),
        h('div', { class: 'chips' }, day.focus.map((m) => h('span', { class: 'tag', text: muscles[m] }))),
        h('ul', { class: 'item-list', style: 'margin:10px 0' }, day.items.map((item) =>
          h('li', { class: 'item' },
            h('span', { class: 'grow title', text: item.exercise.name }),
            h('span', { class: 'meta' }, h('span', { class: 'num', text: `${item.sets} × ${item.reps}` })),
            exerciseInfoButton(item.exercise)))),
        start);
    } catch (error) {
      if (error.status !== 401) fill(workoutCard, calmNotice(error, loadWorkout));
    }
  }
}
