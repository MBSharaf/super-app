/**
 * Super App service worker (backend 8.7): lets the site install as an app and shows a friendly page when offline.
 *
 * - The site's own files (css, js, fonts, icons, models) come from the network first, so an update shows up at once;
 *   each answer is kept so the app still opens on a bad connection.
 * - Page addresses come from the network; offline they show offline.html.
 * - The API (/api/…), sign-in, payments and anything that is not a GET never go through the cache.
 * Bump VERSION to drop the old copies after a release.
 */
const VERSION = 'v1';
const CACHE = `superapp-${VERSION}`;
const OFFLINE = 'offline.html';
const PRECACHE = [OFFLINE, 'icons/icon-192.png', 'manifest.json'];
const STATIC = /\.(?:css|js|woff2?|png|jpe?g|webp|svg|glb|json)$/i;

self.addEventListener('install', (event) => {
  event.waitUntil(caches.open(CACHE).then((c) => c.addAll(PRECACHE)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', (event) => {
  event.waitUntil(caches.keys()
    .then((keys) => Promise.all(keys.filter((k) => k.startsWith('superapp-') && k !== CACHE).map((k) => caches.delete(k))))
    .then(() => self.clients.claim()));
});

/** Network first; a good answer is kept for later, the kept copy answers when the network fails */
const networkFirst = async (request) => {
  const cache = await caches.open(CACHE);
  try {
    const response = await fetch(request);
    if (response.ok && response.type === 'basic') cache.put(request, response.clone());
    return response;
  } catch (err) {
    const kept = await cache.match(request);
    if (kept) return kept;
    throw err;
  }
};

self.addEventListener('fetch', (event) => {
  const { request } = event;
  const url = new URL(request.url);
  if (request.method !== 'GET' || url.origin !== self.location.origin || url.pathname.includes('/api/')) return;
  if (request.mode === 'navigate') {
    event.respondWith(fetch(request).catch(async () => (await caches.match(new URL(OFFLINE, self.registration.scope).href)) || Response.error()));
    return;
  }
  if (url.href.startsWith(self.registration.scope) && STATIC.test(url.pathname)) event.respondWith(networkFirst(request));
});
