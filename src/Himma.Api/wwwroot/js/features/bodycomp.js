import { t } from '../i18n/index.js';
import { get, post, put, del } from '../api.js';
import { todayLocal, formatShort, formatLong, parseIso } from '../dates.js';
import { h, fill, add, fmt } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { lineChart } from '../ui/charts.js';
import { confirmDialog } from '../ui/modal.js';
import { field, numberInput, readNumber, showErrors, clearErrors } from '../ui/forms.js';
import { calmNotice, loadInto } from '../ui/notice.js';
import { showSaved } from '../ui/toast.js';

const DAY_MS = 24 * 60 * 60 * 1000;

const bodyFatCategories = {
  essential: t('دهون أساسية'),
  athletic: t('رياضي'),
  fitness: t('لياقة جيدة'),
  average: t('متوسط'),
  high: t('مرتفع'),
};

const ffmiLevels = {
  belowAverage: t('أقل من المتوسط'),
  average: t('متوسط'),
  aboveAverage: t('فوق المتوسط'),
  advanced: t('متقدم'),
  elite: t('ممتاز'),
  exceptional: t('استثنائي'),
};

const bodyWarnings = {
  veryLowTarget: t('هذه النسبة منخفضة جداً ويصعب الحفاظ عليها لفترة طويلة. يُفضّل استشارة مختص.'),
};

/**
 * Body composition: tape measurements (or a device reading) give body fat, lean mass and FFMI,
 * plus how much fat is left to reach a body fat goal. Every number is calculated by the server.
 */
