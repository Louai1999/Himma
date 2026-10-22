import { t } from '../i18n/index.js';
import { h, fill } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { openModal } from '../ui/modal.js';
import { field, showErrors, clearErrors } from '../ui/forms.js';
import { calmNotice } from '../ui/notice.js';

/**
 * Confirmation dialog for destructive admin actions: asks for the admin's password again and,
 * for permanent deletion, the target's username typed exactly. Server errors (wrong password,
 * wrong username) appear inside the dialog, which stays open.
 */
export function openPasswordAction({ title, message, details = [], confirmText, username, requireUsername = false, irreversible = false, onConfirm }) {
  const password = h('input', { type: 'password', autocomplete: 'current-password', dir: 'ltr' });
  const confirmName = requireUsername ? h('input', { type: 'text', autocomplete: 'off', dir: 'ltr', spellcheck: 'false' }) : null;
  const status = h('div');
  const submit = h('button', { type: 'submit', class: 'btn btn-danger', disabled: true }, confirmText);

  const form = h('form', { class: 'stack', novalidate: true },
    h('p', { text: message }),
    details.length > 0 && h('ul', { class: 'erase-list' }, details.map((item) => h('li', { text: item }))),
    irreversible && h('div', { class: 'irreversible', role: 'note' }, icon('info', 18), h('strong', { text: t('لا يمكن التراجع عن هذا الإجراء.') })),
    confirmName && field(t('اكتب اسم المستخدم {username} للتأكيد', { username }), confirmName, { name: 'confirmUsername' }),
    field(t('كلمة مرورك (للتأكيد)'), password, { name: 'adminPassword' }),
    status,
    h('div', { class: 'row' },
      submit,
      h('button', { type: 'button', class: 'btn btn-secondary', onclick: () => modal.close() }, t('إلغاء'))));

  // The button only wakes up when the typed username matches exactly.
  const refresh = () => {
    submit.disabled = !password.value || (confirmName !== null && confirmName.value !== username);
  };
  password.addEventListener('input', refresh);
  confirmName?.addEventListener('input', refresh);

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    if (submit.disabled) return;
    clearErrors(form);
    fill(status);
    submit.disabled = true;
    try {
      await onConfirm({ adminPassword: password.value, confirmUsername: confirmName?.value });
      modal.close();
    } catch (error) {
      const text = showErrors(form, error);
      if (text) fill(status, calmNotice(text));
      password.value = '';
      password.focus();
    } finally {
      refresh();
    }
  });

  const modal = openModal({ title, content: form });
  (confirmName ?? password).focus();
  return modal;
}
