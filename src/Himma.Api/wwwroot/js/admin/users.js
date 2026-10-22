import { t } from '../i18n/index.js';
import { get } from '../api.js';
import { formatDate, formatDateTime } from '../dates.js';
import { goals } from '../labels.js';
import { h, add, debounce, fmt } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { field, selectOf } from '../ui/forms.js';
import { loadInto } from '../ui/notice.js';
import { navigate } from '../router.js';
import { statusBadge, statuses, percent } from './labels.js';

const PAGE_SIZE = 20;

// Kept between visits, so coming back from a subscriber's page keeps the same filters and page.
const filters = { search: '', status: '', joinedFrom: '', joinedTo: '', sort: 'joined_desc', page: 1 };

const SORTS = {
  joined_desc: t('الأحدث اشتراكاً'),
  joined_asc: t('الأقدم اشتراكاً'),
  lastLogin_desc: t('آخر دخول'),
  username_asc: t('اسم المستخدم'),
  streak_desc: t('أطول سلسلة'),
  consistency_desc: t('الأعلى انتظاماً'),
  adherence_desc: t('الأعلى التزاماً بالسعرات'),
};

/** Subscribers: searchable, filterable, sortable, paginated table. */
export function mount(view) {
  const results = h('div', { class: 'stack' });
  add(view, h('header', { class: 'view-head' }, h('h1', { text: t('المشتركون') })), filterCard(load), results);
  load();

  function load() {
    const query = new URLSearchParams({ page: String(filters.page), pageSize: String(PAGE_SIZE), sort: filters.sort });
    for (const key of ['search', 'status', 'joinedFrom', 'joinedTo']) {
      if (filters[key]) query.set(key, filters[key]);
    }
    loadInto(results, () => get(`/admin/users?${query}`), (data) => render(results, data, load));
  }
}

function filterCard(onChange) {
  const search = h('input', { type: 'search', value: filters.search, placeholder: t('اسم المستخدم أو الاسم الظاهر'), autocomplete: 'off' });
  const status = selectOf({ '': t('الحاليون (نشط وموقوف)'), active: statuses.active, suspended: statuses.suspended, pendingDeletion: statuses.pendingDeletion, all: t('الكل') }, filters.status);
  const from = h('input', { type: 'date', value: filters.joinedFrom });
  const to = h('input', { type: 'date', value: filters.joinedTo });
  const sort = selectOf(SORTS, filters.sort);

  const update = () => {
    Object.assign(filters, {
      search: search.value.trim(), status: status.value, joinedFrom: from.value, joinedTo: to.value, sort: sort.value, page: 1,
    });
    onChange();
  };
  search.addEventListener('input', debounce(update, 300));
  [status, from, to, sort].forEach((el) => el.addEventListener('change', update));

  return h('div', { class: 'card filters' },
    field(t('بحث'), search),
    field(t('الحالة'), status),
    field(t('اشترك من'), from),
    field(t('اشترك حتى'), to),
    field(t('الترتيب'), sort));
}

function render(container, data, reload) {
  if (data.items.length === 0) {
    add(container, h('div', { class: 'card empty', text: t('لا يوجد مشتركون بهذه الفلاتر.') }));
    return;
  }

  const pages = Math.max(1, Math.ceil(data.totalCount / data.pageSize));
  const go = (page) => { filters.page = page; reload(); };

  add(container,
    h('div', { class: 'card table-card' },
      h('div', { class: 'table-scroll' },
        h('table', { class: 'data-table' },
          h('thead', {}, h('tr', {},
            [t('المستخدم'), t('الاشتراك'), t('آخر دخول'), t('آخر نشاط'), t('الحالة'), t('الهدف'), t('السلسلة'), t('الانتظام'), t('السعرات')]
              .map((label) => h('th', { scope: 'col', text: label })))),
          h('tbody', {}, data.items.map(row))))),
    h('div', { class: 'pager' },
      h('button', { type: 'button', class: 'btn btn-secondary btn-sm', disabled: data.page <= 1, onclick: () => go(data.page - 1) },
        icon('prev', 16, 'flip-ltr'), t('السابق')),
      h('span', { class: 'muted small', text: t('صفحة {page} من {pages} · {total} مشترك', { page: data.page, pages, total: data.totalCount }) }),
      h('button', { type: 'button', class: 'btn btn-secondary btn-sm', disabled: data.page >= pages, onclick: () => go(data.page + 1) },
        t('التالي'), icon('next', 16, 'flip-ltr'))));
}

function row(user) {
  const href = `#user/${encodeURIComponent(user.id)}`;
  const tr = h('tr', { class: 'clickable' },
    h('td', {},
      h('a', { href, class: 'user-link' }, h('strong', { text: user.displayName })),
      h('div', { class: 'muted small', dir: 'ltr', text: `@${user.username}` })),
    h('td', { text: formatDate(user.createdAtUtc) }),
    h('td', { text: formatDateTime(user.lastLoginAtUtc) }),
    h('td', { text: formatDateTime(user.lastActivityAtUtc) }),
    h('td', {}, statusBadge(user.status)),
    h('td', { text: user.goal ? goals[user.goal] : '—' }),
    h('td', { class: 'num', text: fmt(user.streak) }),
    h('td', { class: 'num', text: percent(user.consistencyPercent) }),
    h('td', { class: 'num', text: percent(user.calorieAdherencePercent) }));
  // The whole row opens the subscriber; the link inside keeps it keyboard accessible.
  tr.addEventListener('click', (event) => { if (!event.target.closest('a')) navigate(href); });
  return tr;
}