export function bodySection() {
  const summaryCard = h('div', { class: 'card' });
  const goalCard = h('div', { class: 'card', hidden: true });
  const chartCard = h('div', { class: 'card', hidden: true });
  const listCard = h('div', { class: 'card', hidden: true });

  const section = h('section', { class: 'stack', 'aria-labelledby': 'body-heading' },
    h('header', { class: 'view-head' }, h('h1', { id: 'body-heading', text: t('تكوين الجسم') })),
    summaryCard,
    measurementForm(load),
    goalCard,
    chartCard,
    listCard,
    h('p', { class: 'muted small', text: t('كل الأرقام تقديرية لمتابعة التغيّر مع الوقت، وليست تشخيصاً طبياً.') }));

  load();
  return section;

  function load() {
    loadInto(summaryCard, () => get('/body'), (data) => {
      renderSummary(data);
      renderGoal(data);
      renderCharts(data);
      renderList(data);
    });
  }

  function renderSummary({ hasProfile, latest }) {
    add(summaryCard, h('h2', { text: t('آخر نتيجة') }));
    if (!hasProfile) {
      add(summaryCard, h('p', { class: 'muted' }, t('نحتاج طولك وجنسك للحساب. أكمل '), h('a', { href: '#profile', text: t('ملفك الشخصي') }), t(' أولاً.')));
      return;
    }
    if (!latest) {
      add(summaryCard, h('p', { class: 'muted', text: t('أدخل محيط الرقبة والخصر (والورك للنساء)، أو نسبة الدهون من جهاز، لنحسب تكوين جسمك.') }));
      return;
    }
    const c = latest.composition;
    add(summaryCard,
      h('div', { class: 'grid-2' },
        stat(t('{n}٪', { n: fmt(c.bodyFatPercent) }), t('نسبة الدهون'), bodyFatCategories[c.bodyFatCategory]),
        stat(fmt(c.leanMassKg), t('الكتلة الصافية (كغ)'), t('كل شيء عدا الدهون')),
        stat(fmt(c.fatMassKg), t('كتلة الدهون (كغ)')),
        stat(fmt(c.normalizedFfmi), t('مؤشر الكتلة الصافية FFMI'), ffmiLevels[c.ffmiLevel])),
      latest.muscleMassKg !== null && h('p', { class: 'small', style: 'margin-top:8px' },
        t('الكتلة العضلية من الجهاز: '), h('span', { class: 'num', text: fmt(latest.muscleMassKg) }), t(' كغ')),
      h('p', { class: 'muted small', style: 'margin-top:8px' },
        c.source === 'device'
          ? t('من قياس جهاز بتاريخ {date}، بوزن {weight} كغ.', { date: formatLong(latest.date), weight: fmt(latest.weightKg) })
          : t('تقدير بطريقة البحرية الأمريكية (±3-4٪) بتاريخ {date}، بوزن {weight} كغ من سجل الوزن.', { date: formatLong(latest.date), weight: fmt(latest.weightKg) })));
  }

  function renderGoal({ hasProfile, latest, targetBodyFatPercent, projection }) {
    goalCard.hidden = !hasProfile;
    if (!hasProfile) return;

    const target = numberInput(targetBodyFatPercent, { min: 4, max: 50, step: 0.5, placeholder: t('مثال: 12') });
    const status = h('div');
    const save = h('button', { type: 'submit', class: 'btn' }, t('حفظ الهدف'));
    const clear = h('button', { type: 'button', class: 'btn btn-secondary', hidden: targetBodyFatPercent === null }, t('إزالة الهدف'));
    const form = h('form', { class: 'stack', novalidate: true },
      field(t('نسبة الدهون المستهدفة (٪)'), target, { name: 'targetBodyFatPercent' }),
      status,
      h('div', { class: 'row' }, save, clear));

    const submit = async (value) => {
      clearErrors(form);
      fill(status);
      save.disabled = true;
      try {
        await put('/body/goal', { targetBodyFatPercent: value });
        showSaved();
        load();
      } catch (error) {
        const message = showErrors(form, error);
        if (message) fill(status, calmNotice(message));
      } finally {
        save.disabled = false;
      }
    };
    form.addEventListener('submit', (event) => { event.preventDefault(); submit(readNumber(target)); });
    clear.addEventListener('click', () => submit(null));

    fill(goalCard, h('h2', { text: t('هدف نسبة الدهون') }), form, projectionView(projection, latest));
  }

  function renderCharts({ entries }) {
    const measured = entries.filter((e) => e.composition);
    chartCard.hidden = measured.length < 2;
    if (chartCard.hidden) return;
    const points = (pick) => measured.map((e) => ({ x: parseIso(e.date).getTime() / DAY_MS, y: pick(e.composition), label: formatShort(e.date) }));
    fill(chartCard,
      h('h2', { text: t('نسبة الدهون مع الوقت') }),
      lineChart({ points: points((c) => c.bodyFatPercent), label: t('رسم بياني لنسبة الدهون') }),
      h('h2', { text: t('الكتلة الصافية مع الوقت'), style: 'margin-top:16px' }),
      lineChart({ points: points((c) => c.leanMassKg), label: t('رسم بياني للكتلة الصافية') }),
      h('p', { class: 'muted small', text: t('لو نزلت الدهون وثبتت الكتلة الصافية، فأنت تخسر دهوناً لا عضلاً.') }));
  }

  function renderList({ entries }) {
    listCard.hidden = entries.length === 0;
    if (listCard.hidden) return;
    fill(listCard,
      h('h2', { text: t('القياسات المسجلة') }),
      h('ul', { class: 'item-list' }, entries.slice().reverse().map((e) =>
        h('li', { class: 'item' },
          h('div', { class: 'grow' },
            h('div', { class: 'title', text: formatLong(e.date) }),
            h('div', { class: 'muted small', text: describe(e) })),
          e.composition && h('span', { class: 'num', text: t('{n}٪', { n: fmt(e.composition.bodyFatPercent) }) }),
          h('button', { type: 'button', class: 'icon-btn', 'aria-label': t('حذف قياس {date}', { date: formatLong(e.date) }), onclick: () => remove(e) }, icon('trash', 18))))));
  }

  async function remove(entry) {
    const ok = await confirmDialog({ title: t('حذف القياس'), message: t('حذف قياسات {date}؟', { date: formatLong(entry.date) }), confirmText: t('حذف'), danger: true });
    if (!ok) return;
    try {
      await del(`/body/${entry.id}`);
      showSaved(t('تم الحذف'));
      load();
    } catch (error) {
      fill(listCard, calmNotice(error, load));
    }
  }
}

function projectionView(projection, latest) {
  if (!projection) {
    return latest ? null : h('p', { class: 'muted small', text: t('سجّل قياساً أولاً لنحسب لك المطلوب.') });
  }
  if (projection.reached) {
    return h('div', { class: 'stack', style: 'margin-top:12px' },
      h('p', { text: t('نسبتك الحالية عند هدفك أو أقل. حافظ على الكتلة الصافية وارفعها تدريجياً.') }),
      projection.warnings.map((w) => calmNotice(bodyWarnings[w] ?? w)));
  }
  // The week range is written left to right so "9–18" never shows as "18–9" in Arabic.
  const weeks = stat(`${projection.weeksAtFastestPace}–${projection.weeksAtSlowestPace}`, t('أسبوع تقريباً'));
  weeks.querySelector('.value').dir = 'ltr';
  return h('div', { class: 'stack', style: 'margin-top:12px' },
    h('div', { class: 'grid-3 collapse' },
      stat(fmt(projection.targetWeightKg), t('الوزن المستهدف (كغ)')),
      stat(fmt(projection.fatToLoseKg), t('دهون للخسارة (كغ)')),
      weeks),
    h('p', { class: 'muted small', text: t('بافتراض الحفاظ على كتلتك الصافية، وبمعدل آمن من 0.5 إلى 1٪ من وزنك أسبوعياً. حافظ على بروتين عالٍ وتمارين المقاومة.') }),
    projection.warnings.map((w) => calmNotice(bodyWarnings[w] ?? w)));
}

