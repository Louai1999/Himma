import { t } from '../i18n/index.js';
import { h, fill } from './dom.js';

// Rest timer between sets. It counts to an end time (not ticks), so it stays right even when the
// phone throttles the page. At the end: a short beep and a vibration where the device allows it.

function beep() {
  try {
    const AudioCtx = window.AudioContext || window.webkitAudioContext;
    if (!AudioCtx) return;
    const ctx = new AudioCtx();
    const tone = ctx.createOscillator();
    const gain = ctx.createGain();
    tone.frequency.value = 880;
    gain.gain.setValueAtTime(0.15, ctx.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.6);
    tone.connect(gain).connect(ctx.destination);
    tone.start();
    tone.stop(ctx.currentTime + 0.6);
    tone.onended = () => ctx.close();
  } catch { /* sound is a nice extra */ }
}

const clock = (seconds) => `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;

/** Returns { element, start(seconds), stop() }. Put the element anywhere; it shows fixed at the bottom. */
export function restTimer() {
  const time = h('span', { class: 'num rest-time', dir: 'ltr' });
  const label = h('span', { class: 'rest-label' });
  const element = h('div', { class: 'rest-timer', role: 'timer', 'aria-live': 'polite', hidden: true },
    label, time,
    h('button', { type: 'button', class: 'btn btn-sm btn-secondary', onclick: () => extend(15), text: '+15' }),
    h('button', { type: 'button', class: 'btn btn-sm btn-ghost', onclick: stop, text: t('تخطي') }));

  let endsAt = 0;
  let interval = null;
  let hideLater = null;

  function start(seconds) {
    clearTimeout(hideLater);
    endsAt = Date.now() + seconds * 1000;
    element.hidden = false;
    element.classList.remove('finished');
    tick();
    clearInterval(interval);
    interval = setInterval(tick, 250);
  }

  function extend(seconds) {
    endsAt = Math.max(endsAt, Date.now()) + seconds * 1000;
    element.classList.remove('finished');
    if (!interval) interval = setInterval(tick, 250);
    tick();
  }

  function tick() {
    const left = Math.max(0, Math.ceil((endsAt - Date.now()) / 1000));
    fill(label, left > 0 ? t('راحة') : t('يلا الجولة الجاية 💪'));
    fill(time, left > 0 ? clock(left) : '');
    if (left === 0) {
      clearInterval(interval);
      interval = null;
      element.classList.add('finished');
      beep();
      navigator.vibrate?.([200, 100, 200]);
      hideLater = setTimeout(stop, 6000);
    }
  }

  function stop() {
    clearInterval(interval);
    clearTimeout(hideLater);
    interval = null;
    element.hidden = true;
  }

  return { element, start, stop };
}
