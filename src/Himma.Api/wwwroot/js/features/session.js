import { t } from '../i18n/index.js';
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
import { restTimer } from '../ui/restTimer.js';
import { openModal } from '../ui/modal.js';

const DEFAULT_REST_SECONDS = 90;

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
  const timer = restTimer();
  add(view, container, timer.element);
  loadInto(container, () => get(`/workouts/sessions/${encodeURIComponent(id)}`), (session) => render(container, session, timer));
  return () => timer.stop();
}

function render(container, session, timer) {
  const locked = Boolean(session.completedAtUtc);
  const status = h('div');
  const exercises = session.exercises.map((e) => buildExercise(e, session.sets));
  const list = h('div', { class: 'stack' });

  const saveSoon = debounce(save, 600);

  add(container,
    h('header', { class: 'view-head' },
      h('div', {},
        h('h1', { text: session.planName ? `${days[session.dayKey]} - ${session.planName}` : t('تمرين حر') }),
        h('p', { class: 'muted', text: formatLong(session.date) })),
      h('a', { class: 'btn btn-ghost btn-sm', href: '#workouts' }, t('رجوع'))),
    locked && calmNotice(t('أنهيت هذا التمرين. أحسنت!')),
    status,
    list,
    !locked && h('button', {
      type: 'button', class: 'btn btn-secondary', onclick: () => openLibrary({
        onPick: (exercise) => {
          exercises.push(buildExercise({ exercise, plannedSets: exercise.defaultSets, plannedReps: exercise.defaultReps, plannedWeightKg: null, last: null }, []));
          renderList();
        },
      }),
    }, icon('plus', 16), t('إضافة تمرين')),
    !locked && h('button', { type: 'button', class: 'btn btn-block', onclick: finish }, icon('check', 18), t('إنهاء التمرين')));

  renderList();

  function renderList() {
    fill(list, ...(exercises.length
      ? exercises.map(exerciseCard)
      : [h('div', { class: 'card empty', text: t('لا توجد تمارين في هذه الجلسة بعد. أضف تمريناً لتبدأ.') })]));
  }

  function exerciseCard(entry) {
    const { exercise } = entry;
    const rows = h('tbody');
    const renderRows = () => fill(rows, ...entry.sets.map((set, i) => setRow(set, i)));

    function setRow(set, index) {
      const reps = numberInput(set.reps, { min: 1, max: 100, step: 1 });
      const weight = numberInput(set.weightKg, { min: 0, max: 500, step: 0.5 });
      const done = h('input', { type: 'checkbox', checked: set.completed, 'aria-label': t('{name}: الجولة {n} تمت', { name: exercise.name, n: index + 1 }) });
      reps.setAttribute('aria-label', t('{name}: تكرارات الجولة {n}', { name: exercise.name, n: index + 1 }));
      weight.setAttribute('aria-label', t('{name}: وزن الجولة {n} بالكيلو', { name: exercise.name, n: index + 1 }));
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
        // A finished set starts the rest before the next one.
        if (set.completed) timer.start(entry.restSeconds ?? DEFAULT_REST_SECONDS);
      });
      return tr;
    }

    renderRows();
    return h('article', { class: 'card' },
      h('div', { class: 'exercise', style: 'background:transparent;padding:0' },
        muscleFigure(exercise),
        h('div', {},
          h('h2', { text: exercise.name }),
          h('p', { class: 'muted small', text: `${exerciseTarget(exercise)} · ${t('المطلوب {sets} × {reps}', { sets: entry.plannedSets, reps: entry.plannedReps })} · ${t('راحة {s} ث', { s: entry.restSeconds ?? DEFAULT_REST_SECONDS })}` }),
          entry.last && h('p', { class: 'small' }, t('آخر مرة: '), h('span', { class: 'num', text: t('{kg} كغ × {reps}', { kg: fmt(entry.last.weightKg ?? 0), reps: entry.last.reps }) })))),
      h('table', { class: 'set-table' },
        h('thead', {}, h('tr', {}, h('th', { text: t('الجولة') }), h('th', { text: t('التكرار') }), h('th', { text: t('الوزن (كغ)') }), h('th', { text: t('تمت') }))),
        rows),
      !locked && h('button', {
        type: 'button', class: 'btn btn-ghost btn-sm', onclick: () => {
          const previous = entry.sets[entry.sets.length - 1];
          entry.sets.push({ reps: previous?.reps ?? entry.plannedReps, weightKg: previous?.weightKg ?? null, completed: false });
          renderRows();
        },
      }, icon('plus', 16), t('جولة إضافية')));
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
      timer.stop();
      showSaved(t('أحسنت! تم حفظ التمرين'));
      const records = await get(`/workouts/sessions/${session.id}/records`).catch(() => []);
      if (records.length > 0) showRecords(records);
      else navigate('#workouts/history');
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
  return { exercise: planned.exercise, plannedSets: planned.plannedSets, plannedReps: planned.plannedReps, restSeconds: planned.restSeconds ?? null, last: planned.last, sets };
}

/** "New personal record!" after finishing a workout that beat earlier sessions. Closing goes to the history. */
function showRecords(records) {
  let next = '#workouts/history';
  const modal = openModal({
    title: t('🏆 أرقام قياسية جديدة!'),
    content: h('div', { class: 'stack' },
      h('ul', { class: 'item-list' }, records.map((r) => h('li', { class: 'item' },
        h('div', { class: 'grow' },
          h('div', { class: 'title', text: r.exerciseName }),
          h('div', { class: 'muted small', text: r.kind === 'weight'
            ? t('أثقل وزن: {kg} كغ (قبل: {prev} كغ)', { kg: fmt(r.valueKg), prev: fmt(r.previousKg) })
            : t('1RM تقديري: {kg} كغ (قبل: {prev} كغ) من {w} كغ × {reps}', { kg: fmt(r.valueKg), prev: fmt(r.previousKg), w: fmt(r.weightKg), reps: r.reps }) }))))),
      h('button', { type: 'button', class: 'btn btn-secondary', text: t('كل أرقامي القياسية'), onclick: () => { next = '#records'; modal.close(); } })),
    onClose: () => navigate(next),
  });
}
