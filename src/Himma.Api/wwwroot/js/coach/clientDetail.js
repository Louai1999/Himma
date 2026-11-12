import { t } from '../i18n/index.js';
import { get, post, put, api } from '../api.js';
import { formatDate, formatDateTime, todayLocal } from '../dates.js';
import { mealTypes } from '../labels.js';
import { h, fill, add, fmt } from '../ui/dom.js';
import { icon } from '../ui/icons.js';
import { confirmDialog } from '../ui/modal.js';
import { calmNotice, loadInto } from '../ui/notice.js';
import { showSaved } from '../ui/toast.js';
import { navigate } from '../router.js';
import { profileCard, adherenceCard, weightCard, workoutCard, lastDaysCard } from '../admin/userDetail.js';
import { attentionChips, attentionLabels, attentionMessages } from './labels.js';

/** One client: what needs attention, messages, sending a plan, private notes, then the full report. */
export function mount(view, [id]) {
  const body = h('div', { class: 'stack' });
  add(view, h('a', { class: 'btn btn-ghost btn-sm back-link', href: '#clients' }, icon('next', 16, 'flip-ltr'), t('كل المتدربين')), body);
  const load = () => loadInto(body, () => get(`/coach/clients/${encodeURIComponent(id)}`), (client) => render(body, client, load));
  load();
}

function render(body, client, reload) {
  const base = `/coach/clients/${encodeURIComponent(client.id)}`;
  const messages = messagesCard(client, base);
  add(body,
    h('section', { class: 'card stack' },
      h('div', { class: 'row spread' },
        h('div', {},
          h('h1', { text: client.displayName }),
          h('p', { class: 'muted', dir: 'ltr', text: `@${client.username}` })),
        attentionChips(client.attention)),
      h('p', { class: 'muted small', text: t('متدرب عندك منذ {date}', { date: formatDate(client.joinedAtUtc) }) }),
      client.attention.length > 0 && h('div', { class: 'stack' },
        h('p', { class: 'small', text: t('رد سريع (يمكنك تعديله قبل الإرسال):') }),
        h('div', { class: 'row' }, client.attention.map((reason) => h('button', {
          type: 'button', class: 'btn btn-secondary btn-sm',
          onclick: () => messages.prefill(attentionMessages[reason]),
        }, attentionLabels[reason]))))),
    messages.card,
    h('div', { class: 'grid-2 wide-only' }, planCard(client, base), noteCard(client, base)),
    profileCard(client),
    adherenceCard(client),
    h('div', { class: 'grid-2 wide-only' }, weightCard(client), workoutCard(client)),
    lastDaysCard(client),
    mealsCard(base),
    endCard(client, base, reload));
}

function messagesCard(client, base) {
  const text = h('textarea', { maxlength: 500, rows: 3, placeholder: t('مثال: أداء ممتاز هالأسبوع! زد الماء شوي.') });
  const counter = h('span', { class: 'muted small num', text: '0/500' });
  const status = h('div');
  const list = h('ul', { class: 'item-list messages' });
  const send = h('button', { type: 'submit', class: 'btn' }, t('إرسال'));
  const items = [...client.messages];

  const renderList = () => fill(list, items.length === 0
    ? h('li', { class: 'muted small', text: t('لم ترسل رسائل بعد.') })
    : items.map((m) => h('li', { class: 'item' },
      h('div', { class: 'grow' },
        h('div', { text: m.text }),
        h('div', { class: 'meta', text: `${formatDateTime(m.createdAtUtc)} · ${m.readAtUtc ? t('قرأها ✓') : t('لم تُقرأ بعد')}` })))));
  renderList();

  const updateCounter = () => { counter.textContent = `${text.value.length}/500`; };
  text.addEventListener('input', updateCounter);

  const form = h('form', { class: 'stack', novalidate: true },
    text,
    h('div', { class: 'row spread' }, counter, send),
    status);
  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    fill(status);
    send.disabled = true;
    try {
      const note = await post(`${base}/messages`, { text: text.value });
      items.unshift(note);
      renderList();
      text.value = '';
      updateCounter();
      showSaved(t('وصلت الرسالة'));
    } catch (error) {
      fill(status, calmNotice(error));
    } finally {
      send.disabled = false;
    }
  });

  const card = h('section', { class: 'card stack' },
    h('h2', { text: t('رسائل إلى {name}', { name: client.displayName }) }),
    h('p', { class: 'muted small', text: t('تظهر في تطبيقه، ويصله إشعار إن فعّل الإشعارات.') }),
    form,
    list);

  return {
    card,
    prefill(message) {
      text.value = message;
      updateCounter();
      text.focus();
      card.scrollIntoView({ behavior: 'smooth', block: 'start' });
    },
  };
}

