import { svg } from './dom.js';
import { exerciseTarget } from '../labels.js';

// A small front/back body figure with the targeted muscle highlighted.
// Zones are drawn for one figure; the back figure is the same outline shifted to the right.

const FRONT = 0;
const BACK = 62;

const ZONES = {
  delts: [['circle', { cx: 12, cy: 23, r: 5 }], ['circle', { cx: 38, cy: 23, r: 5 }]],
  'chest-upper': [['rect', { x: 16, y: 19, width: 18, height: 5, rx: 2 }]],
  'chest-middle': [['rect', { x: 16, y: 24.5, width: 18, height: 4.5, rx: 1 }]],
  'chest-lower': [['rect', { x: 16, y: 29.5, width: 18, height: 4, rx: 2 }]],
  'abs-upper': [['rect', { x: 18, y: 34.5, width: 14, height: 5, rx: 1 }]],
  'abs-middle': [['rect', { x: 18, y: 40, width: 14, height: 5, rx: 1 }]],
  'abs-lower': [['rect', { x: 18, y: 45.5, width: 14, height: 5, rx: 2 }]],
  'back-upper': [['rect', { x: 16, y: 19, width: 18, height: 9, rx: 2 }]],
  'back-middle': [['rect', { x: 16, y: 28.5, width: 18, height: 9, rx: 1 }]],
  'back-lower': [['rect', { x: 18, y: 38, width: 14, height: 12.5, rx: 2 }]],
  arms: [['rect', { x: 5, y: 27, width: 6, height: 14, rx: 3 }], ['rect', { x: 39, y: 27, width: 6, height: 14, rx: 3 }]],
  hips: [['rect', { x: 17, y: 51, width: 16, height: 6, rx: 3 }]],
  thighs: [['rect', { x: 15.5, y: 57.5, width: 8.5, height: 20, rx: 4 }], ['rect', { x: 26, y: 57.5, width: 8.5, height: 20, rx: 4 }]],
  calves: [['rect', { x: 16.5, y: 79, width: 7, height: 17, rx: 3.5 }], ['rect', { x: 26.5, y: 79, width: 7, height: 17, rx: 3.5 }]],
};

const FRONT_ZONES = ['delts', 'chest-upper', 'chest-middle', 'chest-lower', 'abs-upper', 'abs-middle', 'abs-lower', 'arms', 'hips', 'thighs', 'calves'];
const BACK_ZONES = ['delts', 'back-upper', 'back-middle', 'back-lower', 'arms', 'hips', 'thighs', 'calves'];

const ALL = [...FRONT_ZONES.map((z) => `f:${z}`), ...BACK_ZONES.map((z) => `b:${z}`)];

function zonesFor(muscle, region) {
  const part = (prefix, base) => (region === 'whole'
    ? [`${prefix}:${base}-upper`, `${prefix}:${base}-middle`, `${prefix}:${base}-lower`]
    : [`${prefix}:${base}-${region}`]);

  switch (muscle) {
    case 'chest': return part('f', 'chest');
    case 'back': return part('b', 'back');
    case 'abs': return part('f', 'abs');
    case 'biceps': return ['f:arms'];
    case 'triceps': return ['b:arms'];
    case 'shoulders':
      if (region === 'upper') return ['f:delts'];
      if (region === 'lower') return ['b:delts'];
      return ['f:delts', 'b:delts'];
    case 'legs':
      if (region === 'upper') return ['f:thighs', 'b:thighs', 'b:hips'];
      if (region === 'lower') return ['f:calves', 'b:calves'];
      return ['f:thighs', 'b:thighs', 'b:hips', 'f:calves', 'b:calves'];
    default: return ALL; // full body and cardio
  }
}

function figure(offset, prefix, zoneNames, on) {
  return svg('g', { transform: `translate(${offset} 0)` },
    svg('circle', { cx: 25, cy: 9, r: 6, class: 'base' }),
    svg('rect', { x: 15, y: 17, width: 20, height: 35, rx: 6, class: 'base' }),
    svg('rect', { x: 4.5, y: 41, width: 6, height: 13, rx: 3, class: 'base' }),
    svg('rect', { x: 39.5, y: 41, width: 6, height: 13, rx: 3, class: 'base' }),
    zoneNames.flatMap((zone) => ZONES[zone].map(([tag, attrs]) =>
      svg(tag, { ...attrs, class: on.has(`${prefix}:${zone}`) ? 'zone on' : 'zone' }))),
    svg('text', { x: 25, y: 104, 'text-anchor': 'middle' }, prefix === 'f' ? 'أمام' : 'خلف'));
}

export function muscleFigure(exercise, className = 'figure') {
  const on = new Set(zonesFor(exercise.muscleGroup, exercise.region));
  return svg('svg', {
    viewBox: '0 0 112 108', class: `muscle-fig ${className}`, role: 'img',
    'aria-label': `العضلة المستهدفة: ${exerciseTarget(exercise)}`,
  }, figure(FRONT, 'f', FRONT_ZONES, on), figure(BACK, 'b', BACK_ZONES, on));
}
