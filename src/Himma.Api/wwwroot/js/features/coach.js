import { t } from '../i18n/index.js';
import { get, post, del } from '../api.js';
import { formatDate, formatDateTime } from '../dates.js';
import { h, fill, add } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { field } from '../ui/forms.js';
import { confirmDialog } from '../ui/modal.js';
import { calmNotice, loadInto } from '../ui/notice.js';
import { showSaved } from '../ui/toast.js';
import { navigate } from '../router.js';

// The client's side of coaching: join a coach with their code (explicit consent), read their
// messages, and stop sharing at any time. Also the coach's own link to their dashboard.

const PENDING_KEY = 'himma-coach-code';

/** An invite link (/?coach=CODE) is remembered across sign-up/sign-in, then opened once signed in. */
export function rememberInviteFromUrl() {
  const params = new URLSearchParams(location.search);
  const code = params.get('coach');
  if (!code) return;
  try {
    sessionStorage.setItem(PENDING_KEY, code);
  } catch {
    // Storage blocked: the client can still type the code in "My coach".
  }
  params.delete('coach');
  const query = params.toString();
  window.history.replaceState(null, '', `${location.pathname}${query ? `?${query}` : ''}${location.hash}`);
}

export function openPendingInvite() {
  let code = null;
  try {
    code = sessionStorage.getItem(PENDING_KEY);
    sessionStorage.removeItem(PENDING_KEY);
  } catch {
    return;
  }
  if (code) navigate(`#join/${encodeURIComponent(code)}`);
}

/** Banner on every screen while the coach has sent messages the client has not read. */
export async function showCoachBanner() {
  const slot = document.getElementById('banner-slot');
  if (!slot || slot.childElementCount > 0) return; // another banner (e.g. recovery email) comes first
  try {
    const mine = await get('/my-coach');
    if (!mine.coach || mine.unreadCount === 0) return;
    fill(slot, h('a', { class: 'notice info banner', href: '#coach', onclick: () => slot.replaceChildren() },
      icon('bell', 18),
      h('span', { text: t('رسالة جديدة من مدربك {name}', { name: mine.coach.businessName }) })));
  } catch {
    // Not important enough to show an error.
  }
}

/** Card in "Account": the client's coach, or a way to join one; and for coaches, their dashboard. */
export function coachCard() {
  const card = h('section', { class: 'card stack' }, h('h2', { text: t('مدربي') }));
  const slot = h('div', { class: 'stack' });
  add(card, slot);
  loadInto(slot, () => Promise.all([get('/my-coach'), get('/coach/me').catch(() => null)]), ([mine, ownPackage]) => {
    if (mine.coach) {
      add(slot,
        h('p', {}, h('strong', { text: mine.coach.businessName }), ' · ', h('span', { class: 'muted small', text: t('منذ {date}', { date: formatDate(mine.joinedAtUtc) }) })),
        h('a', { class: 'btn btn-secondary btn-sm', href: '#coach' }, t('الرسائل والتفاصيل')));
    } else {
      add(slot,
        h('p', { class: 'muted small', text: t('عندك مدرب أو نادي على هِمّة؟ اكتب الكود اللي أعطاك إياه ليتابع تقدّمك.') }),
        joinForm());
    }
    if (ownPackage) {
      add(slot, h('a', { class: 'btn btn-sm', href: '/coach.html' }, icon('workouts', 16), t('لوحة المدرب')));
    }
  });
  return card;
}

function joinForm() {
  const input = h('input', { type: 'text', dir: 'ltr', autocomplete: 'off', autocapitalize: 'characters', maxlength: 20, placeholder: 'ABCD2345' });
  const form = h('form', { class: 'row', novalidate: true },
    field(t('كود المدرب'), input),
    h('button', { type: 'submit', class: 'btn btn-secondary' }, t('متابعة')));
  form.addEventListener('submit', (event) => {
    event.preventDefault();
    if (input.value.trim()) navigate(`#join/${encodeURIComponent(input.value.trim())}`);
  });
  return form;
}

/** #coach: the coach, their messages (marked read on open), and leaving. */
export function mount(view) {
  const body = h('div', { class: 'stack' });
  add(view, h('header', { class: 'view-head' }, h('h1', { text: t('مدربي') })), body);
  const load = () => loadInto(body, () => get('/my-coach'), (mine) => render(body, mine, load));
  load();
}

