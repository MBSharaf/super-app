/**
 * Admin console: finance approvals — the CFO's (and the admin manager's) part of the CEO approvals queue.
 * - Withdrawals the payables agent prepared, within the CEO's limit and the role's own limit: send or reject them here.
 * - Over the two-person limit: the CFO gives the second approval here; the CEO gives theirs in the CEO system.
 * - Everything above the CEO's limits shows as "waiting for the CEO" (read-only).
 */
(function (SA) {
  const { esc, timeAgo, money } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const A = SA.pages.adminCommon;
  const CE = () => SA.services.ceo;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });
  const STATE = { send: ['badge-blaze', 'You can send it'], confirm: ['badge-danger', 'Your second approval'], done: ['badge-turf', 'You approved — waiting for the CEO'], ceo: ['badge', 'Waiting for the CEO'], own: ['badge', 'You asked — someone else approves'] };

  SA.pages.adminFinanceApprovals = A.guard({
    title: 'Finance approvals — Super App Admin',
    render() {
      const list = CE().financeQueue();
      const L = CE().limits();
      const me = SA.services.dash.me();
      const r = CE().roleOfAccount(me);
      const act = list.filter((x) => ['send', 'confirm'].includes(x.state));
      const row = (x) => `<li class="dash-card fa-req">
        <div class="row-between wrap" style="--gap:8px;align-items:flex-start"><span class="grow"><span class="xs muted">${x.id} · ${timeAgo(x.at)}</span>
          <b class="h4" style="display:block">${CE().KIND[x.kind]} · <span class="num">${money(x.amount)}</span></b>
          <span class="small" style="display:block">${esc(x.title)}</span>
          <span class="xs muted">Asked by ${esc(x.requestedBy?.name || '')} (${esc(x.requestedBy?.title || '')})</span></span>
          <span class="badge ${STATE[x.state][0]}">${STATE[x.state][1]}</span></div>
        <ul class="xs muted" style="margin-top:6px">${(x.why || []).map((w) => `<li>${icon('info', 11)} ${esc(w)}</li>`).join('')}</ul>
        ${(x.approvals || []).length ? `<p class="xs muted">${icon('check', 12)} ${x.approvals.map((p) => esc(p.name)).join(' · ')}</p>` : ''}
        ${['send', 'confirm'].includes(x.state) ? `<div class="row" style="--gap:8px;margin-top:10px"><button class="btn btn-primary btn-sm" data-fa-ok="${x.id}">${icon('check', 14)} ${x.state === 'send' ? 'Approve & send' : 'Give the second approval'}</button><button class="btn btn-ghost btn-sm" data-fa-no="${x.id}">Reject</button></div>` : ''}
      </li>`;
      const body = `
        <div class="kpi-grid">
          ${C.kpi({ label: 'Waiting for you', value: act.length, ic: 'check', tone: act.length ? 'k-blaze' : '' })}
          ${C.kpi({ label: 'Amount waiting for you', value: money(act.reduce((t, x) => t + x.amount, 0)), ic: 'wallet' })}
          ${C.kpi({ label: 'Your limit', value: r.limit === Infinity ? '∞' : money(r.limit), ic: 'shield', sub: esc(r.title) })}
          ${C.kpi({ label: 'Two approvals above', value: money(L.dual), ic: 'users', sub: 'You and the CEO' })}
        </div>
        <p class="small muted" style="margin:6px 0 12px">${icon('info', 12)} Separation of duties: whoever prepared a request can’t approve it. Above ${money(L.payout)} (withdrawals) or ${money(L.refund)} (refunds) only the CEO decides; above ${money(L.dual)} the CFO and the CEO both approve.</p>
        ${list.length ? `<ul class="stack" style="--gap:12px">${list.map(row).join('')}</ul>` : `<div class="dash-card">${SA.ui.c.empty({ icon: 'check', title: 'Nothing waiting', text: 'Prepared withdrawals and requests that need your approval show up here.' })}</div>`}`;
      return C.layout('finance-approvals', { title: 'Finance approvals', subtitle: 'Prepared withdrawals and second approvals', body });
    },
    mount(el) {
      el.querySelectorAll('[data-fa-ok]').forEach((b) => b.addEventListener('click', async () => {
        const x = SA.services.db.find('ceoApprovals', b.dataset.faOk);
        if (!(await SA.ui.overlay.confirm({ title: `Approve ${money(x.amount)}?`, text: esc(x.title), confirmLabel: 'Approve' }))) return;
        try { const r = await CE().financeApprove(x.id); toast(r.waiting ? 'Approved — waiting for the CEO' : 'Approved — done'); }
        catch (e) { toast(e.message, 'error'); }
        SA.app.rerender();
      }));
      el.querySelectorAll('[data-fa-no]').forEach((b) => b.addEventListener('click', async () => {
        const r = await A.reason({ title: 'Reject this request?', text: 'Whoever asked and the CEO are told, with your reason.', confirmLabel: 'Reject', danger: true });
        if (!r) return;
        try { CE().financeReject(b.dataset.faNo, r.reason); toast('Rejected'); } catch (e) { toast(e.message, 'error'); }
        SA.app.rerender();
      }));
    },
  });
})(window.SA);
