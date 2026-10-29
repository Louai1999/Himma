import { t } from '../i18n/index.js';
import { get, post, put, del } from '../api.js';
import { todayLocal, dayKeyOf, formatLong } from '../dates.js';
import { days, muscles, places, exerciseTarget } from '../labels.js';
import { h, fill, add, fmt } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { muscleFigure } from '../ui/body.js';
import { exerciseInfoButton, openExerciseInfo } from '../ui/exerciseInfo.js';
import { openModal, confirmDialog } from '../ui/modal.js';
import { field, selectOf, numberInput, readNumber, showErrors, clearErrors } from '../ui/forms.js';
import { calmNotice, loadInto, skeleton } from '../ui/notice.js';
import { showSaved } from '../ui/toast.js';
import { navigate } from '../router.js';
import { openLibrary } from './library.js';
import { startAndOpenSession } from './session.js';

const MODES = { plan: t('خطتي'), templates: t('خطط جاهزة'), history: t('السجل') };

/** Workouts: "My plan" (fully editable), ready plans for beginners, and workout history. */
export function mount(view, [modeParam, planParam]) {
  const mode = MODES[modeParam] ? modeParam : 'plan';
  const body = h('div', { class: 'stack' });

  add(view,
    h('header', { class: 'view-head' }, h('h1', { text: t('التمارين') }), h('div', { class: 'row' }, h('a', { class: 'btn btn-ghost btn-sm', href: '#volume' }, '📊 ', t('الحجم التدريبي')), h('a', { class: 'btn btn-ghost btn-sm', href: '#records' }, '🏆 ', t('أرقامي القياسية')))),
    h('div', { class: 'segmented', role: 'group', 'aria-label': t('عرض') },
      Object.entries(MODES).map(([key, label]) =>
        h('button', { type: 'button', 'aria-pressed': String(key === mode), onclick: () => navigate(`#workouts/${key}`) }, label))),
    body);

  if (mode === 'templates') showTemplates(body);
  else if (mode === 'history') showHistory(body);
  else showPlans(body, planParam ? Number(planParam) : null);
}

// ---------- Ready plans ----------

function showTemplates(body) {
  loadInto(body, () => get('/plans/templates'), (templates) => {
    add(body, h('p', { class: 'muted', text: t('خطط جاهزة للمبتدئين. عند اختيار خطة تُنسخ لك نسخة خاصة تعدّلها كما تريد.') }));
    templates.forEach((template) => add(body, templateCard(template)));
  });

  function templateCard(template) {
    const use = h('button', { type: 'button', class: 'btn' }, t('استخدم هذه الخطة'));
    const status = h('div');
    use.addEventListener('click', async () => {
      use.disabled = true;
      try {
        const plan = await post(`/plans/from-template/${encodeURIComponent(template.key)}`);
        showSaved(t('أُضيفت الخطة إلى خططك'));
        navigate(`#workouts/plan/${plan.id}`);
      } catch (error) {
        use.disabled = false;
        fill(status, calmNotice(error));
      }
    });
    return h('article', { class: 'card stack' },
      h('div', { class: 'card-head' }, h('h2', { text: template.name }), h('span', { class: 'tag', text: places[template.place] })),
      h('p', { class: 'muted', text: template.description }),
      h('ul', { class: 'item-list' }, template.days.filter((d) => !d.isRest).map((d) =>
        h('li', { class: 'item' },
          h('div', { class: 'grow' },
            h('div', { class: 'title', text: `${days[d.dayKey]}: ${d.focus.map((m) => muscles[m]).join(' + ')}` }),
            // Each name opens its guide, so beginners can look an exercise up before choosing a plan.
            h('div', { class: 'meta' }, d.items.map((i, n) => [n > 0 && t('، '),
              h('button', { type: 'button', class: 'link-btn', onclick: () => openExerciseInfo(i.exercise) }, i.exercise.name)])))))),
      h('p', { class: 'muted small', text: t('باقي الأيام راحة.') }),
      h('p', { class: 'muted small guide-hint', text: t('اضغط على اسم أي تمرين وتشوف طريقته بالصور.') }),
      status,
      use);
  }
}

