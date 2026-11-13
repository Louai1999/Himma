import { t } from '../i18n/index.js';
import { get } from '../api.js';
import { formatDateTime } from '../dates.js';
import { goals } from '../labels.js';
import { h, add, fmt } from '../ui/dom.js';
import { loadInto } from '../ui/notice.js';
import { navigate } from '../router.js';
import { percent, stat } from '../admin/labels.js';
import { attentionChips } from './labels.js';

/** Home of the dashboard: who needs the coach today, then every client with their numbers. */
export function mount(view) {
  const body = h('div', { class: 'stack' });
  add(view, h('header', { class: 'view-head' }, h('h1', { text: t('متدربيني') })), body);
  loadInto(body, () => get('/coach/clients'), (data) => render(body, data));
}

function render(body, { coach, clients }) {
  const needing = clients.filter((c) => c.attention.length > 0);
  const onTrack = clients.length - needing.length;
  const avg = (values) => {
    const list = values.filter((v) => v !== null && v !== undefined);
    return list.length ? Math.round(list.reduce((a, b) => a + b, 0) / list.length) : null;
  };

  add(body, h('div', { class: 'stat-grid' },
    stat(`${fmt(coach.activeClients)} / ${fmt(coach.maxClients)}`, t('المتدربون / المقاعد')),
    stat(needing.length, t('يحتاجونك اليوم')),
    stat(onTrack, t('على المسار')),
    stat(percent(avg(clients.map((c) => c.calorieAdherencePercent))), t('متوسط الالتزام بالسعرات'))));

  if (clients.length === 0) {
    add(body, h('div', { class: 'card empty stack' },
      h('p', { text: t('لا يوجد متدربون بعد.') }),
      h('p', { class: 'muted small', text: t('أرسل كود الدعوة أو الرابط لمتدربيك من «باقتي والدعوة».') }),
      h('a', { class: 'btn', href: '#settings' }, t('كود الدعوة'))));
    return;
  }

  if (needing.length > 0) {
    add(body, h('section', { class: 'card stack attention-card' },
      h('h2', { text: t('يحتاجونك اليوم') }),
      h('p', { class: 'muted small', text: t('افتح المتدرب وأرسل له رسالة جاهزة بضغطة.') }),
      h('ul', { class: 'item-list' }, needing.map((c) => h('li', { class: 'item' },
        h('a', { class: 'grow user-link', href: `#client/${encodeURIComponent(c.id)}` },
          h('div', { class: 'title', text: c.displayName }),
          h('div', { class: 'meta' }, attentionChips(c.attention))),
        h('span', { class: 'muted small', text: c.lastLoggedAtUtc ? t('آخر تسجيل: {when}', { when: formatDateTime(c.lastLoggedAtUtc) }) : t('لم يسجّل بعد') }))))));
  }

  add(body, h('div', { class: 'card table-card' },
    h('div', { class: 'table-scroll' },
      h('table', { class: 'data-table' },
        h('thead', {}, h('tr', {},
          [t('المتدرب'), t('الهدف'), t('السلسلة'), t('الانتظام'), t('السعرات'), t('البروتين'), t('الماء'), t('الوزن (30 يوماً)'), t('آخر تسجيل'), t('الحالة')]
            .map((label) => h('th', { scope: 'col', text: label })))),
        h('tbody', {}, clients.map(row))))));
}

function row(c) {
  const href = `#client/${encodeURIComponent(c.id)}`;
  const change = c.weightChange30Kg;
  const tr = h('tr', { class: 'clickable' },
    h('td', {},
      h('a', { href, class: 'user-link' }, h('strong', { text: c.displayName })),
      h('div', { class: 'muted small', dir: 'ltr', text: `@${c.username}` })),
    h('td', { text: c.goal ? goals[c.goal] : '—' }),
    h('td', { class: 'num', text: fmt(c.streak) }),
    h('td', { class: 'num', text: percent(c.consistencyPercent) }),
    h('td', { class: 'num', text: percent(c.calorieAdherencePercent) }),
    h('td', { class: 'num', text: percent(c.proteinAdherencePercent) }),
    h('td', { class: 'num', text: percent(c.waterAdherencePercent) }),
    h('td', { class: 'num', dir: 'ltr', text: change === null ? '—' : `${change > 0 ? '+' : ''}${fmt(change)}` }),
    h('td', { class: 'nowrap', text: formatDateTime(c.lastLoggedAtUtc) }),
    h('td', {}, attentionChips(c.attention)));
  tr.addEventListener('click', (event) => { if (!event.target.closest('a')) navigate(href); });
  return tr;
}
