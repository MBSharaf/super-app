/**
 * Admin console: InstaPay payments (/admin/instapay), the stop-gap until Paymob. Payers send the money to Super Malaab's
 * InstaPay account and type the transaction number. Check each number and amount in the bank app, then Confirm (the
 * booking/order is confirmed, receipt and Super Points as after a card payment) or Refuse with a reason the payer sees.
 * Server: GET /admin/instapay?status=waiting|unsent|all, POST /admin/instapay/{code}/confirm|reject (payments.confirm).
 */
(function (SA) {
  const { esc, money, timeAgo } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const A = SA.pages.adminCommon;
  const api = () => SA.services.api;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });
  const WHAT = { Booking: 'Pitch booking', Package: 'Weekly package', GameShare: 'Joining a game', ShopOrder: 'Store order', WalletTopUp: 'Wallet top-up', TournamentFee: 'Tournament entry', FantasyPass: 'Fantasy pass' };
  const ST = { Pending: ['badge-gold', 'Waiting'], Paid: ['badge-turf', 'Confirmed'], Failed: ['badge-danger', 'Refused / expired'] };

  SA.pages.adminInstaPay = A.guard({
    title: 'InstaPay payments — Super Malaab Admin',
    async render(ctx) {
      await api().ready;
      const tab = ['unsent', 'all'].includes(ctx.query.tab) ? ctx.query.tab : 'waiting';
      let list = [];
      let error = '';
      if (api().isLive()) {
        try { list = await api().get(`/admin/instapay?status=${tab}`); } catch (err) { error = err.message; }
      }
      const row = (p) => `<tr>
          <td class="small"><b class="num">${esc(p.code)}</b><span class="xs muted" style="display:block">${timeAgo(new Date(p.createdAt).getTime())}</span></td>
          <td class="small"><b>${esc(WHAT[p.purpose] || p.purpose)}</b></td>
          <td class="small"><b>${esc(p.payerName)}</b><span class="xs muted" style="display:block">${esc(p.payerPhone || p.payerEmail)}</span></td>
          <td class="r num"><b>${money(p.amount)}</b></td>
          <td class="small">${p.reference ? `<b class="num">${esc(p.reference)}</b><span class="xs muted" style="display:block">from ${esc(p.sender || '')}${p.submittedAt ? ` · ${timeAgo(new Date(p.submittedAt).getTime())}` : ''}</span>` : '<span class="muted">No number yet</span>'}</td>
          <td><span class="badge ${(ST[p.status] || ST.Pending)[0]}">${(ST[p.status] || ST.Pending)[1]}</span>${p.note && p.note !== 'confirmed' ? `<span class="xs muted" style="display:block">${esc(p.note)}</span>` : ''}</td>
          <td class="r"><div class="row" style="--gap:4px;justify-content:flex-end">${p.status === 'Pending' && p.reference ? `<button class="btn btn-primary btn-sm" data-ip-ok="${esc(p.code)}" data-amount="${p.amount}">Confirm</button><button class="btn btn-ghost btn-sm" data-ip-no="${esc(p.code)}" style="color:var(--danger)">Refuse</button>` : ''}</div></td></tr>`;
      const body = `<p class="adm-help small">${icon('info', 14)} <span>Until the card gateway is on, players pay by InstaPay to the company account and type the transaction number. Open the bank app, find a transfer with the <b>same number and the exact amount</b>, then press Confirm. Never confirm from a screenshot alone. Refusing tells the player the reason; refunds are made by hand from the bank app.</span></p>
        ${A.tabs('/admin/instapay', [['', 'To check', tab === 'waiting' ? list.length : undefined], ['unsent', 'No number yet'], ['all', 'All']], tab === 'waiting' ? '' : tab)}
        ${error ? `<div class="dash-card">${SA.ui.c.empty({ icon: 'alert', title: 'Couldn’t load InstaPay payments', text: esc(error) })}</div>`
          : list.length ? `<section class="dash-card table-card"><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Payment</th><th>For</th><th>Payer</th><th class="r">Amount</th><th>InstaPay number</th><th>Status</th><th></th></tr></thead><tbody>
          ${list.map(row).join('')}</tbody></table></div></section>`
          : `<div class="dash-card">${SA.ui.c.empty({ icon: 'check', title: 'Nothing to check', text: 'When a player sends an InstaPay transfer number, it shows up here.' })}</div>`}`;
      return C.layout('instapay', { title: 'InstaPay payments', subtitle: 'Check each transfer in the bank app, then confirm or refuse', body });
    },
    mount(el) {
      el.addEventListener('click', async (e) => {
        const t = (s) => e.target.closest(s);
        try {
          if (t('[data-ip-ok]')) {
            const b = t('[data-ip-ok]');
            const ok = await A.reason({ title: `Confirm ${b.dataset.ipOk}?`, text: `Only if you see ${money(Number(b.dataset.amount))} with this number in the bank app. The booking or order is confirmed now and can’t be undone here.`, label: 'Note (optional)', confirmLabel: 'Money received — confirm', required: false });
            if (!ok) return;
            await api().post(`/admin/instapay/${encodeURIComponent(b.dataset.ipOk)}/confirm`);
            toast(`${b.dataset.ipOk} confirmed`); SA.app.rerender(); return;
          }
          if (t('[data-ip-no]')) {
            const code = t('[data-ip-no]').dataset.ipNo;
            const r = await A.reason({ title: `Refuse ${code}?`, text: 'The player is told the transfer wasn’t found, with your reason.', label: 'Reason the player sees', confirmLabel: 'Refuse payment', danger: true });
            if (!r) return;
            await api().post(`/admin/instapay/${encodeURIComponent(code)}/reject`, { reason: r.reason });
            toast(`${code} refused`, 'info'); SA.app.rerender();
          }
        } catch (err) { toast(err.message, 'error'); }
      });
    },
  });
})(window.SA);