function render(body, mine, reload) {
  if (!mine.coach) {
    add(body, h('section', { class: 'card stack' },
      h('p', { text: t('لست مرتبطاً بمدرب الآن.') }),
      h('p', { class: 'muted small', text: t('اكتب كود مدربك ليتابع أكلك ووزنك وتمارينك ويرسل لك خطط ونصائح.') }),
      joinForm()));
    return;
  }

  if (mine.unreadCount > 0) {
    post('/my-coach/messages/read').catch(() => {});
    document.getElementById('banner-slot')?.replaceChildren();
  }

  add(body,
    h('section', { class: 'card stack' },
      h('h2', { text: mine.coach.businessName }),
      mine.coach.bio && h('p', { text: mine.coach.bio }),
      h('p', { class: 'muted small', text: t('مدربك منذ {date}. يرى أكلك وماءك ووزنك وتمارينك وملفك الشخصي، ولا يرى كلمة مرورك أو بريدك.', { date: formatDate(mine.joinedAtUtc) }) })),
    h('section', { class: 'card stack' },
      h('h2', { text: t('رسائل المدرب') }),
      mine.messages.length === 0
        ? h('p', { class: 'muted', text: t('لا توجد رسائل بعد.') })
        : h('ul', { class: 'item-list' }, mine.messages.map((m) => h('li', { class: `item coach-message${m.readAtUtc ? '' : ' unread'}` },
          h('div', { class: 'grow' },
            h('div', { text: m.text }),
            h('div', { class: 'meta', text: formatDateTime(m.createdAtUtc) })))))),
    h('section', { class: 'card stack' },
      h('h2', { text: t('إيقاف المشاركة') }),
      h('p', { class: 'muted small', text: t('عند الإيقاف لا يرى المدرب بياناتك بعد الآن. الخطط التي أرسلها تبقى عندك.') }),
      h('button', {
        type: 'button', class: 'btn btn-danger', onclick: async () => {
          const ok = await confirmDialog({
            title: t('ترك المدرب'),
            message: t('سيتوقف {name} عن رؤية بياناتك فوراً.', { name: mine.coach.businessName }),
            confirmText: t('ترك المدرب'),
            danger: true,
          });
          if (!ok) return;
          try {
            await del('/my-coach');
            showSaved(t('تم إيقاف المشاركة'));
            reload();
          } catch (error) {
            body.append(calmNotice(error));
          }
        },
      }, t('ترك المدرب'))));
}

/** #join/CODE: shows who the coach is and exactly what will be shared, then asks for consent. */
export function joinMount(view, [code = '']) {
  const body = h('div', { class: 'stack' });
  add(view, h('header', { class: 'view-head' }, h('h1', { text: t('الانضمام إلى مدرب') })), body);
  loadInto(body, () => get(`/my-coach/preview?code=${encodeURIComponent(code)}`), (coach) => {
    const status = h('div');
    const accept = h('button', { type: 'button', class: 'btn' }, t('أوافق وانضم'));
    accept.addEventListener('click', async () => {
      accept.disabled = true;
      fill(status);
      try {
        await post('/my-coach/join', { code });
        showSaved(t('انضممت إلى {name}', { name: coach.businessName }));
        navigate('#coach');
      } catch (error) {
        fill(status, calmNotice(error));
        accept.disabled = false;
      }
    });
    add(body, h('section', { class: 'card stack' },
      h('h2', { text: coach.businessName }),
      coach.bio && h('p', { text: coach.bio }),
      h('p', { text: t('بالانضمام، يستطيع مدربك أن يرى:') }),
      h('ul', { class: 'consent-list' },
        h('li', { text: t('ملفك الشخصي وأهدافك اليومية') }),
        h('li', { text: t('وجباتك وماءك ووزنك') }),
        h('li', { text: t('خطط تمرينك ومدى التزامك بها') })),
      h('p', { class: 'muted small', text: t('لا يرى كلمة مرورك ولا بريدك. ويمكنك ترك المدرب في أي وقت من «حسابي».') }),
      h('div', { class: 'row' }, accept, h('a', { class: 'btn btn-secondary', href: '#today' }, t('ليس الآن'))),
      status));
  });
}
