import { t, lang } from './i18n/index.js';
import { get, post } from './api.js';
import { h, fill, add } from './ui/dom.js';
import { icon } from './ui/icons.js';
import { calmNotice } from './ui/notice.js';
import { showSaved } from './ui/toast.js';

// Web Push: reminders that arrive even when Himma is closed. The browser gives an endpoint at its push
// service; the server sends to it at the right local time. iPhone only allows this for web apps
// added to the Home Screen.

export const pushSupported = 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;

const isIos = /iphone|ipad|ipod/i.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
const isInstalled = window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true;

function keyBytes(base64Url) {
  const base64 = (base64Url + '='.repeat((4 - (base64Url.length % 4)) % 4)).replace(/-/g, '+').replace(/_/g, '/');
  return Uint8Array.from(atob(base64), (c) => c.charCodeAt(0));
}

// serviceWorker.ready never settles when the service worker failed to install, so it is always raced
// against a timeout: sign-out and the account screen must never hang on notifications.
function readyRegistration(ms = 4000) {
  return Promise.race([
    navigator.serviceWorker.ready,
    new Promise((_, reject) => setTimeout(() => reject(new Error(t('التنبيهات غير جاهزة في هذا المتصفح. حدّث الصفحة وحاول مرة أخرى.'))), ms)),
  ]);
}

async function subscription() {
  if (!pushSupported) return null;
  // getRegistration answers at once (undefined when there is none), unlike serviceWorker.ready.
  const registration = await navigator.serviceWorker.getRegistration();
  return registration ? registration.pushManager.getSubscription() : null;
}

async function save(sub) {
  const json = sub.toJSON();
  await post('/push/subscriptions', {
    endpoint: json.endpoint,
    p256dh: json.keys.p256dh,
    auth: json.keys.auth,
    timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC',
    language: lang,
  });
}

/** Asks for permission, subscribes this browser and tells the server. Throws a readable message on failure. */
export async function enablePush() {
  if ((await Notification.requestPermission()) !== 'granted') {
    throw new Error(t('لم تسمح بالإشعارات. يمكنك السماح بها من إعدادات المتصفح للموقع.'));
  }
  const { publicKey } = await get('/push/key');
  const registration = await readyRegistration();
  const options = { userVisibleOnly: true, applicationServerKey: keyBytes(publicKey) };
  let sub;
  try {
    sub = await registration.pushManager.subscribe(options);
  } catch {
    // An old subscription made with another key blocks a new one: drop it and try once more.
    await (await registration.pushManager.getSubscription())?.unsubscribe();
    sub = await registration.pushManager.subscribe(options);
  }
  await save(sub);
}

/** Stops reminders on this browser, for this account. */
export async function disablePush() {
  const sub = await subscription();
  if (!sub) return;
  try { await post('/push/subscriptions/delete', { endpoint: sub.endpoint }); } catch { /* unsubscribing below is what matters */ }
  await sub.unsubscribe();
}

/**
 * After sign-in: re-links an existing subscription to the signed-in account and refreshes its time zone
 * and language (both may have changed since it was created).
 */
export async function syncPush() {
  try {
    if (!pushSupported || Notification.permission !== 'granted') return;
    const sub = await subscription();
    if (sub) await save(sub);
  } catch { /* reminders are optional; never block the app for them */ }
}

/** Before sign-out: this browser should stop getting this account's reminders. */
export async function forgetPushDevice() {
  try {
    const sub = await subscription();
    if (sub) await post('/push/subscriptions/delete', { endpoint: sub.endpoint });
  } catch { /* signing out still works */ }
}

/** "Notifications on this device" panel: status, enable/disable, test, and the iPhone hint. */
export function pushControls() {
  const box = h('div', { class: 'stack' });
  render();
  return box;

  async function render() {
    fill(box);
    if (isIos && !isInstalled) {
      add(box, h('p', { class: 'notice info', text: t('على الآيفون: افتح هِمّة في Safari، واضغط زر المشاركة ثم «إضافة إلى الشاشة الرئيسية»، وافتحه من هناك لتفعيل التنبيهات.') }));
      return;
    }
    if (!pushSupported) {
      add(box, h('p', { class: 'muted small', text: t('هذا المتصفح لا يدعم التنبيهات. جرّب Chrome أو Edge أو Firefox أو Safari.') }));
      return;
    }

    const status = h('div');
    const on = Notification.permission === 'granted' && (await subscription()) !== null;
    if (on) {
      add(box,
        h('p', { class: 'row small' }, icon('bell', 16), t('التنبيهات مفعّلة على هذا الجهاز، حتى والتطبيق مغلق.')),
        h('div', { class: 'row' },
          action(t('إرسال تنبيه تجريبي'), 'btn btn-secondary btn-sm', async () => {
            const result = await post('/push/test');
            showSaved(result.sent > 0 ? t('أُرسل التنبيه') : t('لم يصل التنبيه، أعد تفعيله'));
          }),
          action(t('إيقاف التنبيهات هنا'), 'btn btn-ghost btn-sm', async () => { await disablePush(); render(); })),
        status);
    } else {
      add(box,
        h('p', { class: 'muted small', text: t('فعّل التنبيهات لتصلك تذكيرات المكملات والماء في وقتها، حتى والتطبيق مغلق.') }),
        action(t('تفعيل التنبيهات على هذا الجهاز'), 'btn btn-sm', async () => { await enablePush(); showSaved(t('تم التفعيل')); render(); }, 'bell'),
        status);
    }

    function action(label, cls, run, iconName) {
      return h('button', {
        type: 'button', class: cls, onclick: async (event) => {
          const button = event.currentTarget;
          button.disabled = true;
          fill(status);
          try {
            await run();
          } catch (error) {
            fill(status, calmNotice(error));
          } finally {
            button.disabled = false;
          }
        },
      }, iconName && icon(iconName, 16), label);
    }
  }
}
