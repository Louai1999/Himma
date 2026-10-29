import { t, lang } from '../i18n/index.js';
import { exerciseTarget } from '../labels.js';
import { h } from './dom.js';
import { icon } from './icons.js';
import { muscleFigure } from './body.js';
import { openModal } from './modal.js';

/** Small button that opens the "how to do it" window of an exercise. */
export function exerciseInfoButton(exercise) {
  return h('button', {
    type: 'button', class: 'icon-btn', 'aria-label': t('طريقة أداء {name}', { name: exercise.name }),
    onclick: () => openExerciseInfo(exercise),
  }, icon('info', 18));
}

/**
 * Beginner guide: start/end photos that swap back and forth, the target and helper muscles,
 * numbered steps, a safety tip and a YouTube search for a video. User exercises have no guide,
 * so they get the muscle figure, their own notes and the video search.
 */
export function openExerciseInfo(exercise) {
  const guide = exercise.guide;
  const query = lang === 'en' ? `${exercise.name} exercise form` : `طريقة تمرين ${exercise.name}`;
  const youtube = `https://www.youtube.com/results?search_query=${encodeURIComponent(query)}`;

  const photos = guide?.images.length === 2 && h('div', { class: 'motion', role: 'img', 'aria-label': t('صورتا بداية الحركة ونهايتها') },
    h('img', { src: guide.images[0], alt: '', loading: 'lazy', decoding: 'async' }),
    h('img', { src: guide.images[1], alt: '', loading: 'lazy', decoding: 'async', class: 'end' }),
    h('span', { class: 'motion-label start', text: t('البداية') }),
    h('span', { class: 'motion-label end', text: t('النهاية') }));

  openModal({
    title: exercise.name,
    content: h('div', { class: 'exercise-info stack' },
      photos,
      h('div', { class: 'row', style: 'align-items:center;gap:12px' },
        muscleFigure(exercise, 'figure'),
        h('div', { class: 'stack', style: 'gap:4px' },
          h('span', {}, h('strong', { text: t('العضلة المستهدفة: ') }), exerciseTarget(exercise)),
          guide?.helperMuscles.length > 0 && h('span', { class: 'small' },
            h('strong', { text: t('عضلات مساعدة: ') }), guide.helperMuscles.join(t('، '))))),
      guide && h('div', {},
        h('h3', { text: t('الخطوات') }),
        h('ol', { class: 'steps' }, guide.steps.map((step) => h('li', { text: step })))),
      guide && h('p', { class: 'tip small' }, h('strong', { text: t('انتبه: ') }), guide.tip),
      !guide && exercise.instructions && h('p', { class: 'small', text: exercise.instructions }),
      h('a', { class: 'btn', href: youtube, target: '_blank', rel: 'noopener noreferrer' }, icon('play', 16), t('شاهد فيديو على يوتيوب'))),
  });
}
