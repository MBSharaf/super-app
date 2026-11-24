/**
 * Favourite clubs, news, the real bell and the phone switches (live site only; the demo keeps its local bell).
 *
 *   - Favourite clubs (up to 3): GET /matches/clubs (the clubs to pick from), GET/PUT /me/favorite-clubs. The server
 *     tells followers about line-ups, kick-off, goals and full time of their clubs' matches.
 *   - The bell: GET /me/notifications every minute (and when a push arrives); opening one marks it read on the server.
 *   - Phone switches: GET/PUT /me/notification-settings (which kinds reach the phone; the bell keeps everything).
 *   - News written by the Super Malaab team: GET /news, GET /news/{id}; the admin's editor: /admin/news.
 *
 * After signing in, someone who has not picked their clubs yet is asked once on this device (SA.ui.clubPicker).
 */
(function (SA) {
  const api = () => SA.services.api;
  const session = () => SA.services.session;
  const store = SA.core.store;
  const at = (iso) => (iso ? new Date(iso.endsWith('Z') ? iso : `${iso}Z`).getTime() : Date.now());
  const POLL_MS = 60_000;
  /** Server kinds → the bell's icons */
  const KIND = { bookings: 'booking', orders: 'shop', designs: 'shop', store: 'shop', money: 'shop', fantasy: 'fantasy', matches: 'match', community: 'match', news: 'news' };
  let clubsCache = null;
  let timer = null;

  /** True on the server's site with someone signed in */
  const live = () => !!(SA.config.realData && api()?.isLive?.() && session()?.me());

  const rowOf = (n) => ({ id: `s${n.id}`, sid: n.id, server: true, kind: KIND[n.category] || n.category, title: n.title, body: n.body || '',
    link: String(n.link || '/').replace(/^#/, ''), at: at(n.createdAt), read: n.read });

  const follow = {
    live,

    /* ---------- The bell ---------- */
    async sync() {
      if (!live()) return;
      try {
        const r = await api().get('/me/notifications');
        const server = (r.items || []).map(rowOf);
        store.set((s) => ({ notifications: [...server, ...(s.notifications || []).filter((n) => !n.server)].sort((a, b) => b.at - a.at).slice(0, 60) }), 'inbox');
      } catch { /* offline or signed out: keep what the bell shows */ }
    },
    markRead(ids) { if (live()) api().post('/me/notifications/read', { ids }).catch(() => {}); },

    /* ---------- Favourite clubs ---------- */
    /** → [{ key, name, nameAr, clubs: [{ id, name, nameAr, shortName, logo }] }] */
    competitions() {
      clubsCache ||= api().get('/matches/clubs').catch((e) => { clubsCache = null; throw e; });
      return clubsCache;
    },
    mine: () => api().get('/me/favorite-clubs'),
    setMine: (clubIds) => api().put('/me/favorite-clubs', { clubIds }),
    /** Arabic: the server's Arabic name when it has its own, else the site's list of Arabic club names */
    clubName: (c) => (!SA.i18n?.isAr ? c.name : c.nameAr && c.nameAr !== c.name ? c.nameAr : SA.data.clubsAr?.name(c.name) || c.name),

    /* ---------- Phone switches ---------- */
    settings: () => api().get('/me/notification-settings'),
    setSettings: (settings) => api().put('/me/notification-settings', { settings }),

    /* ---------- News ---------- */
    news: (club = null, page = 1) => api().get(`/news?page=${page}${club ? `&club=${club}` : ''}`),
    story: (id) => api().get(`/news/${encodeURIComponent(id)}`),
    admin: {
      list: (page = 1) => api().get(`/admin/news?page=${page}`),
      get: (id) => api().get(`/admin/news/${encodeURIComponent(id)}`),
      save: (id, body) => (id ? api().put(`/admin/news/${encodeURIComponent(id)}`, body) : api().post('/admin/news', body)),
      hide: (id) => api().del(`/admin/news/${encodeURIComponent(id)}`),
    },
  };

  /* ---------- First sign-in: pick your clubs (once per account on this device) ---------- */
  const ASKED = (id) => `sa.clubs.asked.${id}`;
  const STAFF = /^#?\/(admin|ceo|dash|seller|factory|auth)/;
  let asking = false;
  const askOnce = async (me) => {
    if (asking || !me || !live() || localStorage.getItem(ASKED(me.id))) return;
    const where = SA.core.router?.location?.() || location.hash;
    if (STAFF.test(where)) { setTimeout(() => askOnce(session().me()), 4000); return; }
    asking = true;
    try {
      const mine = await follow.mine();
      localStorage.setItem(ASKED(me.id), '1');
      if (!mine.length) SA.ui.clubPicker?.open({ first: true });
    } catch { /* try again on the next visit */ } finally { asking = false; }
  };

  const start = () => {
    clearInterval(timer);
    if (!live()) return;
    follow.sync();
    timer = setInterval(() => { if (document.visibilityState === 'visible') follow.sync(); }, POLL_MS);
  };

  const wire = () => {
    const S = SA.services;
    if (!S.session || !S.user) { setTimeout(wire, 300); return; }
    // Opening a server notification marks it read on the server too
    const markRead = S.user.markRead, markAll = S.user.markAllRead;
    S.user.markRead = (id) => { const n = store.get().notifications.find((x) => x.id === id); if (n?.server && !n.read) follow.markRead([n.sid]); markRead(id); };
    S.user.markAllRead = () => { if (store.get().notifications.some((n) => n.server && !n.read)) follow.markRead([]); markAll(); };
    S.session.subscribe((me) => { start(); if (me) setTimeout(() => askOnce(me), 1200); });
    document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'visible') follow.sync(); });
    api().ready.then(() => { if (S.session.me()) { start(); setTimeout(() => askOnce(S.session.me()), 1500); } });
  };

  SA.services.follow = follow;
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', wire); else wire();
})(window.SA);
