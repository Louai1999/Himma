import { t } from '../i18n/index.js';
import { get, post, put } from '../api.js';
import { formatLong } from '../dates.js';
import { days, exerciseTarget } from '../labels.js';
import { h, fill, add, fmt, debounce } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { muscleFigure } from '../ui/body.js';
import { exerciseInfoButton } from '../ui/exerciseInfo.js';
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
      // Reps in reserve (RPE = 10 - RIR): optional, helps the "go heavier" hint.
      const rir = h('select', { 'aria-label': t('{name}: التكرارات المتبقية في الجولة {n}', { name: exercise.name, n: index + 1 }) },
        h('option', { value: '', text: '—', selected: set.rir == null }),
        [0, 1, 2, 3, 4, 5].map((n) => h('option', { value: String(n), text: n === 5 ? '5+' : String(n), selected: set.rir === n })));
      reps.setAttribute('aria-label', t('{name}: تكرارات الجولة {n}', { name: exercise.name, n: index + 1 }));
      weight.setAttribute('aria-label', t('{name}: وزن الجولة {n} بالكيلو', { name: exercise.name, n: index + 1 }));
      [reps, weight, rir, done].forEach((el) => { el.disabled = locked; });
      const tr = h('tr', { class: set.completed ? 'done' : '' },
        h('td', {}, h('span', { class: 'num', text: String(index + 1) })),
        h('td', {}, reps),
        h('td', {}, weight),
        h('td', {}, rir),
        h('td', {}, done));

      reps.addEventListener('input', () => { set.reps = readNumber(reps); if (set.completed) saveSoon(); });
      weight.addEventListener('input', () => { set.weightKg = readNumber(weight); if (set.completed) saveSoon(); });
      rir.addEventListener('change', () => { set.rir = rir.value === '' ? null : Number(rir.value); if (set.completed) saveSoon(); });
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
          h('div', { class: 'row', style: 'gap:4px;align-items:center' }, h('h2', { text: exercise.name }), exerciseInfoButton(exercise)),
          h('p', { class: 'muted small', text: `${exerciseTarget(exercise)} · ${t('المطلوب {sets} × {reps}', { sets: entry.plannedSets, reps: entry.plannedReps })} · ${t('راحة {s} ث', { s: entry.restSeconds ?? DEFAULT_REST_SECONDS })}` }),
          entry.last && h('p', { class: 'small' }, t('آخر مرة: '), h('span', { class: 'num', text: t('{kg} كغ × {reps}', { kg: fmt(entry.last.weightKg ?? 0), reps: entry.last.reps }) })))),
      !locked && entry.hint && hintNotice(entry, renderRows),
      h('table', { class: 'set-table' },
        h('thead', {}, h('tr', {}, h('th', { text: t('الجولة') }), h('th', { text: t('التكرار') }), h('th', { text: t('الوزن (كغ)') }),
          h('th', { title: t('التكرارات المتبقية: كم تكرار كنت تقدر تزيد. RPE = 10 - RIR'), text: 'RIR' }), h('th', { text: t('تمت') }))),
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
      .map((set, i) => ({ exerciseId: entry.exercise.id, setNumber: i + 1, reps: set.reps, weightKg: set.weightKg, rir: set.rir ?? null, completed: true, ticked: set.completed }))
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
      ? { reps: log.reps, weightKg: log.weightKg, rir: log.rir ?? null, completed: log.completed }
      : { reps: planned.plannedReps, weightKg: defaultWeight, rir: null, completed: false };
  });
  return { exercise: planned.exercise, plannedSets: planned.plannedSets, plannedReps: planned.plannedReps, restSeconds: planned.restSeconds ?? null, last: planned.last, hint: planned.hint ?? null, sets };
}

/**
 * Progressive overload hint from the server: go heavier (with a button that fills the weight into the
 * sets not done yet), add reps, or stay at this weight.
 */
function hintNotice(entry, renderRows) {
  const { hint } = entry;
  const why = {
    twoSessionsHit: t('كمّلت كل الجولات بالتكرارات المطلوبة مرتين متتاليتين.'),
    easyLastTime: t('آخر مرة كانت سهلة (3 تكرارات أو أكثر متبقية).'),
    missedReps: t('آخر مرة نقصت التكرارات، ثبّت الوزن لين تكمّلها.'),
  }[hint.reason] ?? '';
  const text = {
    increase: t('💪 جاهز تزيد: جرّب {kg} كغ', { kg: fmt(hint.weightKg) }),
    addReps: t('💪 جاهز تزيد: جرّب {reps} تكرار', { reps: hint.reps }),
    keep: t('حافظ على {kg} كغ هالمرة', { kg: fmt(hint.weightKg) }),
  }[hint.kind];
  if (!text) return null;

  const apply = hint.kind !== 'keep' && h('button', {
    type: 'button', class: 'btn btn-sm btn-secondary', onclick: () => {
      entry.sets.filter((set) => !set.completed).forEach((set) => {
        if (hint.kind === 'increase') set.weightKg = hint.weightKg;
        else set.reps = hint.reps;
      });
      renderRows();
      showSaved(t('تم التطبيق على الجولات الباقية'));
    },
  }, t('طبّق'));
  return h('div', { class: hint.kind === 'keep' ? 'notice' : 'notice info', role: 'status' },
    h('div', { class: 'grow' }, h('strong', { text }), h('div', { class: 'small', text: why })),
    apply);
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
