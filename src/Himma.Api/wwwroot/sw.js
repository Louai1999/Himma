// Service worker: makes the app installable, lets the static files load offline, and shows reminders
// sent by the server (Web Push). It caches static files only. API responses (personal data) are never cached.

const CACHE = 'himma-static-v2';

self.addEventListener('install', () => self.skipWaiting());

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.filter((key) => key !== CACHE).map((key) => caches.delete(key))))
      .then(() => self.clients.claim()));
});

self.addEventListener('fetch', (event) => {
  const { request } = event;
  const url = new URL(request.url);
  const isStatic = request.method === 'GET'
    && url.origin === self.location.origin
    && !url.pathname.startsWith('/api/')
    && !url.pathname.startsWith('/swagger')
    && url.pathname !== '/health';
  if (!isStatic) return;

  // Network first, so updates show up immediately; the cache is only a fallback when offline.
  event.respondWith(
    fetch(request)
      .then((response) => {
        if (response.ok) {
          const copy = response.clone();
          caches.open(CACHE).then((cache) => cache.put(request, copy));
        }
        return response;
      })
      .catch(() => caches.match(request).then((cached) => cached ?? caches.match('/index.html'))));
});

// ---------- Reminders ----------
// The payload comes from the server: { title, body, tag, url, takeToken?, takeLabel? }.

self.addEventListener('push', (event) => {
  let data = {};
  try { data = event.data?.json() ?? {}; } catch { /* a malformed message still shows something */ }
  const options = {
    body: data.body ?? '',
    tag: data.tag,
    renotify: Boolean(data.tag),
    icon: '/assets/himma-logo.svg',
    badge: '/assets/himma-logo.svg',
    data: { url: data.url ?? '/', takeToken: data.takeToken ?? null },
    actions: data.takeToken ? [{ action: 'take', title: data.takeLabel ?? '✅' }] : [],
  };
  event.waitUntil(self.registration.showNotification(data.title ?? 'Himma', options));
});

self.addEventListener('notificationclick', (event) => {
  const { url, takeToken } = event.notification.data ?? {};
  event.notification.close();

  if (event.action === 'take' && takeToken) {
    // Logs the dose without opening the app. The token can only log this one supplement for that day.
    event.waitUntil(
      fetch('/api/supplements/take', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token: takeToken }),
      }).then((response) => (response.ok ? null : openApp(url))).catch(() => openApp(url)));
    return;
  }
  event.waitUntil(openApp(url));
});

/** Focuses an open Himma window (and moves it to the url) or opens a new one. */
async function openApp(url = '/') {
  const target = new URL(url, self.location.origin).href;
  const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
  const existing = windows.find((client) => new URL(client.url).origin === self.location.origin);
  if (existing) {
    await existing.focus();
    return existing.navigate(target).catch(() => null);
  }
  return self.clients.openWindow(target);
}
