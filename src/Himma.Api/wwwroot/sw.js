// Service worker: makes the app installable and lets the static files load offline.
// It caches static files only. API responses (personal data) are never cached.

const CACHE = 'himma-static-v1';

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
