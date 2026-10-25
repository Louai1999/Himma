import { t } from '../i18n/index.js';
import { get, post } from '../api.js';
import { emit } from '../state.js';
import { todayLocal } from '../dates.js';
import { h, fill, add } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { showSaved } from '../ui/toast.js';

// Water reminder while the app is open: a calm banner every N hours. When the app is closed, the
// server sends the reminder as a push notification instead (see push.js). Settings are stored per user.

let timer = null;

export async function startWaterReminder() {
  stopWaterReminder();
  try {
    const setting = await get('/water/reminder');
    if (setting.enabled) {
      timer = setInterval(remind, setting.intervalHours * 60 * 60 * 1000);
    }
  } catch {
    // A missing reminder is not worth bothering the user about.
  }
}

export function stopWaterReminder() {
  clearInterval(timer);
  timer = null;
  document.getElementById('banner-slot')?.replaceChildren();
}

export async function addWater(ml, date = todayLocal()) {
  const entry = await post('/water', { date, ml });
  emit('water-changed', { date });
  return entry;
}

function remind() {
  const slot = document.getElementById('banner-slot');
  if (!slot) return;
  const close = () => fill(slot);
  fill(slot, h('div', { class: 'notice info banner', role: 'status' },
    h('span', { class: 'row' }, icon('drop', 18), t('وقت كوب ماء.')),
    h('span', { class: 'row' },
      h('button', {
        type: 'button', class: 'btn btn-sm', onclick: async (event) => {
          const button = event.currentTarget;
          button.disabled = true;
          try {
            await addWater(250);
            showSaved();
            close();
          } catch {
            button.disabled = false;
          }
        },
      }, t('سجّل 250 مل')),
      h('button', { type: 'button', class: 'icon-btn', 'aria-label': t('إغلاق التذكير'), onclick: close }, icon('close', 18)))));
}
