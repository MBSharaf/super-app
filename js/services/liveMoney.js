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
 * Withdrawals over the approval limit (/admin/money-approvals) join the CEO approvals inbox and the admin's finance
 * approvals (ids SV-…); deciding one calls the server. Not wired yet: the admin revenue charts.
 */
(function (SA) {
  const api = SA.services.api;
  const store = SA.core.store;
  const { ApiError } = SA.services.http;
  const S = SA.services;
  const demo = { loyalty: { ...S.loyalty }, payment: { ...S.payment }, user: { ...S.user }, dash: { ...S.dash }, admin: S.admin ? { ...S.admin } : null };

  const session = () => S.session;
  const live = () => api.isLive() && !!session()?.me();
  const cache = { wallets: [], ledger: [], receipts: [], points: null, vouchers: [], payouts: [], adminPayouts: [], approvals: [], at: 0 };
  const MONEY_PAGES = /^\/(wallet|profile|dash\/wallet|admin\/payouts|admin\/finance-approvals|ceo\/approvals|ceo$|ceo\?)/;
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
    await S.liveMoney_loadApprovals?.();
    if (MONEY_PAGES.test(SA.core.router.location())) SA.app?.rerender?.();
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
    /**
     * Revenue (GET /admin/revenue): the page reads synchronously, so the first call starts the load and answers with
     * an empty period; the page re-renders when the server's numbers arrive. Store and print streams stay 0 (paused).
     */
    const revenueCache = new Map();
    const fromRevenue = (d) => {
      const daily = d.daily.map((x) => {
        const r = { date: x.date, bookings: +x.bookings, fees: +x.fees, store: 0, official: 0, pod: 0, passes: +x.passes, refusals: 0, refunds: +x.refunds, promos: +x.promos, gmv: +x.gmv };
        r.income = r.bookings + r.fees + r.passes; r.cost = r.refunds + r.promos; r.net = r.income - r.cost;
        return r;
      });
      const total = daily.reduce((t, r) => { Object.keys(t).forEach((k) => (t[k] += r[k] || 0)); return t; },
        { bookings: 0, fees: 0, store: 0, official: 0, pod: 0, passes: 0, refusals: 0, refunds: 0, promos: 0, gmv: 0, income: 0, cost: 0, net: 0 });
      Object.keys(total).forEach((k) => (total[k] = Math.round(total[k])));
      return {
        from: d.from, to: d.to, daily, total, summary: { ledger: [] }, live: true,
        passes: d.passes.map((p, i) => ({ id: `FP-${i}`, plan: p.plan, amount: p.amount, at: at(p.at) })),
        owners: d.owners.map((o) => ({ a: { company: o.name }, n: o.bookings, gross: o.gross, comm: o.commission })), sellers: [],
      };
    };
    const emptyRevenue = ({ from, to }) => fromRevenue({ from, to, daily: [], owners: [], passes: [] });
    S.admin.revenue = (r) => {
      if (!live() || !session().can('revenue.read')) return demo.admin.revenue(r);
      const key = `${r.from}|${r.to}`;
      const hit = revenueCache.get(key);
      if (hit?.data && Date.now() - hit.at < 60000) return hit.data;
      if (!hit?.loading) {
        revenueCache.set(key, { ...hit, loading: true });
        api.get(`/admin/revenue?from=${r.from}&to=${r.to}`).then((d) => { revenueCache.set(key, { data: fromRevenue(d), at: Date.now() }); SA.app?.rerender?.(); },
          (err) => { revenueCache.set(key, { data: emptyRevenue(r), at: Date.now() }); SA.ui.overlay.toast(err.message, { type: 'error' }); });
      }
      return hit?.data || emptyRevenue(r);
    };

    S.admin.payoutDetail = (id) => {
      if (!serverRow(id)) return demo.admin.payoutDetail(id);
      const p = fromServer(serverRow(id));
      return { p, account: p.account, history: S.admin.payouts().filter((x) => x.name === p.name && x.id !== id).slice(0, 8), money: { kind: 'partner' }, disputes: [], checks: [] };
    };
  }

  // ---- Money approvals (CEO approvals inbox and the admin's finance approvals): server rows join the demo ones
  const CE = S.ceo;
  if (CE) {
    const demoCe = { approvals: CE.approvals, approve: CE.approve, reject: CE.reject, financeQueue: CE.financeQueue, financeApprove: CE.financeApprove, financeReject: CE.financeReject };
    const STATUS = { Waiting: 'pending', Approved: 'approved', Rejected: 'rejected', Cancelled: 'rejected' };
    const isServer = (id) => String(id).startsWith('SV-');
    const fromApproval = (a) => {
      const no = (a.decisions || []).find((d) => d.decision === 'Reject');
      return {
        id: `SV-${a.id}`, apiId: a.id, live: true, kind: 'payout', amount: a.amount, title: `${a.summary}${a.refCode && !a.summary.includes(a.refCode) ? ` (${a.refCode})` : ''}`,
        status: STATUS[a.status] || 'pending', need: 1, mine: a.mine, at: at(a.createdAt), doneAt: a.decidedAt ? at(a.decidedAt) : null,
        requestedBy: { name: a.requestedBy || 'Finance team', title: 'Finance' }, reason: no?.note || '', rejectedBy: no?.name,
        approvals: (a.decisions || []).filter((d) => d.decision === 'Approve').map((d) => ({ name: d.name, at: at(d.at), role: '' })),
        why: ['Above the withdrawal approval limit — a second person approves before it can be sent'],
      };
    };
    const canDecide = () => live() && session().can('finance.approve');
    const loadApprovals = async () => {
      if (!canDecide()) { cache.approvals = []; return; }
      cache.approvals = ((await get('/admin/money-approvals?status=all')) || []).map(fromApproval);
    };
    const serverRow = (id) => {
      const x = (cache.approvals || []).find((a) => a.id === id);
      if (!x) throw new ApiError('Request not found.', 'not_found');
      if (x.status !== 'pending') throw new ApiError('This request was already handled.', 'invalid');
      if (x.mine) throw new ApiError('You asked for this — someone else must approve it.', 'forbidden');
      return x;
    };
    const decide = async (id, decision, note) => {
      const x = serverRow(id);
      await api.post(`/admin/money-approvals/${x.apiId}/decide`, { decision, note });
      await sync();
      return { done: true };
    };
    const rejectLive = (id, reason) => {
      if (!reason || reason.trim().length < 3) throw new ApiError('Write the reason.', 'invalid');
      serverRow(id);
      decide(id, 'reject', reason.trim()).catch((err) => SA.ui.overlay.toast(err.message, { type: 'error' }));
    };
    CE.approvals = (opts = {}) => {
      const local = demoCe.approvals(opts);
      if (!canDecide()) return local;
      const rows = (cache.approvals || []).filter((x) => !opts.status || x.status === opts.status);
      return [...rows, ...local].sort((a, b) => (a.status === 'pending') === (b.status === 'pending') ? b.at - a.at : a.status === 'pending' ? -1 : 1);
    };
    CE.findApproval = (id) => CE.approvals().find((x) => x.id === id) || null;
    CE.approve = (id, opts) => (isServer(id) ? decide(id, 'approve', opts?.note) : demoCe.approve(id, opts));
    CE.reject = (id, reason) => (isServer(id) ? rejectLive(id, reason) : demoCe.reject(id, reason));
    CE.financeQueue = () => {
      const local = (() => { try { return demoCe.financeQueue(); } catch { return []; } })();
      if (!canDecide()) return local;
      return [...(cache.approvals || []).filter((x) => x.status === 'pending').map((x) => ({ ...x, state: x.mine ? 'own' : 'send' })), ...local];
    };
    CE.financeApprove = (id) => (isServer(id) ? decide(id, 'approve') : demoCe.financeApprove(id));
    CE.financeReject = (id, reason) => (isServer(id) ? rejectLive(id, reason) : demoCe.financeReject(id, reason));
    S.liveMoney_loadApprovals = loadApprovals;
  }

  S.liveMoney = {
    sync, cache,
    statementUrl: (type = 'PartnerBookings') => `/api/v1/wallet/statement.xlsx?type=${type}`,
  };

  // Fill the cache when the app starts, after sign-in, and when a money page opens
  api.ready.then(sync);
  session()?.subscribe(() => sync());
  SA.core.router.onChange(() => { if (MONEY_PAGES.test(SA.core.router.location()) && Date.now() - cache.at > 5000) sync(); });
})(window.SA);
