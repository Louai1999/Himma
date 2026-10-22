import { get, put, post, setSession, clearSession } from '../api.js';
import { state, emit } from '../state.js';
import { genders, activities, goals, targetWarnings } from '../labels.js';
import { todayLocal } from '../dates.js';
import { h, fill, add, fmt } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { field, selectOf, numberInput, readNumber, showErrors, clearErrors } from '../ui/forms.js';
import { calmNotice, loadInto } from '../ui/notice.js';
import { showSaved } from '../ui/toast.js';
import { startWaterReminder } from './water.js';

/** The profile with its targets, or null when the user has not filled it yet. */
export async function fetchProfile() {
  try {
    return await get('/profile');
  } catch (error) {
    if (error.status === 404) return null;
    throw error;
  }
}

export function targetsList(targets) {
  return h('div', { class: 'stack' },
    h('div', { class: 'grid-3 collapse' },
      stat(targets.targetCalories, 'سعرة يومياً'),
      stat(targets.proteinTargetG, 'غ بروتين'),
      stat(targets.waterTargetMl, 'مل ماء')),
    h('p', { class: 'muted small', text: `معدل الحرق الأساسي ${fmt(targets.bmr)} سعرة، واحتياجك مع النشاط ${fmt(targets.tdee)} سعرة.` }),
    targets.warnings.map((w) => calmNotice(targetWarnings[w] ?? w)));
}

function stat(value, label) {
  return h('div', { class: 'stat' }, h('span', { class: 'value', text: fmt(value) }), h('span', { class: 'label', text: label }));
}

export function mount(view) {
  const profileCard = h('div', { class: 'card' });
  const targetsCard = h('div', { class: 'card', hidden: true });
  add(view, 
    h('header', { class: 'view-head' }, h('h1', { text: 'حسابي' })),
    profileCard,
    targetsCard,
    reminderCard(),
    accountCard());

  loadInto(profileCard, fetchProfile, (data) => {
    add(profileCard, profileForm(data?.profile ?? null));
    if (data) showTargets(data.targets);
  });

  function showTargets(targets) {
    targetsCard.hidden = false;
    fill(targetsCard, h('h2', { text: 'أهدافك اليومية' }), targetsList(targets));
  }

  function profileForm(profile) {
    const gender = selectOf({ '': 'اختر', ...genders }, profile?.gender ?? '');
    const age = numberInput(profile?.age, { min: 13, max: 100, step: 1 });
    const height = numberInput(profile?.heightCm, { min: 100, max: 250, step: 'any' });
    const weight = numberInput(profile?.weightKg, { min: 30, max: 300, step: 'any' });
    const activity = selectOf({ '': 'اختر', ...activities }, profile?.activityLevel ?? '');
    const goal = selectOf({ '': 'اختر', ...goals }, profile?.goal ?? '');
    const targetWeight = numberInput(profile?.targetWeightKg, { min: 30, max: 300, step: 'any' });
    const status = h('div');
    const button = h('button', { type: 'submit', class: 'btn' }, 'حفظ واحسب أهدافي');

    const form = h('form', { class: 'stack', novalidate: true },
      h('h2', { text: 'ملفك الشخصي' }),
      !profile && h('p', { class: 'muted', text: 'املأ بياناتك لنحسب لك السعرات والبروتين والماء المناسبة لهدفك.' }),
      h('div', { class: 'grid-2' },
        field('الجنس', gender, { name: 'gender' }),
        field('العمر', age, { name: 'age' }),
        field('الطول (سم)', height, { name: 'heightCm' }),
        field('الوزن الحالي (كغ)', weight, { name: 'weightKg' })),
      field('مستوى النشاط', activity, { name: 'activityLevel' }),
      h('div', { class: 'grid-2' },
        field('هدفك', goal, { name: 'goal' }),
        field('الوزن المستهدف (اختياري)', targetWeight, { name: 'targetWeightKg' })),
      status,
      button);

    form.addEventListener('submit', async (event) => {
      event.preventDefault();
      clearErrors(form);
      fill(status);
      button.disabled = true;
      try {
        const saved = await put('/profile', {
          date: todayLocal(),
          gender: gender.value || null,
          age: readNumber(age),
          heightCm: readNumber(height),
          weightKg: readNumber(weight),
          activityLevel: activity.value || null,
          goal: goal.value || null,
          targetWeightKg: readNumber(targetWeight),
        });
        showSaved();
        showTargets(saved.targets);
        emit('profile-changed', saved);
      } catch (error) {
        const message = showErrors(form, error);
        if (message) fill(status, calmNotice(message));
      } finally {
        button.disabled = false;
      }
    });
    return form;
  }
}

