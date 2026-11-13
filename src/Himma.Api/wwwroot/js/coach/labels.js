import { t } from '../i18n/index.js';
import { h } from '../ui/dom.js';

/** Why a client needs the coach today. The server sends them worst first. */
export const attentionLabels = {
  inactive: t('لم يسجّل شيئاً منذ 3 أيام'),
  noProfile: t('لم يُكمل ملفه الشخصي'),
  lowCalories: t('بعيد عن هدف السعرات'),
  lowProtein: t('البروتين قليل'),
  lowWater: t('الماء قليل'),
};

/** Ready-to-send messages for each reason, so the coach can react in one tap (and edit first). */
export const attentionMessages = {
  inactive: t('اشتقنا لك! سجّل وجبات اليوم ولو بشكل تقريبي، المهم نرجع للروتين 💪'),
  noProfile: t('أكمل ملفك الشخصي (العمر والطول والوزن والهدف) عشان أقدر أحسب لك أهدافك بدقة.'),
  lowCalories: t('خلنا نقرّب من هدف السعرات هالأسبوع. لو تحتاج أفكار وجبات قل لي.'),
  lowProtein: t('البروتين عندك أقل من الهدف. جرّب تضيف مصدر بروتين لكل وجبة (بيض، دجاج، زبادي يوناني).'),
  lowWater: t('لا تنسى الماء! حاول تشرب كوب مع كل وجبة وكوب بينها.'),
};

export function attentionChips(reasons) {
  if (reasons.length === 0) return h('span', { class: 'chip chip-ok', text: t('على المسار ✓') });
  return h('span', { class: 'chips' }, reasons.map((r) => h('span', { class: `chip chip-${r === 'inactive' ? 'alert' : 'warn'}`, text: attentionLabels[r] ?? r })));
}
