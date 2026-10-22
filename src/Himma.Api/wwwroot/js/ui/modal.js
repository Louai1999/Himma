import { t } from '../i18n/index.js';
import { h, fill, add, uid } from './dom.js';
import { icon } from './icons.js';

/**
 * Opens an accessible modal using the native <dialog>: focus stays inside, Escape closes it,
 * and focus returns to the element that opened it.
 */
export function openModal({ title, content, onClose }) {
  const previous = document.activeElement;
  const titleId = uid('modal-title');
  let dialog;
  const close = () => { if (dialog.open) dialog.close(); };

  dialog = h('dialog', { class: 'modal', 'aria-labelledby': titleId },
    h('div', { class: 'modal-inner' },
      h('div', { class: 'modal-head' },
        h('h2', { id: titleId, text: title }),
        h('button', { type: 'button', class: 'icon-btn', 'aria-label': t('إغلاق'), onclick: close }, icon('close'))),
      content));

  dialog.addEventListener('close', () => {
    dialog.remove();
    onClose?.();
    if (previous instanceof HTMLElement && previous.isConnected) previous.focus();
  });
  // A click on the backdrop lands on the <dialog> itself, not on its inner wrapper.
  dialog.addEventListener('click', (event) => { if (event.target === dialog) close(); });

  add(document.body, dialog);
  dialog.showModal();
  return { close, dialog };
}

export function confirmDialog({ title, message, confirmText = t('تأكيد'), danger = false }) {
  return new Promise((resolve) => {
    let answered = false;
    const finish = (value) => { answered = true; resolve(value); modal.close(); };
    const modal = openModal({
      title,
      content: h('div', { class: 'stack' },
        h('p', { text: message }),
        h('div', { class: 'row' },
          h('button', { type: 'button', class: danger ? 'btn btn-danger' : 'btn', onclick: () => finish(true) }, confirmText),
          h('button', { type: 'button', class: 'btn btn-secondary', onclick: () => finish(false) }, t('إلغاء')))),
      onClose: () => { if (!answered) resolve(false); },
    });
  });
}
