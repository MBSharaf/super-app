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

  const paymentService = {
    validateCard,
    walletBalance: () => store.get().wallet?.balance ?? 0,
    savedCards: () => store.get().savedCards,
    /**
     * @param {{amount:number, method:'wallet'|'card', card?:object, savedCardId?:string, saveCard?:boolean, label:string}} p
     */
    charge: (p) => request(() => {
      const amount = Math.round(p.amount);
      if (p.method === 'wallet') {
        const bal = store.get().wallet.balance;
        if (bal < amount) throw new ApiError(`Your wallet has ${bal.toLocaleString()} EGP. Top up ${(amount - bal).toLocaleString()} EGP or pay by card.`, 'insufficient_funds');
        store.set((s) => ({ wallet: { ...s.wallet, balance: s.wallet.balance - amount } }));
        addTx({ type: 'debit', label: p.label, amount });
        return { id: uid('PAY'), method: 'wallet', label: 'Super App Wallet', amount, status: 'paid' };
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
      return { id: uid('PAY'), method: 'card', label: `${brand} •••• ${last4}`, amount, status: 'paid' };
    }, { latency: 1400 }),

    refund: (amount, label) => request(() => {
      store.set((s) => ({ wallet: { ...s.wallet, balance: s.wallet.balance + amount } }));
      addTx({ type: 'credit', label, amount });
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
      return { ok: true };
    }, { latency: 1200 }),
  };

  SA.services.payment = paymentService;
})(window.SA);
