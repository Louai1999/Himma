import { h, fill, add } from './dom.js';
import { icon } from './icons.js';

const NETWORK_TEXT = 'تعذّر الاتصال. تحقق من الإنترنت ثم حاول مرة أخرى.';

export function errorText(error) {
  if (!error) return 'حدث خطأ غير متوقع.';
  if (error.isNetwork) return NETWORK_TEXT;
  const fieldMessages = Object.values(error.errors ?? {}).flat();
  return error.detail || fieldMessages[0] || error.title || error.message || 'حدث خطأ غير متوقع.';
}

/**
 * A calm inline message with an optional retry action. Used instead of red error pop-ups,
 * especially for temporary problems like a lost connection.
 */
export function calmNotice(error, onRetry) {
  return h('div', { class: 'notice', role: 'status' },
    h('span', { text: typeof error === 'string' ? error : errorText(error) }),
    onRetry && h('button', { type: 'button', class: 'btn btn-ghost btn-sm', onclick: onRetry }, icon('refresh', 16), 'إعادة المحاولة'));
}

export function skeleton(lines = 3) {
  return h('div', { class: 'skeleton', 'aria-hidden': 'true' }, Array.from({ length: lines }, () => h('span')));
}

/**
 * Shows a skeleton, runs the loader, then renders. On failure shows a calm notice with retry.
 * 401 is ignored here because the app already returns to the sign-in screen.
 */
export async function loadInto(container, loader, render) {
  fill(container, skeleton());
  try {
    const data = await loader();
    fill(container);
    render(data);
  } catch (error) {
    if (error?.status === 401) return;
    fill(container, calmNotice(error, () => loadInto(container, loader, render)));
  }
}
