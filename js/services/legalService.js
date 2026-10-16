/**
 * Legal: documents (js/data/legal.js), who accepted which version, the lawyer-review status, and cookie consent.
 * Acceptances are stored in db.legalAccepts (one line per person, document and version) so the CEO can see them; with
 * the backend they are kept as legal records. Cookie consent lives on the device (superapp.consent).
 */
(function (SA) {
  const { ApiError } = SA.services.http;
  const db = () => SA.services.db;
  const CONSENT = 'superapp.consent';

  const ensure = () => { const d = db().raw(); if (!Array.isArray(d.legalAccepts)) { d.legalAccepts = []; db().touch('legalAccepts'); } };
  const reviews = () => db().settings().legalReview || {};
  const who = () => { const u = SA.core.store.get().user; return u ? { id: u.id, name: u.name, kind: 'customer' } : { id: 'guest', name: 'Guest', kind: 'guest' }; };

  const legal = {
    docs: () => SA.data.legal.DOCS.map((d) => ({ ...d, review: reviews()[d.key] || null, accepted: (db().all('legalAccepts') || []).filter((x) => x.doc === d.key).length })),
    doc: (key) => { const d = legal.docs().find((x) => x.key === key); if (!d) throw new ApiError('Document not found.', 'not_found'); return d; },
    /** Record that someone accepted documents (booking, joining a game, a tournament, applying to sell or partner, cookies) */
    accept(keys, { where = '', person = who() } = {}) {
      ensure();
      [].concat(keys).forEach((k) => { const d = SA.data.legal.DOCS.find((x) => x.key === k); if (d) db().raw().legalAccepts.unshift({ id: db().uid('la'), doc: k, version: d.version, who: person, where, at: Date.now() }); });
      db().raw().legalAccepts = db().raw().legalAccepts.slice(0, 3000);
      db().touch('legalAccepts');
    },
    /** Services call this before a booking, a game request or a tournament entry */
    requireWaiver(flag, where) {
      if (!flag) throw new ApiError('Please accept the injury & conduct waiver.', 'invalid', { waiver: 'Please accept the injury & conduct waiver.' });
      legal.accept(['waiver', 'terms'], { where });
    },
    /** CEO: a lawyer reviewed this version (or it goes back to draft) */
    setReviewed(key, reviewed, note = '') {
      SA.services.ceo.me();
      const d = SA.data.legal.DOCS.find((x) => x.key === key); if (!d) throw new ApiError('Document not found.', 'not_found');
      db().setSettings({ legalReview: { ...reviews(), [key]: reviewed ? { version: d.version, at: Date.now(), note: String(note).trim().slice(0, 200) } : null } });
      SA.services.ceo.log('legal', `${d.title.en} v${d.version} ${reviewed ? 'marked as reviewed by the lawyer' : 'back to draft'}${note ? ` — ${note}` : ''}`, key);
    },
    /* Cookie consent (this device) */
    consent() { try { return JSON.parse(localStorage.getItem(CONSENT)) || null; } catch { return null; } },
    setConsent({ analytics }) {
      try { localStorage.setItem(CONSENT, JSON.stringify({ analytics: !!analytics, at: Date.now(), v: SA.data.legal.DOCS.find((x) => x.key === 'cookies').version })); } catch { /* private mode */ }
      legal.accept(['cookies'], { where: analytics ? 'cookies: all' : 'cookies: essential only' });
    },
  };
  SA.services.legal = legal;
})(window.SA);
