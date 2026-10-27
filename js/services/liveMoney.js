/**
 * Money from the real API (backend Phase 5). Loaded after the payment, loyalty, dash, admin and live* services.
 *
 * In API mode the wallet page (balance, activity, receipts as server PDFs), Super Points (balance, history, vouchers,
 * redeem), the partner dashboard's wallet (pockets, withdrawals, the server's Excel statement) and the admin
 * withdrawals list read and write the server; the pages keep their design and call the same service methods.
 * The methods the pages call synchronously answer from a small cache that `sync()` fills (on start, after each
 * money action and when a money page opens). Demo mode (GitHub Pages) is unchanged.
 *
 * Wallet top-ups go through the server (the simulated gateway pays at once; Paymob opens its page and the money arrives
 * with its callback), and "Pay from wallet" is sent with bookings, packages, tournament entries and the Fantasy Pass.
 * Not wired yet (their screens stay demo): the CEO finance-approvals queue (server: /admin/money-approvals — exposed
 * below) and the admin revenue charts.
 */
(function (SA) {
  const api = SA.services.api;
  const store = SA.core.store;
  const { ApiError } = SA.services.http;
  const S = SA.services;
  const demo = { loyalty: { ...S.loyalty }, payment: { ...S.payment }, user: { ...S.user }, dash: { ...S.dash }, admin: S.admin ? { ...S.admin } : null };

  const session = () => S.session;
  const live = () => api.isLive() && !!session()?.me();
  const cache = { wallets: [], ledger: [], receipts: [], points: null, vouchers: [], payouts: [], adminPayouts: [], at: 0 };
  const at = (iso) => (iso ? new Date(iso.endsWith('Z') ? iso : `${iso}Z`).getTime() : Date.now());
  const wallet = (type) => cache.wallets.find((w) => w.type === type) || { pending: 0, available: 0, settled: 0 };

  const KIND = {
    Refund: 'Refund', Payout: 'Withdrawal', Adjustment: 'Adjustment', ReleaseFromHold: 'Money released', Voucher: 'Voucher',
    BookingEarning: 'Booking', Deduction: 'Deduction', Commission: 'Commission', ServiceFee: 'Service fee',
    WalletTopUp: 'Wallet top-up', WalletPayment: 'Paid from wallet',
  };
  const RECEIPT = { Refund: 'refund', PayoutAdvice: 'payout', TopUp: 'topup' };
  const RECEIPT_LABEL = { Refund: 'Refund', PayoutAdvice: 'Withdrawal', TopUp: 'Wallet top-up' };
  const PAYOUT = { Requested: 'processing', Prepared: 'processing', WaitingForApproval: 'processing', Sent: 'paid', Rejected: 'rejected', Failed: 'rejected' };

  const get = (path) => api.get(path).catch(() => null);

  /** Reads everything money the signed-in person can see; rerenders a money page when it changes */
  const sync = async () => {
    await api.ready;
    patchDocs();
    if (!live()) return;
    const [wallets, ledger, receipts, points, vouchers, payouts] = await Promise.all([
      get('/wallet'), get('/wallet/ledger?type=Customer&pageSize=50'), get('/receipts'), get('/points'), get('/vouchers/mine'), get('/wallet/payouts'),
    ]);
    Object.assign(cache, {
      wallets: wallets || [], ledger: ledger?.items || ledger || [], receipts: receipts || [], points, vouchers: vouchers || [], payouts: payouts || [], at: Date.now(),
    });
    if (S.admin && session().can('payouts.read')) cache.adminPayouts = (await get('/admin/payouts?pageSize=100'))?.items || [];
    const route = location.hash;
    if (/#\/(wallet|profile|dash\/wallet|admin\/payouts)/.test(route)) SA.app?.rerender?.();
  };

  // ---- Customer wallet and receipts
  S.user.wallet = () => (!live() ? demo.user.wallet() : {
    balance: wallet('Customer').available,
    transactions: cache.ledger.map((e) => ({ id: `L${e.id}`, type: e.amount >= 0 ? 'credit' : 'debit', label: KIND[e.kind] || e.kind, amount: Math.abs(e.amount), at: at(e.createdAt) })),
  });

  S.payment.receipts = () => (!live() ? demo.payment.receipts() : cache.receipts.map((r) => ({
    id: r.number, kind: RECEIPT[r.kind] || 'payment', amount: r.amount, at: at(r.createdAt),
    label: `${RECEIPT_LABEL[r.kind] || 'Payment'}${r.refCode ? ` — ${r.refCode}` : ''}`, method: r.number, pdf: `/api/v1/receipts/${r.number}.pdf`,
  })));
  S.payment.walletBalance = () => (!live() ? demo.payment.walletBalance() : wallet('Customer').available);
  /** The card is entered on the gateway's page (Paymob); the simulated gateway pays at once. One key per click. */
  S.payment.topUp = async (amount, ...rest) => {
    if (!live()) return demo.payment.topUp(amount, ...rest);
    const key = `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
    const r = await api.post('/wallet/top-ups', { amount: Math.round(+amount) }, { headers: { 'Idempotency-Key': key } });
    if (r.paymentUrl) { window.location.assign(r.paymentUrl); return r; }
    await sync();
    return r;
  };

  /** The receipt button opens the server's PDF for server receipts (the docs component loads after the services) */
  const patchDocs = () => {
    const docs = SA.ui?.docs;
    if (!docs?.receipt || docs.receipt.live) return;
    const demoReceipt = docs.receipt;
    docs.receipt = Object.assign((r) => (r?.pdf ? window.open(r.pdf, '_blank', 'noopener') : demoReceipt(r)), { live: true });
  };

  // ---- Super Points and vouchers
  S.loyalty.summary = () => {
    const base = demo.loyalty.summary();
    const p = cache.points;
    if (!live() || !p) return base;
    const cfg = { ...base.cfg, earnPer: p.earnPer, pointValue: p.pointValue, minRedeem: p.minRedeem };
    const history = p.history.map((h, i) => ({ id: `ph${i}`, points: h.points, label: h.reason || h.kind, kind: h.points < 0 ? 'redeem' : h.kind === 'Booking' ? 'earn' : 'bonus', at: at(h.createdAt) }));
    return { ...base, balance: p.balance, value: p.balance * p.pointValue, history, cfg };
  };
  const liveVouchers = () => cache.vouchers.map((v) => ({ code: v.code, amount: v.amount, balance: v.status === 'Active' ? v.amount : 0, at: at(v.createdAt), uses: [] }));
  S.loyalty.vouchers = () => (!live() ? demo.loyalty.vouchers() : liveVouchers());
  S.loyalty.usable = () => (!live() ? demo.loyalty.usable() : liveVouchers().filter((v) => v.balance > 0).sort((a, b) => b.balance - a.balance));
  /** The server makes vouchers in hundreds of EGP: the points are rounded down to the nearest 100 EGP */
  S.loyalty.redeem = async (points) => {
    if (!live()) return demo.loyalty.redeem(points);
    const value = cache.points?.pointValue || 0.05;
    const amount = Math.floor((+points * value) / 100) * 100;
    if (amount < 100) throw new ApiError(`The smallest voucher is 100 EGP (${Math.round(100 / value).toLocaleString()} points).`, 'invalid');
    const v = await api.post('/points/redeem', { amount });
    await sync();
    return { points: v.amount / value, amount: v.amount, code: v.code };
  };

  // ---- Partner wallet (dashboard)
  const isLivePartner = () => live() && S.dash.auth?.current?.()?.id === S.livePartner?.ACCOUNT;
  S.dash.wallet = (opts = {}) => {
    if (!isLivePartner()) return demo.dash.wallet(opts);
    const base = (() => { try { return demo.dash.wallet(opts); } catch { return {}; } })();
    const b = wallet('PartnerBookings');
    const payouts = cache.payouts.map((p) => ({
      id: p.code, createdAt: at(p.createdAt), amount: p.amount, method: p.method === 'InstaPay' ? 'InstaPay' : 'Bank transfer', detail: p.destination,
      status: PAYOUT[p.status] || 'processing', reason: p.rejectReason, ref: p.transferRef, live: true,
    }));
    const withdrawn = payouts.filter((p) => p.status !== 'rejected').reduce((t, p) => t + p.amount, 0);
    return {
      ...base, payouts, adjustments: [], adjusted: 0, withdrawn, ledger: [],
      bookings: { available: b.available, pending: b.pending, settled: b.settled }, store: { available: 0, pending: 0, settled: 0 },
      balance: Math.max(0, Math.round(b.available)),
    };
  };
  S.dash.requestPayout = (d, ownerId) => {
    if (!isLivePartner()) return demo.dash.requestPayout(d, ownerId);
    const amount = Math.round(+d.amount);
    if (!(amount >= 500)) throw new ApiError('The minimum withdrawal is 500 EGP.', 'invalid', { amount: 'Minimum 500 EGP' });
    if (amount > wallet('PartnerBookings').available) throw new ApiError('That is more than your available balance.', 'invalid', { amount: 'More than your available balance' });
    api.post('/wallet/payouts', { walletType: 'PartnerBookings', amount }).then(sync, (err) => SA.ui.overlay.toast(err.message, { type: 'error' }));
    return { id: 'pending', amount, status: 'processing' };
  };

  // ---- Admin withdrawals list (server rows join the demo ones)
  if (S.admin) {
    const fromServer = (p) => ({
      id: p.code, live: true, serverStatus: p.status, kind: 'partner', name: p.ownerName || 'Partner', company: p.ownerName || '', account: { name: p.ownerName },
      amount: p.amount, method: p.method === 'InstaPay' ? 'InstaPay' : 'Bank transfer', detail: p.destination, createdAt: at(p.createdAt),
      status: PAYOUT[p.status] || 'processing', reason: p.rejectReason, ref: p.transferRef, paidAt: p.sentAt ? at(p.sentAt) : null,
    });
    const liveAdmin = () => live() && cache.adminPayouts.length > 0;
    S.admin.payouts = () => (!liveAdmin() ? demo.admin.payouts() : [...cache.adminPayouts.map(fromServer), ...demo.admin.payouts()]);
    S.admin.pendingPayouts = () => S.admin.payouts().filter((p) => p.status === 'processing');
    const serverRow = (id) => cache.adminPayouts.find((p) => p.code === id);
    /** Requested → prepare (over the limit it waits for a second approver); Prepared → send (by someone else) */
    S.admin.approvePayout = (id, opts = {}) => {
      const p = serverRow(id);
      if (!p) return demo.admin.approvePayout(id, opts);
      if (p.status === 'WaitingForApproval') throw new ApiError('This withdrawal is waiting for a finance approval.', 'invalid');
      const call = p.status === 'Requested'
        ? api.post(`/admin/payouts/${id}/prepare`).then((r) => SA.ui.overlay.toast(r.status === 'WaitingForApproval' ? 'Prepared — it now needs a finance approval.' : 'Prepared — another team member sends it.'))
        : api.post(`/admin/payouts/${id}/send`, { transferRef: (opts.ref || '').trim() || `TRX-${Date.now().toString(36).toUpperCase().slice(-6)}` });
      call.then(sync, (err) => SA.ui.overlay.toast(err.message, { type: 'error' }));
    };
    S.admin.rejectPayout = (id, reason) => {
      if (!serverRow(id)) return demo.admin.rejectPayout(id, reason);
      if (!reason || reason.trim().length < 3) throw new ApiError('Write the reason — the partner sees it.', 'invalid');
      api.post(`/admin/payouts/${id}/reject`, { reason: reason.trim() }).then(sync, (err) => SA.ui.overlay.toast(err.message, { type: 'error' }));
    };
    S.admin.payoutDetail = (id) => {
      if (!serverRow(id)) return demo.admin.payoutDetail(id);
      const p = fromServer(serverRow(id));
      return { p, account: p.account, history: S.admin.payouts().filter((x) => x.name === p.name && x.id !== id).slice(0, 8), money: { kind: 'partner' }, disputes: [], checks: [] };
    };
  }

  S.liveMoney = {
    sync, cache,
    /** For the CEO finance-approvals screen (end-of-backend pass) */
    approvals: () => api.get('/admin/money-approvals'),
    decide: (id, decision, note) => api.post(`/admin/money-approvals/${id}/decide`, { decision, note }).then((r) => { sync(); return r; }),
    statementUrl: (type = 'PartnerBookings') => `/api/v1/wallet/statement.xlsx?type=${type}`,
  };

  // Fill the cache when the app starts, after sign-in, and when a money page opens
  api.ready.then(sync);
  session()?.subscribe(() => sync());
  window.addEventListener('hashchange', () => { if (/#\/(wallet|profile|dash\/wallet|admin\/payouts)/.test(location.hash) && Date.now() - cache.at > 5000) sync(); });
})(window.SA);
