import { svg } from './dom.js';

// Thin monochrome line icons (24x24), drawn with currentColor. No emoji anywhere.
const PATHS = {
  today: ['M4 6h16v14H4z', 'M4 10h16', 'M8 3v4', 'M16 3v4', 'M9 14h2', 'M13 14h2'],
  weight: ['M5 4h14l1 16H4z', 'M9 10a3 3 0 0 1 6 0', 'M12 10l1.5-2'],
  workouts: ['M3 10v4', 'M6 7v10', 'M18 7v10', 'M21 10v4', 'M6 12h12'],
  history: ['M4 20V11', 'M10 20V5', 'M16 20v-8', 'M3 20h18'],
  profile: ['M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8z', 'M4 21c1-4 4-6 8-6s7 2 8 6'],
  plus: ['M12 5v14', 'M5 12h14'],
  trash: ['M4 7h16', 'M9 7V4h6v3', 'M6 7l1 13h10l1-13', 'M10 11v6', 'M14 11v6'],
  edit: ['M4 20h4L19 9l-4-4L4 16z', 'M13 7l4 4'],
  up: ['M6 15l6-6 6 6'],
  down: ['M6 9l6 6 6-6'],
  check: ['M5 12l5 5 9-10'],
  close: ['M6 6l12 12', 'M18 6L6 18'],
  drop: ['M12 3c4 5 6 8 6 11a6 6 0 0 1-12 0c0-3 2-6 6-11z'],
  grip: ['M9 6h.01', 'M15 6h.01', 'M9 12h.01', 'M15 12h.01', 'M9 18h.01', 'M15 18h.01'],
  play: ['M8 5l11 7-11 7z'],
  logout: ['M15 4h4v16h-4', 'M10 8l-4 4 4 4', 'M6 12h10'],
  copy: ['M9 9h11v11H9z', 'M5 15H4V4h11v1'],
  bell: ['M6 16v-5a6 6 0 0 1 12 0v5l2 2H4z', 'M10 21h4'],
  flame: ['M12 3c1 4 6 6 6 11a6 6 0 0 1-12 0c0-3 2-5 3-6 0 3 2 4 3 4-1-3 0-6 0-9z'],
  refresh: ['M20 6v5h-5', 'M4 18v-5h5', 'M19 11a7 7 0 0 0-13-3', 'M5 13a7 7 0 0 0 13 3'],
  prev: ['M9 6l6 6-6 6'],
  next: ['M15 6l-6 6 6 6'],
  info: ['M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18z', 'M12 11v5', 'M12 8h.01'],
  calendar: ['M4 6h16v14H4z', 'M4 10h16', 'M8 3v4', 'M16 3v4'],
};

/** `className` 'flip-ltr' mirrors direction arrows when the page is left-to-right. */
export function icon(name, size = 20, className) {
  const paths = PATHS[name] ?? PATHS.info;
  return svg('svg', {
    viewBox: '0 0 24 24',
    width: size,
    height: size,
    fill: 'none',
    stroke: 'currentColor',
    'stroke-width': name === 'grip' ? 3 : 1.6,
    'stroke-linecap': 'round',
    'stroke-linejoin': 'round',
    class: className,
    'aria-hidden': 'true',
    focusable: 'false',
  }, paths.map((d) => svg('path', { d })));
}