function describe(e) {
  const parts = [
    [e.waistCm, t('خصر')], [e.neckCm, t('رقبة')], [e.hipCm, t('ورك')],
    [e.chestCm, t('صدر')], [e.armCm, t('ذراع')], [e.thighCm, t('فخذ')],
  ].filter(([value]) => value !== null).map(([value, label]) => `${label} ${fmt(value)}`);
  if (e.bodyFatPercentInput !== null) parts.push(t('دهون الجهاز {n}٪', { n: fmt(e.bodyFatPercentInput) }));
  if (e.muscleMassKg !== null) parts.push(t('عضل {n} كغ', { n: fmt(e.muscleMassKg) }));
  return parts.join(' · ');
}

function stat(value, label, note) {
  return h('div', { class: 'stat' },
    h('span', { class: 'value', text: value }),
    h('span', { class: 'label', text: label }),
    note && h('span', { class: 'label', style: 'display:block;font-weight:700', text: note }));
}

function measurementForm(onSaved) {
  const date = h('input', { type: 'date', value: todayLocal(), max: todayLocal(), required: true });
  const cm = (min, max) => numberInput(null, { min, max, step: 0.1, placeholder: t('سم') });
  const inputs = {
    neckCm: cm(20, 70), waistCm: cm(40, 200), hipCm: cm(50, 200),
    chestCm: cm(50, 200), armCm: cm(15, 80), thighCm: cm(30, 120),
    bodyFatPercent: numberInput(null, { min: 2, max: 70, step: 0.1, placeholder: '٪' }),
    muscleMassKg: numberInput(null, { min: 10, max: 150, step: 0.1, placeholder: t('كغ') }),
  };
  const status = h('div');
  const button = h('button', { type: 'submit', class: 'btn' }, icon('plus', 16), t('حفظ القياسات'));
  const form = h('form', { class: 'card stack', novalidate: true },
    h('h2', { text: t('سجّل قياساتك') }),
    field(t('التاريخ'), date, { name: 'date' }),
    h('p', { class: 'muted small', text: t('قِس بشريط قياس على الجلد مباشرة، صباحاً قبل الأكل. الرقبة تحت تفاحة آدم، والخصر عند السرة للرجال وعند أنحف نقطة للنساء، والورك عند أعرض نقطة (مطلوب للنساء).') }),
    h('div', { class: 'grid-3 collapse' },
      field(t('الرقبة (سم)'), inputs.neckCm, { name: 'neckCm' }),
      field(t('الخصر (سم)'), inputs.waistCm, { name: 'waistCm' }),
      field(t('الورك (سم)'), inputs.hipCm, { name: 'hipCm' })),
    h('div', { class: 'grid-3 collapse' },
      field(t('الصدر (سم)'), inputs.chestCm, { name: 'chestCm' }),
      field(t('الذراع (سم)'), inputs.armCm, { name: 'armCm' }),
      field(t('الفخذ (سم)'), inputs.thighCm, { name: 'thighCm' })),
    h('details', { class: 'inline-form' },
      h('summary', { text: t('عندي قياس من جهاز (InBody أو DEXA)') }),
      h('div', { class: 'grid-2' },
        field(t('نسبة الدهون (٪)'), inputs.bodyFatPercent, { name: 'bodyFatPercent', hint: t('تُستخدم بدل تقدير الشريط') }),
        field(t('الكتلة العضلية (كغ)'), inputs.muscleMassKg, { name: 'muscleMassKg' }))),
    h('p', { class: 'muted small', text: t('الوزن يؤخذ من سجل الوزن. تسجيل نفس اليوم مرة أخرى يستبدل القياسات السابقة.') }),
    status,
    button);

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    clearErrors(form);
    fill(status);
    button.disabled = true;
    try {
      const body = { date: date.value };
      for (const [name, input] of Object.entries(inputs)) {
        const value = readNumber(input);
        if (value !== null) body[name] = value;
      }
      await post('/body', body);
      Object.values(inputs).forEach((input) => { input.value = ''; });
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
