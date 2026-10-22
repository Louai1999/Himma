import { t } from '../i18n/index.js';
import { h, svg, fmt } from './dom.js';

// Hand-written SVG charts: no chart library, so the Content-Security-Policy can stay strict.

/** Progress ring. `color` is a CSS color; the arc animates in with the settle easing. */
export function ring({ value, max, label, sub = '', color = 'var(--forest)', remaining }) {
  const radius = 46;
  const circumference = 2 * Math.PI * radius;
  const ratio = max > 0 ? Math.min(Math.max(value / max, 0), 1) : 0;

  const progress = svg('circle', {
    cx: 56, cy: 56, r: radius, class: 'ring-progress', stroke: color,
    'stroke-dasharray': circumference.toFixed(2), transform: 'rotate(-90 56 56)',
  });
  progress.style.strokeDashoffset = String(circumference);
  requestAnimationFrame(() => requestAnimationFrame(() => {
    progress.style.strokeDashoffset = String(circumference * (1 - ratio));
  }));

  return h('figure', { class: 'ring' },
    svg('svg', { viewBox: '0 0 112 112', role: 'img', 'aria-label': t('{label}: {value} من {max} {sub}', { label, value: fmt(value), max: fmt(max), sub }) },
      svg('circle', { cx: 56, cy: 56, r: radius, class: 'ring-track' }),
      progress,
      svg('text', { x: 56, y: 58, 'text-anchor': 'middle', class: 'ring-value' }, fmt(value)),
      svg('text', { x: 56, y: 74, 'text-anchor': 'middle', class: 'ring-sub' }, max > 0 ? `/ ${fmt(max)} ${sub}` : sub)),
    h('figcaption', { text: label }),
    remaining !== undefined && h('span', { class: 'remaining', text: remaining }));
}

const W = 320;
const PAD = { left: 34, right: 10, top: 12, bottom: 22 };

function scale(domainMin, domainMax, rangeMin, rangeMax) {
  const span = domainMax - domainMin || 1;
  return (value) => rangeMin + ((value - domainMin) / span) * (rangeMax - rangeMin);
}

/**
 * Line chart. points: [{ x: number, y: number, label: string }] sorted by x.
 * `target` draws a dashed horizontal line.
 */
export function lineChart({ points, target = null, height = 170, label }) {
  if (points.length === 0) return h('p', { class: 'muted small', text: t('لا توجد بيانات بعد.') });

  const ys = points.map((p) => p.y).concat(target ?? []);
  const yMin = Math.floor(Math.min(...ys) - 1);
  const yMax = Math.ceil(Math.max(...ys) + 1);
  const xMin = points[0].x;
  const xMax = points[points.length - 1].x;
  const x = scale(xMin, xMax === xMin ? xMin + 1 : xMax, PAD.left, W - PAD.right);
  const y = scale(yMin, yMax, height - PAD.bottom, PAD.top);
  const single = points.length === 1;
  const px = (p) => (single ? (PAD.left + W - PAD.right) / 2 : x(p.x));

  const path = points.map((p, i) => `${i === 0 ? 'M' : 'L'}${px(p).toFixed(1)} ${y(p.y).toFixed(1)}`).join(' ');

  return svg('svg', { viewBox: `0 0 ${W} ${height}`, class: 'chart', role: 'img', 'aria-label': label },
    svg('line', { x1: PAD.left, x2: W - PAD.right, y1: y(yMax), y2: y(yMax), class: 'grid' }),
    svg('line', { x1: PAD.left, x2: W - PAD.right, y1: y(yMin), y2: y(yMin), class: 'axis' }),
    svg('text', { x: PAD.left - 4, y: y(yMax) + 3, 'text-anchor': 'end' }, String(yMax)),
    svg('text', { x: PAD.left - 4, y: y(yMin) + 3, 'text-anchor': 'end' }, String(yMin)),
    target !== null && svg('line', { x1: PAD.left, x2: W - PAD.right, y1: y(target), y2: y(target), class: 'target' }),
    svg('path', { d: path, class: 'line' }),
    points.map((p) => svg('circle', { cx: px(p), cy: y(p.y), r: 3, class: 'dot' }, svg('title', {}, `${p.label}: ${fmt(p.y)}`))),
    svg('text', { x: PAD.left, y: height - 6, 'text-anchor': 'start' }, points[0].label),
    !single && svg('text', { x: W - PAD.right, y: height - 6, 'text-anchor': 'end' }, points[points.length - 1].label));
}

/**
 * Bar chart. bars: [{ label, value, ok }] where ok is true (on target), false (off) or null (unknown).
 */
export function barChart({ bars, target = null, height = 170, label }) {
  if (bars.length === 0) return h('p', { class: 'muted small', text: t('لا توجد بيانات بعد.') });

  const yMax = Math.max(...bars.map((b) => b.value), target ?? 0, 1) * 1.1;
  const y = scale(0, yMax, height - PAD.bottom, PAD.top);
  const slot = (W - PAD.left - PAD.right) / bars.length;
  const barWidth = Math.max(slot * 0.62, 2);

  return svg('svg', { viewBox: `0 0 ${W} ${height}`, class: 'chart', role: 'img', 'aria-label': label },
    svg('line', { x1: PAD.left, x2: W - PAD.right, y1: y(0), y2: y(0), class: 'axis' }),
    svg('text', { x: PAD.left - 4, y: y(yMax) + 3, 'text-anchor': 'end' }, String(Math.round(yMax))),
    bars.map((b, i) => {
      const top = y(b.value);
      const cls = b.ok === true ? 'bar ok' : b.ok === false && b.value > 0 ? 'bar miss' : 'bar';
      return svg('rect', {
        x: PAD.left + i * slot + (slot - barWidth) / 2, y: top, width: barWidth, height: Math.max(y(0) - top, 0),
        rx: 2, class: cls,
      }, svg('title', {}, `${b.label}: ${fmt(b.value)}`));
    }),
    target !== null && svg('line', { x1: PAD.left, x2: W - PAD.right, y1: y(target), y2: y(target), class: 'target' }),
    svg('text', { x: PAD.left + slot / 2, y: height - 6, 'text-anchor': 'middle' }, bars[0].label),
    bars.length > 1 && svg('text', { x: PAD.left + (bars.length - 0.5) * slot, y: height - 6, 'text-anchor': 'middle' }, bars[bars.length - 1].label));
}
