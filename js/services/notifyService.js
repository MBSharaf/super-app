/**
 * Notifications for dashboard accounts (sellers first, also owners, the print partner and admins).
 * Stored in the platform DB per account; the dashboard bell, the notifications page and live toasts read them.
 * Sellers who are also the shopper on this device get a copy in the site bell too.
 *
 * Categories (each can be switched off in preferences): designs, orders, money, level, store, account.
 * Time-based events (profit released after the hold, level changes) are found by sweep(), which runs on start-up,
 * after data changes and every minute. With the backend these become server jobs + push/email/SMS.
 */
(function (SA) {
  const db = () => SA.services.db;
  const DAY = 864e5;
  const CATS = {
    bookings: { label: 'Bookings', icon: 'calendar' },
    designs: { label: 'Designs & review', icon: 'image' },
    orders: { label: 'Orders', icon: 'package' },
    money: { label: 'Money', icon: 'wallet' },
    level: { label: 'Level & perks', icon: 'trend' },
    store: { label: 'Store & catalogue', icon: 'bag' },
    account: { label: 'Account & safety', icon: 'shield' },
  };
  const uid = () => `nt-${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
  const prefsOf = (accountId) => db().find('accounts', accountId)?.notifyPrefs || {};

  const notify = {
    CATS,
    /** Add a notification for an account. Returns it, or null when that category is switched off. */
    toAccount(accountId, { cat = 'account', title, body = '', link = '', icon = '', tone = '' }) {
      if (!accountId || !title) return null;
      if (prefsOf(accountId)[cat] === false && cat !== 'account') return null;
      const n = { id: uid(), to: accountId, cat, title, body, link, icon: icon || CATS[cat]?.icon || 'bell', tone, at: Date.now(), read: false };
      const list = db().raw().notifications;
      list.unshift(n);
      // Keep the newest 150 per account
      const mine = list.filter((x) => x.to === accountId);
      if (mine.length > 150) { const drop = new Set(mine.slice(150).map((x) => x.id)); db().raw().notifications = list.filter((x) => !drop.has(x.id)); }
      try { db().touch('notifications'); } catch { /* storage full */ }
      // Seller who is the shopper on this device: copy to the site bell
      const s = SA.services.sellers?.byAccount(accountId);
      const me = SA.core.store.get().user;
      if (s && me && s.userId === me.id) SA.services.user.notify({ kind: 'shop', title, body, link: link.startsWith('#') ? link.slice(1) : link });
      return n;
    },
    toSeller(sellerId, n) { const s = SA.services.sellers?.get(sellerId); return s ? notify.toAccount(s.accountId, n) : null; },
    toAdmins(n) { db().all('accounts').filter((a) => a.role === 'admin').forEach((a) => notify.toAccount(a.id, n)); },

    list: (accountId, { unread = false, cat = '' } = {}) => db().all('notifications').filter((n) => n.to === accountId && (!unread || !n.read) && (!cat || n.cat === cat)),
    unread: (accountId) => db().all('notifications').filter((n) => n.to === accountId && !n.read).length,
    markRead(id) { const n = db().find('notifications', id); if (n && !n.read) db().update('notifications', id, { read: true }); },
    markAll(accountId) { db().raw().notifications.forEach((n) => { if (n.to === accountId) n.read = true; }); db().touch('notifications'); },
    remove(id) { db().remove('notifications', id); },
    prefs: prefsOf,
    setPrefs(accountId, patch) { const a = db().find('accounts', accountId); return db().update('accounts', accountId, { notifyPrefs: { ...(a?.notifyPrefs || {}), ...patch } }); },

    /**
     * Time-based events: profit released after the hold period, level up/down, level freeze ending.
     * Idempotent — markers on the records stop repeats.
     */
    sweep() {
      const S = SA.services.sellers, P = SA.services.pod;
      if (!S || !P) return;
      const hold = P.cfg().holdDays * DAY, now = Date.now();
      // Profit released (one notification per seller per sweep, summing the orders)
      const cleared = {}; let changed = false;
      db().all('podOrders').forEach((o) => {
        if (o.status === 'delivered' && !o.clearedNoticed && o.deliveredAt && now - o.deliveredAt >= hold) {
          if (!o.seed) (cleared[o.sellerId] ||= []).push(o);
          o.clearedNoticed = true; changed = true;
        }
      });
      Object.entries(cleared).forEach(([sid, list]) => {
        const amt = list.reduce((t, o) => t + o.profit, 0);
        notify.toSeller(sid, { cat: 'money', tone: 'turf', title: `${amt.toLocaleString()} EGP is ready to withdraw`, body: `Profit from ${list.length} delivered item${list.length > 1 ? 's' : ''} has cleared the ${P.cfg().holdDays}-day hold.`, link: '#/dash/seller/wallet' });
        const e = P.earnings(sid);
        if (e.available >= e.minWithdraw && e.available - amt < e.minWithdraw) notify.toSeller(sid, { cat: 'money', tone: 'turf', title: 'You can withdraw now', body: `Your available balance passed the ${e.minWithdraw.toLocaleString()} EGP minimum.`, link: '#/dash/seller/wallet' });
      });
      // Level changes
      S.list().forEach((s) => {
        const l = S.levelOf(s);
        const rec = db().raw().sellers.find((x) => x.id === s.id);
        if (s.notifiedLevel == null) { rec.notifiedLevel = l.index; changed = true; return; }
        if (l.index !== s.notifiedLevel) {
          const up = l.index > s.notifiedLevel;
          notify.toSeller(s.id, up
            ? { cat: 'level', tone: 'gold', title: `You reached ${l.name}!`, body: `New perks: ${l.designs == null ? 'unlimited' : l.designs} active designs, ${l.baseDiscount} EGP off every base price, ${Math.round(l.commission * 100)}% store commission.`, link: '#/dash/seller' }
            : { cat: 'level', tone: 'danger', title: `Your level is now ${l.name}`, body: 'Sales or rating dropped below the level requirement. Keep selling to climb back.', link: '#/dash/seller' });
          rec.notifiedLevel = l.index; changed = true;
        } else if (l.next && !l.frozen && l.toNext > 0 && l.toNext <= 3 && s.nearNoticed !== l.next.id) {
          notify.toSeller(s.id, { cat: 'level', tone: 'gold', title: `${l.toNext} more sale${l.toNext > 1 ? 's' : ''} to ${l.next.name}`, body: 'You’re almost at the next level.', link: '#/dash/seller' });
          rec.nearNoticed = l.next.id; changed = true;
        }
        if (s.frozenUntil && s.frozenUntil <= now && !s.unfrozenNoticed) {
          notify.toSeller(s.id, { cat: 'level', tone: 'turf', title: 'Your level is unfrozen', body: 'The 90 days after the copyright strike are over — you can level up again.', link: '#/dash/seller' });
          rec.unfrozenNoticed = true; changed = true;
        }
      });
      // Save the markers without waking the sweep again (sellers/notifications are not watched)
      if (changed) try { db().touch('sellers'); } catch { /* storage full */ }
    },
  };

  SA.services.notify = notify;

  // Run the sweep after start-up, after data changes (debounced) and every minute
  let t = 0;
  const later = () => { clearTimeout(t); t = setTimeout(() => { try { notify.sweep(); } catch (e) { console.warn(e); } }, 400); };
  setTimeout(() => {
    later();
    db().subscribe((col) => { if (['podOrders', 'sales', 'settings', '*'].includes(col)) later(); });
    setInterval(later, 60000);
  }, 800);
})(window.SA);
