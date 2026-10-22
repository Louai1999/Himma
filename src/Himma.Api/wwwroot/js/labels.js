// Arabic labels for the values the API sends (camelCase enum names).

export const muscles = {
  chest: 'صدر',
  triceps: 'ترايسبس',
  back: 'ظهر',
  biceps: 'بايسبس',
  legs: 'أرجل',
  shoulders: 'أكتاف',
  abs: 'بطن',
  fullBody: 'الجسم كامل',
  cardio: 'كارديو',
};

const REGION_LABELS = {
  default: { upper: 'علوي', middle: 'أوسط', lower: 'سفلي', whole: 'كامل' },
  legs: { upper: 'الفخذ والأرداف', middle: 'أوسط', lower: 'السمانة', whole: 'كامل' },
  shoulders: { upper: 'أمامي', middle: 'جانبي', lower: 'خلفي', whole: 'كامل' },
};

export const regions = REGION_LABELS.default;

export function regionLabel(muscle, region) {
  return (REGION_LABELS[muscle] ?? REGION_LABELS.default)[region] ?? '';
}

/** "صدر علوي", "أكتاف جانبي", or just "ظهر" when the whole muscle is targeted. */
export function exerciseTarget(exercise) {
  const muscle = muscles[exercise.muscleGroup] ?? '';
  return exercise.region === 'whole' ? muscle : `${muscle} ${regionLabel(exercise.muscleGroup, exercise.region)}`;
}

export const days = {
  sat: 'السبت', sun: 'الأحد', mon: 'الاثنين', tue: 'الثلاثاء', wed: 'الأربعاء', thu: 'الخميس', fri: 'الجمعة',
};

export const mealTypes = { breakfast: 'الفطور', lunch: 'الغداء', dinner: 'العشاء', snack: 'وجبة خفيفة' };

export const places = { home: 'البيت', gym: 'الجيم' };

export const genders = { male: 'ذكر', female: 'أنثى' };

export const activities = {
  sedentary: 'قليل الحركة (عمل مكتبي)',
  light: 'نشاط خفيف (تمرين 1-3 أيام)',
  moderate: 'نشاط متوسط (تمرين 3-5 أيام)',
  active: 'نشاط عالٍ (تمرين 6-7 أيام)',
  veryActive: 'نشاط عالٍ جداً (عمل بدني أو تمرين مرتين يومياً)',
};

export const goals = { lose: 'إنقاص الوزن', maintain: 'المحافظة على الوزن', gain: 'زيادة الوزن' };

export const targetWarnings = {
  calorieFloorApplied: 'رفعنا السعرات إلى الحد الأدنى الآمن، لأن الحساب نزل عنه. لا تأكل أقل من ذلك دون استشارة مختص.',
};
