/**
 * Cancellation requests on the real API (owner's rule, 2026-11-07: the server decides, not the browser). Loaded after
 * cancellationService and automation; demo mode (GitHub Pages) is unchanged.
 *
 * In API mode the server refuses a direct cancel of anything paid (409 request_required). "Cancel" posts
 * /cancellations, the server gives the request its number, tells the team and sends the n8n event
 * cancellation.requested (the AI agent's trigger). The admin console (or the agent, with its key) answers on
 * /admin/cancellations/{code}/approve|reject; only the server cancels and refunds.
 *
 * Pages read `SA.services.cancellations` synchronously, so they call `ready()` first to fill a small cache.
 */
(function (SA) {
  const api = SA.services.api;
  const CX = SA.services.cancellations;
  if (!api || !CX) return;
  const demo = { ...CX };
  const live = () => api.isLive();

  const TO_SERVER = { booking: 'booking', package: 'package', order: 'order', 'game-request': 'game', game: 'game' };
  const FROM_SERVER = { Booking: 'booking', Package: 'package', Order: 'order', Game: 'game' };
  const STATUS = { Pending: 'pending', Approved: 'approved', Rejected: 'rejected' };
  const KIND_LABEL = { booking: 'Pitch booking', package: 'Weekly package', order: 'Shop order', game: 'Place in a game' };
  const TOLD = 'superapp.cx.told';
  const ref = (kind, refId) => String(refId).replace(/^mine:/, '');
  const time = (s) => (s ? Date.parse(s) : 0);

  let mine = [];      // the customer's requests (site shape)
  let queue = [];     // the admin console's requests
  let mineAt = 0;

  /** Server request → the shape the pages and the chatbot read */
  const toRec = (r) => {
    const kind = FROM_SERVER[r.kind] || 'booking';
    return {
      id: r.code, kind, kindLabel: KIND_LABEL[kind], refId: r.reference, serverKind: kind, label: r.label, link: String(r.link || '').replace(/^#/, ''),
      paid: r.paidOnline, refund: r.refundIfApproved, refunded: r.refunded, reason: r.reason || '', status: STATUS[r.status] || 'pending', note: r.note || '',
      createdAt: time(r.createdAt), dueAt: time(r.dueAt), decidedAt: time(r.decidedAt), channel: 'chatbot', live: true,
      customer: r.customer ? { id: r.customer.id, name: r.customer.name, email: r.customer.email, phone: r.customer.phone } : null,
    };
  };
  const same = (r, kind, refId) => r.serverKind === TO_SERVER[kind] && r.refId === ref(kind, refId);

  /** Decisions the customer hasn't seen in the chat yet go there once (the server also sends a notification) */
  const tellChat = () => {
    let told = [];
    try { told = JSON.parse(localStorage.getItem(TOLD) || '[]'); } catch { /* private window */ }
    const fresh = mine.filter((r) => r.status !== 'pending' && !told.includes(r.id) && Date.now() - r.decidedAt < 7 * 864e5);
    fresh.forEach((r) => SA.ui.chatbot?.cancellation(r, r.status));
    try { localStorage.setItem(TOLD, JSON.stringify([...told, ...fresh.map((r) => r.id)].slice(-200))); } catch { /* ignore */ }
  };

  const loadMine = async (force = false) => {
    if (!SA.services.session?.me()) { mine = []; return mine; }
    if (!force && Date.now() - mineAt < 15e3) return mine;
    mine = (await api.get('/cancellations/mine').catch(() => [])).map(toRec);
    mineAt = Date.now();
    tellChat();
    return mine;
  };
  const loadQueue = async () => {
    const page = await api.get('/admin/cancellations?status=all&pageSize=100');
    queue = (page.items || page).map(toRec);
    return queue;
  };

  const request = async ({ kind, refId, reason = '' }) => {
    const r = toRec(await api.post('/cancellations', { kind: TO_SERVER[kind], reference: ref(kind, refId), reason: String(reason || '').trim() || null }));
    mine = [r, ...mine.filter((x) => x.id !== r.id)];
    SA.ui.chatbot?.cancellation(r, 'sent');
    return r;
  };
  const decide = async (id, action, note) => {
    const r = toRec(await api.post(`/admin/cancellations/${encodeURIComponent(id)}/${action}`, { note: String(note || '').trim() || null }));
    queue = queue.map((x) => (x.id === r.id ? r : x));
    return r;
  };

  Object.assign(CX, {
    /** Fills the cache the pages read: the customer's requests, and on the admin console its queue */
    ready: () => (live() ? Promise.all([loadMine(), /^\/admin/.test(SA.core.router.location()) ? loadQueue().catch(() => []) : null]) : Promise.resolve()),
    all: () => (live() ? (queue.length ? queue : mine).slice().sort((a, b) => b.createdAt - a.createdAt) : demo.all()),
    mine: () => (live() ? mine.slice() : demo.mine()),
    openFor: (kind, refId) => (live() ? mine.find((r) => same(r, kind, refId) && r.status === 'pending') || null : demo.openFor(kind, refId)),
    lastFor: (kind, refId) => (live() ? mine.find((r) => same(r, kind, refId)) || null : demo.lastFor(kind, refId)),
    pending: () => (live() ? queue.filter((r) => r.status === 'pending') : demo.pending()),
    request: (args) => (live() ? request(args) : demo.request(args)),
    approve: (id, note) => (live() ? decide(id, 'approve', note) : demo.approve(id, note)),
    reject: (id, reason) => (live() ? decide(id, 'reject', reason) : demo.reject(id, reason)),
  });

  // The pages that show a Cancel button read the cache first
  api.ready?.then(() => { if (live()) loadMine().catch(() => null); });
  SA.services.liveCancellations = { loadMine, loadQueue, toRec };
})(window.SA);