/** Lists the coach's own plans (built in the app's Workouts tab) and sends a copy to the client. */
function planCard(client, base) {
  const card = h('section', { class: 'card stack' }, h('h2', { text: t('إرسال خطة تمرين') }));
  const slot = h('div', { class: 'stack' });
  add(card, h('p', { class: 'muted small', text: t('ابنِ الخطة مرة واحدة في قسم «التمارين» بالتطبيق، ثم أرسل نسخة منها لأي متدرب.') }), slot);

  loadInto(slot, () => get('/plans'), (plans) => {
    if (plans.length === 0) {
      add(slot, h('p', { class: 'muted', text: t('ليس عندك خطط بعد.') }), h('a', { class: 'btn btn-secondary btn-sm', href: '/#workouts' }, t('أنشئ خطة في التطبيق')));
      return;
    }
    const select = h('select', { 'aria-label': t('الخطة') }, plans.map((p) => h('option', { value: String(p.id), text: p.name })));
    const activate = h('input', { type: 'checkbox', checked: true });
    const status = h('div');
    const button = h('button', { type: 'button', class: 'btn' }, t('أرسل الخطة'));
    button.addEventListener('click', async () => {
      button.disabled = true;
      fill(status);
      try {
        const result = await post(`${base}/plans`, { planId: Number(select.value), activate: activate.checked });
        showSaved(t('وصلت خطة «{name}» إلى {client}', { name: result.name, client: client.displayName }));
      } catch (error) {
        fill(status, calmNotice(error));
      } finally {
        button.disabled = false;
      }
    });
    add(slot,
      select,
      h('label', { class: 'row small' }, activate, t('اجعلها خطته النشطة الآن')),
      button,
      status);
  });
  return card;
}

function noteCard(client, base) {
  const text = h('textarea', { maxlength: 2000, rows: 4, placeholder: t('إصابات، تفضيلات أكل، مواعيد قياس...') });
  text.value = client.privateNote ?? '';
  const status = h('div');
  const save = h('button', { type: 'button', class: 'btn btn-secondary' }, t('حفظ الملاحظة'));
  save.addEventListener('click', async () => {
    save.disabled = true;
    fill(status);
    try {
      await put(`${base}/note`, { note: text.value });
      showSaved();
    } catch (error) {
      fill(status, calmNotice(error));
    } finally {
      save.disabled = false;
    }
  });
  return h('section', { class: 'card stack' },
    h('h2', { text: t('ملاحظاتي الخاصة') }),
    h('p', { class: 'muted small', text: t('لك وحدك. المتدرب لا يراها أبداً.') }),
    text,
    save,
    status);
}

/** Reading a client's meals is audit-logged on the server. */
function mealsCard(base) {
  const date = h('input', { type: 'date', value: todayLocal(), max: todayLocal(), 'aria-label': t('اختر يوماً') });
  const result = h('div');
  const show = () => loadInto(result, () => get(`${base}/meals?date=${date.value}`), (day) => {
    if (day.items.length === 0) {
      add(result, h('p', { class: 'muted', text: t('لا يوجد تسجيل في هذا اليوم.') }));
      return;
    }
    add(result,
      h('p', { class: 'muted small' }, h('span', { class: 'num', text: fmt(day.totals.calories) }), t(' سعرة · '),
        h('span', { class: 'num', text: fmt(day.totals.proteinG) }), t(' غ بروتين')),
      h('ul', { class: 'item-list' }, day.items.map((m) => h('li', { class: 'item' },
        h('div', { class: 'grow' },
          h('div', { class: 'title', text: m.name }),
          h('div', { class: 'meta', text: `${mealTypes[m.mealType]} · ${t('{grams} غ', { grams: fmt(m.grams) })}` })),
        h('span', { class: 'num', text: fmt(m.calories) })))));
  });
  show();

  return h('section', { class: 'card stack' },
    h('h2', { text: t('وجبات يوم معيّن') }),
    h('div', { class: 'row' }, date, h('button', { type: 'button', class: 'btn btn-secondary btn-sm', onclick: show }, t('عرض'))),
    result);
}

function endCard(client, base) {
  const status = h('div');
  const end = async () => {
    const ok = await confirmDialog({
      title: t('إنهاء التدريب'),
      message: t('لن ترى بيانات {name} بعد الآن، ويتحرر مقعده. خططه ورسائله تبقى عنده.', { name: client.displayName }),
      confirmText: t('إنهاء التدريب'),
      danger: true,
    });
    if (!ok) return;
    try {
      await api(base, { method: 'DELETE' });
      showSaved(t('انتهى التدريب'));
      navigate('#clients');
    } catch (error) {
      fill(status, calmNotice(error));
    }
  };
  return h('section', { class: 'card danger-zone stack' },
    h('h2', { text: t('إنهاء التدريب') }),
    h('button', { type: 'button', class: 'btn btn-danger', onclick: end }, t('إنهاء التدريب مع هذا المتدرب')),
    status);
}
