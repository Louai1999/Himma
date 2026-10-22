import { t } from '../i18n/index.js';
import { get } from '../api.js';
import { formatDateTime } from '../dates.js';
import { h, add } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { field, selectOf } from '../ui/forms.js';
import { loadInto } from '../ui/notice.js';
import { auditActions } from './labels.js';

const PAGE_SIZE = 50;
const filters = { actor: '', target: '', action: '', from: '', to: '', page: 1 };

/** Read-only audit trail with filters. */
export function mount(view) {
  const results = h('div', { class: 'stack' });
  add(view, h('header', { class: 'view-head' }, h('h1', { text: t('سجل المراجعة') })), filterCard(load), results);
  load();

  function load() {
    const query = new URLSearchParams({ page: String(filters.page), pageSize: String(PAGE_SIZE) });
    for (const key of ['actor', 'target', 'action', 'from', 'to']) {
      if (filters[key]) query.set(key, filters[key]);
    }
    loadInto(results, () => get(`/admin/audit-logs?${query}`), (data) => render(results, data, load));
  }
}

function filterCard(onChange) {
  const actor = h('input', { type: 'text', value: filters.actor, dir: 'ltr', placeholder: t('اسم المستخدم أو system') });
  const target = h('input', { type: 'text', value: filters.target, dir: 'ltr', placeholder: t('اسم المستخدم') });
  const action = selectOf({ '': t('كل الإجراءات'), ...auditActions }, filters.action);
  const from = h('input', { type: 'date', value: filters.from });
  const to = h('input', { type: 'date', value: filters.to });
  const apply = () => {
    Object.assign(filters, { actor: actor.value.trim(), target: target.value.trim(), action: action.value, from: from.value, to: to.value, page: 1 });
    onChange();
  };
  [action, from, to].forEach((el) => el.addEventListener('change', apply));
  [actor, target].forEach((el) => el.addEventListener('change', apply));

  return h('div', { class: 'card filters' },
    field(t('المنفّذ'), actor),
    field(t('المستهدف'), target),
    field(t('الإجراء'), action),
    field(t('من تاريخ'), from),
    field(t('إلى تاريخ'), to));
}

function render(container, data, reload) {
  if (data.items.length === 0) {
    add(container, h('div', { class: 'card empty', text: t('لا توجد سجلات بهذه الفلاتر.') }));
    return;
  }
  const pages = Math.max(1, Math.ceil(data.totalCount / data.pageSize));
  const go = (page) => { filters.page = page; reload(); };

  add(container,
    h('div', { class: 'card table-card' },
      h('div', { class: 'table-scroll' },
        h('table', { class: 'data-table' },
          h('thead', {}, h('tr', {},
            [t('الوقت'), t('المنفّذ'), t('الإجراء'), t('المستهدف'), t('التفاصيل'), t('عنوان IP')].map((label) => h('th', { scope: 'col', text: label })))),
          h('tbody', {}, data.items.map(row))))),
    h('div', { class: 'pager' },
      h('button', { type: 'button', class: 'btn btn-secondary btn-sm', disabled: data.page <= 1, onclick: () => go(data.page - 1) },
        icon('prev', 16, 'flip-ltr'), t('السابق')),
      h('span', { class: 'muted small', text: t('صفحة {page} من {pages} · {total} سجل', { page: data.page, pages, total: data.totalCount }) }),
      h('button', { type: 'button', class: 'btn btn-secondary btn-sm', disabled: data.page >= pages, onclick: () => go(data.page + 1) },
        t('التالي'), icon('next', 16, 'flip-ltr'))));
}

function row(log) {
  return h('tr', {},
    h('td', { class: 'nowrap', text: formatDateTime(log.createdAtUtc) }),
    h('td', {}, who(log.actorUsername, log.actorUserId), h('div', { class: 'muted small', text: log.actorRole })),
    h('td', { text: auditActions[log.action] ?? log.action }),
    h('td', {}, log.targetUserId ? targetCell(log) : '—'),
    h('td', { class: 'details', text: detailsText(log.details) }),
    h('td', { class: 'num small', text: log.ipAddress ?? '—' }));
}

function targetCell(log) {
  // Purged accounts no longer have a page; show their id (and the username kept in the details).
  return log.targetUsername
    ? h('a', { href: `#user/${encodeURIComponent(log.targetUserId)}`, dir: 'ltr', text: `@${log.targetUsername}` })
    : who(null, log.targetUserId);
}

function who(username, id) {
  return username
    ? h('span', { dir: 'ltr', text: `@${username}` })
    : h('span', { class: 'muted small', dir: 'ltr', title: id, text: id === 'system' ? 'system' : `${id.slice(0, 8)}…` });
}

/** Details are a small JSON object written by the server; shown as "key: value" text. */
function detailsText(json) {
  try {
    const value = JSON.parse(json);
    const parts = Object.entries(value ?? {}).map(([key, v]) => `${key}: ${v}`);
    return parts.length ? parts.join(' · ') : '—';
  } catch {
    return json;
  }
}