// ---------- History ----------

function showHistory(body) {
  loadInto(body, () => get(`/workouts/history?days=30&today=${todayLocal()}`), (sessions) => {
    if (sessions.length === 0) {
      add(body, h('div', { class: 'card empty' }, h('p', { text: t('لا توجد تمارين في آخر 30 يوماً.') })));
      return;
    }
    add(body, h('div', { class: 'card' },
      h('h2', { text: t('آخر 30 يوماً') }),
      h('ul', { class: 'item-list' }, sessions.map((s) =>
        h('li', { class: 'item' },
          h('a', { class: 'grow', href: `#session/${s.id}`, style: 'text-decoration:none;color:inherit' },
            h('div', { class: 'title', text: formatLong(s.date) }),
            h('div', { class: 'meta' },
              s.planName ? `${s.planName} · ${days[s.dayKey]} · ` : t('تمرين حر · '),
              h('span', { class: 'num', text: String(s.completedSets) }), t(' جولة · '),
              h('span', { class: 'num', text: fmt(s.totalVolumeKg) }), t(' كغ إجمالي'))),
          h('span', { class: s.completedAtUtc ? 'tag' : 'tag gold', text: s.completedAtUtc ? t('مكتمل') : t('لم يكتمل') }))))));
  });
}

// ---------- My plan ----------

function showPlans(body, preferredId) {
  loadInto(body, () => get('/plans'), async (plans) => {
    if (plans.length === 0) {
      add(body, h('div', { class: 'card empty stack' },
        h('p', { text: t('لا توجد لديك خطة بعد. ابدأ بخطة جاهزة أو أنشئ خطتك من الصفر.') }),
        h('div', { class: 'row', style: 'justify-content:center' },
          h('a', { class: 'btn', href: '#workouts/templates' }, t('تصفح الخطط الجاهزة')),
          h('button', { type: 'button', class: 'btn btn-secondary', onclick: () => openPlanForm({ onSaved: (p) => navigate(`#workouts/plan/${p.id}`) }) }, icon('plus', 16), t('خطة جديدة')))));
      return;
    }
    const planId = plans.some((p) => p.id === preferredId) ? preferredId : (plans.find((p) => p.isActive) ?? plans[0]).id;
    const planBox = h('div', { class: 'stack' }, skeleton());
    add(body, planToolbar(plans, planId), planBox);
    loadInto(planBox, () => get(`/plans/${planId}`), (plan) => renderWeek(planBox, plan));
  });
}

function planToolbar(plans, planId) {
  const current = plans.find((p) => p.id === planId);
  const picker = selectOf(Object.fromEntries(plans.map((p) => [p.id, p.isActive ? t('{name} (النشطة)', { name: p.name }) : p.name])), String(planId));
  picker.addEventListener('change', () => navigate(`#workouts/plan/${picker.value}`));
  const status = h('div');

  const activate = !current.isActive && h('button', {
    type: 'button', class: 'btn btn-sm', onclick: async () => {
      try {
        await post(`/plans/${planId}/activate`);
        showSaved(t('أصبحت الخطة النشطة'));
        navigate(`#workouts/plan/${planId}`);
      } catch (error) { fill(status, calmNotice(error)); }
    },
  }, t('اجعلها النشطة'));

  const remove = async () => {
    const ok = await confirmDialog({ title: t('حذف الخطة'), message: t('حذف "{name}"؟ سجل تمارينك السابق يبقى محفوظاً.', { name: current.name }), confirmText: t('حذف'), danger: true });
    if (!ok) return;
    try {
      await del(`/plans/${planId}`);
      showSaved(t('تم الحذف'));
      navigate('#workouts/plan');
    } catch (error) { fill(status, calmNotice(error)); }
  };

  return h('div', { class: 'card stack' },
    field(t('الخطة'), picker),
    h('div', { class: 'row' },
      current.isActive ? h('span', { class: 'tag', text: t('الخطة النشطة') }) : activate,
      h('button', { type: 'button', class: 'btn btn-ghost btn-sm', onclick: () => openPlanForm({ plan: current, onSaved: () => navigate(`#workouts/plan/${planId}`) }) }, icon('edit', 16), t('تعديل الاسم')),
      h('button', { type: 'button', class: 'btn btn-ghost btn-sm', onclick: () => openPlanForm({ onSaved: (p) => navigate(`#workouts/plan/${p.id}`) }) }, icon('plus', 16), t('خطة جديدة')),
      h('button', { type: 'button', class: 'btn btn-ghost btn-sm', onclick: remove }, icon('trash', 16), t('حذف'))),
    status);
}

