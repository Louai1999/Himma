import { h, uid } from './dom.js';
import { errorText } from './notice.js';

/** A labelled field. `name` is the API field name used to show server validation errors. */
export function field(labelText, control, { name, hint } = {}) {
  const id = control.id || uid('f');
  control.id = id;
  if (name) control.name = name;
  const errorId = `${id}-error`;
  control.setAttribute('aria-describedby', errorId);
  return h('div', { class: 'field', dataset: name ? { field: name } : {} },
    h('label', { for: id, text: labelText }),
    control,
    hint && h('span', { class: 'hint', text: hint }),
    h('span', { class: 'field-error', id: errorId }));
}

export function selectOf(options, selected, props = {}) {
  return h('select', props,
    Object.entries(options).map(([value, label]) => h('option', { value, selected: value === selected, text: label })));
}

export function numberInput(value, { min, max, step = 'any', placeholder } = {}) {
  return h('input', { type: 'number', inputmode: 'decimal', min, max, step, placeholder, value: value ?? '' });
}

/** Reads a number input: empty means null. */
export function readNumber(input) {
  const text = input.value.trim();
  if (text === '') return null;
  const value = Number(text);
  return Number.isFinite(value) ? value : null;
}

export function clearErrors(form) {
  form.querySelectorAll('.field-error').forEach((el) => { el.textContent = ''; });
  form.querySelectorAll('[aria-invalid]').forEach((el) => el.removeAttribute('aria-invalid'));
}

/**
 * Shows server validation errors next to their fields. Returns the message for anything
 * that has no matching field, so the caller can show it in a notice.
 */
export function showErrors(form, error) {
  clearErrors(form);
  const unmatched = [];
  for (const [key, messages] of Object.entries(error?.errors ?? {})) {
    const fieldName = key.charAt(0).toLowerCase() + key.slice(1);
    const wrapper = form.querySelector(`[data-field="${CSS.escape(fieldName)}"]`);
    if (wrapper) {
      wrapper.querySelector('.field-error').textContent = messages.join(' ');
      wrapper.querySelector('input, select, textarea')?.setAttribute('aria-invalid', 'true');
    } else {
      unmatched.push(...messages);
    }
  }
  if (Object.keys(error?.errors ?? {}).length === 0) return errorText(error);
  return unmatched.length ? unmatched.join(' ') : null;
}