function reminderCard() {
  const card = h('div', { class: 'card' });
  loadInto(card, () => get('/water/reminder'), (setting) => {
    const enabled = h('input', { type: 'checkbox', id: 'reminder-enabled', checked: setting.enabled });
    const interval = selectOf({ 1: 'كل ساعة', 2: 'كل ساعتين', 3: 'كل 3 ساعات', 4: 'كل 4 ساعات', 6: 'كل 6 ساعات' }, String(setting.intervalHours));
    const status = h('div');
    const permission = 'Notification' in window && Notification.permission === 'default'
      ? h('button', {
        type: 'button', class: 'btn btn-secondary btn-sm', onclick: async (event) => {
          const button = event.currentTarget;
          await Notification.requestPermission();
          button.remove();
        },
      }, icon('bell', 16), 'السماح بإشعارات المتصفح')
      : null;

    async function save() {
      fill(status);
      try {
        await put('/water/reminder', { enabled: enabled.checked, intervalHours: Number(interval.value) });
        showSaved();
        startWaterReminder();
      } catch (error) {
        fill(status, calmNotice(error, save));
      }
    }
    enabled.addEventListener('change', save);
    interval.addEventListener('change', save);

    add(card, 
      h('h2', { text: 'تذكير الماء' }),
      h('p', { class: 'muted small', text: 'يظهر التذكير أثناء فتح التطبيق، ويمكنك السماح بإشعارات المتصفح أيضاً.' }),
      h('label', { class: 'check', for: 'reminder-enabled' }, enabled, h('span', { text: 'فعّل التذكير' })),
      field('المدة', interval),
      permission,
      status);
  });
  return card;
}

function accountCard() {
  const current = h('input', { type: 'password', autocomplete: 'current-password', dir: 'ltr' });
  const next = h('input', { type: 'password', autocomplete: 'new-password', dir: 'ltr', minlength: 8 });
  const status = h('div');
  const button = h('button', { type: 'submit', class: 'btn btn-secondary' }, 'تغيير كلمة المرور');
  const form = h('form', { class: 'stack', novalidate: true },
    h('h3', { text: 'تغيير كلمة المرور' }),
    field('كلمة المرور الحالية', current, { name: 'currentPassword' }),
    field('كلمة المرور الجديدة', next, { name: 'newPassword', hint: 'تغييرها يسجّل خروجك من الأجهزة الأخرى.' }),
    status,
    button);

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    clearErrors(form);
    fill(status);
    button.disabled = true;
    try {
      const auth = await post('/auth/change-password', { currentPassword: current.value, newPassword: next.value });
      setSession(auth);
      current.value = '';
      next.value = '';
      showSaved();
    } catch (error) {
      const message = showErrors(form, error);
      if (message) fill(status, calmNotice(message));
    } finally {
      button.disabled = false;
    }
  });

  const logout = h('button', {
    type: 'button', class: 'btn btn-ghost', onclick: async () => {
      try { await post('/auth/logout'); } catch { /* signing out locally is enough */ }
      clearSession();
      emit('signed-out');
    },
  }, icon('logout', 18), 'تسجيل الخروج');

  return h('div', { class: 'card stack' },
    h('div', { class: 'row spread' },
      h('div', {},
        h('h2', { text: state.user?.displayName ?? '' }),
        h('p', { class: 'muted small', dir: 'ltr', text: `@${state.user?.username ?? ''}` })),
      logout),
    form);
}
