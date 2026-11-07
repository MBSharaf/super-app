/**
 * Admin console: cancellation requests (#/admin/cancellations). Customers can't cancel a paid booking, package, order
 * or game themselves: each "Cancel" creates a request (CX-…) that lands here. Approve it (the service is cancelled with
 * the usual refund rules) or refuse it with a reason; the customer is told in the chat and by notification. Answer
 * within 2 hours. The AI agent works the same queue (its trigger is the n8n event cancellation.requested).
 */
(function (SA) {
  const { esc, money, timeAgo } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const A = SA.pages.adminCommon;
  const CX = () => SA.services.cancellations;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });
  const ST = { pending: ['badge-gold', 'Waiting'], approved: ['badge-turf', 'Approved'], rejected: ['badge-danger', 'Refused'] };
  const left = (r) => {
    const min = Math.round((r.dueAt - Date.now()) / 6e4);
    return min >= 0 ? `<span class="xs ${min < 30 ? 'text-danger' : 'muted'}">${min >= 60 ? `${Math.floor(min / 60)} h${min % 60 ? ` ${min % 60} min` : ''}` : `${min} min`} left</span>` : `<span class="badge badge-danger">Late by ${Math.abs(min) >= 60 ? `${Math.floor(Math.abs(min) / 60)} h` : `${Math.abs(min)} min`}</span>`;
  };

  SA.pages.adminCancellations = A.guard({
    title: 'Cancellation requests — Super App Admin',
    async render(ctx) {
      await CX().ready?.();
      const tab = ctx.query.tab || '';
      const all = CX().all();
      const waiting = all.filter((r) => r.status === 'pending');
      const list = tab === 'answered' ? all.filter((r) => r.status !== 'pending') : waiting;
      const late = waiting.filter((r) => r.dueAt < Date.now()).length;
      const row = (r) => `<tr class="${ctx.query.id === r.id ? 'is-hl' : ''}">
          <td class="small"><b class="num">${esc(r.id)}</b><span class="xs muted" style="display:block">${timeAgo(r.createdAt)}</span></td>
          <td class="small"><b>${esc(r.kindLabel)}</b><span class="xs muted" style="display:block">${esc(r.label)}</span>${r.reason ? `<span class="xs" style="display:block">“${esc(r.reason)}”</span>` : ''}</td>
          <td class="small"><b>${esc(r.customer?.name || '')}</b><span class="xs muted" style="display:block">${esc(r.customer?.email || r.customer?.phone || '')}</span></td>
          <td class="r num small">${money(r.paid || 0)}<span class="xs muted" style="display:block">${r.status === 'approved' ? `refunded ${money(r.refunded || 0)}` : `refund ${money(r.refund || 0)}`}</span></td>
          <td>${r.status === 'pending' ? left(r) : `<span class="badge ${ST[r.status][0]}">${ST[r.status][1]}</span>${r.note ? `<span class="xs muted" style="display:block">${esc(r.note)}</span>` : ''}`}</td>
          <td class="r"><div class="row" style="--gap:4px;justify-content:flex-end">${r.status === 'pending' ? `<button class="btn btn-primary btn-sm" data-cx-ok="${r.id}">Approve</button><button class="btn btn-ghost btn-sm" data-cx-no="${r.id}" style="color:var(--danger)">Refuse</button>` : ''}</div></td></tr>`;
      const body = `<div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'Waiting', value: waiting.length, ic: 'clock', tone: waiting.length ? 'k-blaze' : '' })}
          ${C.kpi({ label: 'Late (over 2 hours)', value: late, ic: 'alert', tone: late ? 'k-blaze' : '' })}
          ${C.kpi({ label: 'Approved', value: all.filter((r) => r.status === 'approved').length, ic: 'check', tone: 'k-turf' })}
          ${C.kpi({ label: 'Refused', value: all.filter((r) => r.status === 'rejected').length, ic: 'x' })}
        </div>
        <p class="adm-help small">${icon('info', 14)} <span>Customers don’t cancel paid services themselves: each “Cancel” sends a request here with its own number, and the chat tells them we reply within ${CX().REPLY_HOURS} hours. Approve to cancel it with the usual refund rules, or refuse with a reason. The customer is told in the chat and by notification.</span></p>
        ${A.tabs('/admin/cancellations', [['', 'Waiting', waiting.length], ['answered', 'Answered', all.length - waiting.length]], tab)}
        ${list.length ? `<section class="dash-card table-card"><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Request</th><th>What</th><th>Customer</th><th class="r">Paid</th><th>Status</th><th></th></tr></thead><tbody>
          ${list.map(row).join('')}</tbody></table></div></section>`
          : `<div class="dash-card">${SA.ui.c.empty({ icon: 'check', title: tab ? 'Nothing answered yet' : 'No requests waiting', text: 'When a customer asks to cancel a booking, a package, an order or a game, it shows up here.' })}</div>`}`;
      return C.layout('cancellations', { title: 'Cancellation requests', subtitle: 'Customers’ requests to cancel paid bookings, packages, orders and games — reply within 2 hours', body });
    },
    mount(el) {
      el.addEventListener('click', async (e) => {
        const t = (s) => e.target.closest(s);
        try {
          if (t('[data-cx-ok]')) {
            const id = t('[data-cx-ok]').dataset.cxOk;
            const r = await A.reason({ title: `Approve cancellation ${id}?`, text: 'The service is cancelled now and refunded under its usual rules.', label: 'Note for the customer (optional)', confirmLabel: 'Approve and cancel', required: false });
            if (!r) return;
            const out = await CX().approve(id, r.reason);
            toast(`Cancellation ${id} approved${out.refunded ? ` — ${money(out.refunded)} refunded` : ''}`); SA.app.rerender(); return;
          }
          if (t('[data-cx-no]')) {
            const id = t('[data-cx-no]').dataset.cxNo;
            const r = await A.reason({ title: `Refuse cancellation ${id}?`, text: 'The service stays active. The customer sees your reason in the chat.', label: 'Reason the customer sees', confirmLabel: 'Refuse request', danger: true });
            if (!r) return;
            await CX().reject(id, r.reason); toast(`Cancellation ${id} refused`, 'info'); SA.app.rerender();
          }
        } catch (err) { toast(err.message, 'error'); }
      });
    },
  });
})(window.SA);
