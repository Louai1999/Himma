// Builds DOM nodes without ever parsing HTML. Text always goes in as text nodes,
// so user content (food names, notes, display names) can never become markup.

const SVG_NS = 'http://www.w3.org/2000/svg';
const PROPERTIES = new Set(['value', 'checked', 'disabled', 'selected', 'hidden', 'required', 'readOnly', 'multiple', 'open']);

export function h(tag, props = {}, ...children) {
  const el = document.createElement(tag);
  setProps(el, props);
  append(el, children);
  return el;
}

export function svg(tag, attrs = {}, ...children) {
  const el = document.createElementNS(SVG_NS, tag);
  for (const [key, value] of Object.entries(attrs)) {
    if (value === undefined || value === null || value === false) continue;
    if (key === 'class') el.setAttribute('class', value);
    else el.setAttribute(key, String(value));
  }
  append(el, children);
  return el;
}

/**
 * Replaces an element's children. Unlike the built-in replaceChildren, it accepts nested
 * arrays and skips null/false, so `cond && node` and `list.map(...)` work as in h().
 */
export function fill(el, ...children) {
  el.replaceChildren();
  append(el, children);
  return el;
}

/** Appends children with the same rules as fill() and h(). */
export function add(el, ...children) {
  append(el, children);
  return el;
}

function setProps(el, props) {
  for (const [key, value] of Object.entries(props ?? {})) {
    if (value === undefined || value === null || value === false) continue;
    if (key === 'class') el.className = value;
    else if (key === 'text') el.textContent = String(value);
    else if (key === 'dataset') Object.assign(el.dataset, value);
    else if (key.startsWith('on') && typeof value === 'function') el.addEventListener(key.slice(2), value);
    else if (PROPERTIES.has(key)) el[key] = value;
    else el.setAttribute(key, value === true ? '' : String(value));
  }
}

function append(el, children) {
  for (const child of children.flat(Infinity)) {
    if (child === undefined || child === null || child === false) continue;
    el.append(child instanceof Node ? child : String(child));
  }
}

let idCounter = 0;
export const uid = (prefix = 'id') => `${prefix}-${++idCounter}`;

export function debounce(fn, ms) {
  let timer;
  return (...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), ms);
  };
}

/** Formats a number for display: at most one decimal, no trailing zeros. */
export function fmt(value) {
  if (value === null || value === undefined || Number.isNaN(value)) return '—';
  return String(Math.round(value * 10) / 10);
}

export function num(value, extraClass = '') {
  return h('span', { class: `num ${extraClass}`.trim(), text: fmt(value) });
}
