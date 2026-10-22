import { get, post, put } from '../api.js';
import { formatLong } from '../dates.js';
import { days, exerciseTarget } from '../labels.js';
import { h, fill, add, fmt, debounce } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { muscleFigure } from '../ui/body.js';
import { numberInput, readNumber } from '../ui/forms.js';
import { calmNotice, loadInto } from '../ui/notice.js';
import { showSaved } from '../ui/toast.js';
import { navigate } from '../router.js';
import { openLibrary } from './library.js';

/** Starts (or reopens) the session for a plan day and shows it. */
export async function startAndOpenSession(date, planDayId) {
  const session = await post('/workouts/sessions', { date, planDayId });
  navigate(`#session/${session.id}`);
}

/**
 * A workout session: one card per exercise with a row per set. Ticking a set saves it;
 * the weights are remembered and shown as "last time" in the next session.
 */
export function mount(view, [id]) {
  const container = h('div', { class: 'stack' });
  add(view, container);
  loadInto(container, () => get(`/workouts/sessions/${encodeURIComponent(id)}`), (session) => render(container, session));
}

function render(container, session) {
  const locked = Boolean(session.completedAtUtc);
  const status = h('div');
  const exercises = session.exercises.map((e) => buildExercise(e, session.sets));
  const list = h('div', { class: 'stack' });

  const saveSoon = debounce(save, 600);

  add(container, 
    h('header', { class: 'view-head' },
      h('div', {},
        h('h1', { text: session.planName ? `${days[session.dayKey]} - ${session.planName}` : 'تمرين حر' }),
        h('p', { class: 'muted', text: formatLong(session.date) })),
      h('a', { class: 'btn btn-ghost btn-sm', href: '#workouts' }, 'رجوع')),
    locked && calmNotice('أنهيت هذا التمرين. أحسنت!'),
    status,
    list,
    !locked && h('button', {
      type: 'button', class: 'btn btn-secondary', onclick: () => openLibrary({
        onPick: (exercise) => {
          exercises.push(buildExercise({ exercise, plannedSets: exercise.defaultSets, plannedReps: exercise.defaultReps, plannedWeightKg: null, last: null }, []));
          renderList();
        },
      }),
    }, icon('plus', 16), 'إضافة تمرين'),
    !locked && h('button', { type: 'button', class: 'btn btn-block', onclick: finish }, icon('check', 18), 'إنهاء التمرين'));

  renderList();

  function renderList() {
    fill(list, ...(exercises.length
      ? exercises.map(exerciseCard)
      : [h('div', { class: 'card empty', text: 'لا توجد تمارين في هذه الجلسة بعد. أضف تمريناً لتبدأ.' })]));
  }

  function exerciseCard(entry) {
    const { exercise } = entry;
    const rows = h('tbody');
    const renderRows = () => fill(rows, ...entry.sets.map((set, i) => setRow(set, i)));

    function setRow(set, index) {
      const reps = numberInput(set.reps, { min: 1, max: 100, step: 1 });
      const weight = numberInput(set.weightKg, { min: 0, max: 500, step: 0.5 });
      const done = h('input', { type: 'checkbox', checked: set.completed, 'aria-label': `${exercise.name}: الجولة ${index + 1} تمت` });
      reps.setAttribute('aria-label', `${exercise.name}: تكرارات الجولة ${index + 1}`);
      weight.setAttribute('aria-label', `${exercise.name}: وزن الجولة ${index + 1} بالكيلو`);
      [reps, weight, done].forEach((el) => { el.disabled = locked; });
      const tr = h('tr', { class: set.completed ? 'done' : '' },
        h('td', {}, h('span', { class: 'num', text: String(index + 1) })),
        h('td', {}, reps),
        h('td', {}, weight),
        h('td', {}, done));

      reps.addEventListener('input', () => { set.reps = readNumber(reps); if (set.completed) saveSoon(); });
      weight.addEventListener('input', () => { set.weightKg = readNumber(weight); if (set.completed) saveSoon(); });
      done.addEventListener('change', () => {
        set.completed = done.checked;
        tr.className = set.completed ? 'done' : '';
        saveSoon();
      });
      return tr;
    }

    renderRows();
    return h('article', { class: 'card' },
      h('div', { class: 'exercise', style: 'background:transparent;padding:0' },
        muscleFigure(exercise),
        h('div', {},
          h('h2', { text: exercise.name }),
          h('p', { class: 'muted small', text: `${exerciseTarget(exercise)} · المطلوب ${entry.plannedSets} × ${entry.plannedReps}` }),
          entry.last && h('p', { class: 'small' }, 'آخر مرة: ', h('span', { class: 'num', text: `${fmt(entry.last.weightKg ?? 0)} كغ × ${entry.last.reps}` })))),
      h('table', { class: 'set-table' },
        h('thead', {}, h('tr', {}, h('th', { text: 'الجولة' }), h('th', { text: 'التكرار' }), h('th', { text: 'الوزن (كغ)' }), h('th', { text: 'تمت' }))),
        rows),
      !locked && h('button', {
        type: 'button', class: 'btn btn-ghost btn-sm', onclick: () => {
          const previous = entry.sets[entry.sets.length - 1];
          entry.sets.push({ reps: previous?.reps ?? entry.plannedReps, weightKg: previous?.weightKg ?? null, completed: false });
          renderRows();
        },
      }, icon('plus', 16), 'جولة إضافية'));
  }

  /** Sends every ticked set. Unticked rows are only on screen until they are ticked. */
  async function save() {
    const sets = exercises.flatMap((entry) => entry.sets
      .map((set, i) => ({ exerciseId: entry.exercise.id, setNumber: i + 1, reps: set.reps, weightKg: set.weightKg, completed: true, ticked: set.completed }))
      .filter((set) => set.ticked && set.reps >= 1)
      .map(({ ticked, ...set }) => set));
    fill(status);
    try {
      await put(`/workouts/sessions/${session.id}/sets`, { sets });
      showSaved();
      return true;
    } catch (error) {
      if (error.status !== 401) fill(status, calmNotice(error, save));
      return false;
    }
  }

  async function finish() {
    if (!(await save())) return;
    try {
      await post(`/workouts/sessions/${session.id}/complete`);
      showSaved('أحسنت! تم حفظ التمرين');
      navigate('#workouts/history');
    } catch (error) {
      if (error.status !== 401) fill(status, calmNotice(error, finish));
    }
  }
}

/** Builds the on-screen rows: logged sets first, then empty rows up to the planned count. */
function buildExercise(planned, loggedSets) {
  const logged = loggedSets.filter((s) => s.exerciseId === planned.exercise.id);
  const count = Math.max(planned.plannedSets, ...logged.map((s) => s.setNumber), 1);
  const defaultWeight = planned.plannedWeightKg ?? planned.last?.weightKg ?? null;
  const sets = Array.from({ length: count }, (_, i) => {
    const log = logged.find((s) => s.setNumber === i + 1);
    return log
      ? { reps: log.reps, weightKg: log.weightKg, completed: log.completed }
      : { reps: planned.plannedReps, weightKg: defaultWeight, completed: false };
  });
  return { exercise: planned.exercise, plannedSets: planned.plannedSets, plannedReps: planned.plannedReps, last: planned.last, sets };
}
