import { t } from '../i18n/index.js';
import { h, fmt } from '../ui/dom.js';

export const statuses = {
  active: t('نشط'),
  suspended: t('موقوف'),
  pendingDeletion: t('بانتظار الحذف'),
};

export function statusBadge(status) {
  return h('span', { class: `badge badge-${status}`, text: statuses[status] ?? status });
}

export const auditActions = {
  'admin.view_user': t('عرض بيانات مشترك'),
  'admin.view_meals': t('عرض وجبات مشترك'),
  'admin.export_user': t('تصدير بيانات مشترك'),
  'admin.suspend': t('إيقاف حساب'),
  'admin.reactivate': t('إعادة تفعيل حساب'),
  'admin.soft_delete': t('حذف حساب (قابل للاسترجاع)'),
  'admin.restore': t('استرجاع حساب'),
  'admin.purge': t('حذف نهائي'),
  'admin.reauth_failed': t('كلمة مرور المدير خاطئة'),
  'admin.reset_password': t('إعادة تعيين كلمة مرور مشترك'),
  'system.purge': t('حذف نهائي تلقائي'),
  'user.self_delete': t('المستخدم حذف حسابه'),
  'user.export': t('المستخدم صدّر بياناته'),
};

export const percent = (value) => (value === null || value === undefined ? '—' : `${fmt(value)}%`);

export function stat(value, label, hint) {
  return h('div', { class: 'stat', title: hint },
    h('span', { class: 'value', text: typeof value === 'number' ? fmt(value) : value }),
    h('span', { class: 'label', text: label }));
}
