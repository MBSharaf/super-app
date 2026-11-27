/**
 * InstaPay payment page (/pay/:code?next=…): the stop-gap until the card gateway (Paymob) is on.
 * The server sends the payer here instead of a card page. They send the amount to Super Malaab's InstaPay account
 * (open-app button on phones, QR code on computers), then type the transaction number. Only the Super Malaab team
 * confirms the payment, after seeing the money in the bank app (POST /admin/instapay/{code}/confirm) — then the
 * booking or order is confirmed, the receipt is made and Super Points are given, exactly as after a card payment.
 * The page polls GET /payments/{code} and moves on by itself when the payment is confirmed.
 */
(function (SA) {
  const { esc, money } = SA.core.utils;
  const { icon } = SA.ui;
  const c = SA.ui.c;
  const api = () => SA.services.api;
  let timer = null;

  const safeNext = (n) => (/^\/[A-Za-z0-9/_\-?=&.]*$/.test(n || '') && !String(n).startsWith('//') ? n : '/bookings');
  const minsLeft = (p) => Math.max(0, Math.ceil((new Date(p.instaPay.payBy).getTime() - Date.now()) / 6e4));

  const waiting = (p) => `<div class="pay-ip-state">${icon('clock', 22)}
      <div><b>We got your transfer number</b>
      <p class="small soft">Number <b class="num">${esc(p.instaPay.reference)}</b> from ${esc(p.instaPay.sender)}. Our team checks it in the bank app and confirms it — usually within minutes. Your time is kept until then, and you’ll get a notification.</p></div></div>`;

  const form = (p) => `<form class="stack" style="--gap:12px" data-ip-form>
      <h2 class="h4">2. Tell us you paid</h2>
      <label class="field"><span class="field-label">InstaPay transaction number</span>
        <input class="input" name="reference" required maxlength="40" inputmode="text" autocomplete="off" placeholder="e.g. 123456789012" /></label>
      <label class="field"><span class="field-label">Sent from (your name or phone in InstaPay)</span>
        <input class="input" name="sender" required maxlength="80" autocomplete="name" /></label>
      <button class="btn btn-primary btn-lg" type="submit">${icon('check', 18)} I sent the money</button>
      <p class="xs muted">Send the exact amount. A wrong amount or number can’t be confirmed, and the time is freed after ${p.instaPay.payWithinMinutes} minutes without a number.</p>
    </form>`;

  SA.pages.pay = {
    title: 'Pay with InstaPay — Super Malaab',
    async render(ctx) {
      await api().ready;
      const head = c.pageHead({ crumbs: [['Home', '#/'], ['Pay with InstaPay']], title: 'Pay with InstaPay' });
      let p;
      try { p = await api().get(`/payments/${encodeURIComponent(ctx.params.code)}`); }
      catch (err) {
        return `${head}<div class="container">${c.empty({ icon: 'lock', title: err.status === 401 ? 'Sign in to pay' : 'Payment not found', text: err.status === 401 ? 'Sign in with the account that made this booking.' : 'This payment link is not yours or no longer exists.', action: '<a class="btn btn-primary" href="#/auth/sign-in">Sign in</a>' })}</div>`;
      }
      const next = safeNext(ctx.query.next);
      if (p.status === 'Paid') { setTimeout(() => SA.core.router.go(next, { replace: true })); return ''; }
      if (!p.instaPay) return `${head}<div class="container">${c.empty({ icon: 'card', title: 'This payment is not by InstaPay', text: 'Go back and try again.', action: `<a class="btn btn-primary" href="#${esc(next)}">Back</a>` })}</div>`;
      const ip = p.instaPay;
      const closed = p.status !== 'Pending';
      const qr = ip.link ? SA.core.qr.svg(ip.link, { size: 220, margin: 2 }) : '';
      return `${head}<div class="container" style="max-width:640px">
        <section class="panel panel-pad stack" style="--gap:18px" data-pay="${esc(p.code)}" data-next="${esc(next)}">
          <div class="row wrap" style="justify-content:space-between;--gap:8px">
            <div><span class="xs muted">Amount to send</span><div class="display num" style="font-size:2rem">${money(p.amount)}</div></div>
            <div class="r"><span class="xs muted">Payment</span><div class="num"><b>${esc(p.code)}</b></div>${!closed && !ip.submittedAt ? `<span class="badge badge-gold">${minsLeft(p)} min left</span>` : ''}</div>
          </div>
          ${closed ? `<div class="pay-ip-state">${icon('alert', 22)}<div><b>This payment was not confirmed</b><p class="small soft">The time ran out or the transfer wasn’t found. If you already sent money, contact support with the number and we’ll sort it out.</p><a class="btn btn-outline btn-sm" href="#/support">Contact support</a></div></div>`
          : `<div class="stack" style="--gap:10px">
              <h2 class="h4">1. Send ${money(p.amount)} by InstaPay</h2>
              ${ip.link ? `<a class="btn btn-primary btn-lg" href="${esc(ip.link)}" target="_blank" rel="noopener">${icon('arrowRight', 18)} Open InstaPay and pay</a>` : ''}
              <div class="row wrap" style="--gap:16px;align-items:center">
                ${qr ? `<div class="pay-ip-qr" aria-label="InstaPay QR code">${qr}</div>` : ''}
                <div class="stack" style="--gap:6px"><span class="small soft">Or send to this InstaPay address:</span>
                  <b class="num" dir="ltr">${esc(ip.address)}</b>
                  <button class="btn btn-ghost btn-sm" type="button" data-ip-copy="${esc(ip.address)}">${icon('copy', 14)} Copy address</button>
                  <span class="xs muted">On a computer, scan the code with your phone’s InstaPay app.</span></div>
              </div>
            </div>
            ${ip.submittedAt ? waiting(p) : form(p)}`}
        </section></div>`;
    },
    mount(el) {
      const box = el.querySelector('[data-pay]');
      if (!box) return undefined;
      const { pay: code, next } = box.dataset;
      el.addEventListener('click', async (e) => {
        const b = e.target.closest('[data-ip-copy]');
        if (b) { try { await navigator.clipboard.writeText(b.dataset.ipCopy); SA.ui.overlay.toast('Address copied'); } catch { /* no clipboard */ } }
      });
      el.querySelector('[data-ip-form]')?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const f = new FormData(e.target);
        try {
          await api().post(`/payments/${encodeURIComponent(code)}/instapay`, { reference: String(f.get('reference')).trim(), sender: String(f.get('sender')).trim() });
          SA.ui.overlay.toast('Sent — we’ll confirm it as soon as we see it'); SA.app.rerender();
        } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
      });
      clearInterval(timer);
      timer = setInterval(async () => {
        try { const p = await api().get(`/payments/${encodeURIComponent(code)}`); if (p.status === 'Paid') { clearInterval(timer); SA.ui.overlay.toast('Payment confirmed'); SA.core.router.go(next, { replace: true }); } else if (p.status !== 'Pending') { clearInterval(timer); SA.app.rerender(); } } catch { /* try again */ }
      }, 15000);
      return () => clearInterval(timer);
    },
  };
})(window.SA);