function openPlanForm({ plan = null, onSaved }) {
  const name = h('input', { type: 'text', maxlength: 60, value: plan?.name ?? '' });
  const place = selectOf(places, plan?.place ?? 'gym');
  const status = h('div');
  const form = h('form', { class: 'stack', novalidate: true },
    field(t('اسم الخطة'), name, { name: 'name' }),
    field(t('المكان'), place, { name: 'place' }),
    status,
    h('button', { type: 'submit', class: 'btn' }, t('حفظ')));
  const modal = openModal({ title: plan ? t('تعديل الخطة') : t('خطة جديدة'), content: form });
  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    clearErrors(form);
    try {
      const body = { name: name.value.trim(), place: place.value };
      const saved = plan ? await put(`/plans/${plan.id}`, body) : await post('/plans', body);
      showSaved();
      modal.close();
      onSaved(saved);
    } catch (error) {
      const message = showErrors(form, error);
      if (message) fill(status, calmNotice(message));
    }
  });
}

function renderWeek(container, plan) {
  const todayKey = dayKeyOf(todayLocal());
  add(container, h('p', { class: 'muted small', text: t('اختر يوماً لتحدد عضلاته وتمارينه. يمكنك ترتيب التمارين بالسحب أو بالأسهم.') }));
  plan.days.forEach((day) => add(container, dayCard(plan, day, day.dayKey === todayKey)));
}

function daySummary(day) {
  if (day.isRest) return [h('span', { class: 'tag gold', text: t('راحة') })];
  return [
    ...day.focus.map((m) => h('span', { class: 'tag', text: muscles[m] })),
    h('span', { class: 'muted small' }, h('span', { class: 'num', text: String(day.items.length) }), t(' تمارين')),
  ];
}

function dayCard(plan, day, isToday) {
  let current = day;
  const summary = h('span', { class: 'chips' }, daySummary(day));
  const toggle = h('button', { type: 'button', class: 'day-toggle', 'aria-expanded': 'false' },
    h('span', { class: 'stack', style: 'gap:4px' },
      h('span', { class: 'day-name' }, days[day.dayKey], isToday ? t(' (اليوم)') : ''),
      summary),
    icon('down'));
  const bodyEl = h('div', { class: 'day-body', hidden: true });
  const card = h('article', { class: isToday ? 'card day-card today' : 'card day-card' }, toggle, bodyEl);

  toggle.addEventListener('click', () => {
    const open = toggle.getAttribute('aria-expanded') !== 'true';
    toggle.setAttribute('aria-expanded', String(open));
    bodyEl.hidden = !open;
    if (open) fill(bodyEl, dayEditor(plan, current, (saved) => {
      current = saved;
      fill(summary, ...daySummary(saved));
    }));
  });
  return card;
}

