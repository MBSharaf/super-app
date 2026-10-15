/**
 * Simulated payments. No card data leaves the browser and only masked cards are stored.
 * Test behaviour: any valid Visa number succeeds; a number ending in 0002 is declined by the "bank".
 * Replace `charge` with a payment-provider SDK (Paymob / Fawry / Stripe) later.
 */
(function (SA) {
  const store = SA.core.store;
  const { request, ApiError } = SA.services.http;
  const { uid } = SA.core.utils;

  const luhn = (num) => {
    let s = 0, dbl = false;
    for (let i = num.length - 1; i >= 0; i--) { let d = +num[i]; if (dbl) { d *= 2; if (d > 9) d -= 9; } s += d; dbl = !dbl; }
    return s % 10 === 0;
  };

  /** Returns { field: message } for invalid card fields (empty object when valid). */
  const validateCard = ({ number = '', name = '', exp = '', cvc = '' }) => {
    const errors = {};
    const n = number.replace(/\s+/g, '');
    if (!/^4\d{15}$/.test(n)) errors.number = 'Enter a 16-digit Visa number starting with 4.';
    else if (!luhn(n)) errors.number = 'This card number isn’t valid. Check the digits.';
    if (name.trim().length < 3) errors.name = 'Enter the name as it appears on the card.';
    const m = exp.match(/^(\d{2})\s*\/\s*(\d{2})$/);
    if (!m || +m[1] < 1 || +m[1] > 12) errors.exp = 'Use MM/YY.';
    else { const end = new Date(2000 + +m[2], +m[1], 0, 23, 59); if (end < new Date()) errors.exp = 'This card has expired.'; }
    if (!/^\d{3}$/.test(cvc)) errors.cvc = '3 digits on the back.';
    return errors;
  };

  const addTx = (tx) => store.set((s) => ({ wallet: { ...s.wallet, transactions: [{ id: uid('TX'), at: Date.now(), ...tx }, ...s.wallet.transactions] } }));
  /** Every money movement gets a receipt (PDF from Profile → Payments & receipts, and from the toast right after paying) */
  const record = (r) => { const rec = { id: r.id || uid('RC'), at: Date.now(), ...r }; store.set((s) => ({ payments: [rec, ...(s.payments || [])].slice(0, 500) }), 'payment:new'); return rec; };

  const paymentService = {
    validateCard, record,
    receipts: () => store.get().payments || [],
    walletBalance: () => store.get().wallet?.balance ?? 0,
    savedCards: () => store.get().savedCards,
    /**
     * @param {{amount:number, method:'wallet'|'card', card?:object, savedCardId?:string, saveCard?:boolean, label:string}} p
     */
    charge: (p) => request(() => {
      let amount = Math.round(p.amount);
      // A Super Points voucher pays first; the rest goes to the chosen method
      let fromVoucher = 0;
      if (p.voucher && SA.services.loyalty) {
        // Only spent once the rest of the payment goes through
        fromVoucher = Math.min(SA.services.loyalty.usable().find((v) => v.code === p.voucher)?.balance || 0, amount);
        amount -= fromVoucher;
        if (amount <= 0) {
          SA.services.loyalty.spend(p.voucher, fromVoucher, p.label);
          const out = { id: uid('PAY'), method: 'voucher', label: `Voucher ${p.voucher}`, amount: fromVoucher, status: 'paid' };
          record({ id: out.id, kind: 'payment', label: p.label, amount: fromVoucher, method: out.label });
          return out;
        }
      }
      const via = (lbl) => { if (fromVoucher) SA.services.loyalty.spend(p.voucher, fromVoucher, p.label); return fromVoucher ? `Voucher ${p.voucher} (${fromVoucher} EGP) + ${lbl}` : lbl; };
      const total = amount + fromVoucher;
      if (p.method === 'wallet') {
        const bal = store.get().wallet.balance;
        if (bal < amount) throw new ApiError(`Your wallet has ${bal.toLocaleString()} EGP. Top up ${(amount - bal).toLocaleString()} EGP or pay by card.`, 'insufficient_funds');
        store.set((s) => ({ wallet: { ...s.wallet, balance: s.wallet.balance - amount } }));
        addTx({ type: 'debit', label: p.label, amount });
        const out = { id: uid('PAY'), method: 'wallet', label: via('Super App Wallet'), amount: total, status: 'paid' };
        record({ id: out.id, kind: 'payment', label: p.label, amount: total, method: out.label });
        return out;
      }
      let last4, brand = 'Visa';
      if (p.savedCardId) {
        const c = store.get().savedCards.find((x) => x.id === p.savedCardId);
        if (!c) throw new ApiError('That saved card is no longer available.', 'card_missing');
        last4 = c.last4;
      } else {
        const errors = validateCard(p.card || {});
        if (Object.keys(errors).length) throw new ApiError('Check your card details.', 'invalid_card', errors);
        const n = p.card.number.replace(/\s+/g, '');
        last4 = n.slice(-4);
        if (n.endsWith('0002')) throw new ApiError('Your bank declined this payment. Try another card or pay with your wallet.', 'declined');
        if (p.saveCard && !store.get().savedCards.some((c) => c.last4 === last4)) {
          store.set((s) => ({ savedCards: [...s.savedCards, { id: uid('card'), brand, last4, exp: p.card.exp, holder: p.card.name.toUpperCase() }] }));
        }
      }
      const out = { id: uid('PAY'), method: 'card', label: via(`${brand} •••• ${last4}`), amount: total, status: 'paid' };
      record({ id: out.id, kind: 'payment', label: p.label, amount: total, method: out.label });
      return out;
    }, { latency: 1400 }),

    refund: (amount, label) => request(() => {
      store.set((s) => ({ wallet: { ...s.wallet, balance: s.wallet.balance + amount } }));
      addTx({ type: 'credit', label, amount });
      record({ kind: 'refund', label, amount, method: 'Super App Wallet' });
      return { ok: true };
    }),

    topUp: (amount, cardPayload) => request(async () => {
      if (!(amount >= 50)) throw new ApiError('Minimum top-up is 50 EGP.', 'min_amount');
      if (cardPayload?.savedCardId == null) {
        const errors = validateCard(cardPayload?.card || {});
        if (Object.keys(errors).length) throw new ApiError('Check your card details.', 'invalid_card', errors);
      }
      const last4 = cardPayload.savedCardId ? store.get().savedCards.find((c) => c.id === cardPayload.savedCardId)?.last4 : cardPayload.card.number.replace(/\s+/g, '').slice(-4);
      store.set((s) => ({ wallet: { ...s.wallet, balance: s.wallet.balance + amount } }));
      addTx({ type: 'credit', label: `Wallet top-up — Visa •••• ${last4}`, amount });
      record({ kind: 'topup', label: 'Wallet top-up', amount, method: `Visa •••• ${last4}` });
      return { ok: true };
    }, { latency: 1200 }),
  };

  SA.services.payment = paymentService;
})(window.SA);
