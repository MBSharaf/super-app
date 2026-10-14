/**
 * Payment method picker (Wallet / saved Visa / new Visa / optional cash) + processing dialog.
 * Usage: html = payment.render({amount}); payment.bind(root); const p = payment.read(root);
 *        await payment.process(() => service.call(p))
 */
(function (SA) {
  const { esc, money } = SA.core.utils;
  const { icon } = SA.ui;
  const { overlay } = SA.ui;

  const visaMark = `<svg width="38" height="24" viewBox="0 0 38 24" aria-hidden="true"><rect width="38" height="24" rx="4" fill="#1A1F71"/><path fill="#fff" d="M15.6 16.4h-2.3l1.4-8.8H17l-1.4 8.8Zm8.3-8.6a5.6 5.6 0 0 0-2-.4c-2.2 0-3.8 1.2-3.8 2.9 0 1.2 1.1 1.9 2 2.3.9.4 1.2.7 1.2 1.1 0 .6-.7.9-1.4.9-.9 0-1.4-.1-2.2-.5l-.3-.1-.3 1.9c.6.3 1.6.5 2.6.5 2.4 0 3.9-1.2 3.9-3 0-1-.6-1.8-1.9-2.4-.8-.4-1.3-.7-1.3-1.1 0-.4.4-.8 1.3-.8.7 0 1.3.1 1.7.3l.2.1.3-1.8Zm5.9-.2h-1.7c-.5 0-.9.2-1.2.7l-3.3 8.1h2.4l.5-1.3h2.9l.3 1.3h2.1l-2-8.8Zm-2.8 5.7.9-2.4.3-.8.2.7.5 2.5h-1.9ZM11.4 7.6l-2.2 6-.2-1.2c-.4-1.4-1.7-2.9-3.2-3.6l2 7.6h2.4l3.6-8.8h-2.4Z"/><path fill="#F7B600" d="M7.1 7.6H3.4l-.1.2c2.9.7 4.8 2.5 5.6 4.6l-.8-4c-.1-.6-.5-.8-1-.8Z"/></svg>`;

  const render = ({ amount, allowCash = false, allowCOD = false, name = 'pay' } = {}) => {
    const bal = SA.services.payment.walletBalance();
    const cards = SA.services.payment.savedCards();
    const short = bal < amount;
    const defaultMethod = short ? (cards[0] ? `saved:${cards[0].id}` : 'new') : 'wallet';
    const opt = (value, inner, extra = '') => `<label class="choice pay-choice" ${extra}><input type="radio" name="${name}" value="${value}" ${value === defaultMethod ? 'checked' : ''} /><span class="radio-dot"></span>${inner}</label>`;
    return `<fieldset class="pay-methods stack" style="--gap:10px" data-amount="${amount}">
      <legend class="field-label" style="margin-bottom:10px">Payment method</legend>
      ${opt('wallet', `<span class="pay-ico pay-ico-wallet">${icon('wallet', 18)}</span><span class="grow"><b>Super App Wallet</b><span class="xs ${short ? '' : 'muted'}" style="display:block;${short ? 'color:var(--danger)' : ''}">Balance ${money(bal)}${short ? ` — ${money(amount - bal)} short` : ''}</span></span>`)}
      ${cards.map((c) => opt(`saved:${c.id}`, `<span class="pay-ico">${visaMark}</span><span class="grow"><b>Visa •••• ${esc(c.last4)}</b><span class="xs muted" style="display:block">Expires ${esc(c.exp)}</span></span>`)).join('')}
      ${opt('new', `<span class="pay-ico pay-ico-new">${icon('card', 18)}</span><span class="grow"><b>New Visa card</b><span class="xs muted" style="display:block">Debit or credit</span></span>`)}
      ${allowCash ? opt('cash', `<span class="pay-ico pay-ico-new">${icon('pitch', 18)}</span><span class="grow"><b>Pay the host at the pitch</b><span class="xs muted" style="display:block">Cash on arrival</span></span>`) : ''}
      ${allowCOD ? opt('cod', `<span class="pay-ico pay-ico-new">${icon('truck', 18)}</span><span class="grow"><b>Cash on delivery</b><span class="xs muted" style="display:block">Pay the courier</span></span>`) : ''}
      <div class="card-form" ${defaultMethod === 'new' ? '' : 'hidden'}>
        <div class="form-grid">
          <div class="field span-2"><label for="${name}-num">Card number</label>
            <div class="input-icon">${icon('card', 18)}<input class="input num" id="${name}-num" name="number" inputmode="numeric" autocomplete="cc-number" placeholder="4242 4242 4242 4242" maxlength="19" /></div>
            <span class="field-error" data-err="number"></span></div>
          <div class="field span-2"><label for="${name}-name">Name on card</label><input class="input" id="${name}-name" name="name" autocomplete="cc-name" placeholder="As printed on the card" /><span class="field-error" data-err="name"></span></div>
          <div class="field"><label for="${name}-exp">Expiry</label><input class="input num" id="${name}-exp" name="exp" inputmode="numeric" autocomplete="cc-exp" placeholder="MM/YY" maxlength="5" /><span class="field-error" data-err="exp"></span></div>
          <div class="field"><label for="${name}-cvc">CVC</label><input class="input num" id="${name}-cvc" name="cvc" inputmode="numeric" autocomplete="cc-csc" placeholder="123" maxlength="3" /><span class="field-error" data-err="cvc"></span></div>
        </div>
        <label class="row small" style="margin-top:12px;--gap:8px"><input type="checkbox" name="save" checked /> Save this card for next time</label>
        <p class="xs muted" style="margin-top:10px">${icon('lock', 12)} Demo checkout — no real charge is made. Use any valid Visa test number, e.g. 4242 4242 4242 4242. Numbers ending in 0002 simulate a decline.</p>
      </div>
    </fieldset>`;
  };

  const bind = (root, { name = 'pay', onChange } = {}) => {
    const form = root.querySelector('.card-form');
    root.querySelectorAll(`input[name="${name}"]`).forEach((r) => r.addEventListener('change', () => {
      form.hidden = r.value !== 'new' || !r.checked;
      if (r.checked && r.value === 'new') form.querySelector('input')?.focus();
      onChange?.(r.value);
    }));
    const num = form.querySelector('[name="number"]');
    num.addEventListener('input', () => { num.value = num.value.replace(/\D/g, '').slice(0, 16).replace(/(\d{4})(?=\d)/g, '$1 '); });
    const exp = form.querySelector('[name="exp"]');
    exp.addEventListener('input', (e) => { let v = exp.value.replace(/\D/g, '').slice(0, 4); if (v.length >= 3) v = v.slice(0, 2) + '/' + v.slice(2); exp.value = e.inputType === 'deleteContentBackward' ? exp.value : v; });
    form.querySelector('[name="cvc"]').addEventListener('input', (e) => { e.target.value = e.target.value.replace(/\D/g, '').slice(0, 3); });
  };

  /** Returns the payload paymentService.charge expects. */
  const read = (root, { name = 'pay' } = {}) => {
    const v = root.querySelector(`input[name="${name}"]:checked`)?.value || 'wallet';
    if (v === 'wallet') return { method: 'wallet' };
    if (v === 'cash') return { method: 'cash' };
    if (v === 'cod') return { method: 'cod' };
    if (v.startsWith('saved:')) return { method: 'card', savedCardId: v.slice(6) };
    const f = root.querySelector('.card-form');
    const g = (n) => f.querySelector(`[name="${n}"]`).value;
    return { method: 'card', card: { number: g('number'), name: g('name'), exp: g('exp'), cvc: g('cvc') }, saveCard: f.querySelector('[name="save"]').checked };
  };

  const showErrors = (root, errors = {}) => {
    root.querySelectorAll('[data-err]').forEach((el) => {
      const msg = errors[el.dataset.err] || '';
      el.textContent = msg;
      el.closest('.field')?.querySelector('input')?.setAttribute('aria-invalid', msg ? 'true' : 'false');
    });
    const firstBad = root.querySelector('[aria-invalid="true"]');
    firstBad?.focus();
  };

  /**
   * Runs `task` while showing a bank-style processing dialog. Resolves with the task result or
   * rejects after showing field errors inline (payment root) or a toast.
   */
  const process = async (task, { root, label = 'Processing payment', methodHint = 'card' } = {}) => {
    const steps = methodHint === 'wallet' ? ['Checking wallet balance', 'Confirming'] : methodHint === 'cash' || methodHint === 'cod' ? ['Reserving', 'Confirming'] : ['Securing connection', 'Authorising with your bank', 'Confirming'];
    const m = overlay.open({
      label,
      body: `<div class="processing"><span class="ball-loader" aria-hidden="true"><span class="b"></span><span class="s"></span></span><h2 class="h3">${label}</h2><ol class="proc-steps">${steps.map((s, i) => `<li data-i="${i}">${s}</li>`).join('')}</ol><p class="xs muted">Don’t close this window.</p></div>`,
    });
    m.el.querySelector('.overlay-scrim').removeAttribute('data-close');
    m.el.querySelector('[data-close]')?.remove();
    const lis = [...m.el.querySelectorAll('.proc-steps li')];
    let i = 0; lis[0]?.classList.add('is-active');
    const timer = setInterval(() => { lis[i]?.classList.replace('is-active', 'is-done'); i = Math.min(i + 1, lis.length - 1); lis[i]?.classList.add('is-active'); }, 480);
    try {
      const out = await task();
      clearInterval(timer);
      m.close();
      return out;
    } catch (err) {
      clearInterval(timer);
      m.close();
      if (err.details && root) showErrors(root, err.details);
      overlay.toast(err.message || 'Something went wrong. Try again.', { type: 'error', duration: 5200 });
      throw err;
    }
  };

  SA.ui.payment = { render, bind, read, process, showErrors, visaMark };
})(window.SA);
