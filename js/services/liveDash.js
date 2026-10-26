/**
 * The venue dashboard's reviews and free slots on the real API (backend 3.6). Loaded after livePartner.
 *
 * In API mode, for the signed-in venue (the live partner account): the reviews page lists `/partner/reviews`
 * and replies, removes replies and reports through the server; "Free slots" posts each hour to
 * `/partner/free-slots` (with its discount) instead of the demo community feed. The same service methods
 * answer, from a small cache filled on first read; the page re-renders when the server answers.
 */
(function (SA) {
  const api = SA.services.api;
  const D = SA.services.dash;
  const C = SA.services.community;
  const { ApiError } = SA.services.http;
  const demo = { reviews: D.reviews, review: D.review, replyReview: D.replyReview, deleteReply: D.deleteReply, reportReview: D.reportReview, createPromo: D.createPromo, postSlot: C?.postSlot };

  const isLivePartner = () => api.isLive() && D.auth?.current?.()?.id === SA.services.livePartner?.ACCOUNT;
  const sitePitch = (apiId) => SA.services.db.all('pitches').find((p) => p.live && p.apiId === apiId);
  const at = (iso) => (iso ? new Date(iso.endsWith('Z') ? iso : `${iso}Z`).getTime() : null);
  let cache = [];
  let loaded = false;

  const fromApi = (r) => {
    const p = sitePitch(r.pitchId);
    return {
      id: `rv-${r.id}`, apiId: r.id, live: true, pitchId: p?.id || `p-${r.pitchId}`, pitchName: r.pitchName, rating: r.stars, text: r.text || '',
      customer: { id: null, name: r.customerName }, bookingId: r.bookingCode, createdAt: at(r.createdAt),
      reply: r.reply ? { text: r.reply, at: at(r.repliedAt) } : null, reported: r.reported ? { reason: 'Reported', at: null } : null, hidden: r.isHidden,
    };
  };
  const reload = () => api.get('/partner/reviews?pageSize=100').then((r) => { cache = (r.items || r).map(fromApi); SA.app?.rerender?.(); }, () => {});
  const fail = (err) => SA.ui.overlay.toast(err.message, { type: 'error' });
  const liveReview = (id) => String(id).startsWith('rv-') && cache.find((r) => r.id === id);

  D.reviews = (opts = {}) => {
    if (!isLivePartner()) return demo.reviews(opts);
    if (!loaded) { loaded = true; reload(); }
    return cache.filter((r) => !opts.pitchId || r.pitchId === opts.pitchId);
  };
  D.review = (id) => liveReview(id) || demo.review(id);
  D.replyReview = (id, text) => {
    const r = liveReview(id);
    if (!r) return demo.replyReview(id, text);
    const t = (text || '').trim();
    if (t.length < 2) throw new ApiError('Write a reply first.', 'invalid', { reply: 'Write a reply' });
    if (t.length > 600) throw new ApiError('Keep replies under 600 characters.', 'invalid', { reply: 'Too long' });
    r.reply = { text: t, at: Date.now() };
    api.post(`/partner/reviews/${r.apiId}/reply`, { text: t }).then(reload, (err) => { fail(err); reload(); });
    return r;
  };
  D.deleteReply = (id) => {
    const r = liveReview(id);
    if (!r) return demo.deleteReply(id);
    r.reply = null;
    api.del(`/partner/reviews/${r.apiId}/reply`).then(reload, (err) => { fail(err); reload(); });
  };
  D.reportReview = (id, reason) => {
    const r = liveReview(id);
    if (!r) return demo.reportReview(id, reason);
    if (!reason) throw new ApiError('Choose a reason.', 'invalid', { reason: 'Required' });
    r.reported = { reason, at: Date.now() };
    api.post(`/partner/reviews/${r.apiId}/report`, { reason }).then(reload, (err) => { fail(err); reload(); });
  };

  // Free slots: the server applies the discount itself, so no separate offer is made for a live pitch
  D.createPromo = (d) => {
    const p = SA.services.db.find('pitches', d.pitchId);
    return isLivePartner() && p?.live && d.label === 'Last-minute slot' ? { id: null } : demo.createPromo(d);
  };
  if (C && demo.postSlot) {
    C.postSlot = (d) => {
      const p = SA.services.db.find('pitches', d.pitchId);
      if (!(isLivePartner() && p?.apiId)) return demo.postSlot(d);
      if (!((d.pct || 0) >= 0.05)) throw new ApiError('Give 5% to 90% off to post a free slot.', 'invalid');
      api.post('/partner/free-slots', { pitchId: p.apiId, date: d.date, hour: d.hour, discountPercent: Math.round((d.pct || 0) * 100) }).catch(fail);
      return { id: null, live: true };
    };
  }

  SA.services.liveDash = { reloadReviews: reload };
})(window.SA);
