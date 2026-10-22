import { t } from './i18n/index.js';
// Arabic labels for the values the API sends (camelCase enum names).

export const muscles = {
  chest: t('صدر'),
  triceps: t('ترايسبس'),
  back: t('ظهر'),
  biceps: t('بايسبس'),
  legs: t('أرجل'),
  shoulders: t('أكتاف'),
  abs: t('بطن'),
  fullBody: t('الجسم كامل'),
  cardio: t('كارديو'),
};

const REGION_LABELS = {
  default: { upper: t('علوي'), middle: t('أوسط'), lower: t('سفلي'), whole: t('كامل') },
  legs: { upper: t('الفخذ والأرداف'), middle: t('أوسط'), lower: t('السمانة'), whole: t('كامل') },
  shoulders: { upper: t('أمامي'), middle: t('جانبي'), lower: t('خلفي'), whole: t('كامل') },
};

export const regions = REGION_LABELS.default;

export function regionLabel(muscle, region) {
  return (REGION_LABELS[muscle] ?? REGION_LABELS.default)[region] ?? '';
}

/** "صدر علوي", "أكتاف جانبي", or just "ظهر" when the whole muscle is targeted. */
export function exerciseTarget(exercise) {
  const muscle = muscles[exercise.muscleGroup] ?? '';
  return exercise.region === 'whole'
    ? muscle
    : t('{muscle} {region}', { muscle, region: regionLabel(exercise.muscleGroup, exercise.region) });
}

export const days = {
  sat: t('السبت'), sun: t('الأحد'), mon: t('الاثنين'), tue: t('الثلاثاء'), wed: t('الأربعاء'), thu: t('الخميس'), fri: t('الجمعة'),
};

export const mealTypes = { breakfast: t('الفطور'), lunch: t('الغداء'), dinner: t('العشاء'), snack: t('وجبة خفيفة') };

export const places = { home: t('البيت'), gym: t('الجيم') };

export const genders = { male: t('ذكر'), female: t('أنثى') };

export const activities = {
  sedentary: t('قليل الحركة (عمل مكتبي)'),
  light: t('نشاط خفيف (تمرين 1-3 أيام)'),
  moderate: t('نشاط متوسط (تمرين 3-5 أيام)'),
  active: t('نشاط عالٍ (تمرين 6-7 أيام)'),
  veryActive: t('نشاط عالٍ جداً (عمل بدني أو تمرين مرتين يومياً)'),
};

export const goals = { lose: t('إنقاص الوزن'), maintain: t('المحافظة على الوزن'), gain: t('زيادة الوزن') };

export const targetWarnings = {
  calorieFloorApplied: t('رفعنا السعرات إلى الحد الأدنى الآمن، لأن الحساب نزل عنه. لا تأكل أقل من ذلك دون استشارة مختص.'),
};
