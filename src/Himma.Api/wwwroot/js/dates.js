// Dates are "yyyy-MM-dd" strings in the user's local timezone.

const pad = (n) => String(n).padStart(2, '0');

export function toIso(date) {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

export function todayLocal() {
  return toIso(new Date());
}

export function parseIso(iso) {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, m - 1, d);
}

export function addDays(iso, days) {
  const date = parseIso(iso);
  date.setDate(date.getDate() + days);
  return toIso(date);
}

const DAY_KEYS = ['sun', 'mon', 'tue', 'wed', 'thu', 'fri', 'sat'];

export function dayKeyOf(iso) {
  return DAY_KEYS[parseIso(iso).getDay()];
}

// Arabic month and weekday names, with Latin digits to match the number font.
const longFormat = new Intl.DateTimeFormat('ar-u-nu-latn', { weekday: 'long', day: 'numeric', month: 'long' });
const timeFormat = new Intl.DateTimeFormat('ar-u-nu-latn', { hour: 'numeric', minute: '2-digit' });

export const formatLong = (iso) => longFormat.format(parseIso(iso));
// Day/month for chart labels, built by hand: Intl adds direction marks that garble inside LTR charts.
export const formatShort = (iso) => {
  const date = parseIso(iso);
  return `${date.getDate()}/${date.getMonth() + 1}`;
};
export const formatTime = (utc) => timeFormat.format(new Date(utc));
