import { t, lang } from '../i18n/index.js';
import { exerciseTarget } from '../labels.js';
import { h } from './dom.js';
import { icon } from './icons.js';
import { muscleFigure } from './body.js';
import { openModal } from './modal.js';

/** Labelled "how to do it" button, the same everywhere an exercise is listed so beginners learn to spot it. */
export function exerciseInfoButton(exercise) {
  return h('button', {
    type: 'button', class: 'guide-btn', 'aria-label': t('طريقة أداء {name}', { name: exercise.name }),
    onclick: () => openExerciseInfo(exercise),
  }, icon('play', 14), t('الشرح'));
}

/**
 * Beginner guide in plain sections: the movement (start/end photos that swap back and forth),
 * the muscles, numbered steps, one safety tip and a YouTube search for a video. User exercises
 * have no guide, so they get the muscles, their own notes and the video search.
 */
export function openExerciseInfo(exercise) {
  const guide = exercise.guide;
  const query = lang === 'en' ? `${exercise.name} exercise form` : `طريقة تمرين ${exercise.name}`;
  const youtube = `https://www.youtube.com/results?search_query=${encodeURIComponent(query)}`;
  const section = (title, ...content) => h('section', { class: 'guide-section' }, h('h3', { text: title }), content);

  const photos = guide?.images.length === 2 && section(t('شوف الحركة'),
    h('div', { class: 'motion', role: 'img', 'aria-label': t('صورتا بداية الحركة ونهايتها') },
      h('img', { src: guide.images[0], alt: '', decoding: 'async' }),
      h('img', { src: guide.images[1], alt: '', decoding: 'async', class: 'end' }),
      h('span', { class: 'motion-label start', text: t('البداية') }),
      h('span', { class: 'motion-label end', text: t('النهاية') })));

  openModal({
    title: exercise.name,
    content: h('div', { class: 'exercise-info' },
      photos,
      section(t('العضلات'),
        h('div', { class: 'guide-muscles' },
          muscleFigure(exercise, 'figure'),
          h('div', { class: 'stack', style: 'gap:6px' },
            h('span', {}, h('span', { class: 'muted small', text: t('الأساسية') }), h('br'), h('strong', { text: exerciseTarget(exercise) })),
            guide?.helperMuscles.length > 0 && h('span', {},
              h('span', { class: 'muted small', text: t('تساعدها') }), h('br'), guide.helperMuscles.join(t('، ')))))),
      guide && section(t('الخطوات'),
        h('ol', { class: 'steps' }, guide.steps.map((step) => h('li', { text: step })))),
      guide && h('p', { class: 'tip' }, h('strong', { text: t('انتبه: ') }), guide.tip),
      !guide && exercise.instructions && section(t('ملاحظاتك'), h('p', { class: 'small', text: exercise.instructions })),
      h('a', { class: 'btn video-btn', href: youtube, target: '_blank', rel: 'noopener noreferrer' }, icon('play', 18), t('شاهد فيديو على يوتيوب'))),
  });
}