/** Edits one day locally, then saves the whole day with one PUT. */
function dayEditor(plan, day, onSaved) {
  const draft = {
    isRest: day.isRest,
    focus: [...day.focus],
    items: day.items.map((i) => ({
      exercise: i.exercise, sets: i.sets, reps: i.reps, weightKg: i.weightKg, restSeconds: i.restSeconds, notes: i.notes ?? '', last: i.last,
    })),
  };
  let dragFrom = null;

  const chips = h('div', { class: 'chips', role: 'group', 'aria-label': t('عضلات اليوم') });
  const list = h('ol', { class: 'exercise-list' });
  const status = h('div');
  const saveButton = h('button', { type: 'button', class: 'btn' }, icon('check', 16), t('حفظ اليوم'));
  const startButton = h('button', { type: 'button', class: 'btn btn-secondary' }, icon('play', 16), t('ابدأ هذا التمرين الآن'));
  const addButton = h('button', { type: 'button', class: 'btn btn-secondary btn-sm' }, icon('plus', 16), t('إضافة تمرين'));

  addButton.addEventListener('click', () => openLibrary({
    place: plan.place,
    muscle: draft.focus.length === 1 ? draft.focus[0] : '',
    onPick: (exercise) => {
      draft.isRest = false;
      if (!draft.focus.includes(exercise.muscleGroup)) draft.focus.push(exercise.muscleGroup);
      draft.items.push({ exercise, sets: exercise.defaultSets, reps: exercise.defaultReps, weightKg: null, restSeconds: 60, notes: '', last: null });
      renderChips();
      renderItems();
    },
  }));

  // Only a saved training day can be started.
  startButton.hidden = day.isRest || day.items.length === 0;
  saveButton.addEventListener('click', save);
  startButton.addEventListener('click', async () => {
    startButton.disabled = true;
    try {
      await startAndOpenSession(todayLocal(), day.id);
    } catch (error) {
      startButton.disabled = false;
      fill(status, calmNotice(error));
    }
  });

  renderChips();
  renderItems();

  return h('div', { class: 'stack' },
    chips,
    h('div', { class: 'row spread' }, h('h3', { text: t('التمارين') }), addButton),
    list,
    status,
    h('div', { class: 'row' }, saveButton, startButton));

  function renderChips() {
    const restChip = h('button', { type: 'button', class: 'chip rest', 'aria-pressed': String(draft.isRest) }, t('راحة'));
    restChip.addEventListener('click', async () => {
      if (!draft.isRest && draft.items.length > 0) {
        const ok = await confirmDialog({ title: t('يوم راحة'), message: t('جعل هذا اليوم راحة يزيل تمارينه من الخطة عند الحفظ. متابعة؟'), confirmText: t('نعم، يوم راحة') });
        if (!ok) return;
      }
      draft.isRest = !draft.isRest;
      if (draft.isRest) {
        draft.focus = [];
        draft.items = [];
      }
      renderChips();
      renderItems();
    });
    fill(chips, restChip, ...Object.entries(muscles).map(([key, label]) => {
      const chip = h('button', { type: 'button', class: 'chip', 'aria-pressed': String(draft.focus.includes(key)) }, label);
      chip.addEventListener('click', () => {
        draft.isRest = false;
        draft.focus = draft.focus.includes(key) ? draft.focus.filter((m) => m !== key) : [...draft.focus, key];
        renderChips();
        renderItems();
      });
      return chip;
    }));
  }

  function renderItems() {
    if (draft.isRest) {
      fill(list, h('li', { class: 'muted', text: t('يوم راحة. اختر عضلة لتحويله إلى يوم تمرين.') }));
      addButton.disabled = false;
      return;
    }
    if (draft.items.length === 0) {
      fill(list, h('li', { class: 'muted', text: t('لا توجد تمارين بعد. أضف تمريناً من المكتبة.') }));
      return;
    }
    fill(list, ...draft.items.map(itemRow));
  }

  function itemRow(item, index) {
    const input = (key, opts, label) => {
      const el = numberInput(item[key], opts);
      el.setAttribute('aria-label', `${label} - ${item.exercise.name}`);
      el.addEventListener('input', () => { item[key] = readNumber(el); });
      return h('label', {}, label, el);
    };
    const notes = h('input', { type: 'text', maxlength: 300, value: item.notes, placeholder: t('ملاحظة (اختياري)'), 'aria-label': t('ملاحظة - {name}', { name: item.exercise.name }) });
    notes.addEventListener('input', () => { item.notes = notes.value; });

    const handle = h('span', { class: 'icon-btn drag-handle', draggable: 'true', title: t('اسحب لتغيير الترتيب'), 'aria-hidden': 'true' }, icon('grip', 18));
    const li = h('li', { class: 'exercise' },
      muscleFigure(item.exercise),
      h('div', {},
        h('div', { class: 'row spread' },
          h('div', {},
            h('div', { class: 'name', text: item.exercise.name }),
            h('div', { class: 'muted small', text: exerciseTarget(item.exercise) }),
            item.exercise.isArchived && h('span', { class: 'tag muted', text: t('محذوف من المكتبة') })),
          h('div', { class: 'actions' },
            handle,
            exerciseInfoButton(item.exercise),
            h('button', { type: 'button', class: 'icon-btn', 'aria-label': t('تحريك {name} للأعلى', { name: item.exercise.name }), disabled: index === 0, onclick: () => move(index, index - 1) }, icon('up', 18)),
            h('button', { type: 'button', class: 'icon-btn', 'aria-label': t('تحريك {name} للأسفل', { name: item.exercise.name }), disabled: index === draft.items.length - 1, onclick: () => move(index, index + 1) }, icon('down', 18)),
            h('button', { type: 'button', class: 'icon-btn danger', 'aria-label': t('حذف {name} من اليوم', { name: item.exercise.name }), onclick: () => { draft.items.splice(index, 1); renderItems(); } }, icon('trash', 18)))),
        item.last && h('p', { class: 'small muted', style: 'margin:4px 0 0' }, t('آخر مرة: '), h('span', { class: 'num', text: t('{kg} كغ × {reps}', { kg: fmt(item.last.weightKg ?? 0), reps: item.last.reps }) })),
        h('div', { class: 'settings' },
          input('sets', { min: 1, max: 20, step: 1 }, t('جولات')),
          input('reps', { min: 1, max: 100, step: 1 }, t('تكرار')),
          input('weightKg', { min: 0, max: 500, step: 0.5 }, t('وزن كغ')),
          input('restSeconds', { min: 0, max: 600, step: 5 }, t('راحة ث')),
          h('span', { class: 'notes' }, notes))));

    // Drag to reorder (the up/down buttons are the keyboard alternative).
    handle.addEventListener('dragstart', (event) => {
      dragFrom = index;
      event.dataTransfer.effectAllowed = 'move';
      event.dataTransfer.setData('text/plain', String(index));
      event.dataTransfer.setDragImage(li, 20, 20);
      li.classList.add('dragging');
    });
    handle.addEventListener('dragend', () => li.classList.remove('dragging'));
    li.addEventListener('dragover', (event) => { event.preventDefault(); li.classList.add('drop-target'); });
    li.addEventListener('dragleave', () => li.classList.remove('drop-target'));
    li.addEventListener('drop', (event) => {
      event.preventDefault();
      li.classList.remove('drop-target');
      if (dragFrom !== null && dragFrom !== index) move(dragFrom, index);
      dragFrom = null;
    });
    return li;
  }

  function move(from, to) {
    if (to < 0 || to >= draft.items.length) return;
    const [item] = draft.items.splice(from, 1);
    draft.items.splice(to, 0, item);
    renderItems();
    list.querySelectorAll('.exercise')[to]?.querySelector('.actions button')?.focus();
  }

  async function save() {
    fill(status);
    saveButton.disabled = true;
    try {
      const saved = await put(`/plans/${plan.id}/days/${day.dayKey}`, {
        isRest: draft.isRest,
        focus: draft.focus,
        items: draft.items.map((i) => ({
          exerciseId: i.exercise.id,
          sets: i.sets,
          reps: i.reps,
          weightKg: i.weightKg,
          restSeconds: i.restSeconds,
          notes: i.notes?.trim() || null,
        })),
      });
      showSaved();
      onSaved(saved);
      day = saved;
      startButton.hidden = saved.isRest || saved.items.length === 0;
    } catch (error) {
      if (error.status !== 401) fill(status, calmNotice(error, save));
    } finally {
      saveButton.disabled = false;
    }
  }
}
