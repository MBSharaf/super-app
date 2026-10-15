/**
 * Admin console: factories (#/admin/factories) — the POD factories (one today), their production line, late jobs and
 * problems (reassign, cancel, send back for a reprint), the courier (switch provider, test/live), price requests,
 * money (settle, deductions, bonuses) and the factory team.
 */
(function (SA) {
  const { esc, money, fmtNum, timeAgo, fmtDate } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const A = SA.pages.adminCommon;
  const F = () => SA.services.factory;
  const SH = () => SA.services.shipping;
  const AD = () => SA.services.admin;
  const db = () => SA.services.db;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });
  const H = 36e5;
  const ST = { active: ['badge-turf', 'Active'], paused: ['badge-gold', 'Paused by Super App'], suspended: ['badge-danger', 'Suspended'] };
  const SH_TONE = { awaiting_pickup: 'badge-blaze', picked_up: 'badge-info', out_for_delivery: 'badge-gold', delivered: 'badge-turf', refused: 'badge-danger', cancelled: 'badge-danger' };

  const jobRow = (o, { actions = true } = {}) => `<tr class="${o.late ? 'is-late' : ''}">
      <td class="small"><b class="num">${o.id}</b>${o.sample ? ' <span class="badge badge-gold">Sample</span>' : ''}<span class="xs muted" style="display:block">${esc(o.orderId)} · <span>${esc(o.customer?.city || '')}</span></span></td>
      <td class="small">${o.qty} × ${esc(o.product?.name || '')}<span class="xs muted" style="display:block"><span>${esc(o.color)}</span> · ${esc(o.size)} · “${esc(o.title)}”</span></td>
      <td>${SA.pages.factoryCommon.stageBadge(o.stage)}${o.issue && !o.issue.resolved ? `<span class="xs" style="display:block;color:var(--danger)">${icon('alert', 11)} ${esc(o.issue.type)}${o.issue.note ? ` — ${esc(o.issue.note)}` : ''}</span>` : ''}</td>
      <td class="small">${F().ACTIVE.includes(o.stage) ? SA.pages.factoryCommon.dueChip(o) : `<span class="xs muted">${timeAgo(o.updatedAt || o.placedAt)}</span>`}${o.reprints ? `<span class="xs muted" style="display:block">reprinted ${o.reprints}×</span>` : ''}</td>
      ${actions ? `<td class="r">${o.issue && !o.issue.resolved ? `<button class="btn btn-ghost btn-sm" data-resolve="${o.id}">Resolved</button>` : ''}${F().ACTIVE.includes(o.stage) && F().list().length > 1 ? `<button class="btn btn-ghost btn-sm" data-move-f="${o.id}">Move</button>` : ''}${['received', 'printing'].includes(o.status) ? `<button class="btn btn-ghost btn-sm" data-cancel="${o.id}" style="color:var(--danger)">Cancel</button>` : ''}${['shipped', 'delivered'].includes(o.status) ? `<button class="btn btn-ghost btn-sm" data-complaint="${o.id}">Reprint (complaint)</button>` : ''}</td>` : ''}
    </tr>`;
  const jobTable = (list, empty) => (list.length ? `<section class="dash-card table-card"><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Job</th><th>Item</th><th>Stage</th><th>Deadline</th><th></th></tr></thead><tbody>${list.slice(0, 80).map((o) => jobRow(o)).join('')}</tbody></table></div></section>` : `<div class="dash-card">${SA.ui.c.empty({ icon: 'check', title: empty, text: '' })}</div>`);

  SA.pages.adminFactories = A.guard({
    title: 'Factories — Super App Admin',
    render(ctx) {
      const tab = ctx.query.tab || '';
      const fs = F().list();
      const al = F().alerts();
      const all = F().jobs({});
      let section = '';
      if (!tab) {
        section = fs.map((f) => {
          const t = F().today(f.id), st = F().stats(f.id, 30), m = F().money(f.id);
          const staff = F().staff(f.id);
          return `<section class="dash-card adm-factory">
            <div class="row-between wrap" style="--gap:12px"><div><h2 class="h3">${esc(f.name)}</h2><p class="small muted">${esc(f.address)} · ${esc(f.phone)} · ${f.techniques.join(', ')}</p></div>
              <span class="row wrap" style="--gap:6px"><span class="badge ${ST[f.status][0]}">${ST[f.status][1]}</span>${f.intake ? '<span class="badge badge-turf">Taking orders</span>' : `<span class="badge badge-danger" title="${esc(f.intakeNote || '')}">New orders paused</span>`}${(f.strikes || []).length ? `<span class="badge badge-danger">${f.strikes.length} warning${f.strikes.length > 1 ? 's' : ''}</span>` : ''}</span></div>
            ${!f.intake ? `<p class="small" style="color:var(--danger);margin-top:6px">${icon('alert', 13)} <span>The factory paused new orders</span>${f.intakeNote ? `: ${esc(f.intakeNote)}` : ''}. <span>Printed products are hidden from the shop.</span></p>` : ''}
            ${(f.pausedProducts || []).length ? `<p class="small" style="margin-top:6px">${icon('info', 13)} <span>Can’t make right now (hidden from the shop):</span> ${f.pausedProducts.map((id) => `<span>${esc(db().find('podCatalog', id)?.name || id)}</span>`).join(', ')}</p>` : ''}
            <div class="kpi-grid kpi-sm" style="margin-top:14px">
              ${C.kpi({ label: 'In production', value: fmtNum(t.open), sub: `${t.inToday} came in today · capacity ${t.capacity}/day`, ic: 'package', tone: 'k-blaze' })}
              ${C.kpi({ label: 'Late now', value: fmtNum(t.late), sub: `${f.slaHours} h to pack`, ic: 'alert', tone: t.late ? 'k-danger' : '' })}
              ${C.kpi({ label: 'On time (30 days)', value: `${st.onTimePct}%`, sub: `avg ${st.avgHours} h to pack`, ic: 'clock', tone: st.onTimePct >= 95 ? 'k-turf' : 'k-danger' })}
              ${C.kpi({ label: 'Defect rate (30 days)', value: `${st.defectPct}%`, sub: `${st.qcFails} failed QC · ${st.complaints} complaints`, ic: 'shield', tone: st.defectPct > 3 ? 'k-danger' : '' })}
            </div>
            <div class="row-between wrap" style="--gap:10px;margin-top:14px"><span class="small"><span>Owed to the factory:</span> <b>${money(m.owed)}</b> · <span>${staff.filter((x) => x.active).length} people on the team</span></span>
              <span class="row wrap" style="--gap:6px">${f.status === 'active' ? `<button class="btn btn-ghost btn-sm" data-status="${f.id}" data-to="paused">Pause</button><button class="btn btn-ghost btn-sm" data-status="${f.id}" data-to="suspended" style="color:var(--danger)">Suspend</button>` : `<button class="btn btn-primary btn-sm" data-status="${f.id}" data-to="active">Make active</button>`}<button class="btn btn-ghost btn-sm" data-strike="${f.id}">Send a warning</button></span></div>
          </section>`;
        }).join('') + `<section class="dash-card"><h2 class="h4">How it works</h2><p class="small muted">Every printed item goes to a factory as a job with a deadline. Inside the factory it is printed (DTF), checked and packed — and when a whole order is packed, the courier is booked automatically. The factory owns its materials and stock; Super App watches deadlines, quality, prices and payments.</p></section>`;
      } else if (tab === 'jobs') {
        const f = ctx.query.f || 'attention';
        const lists = { attention: all.filter((o) => o.late || (o.issue && !o.issue.resolved)), line: all.filter((o) => F().ACTIVE.includes(o.stage)), ready: all.filter((o) => o.stage === 'ready'), done: all.filter((o) => ['shipped', 'delivered', 'refused', 'cancelled'].includes(o.stage)) };
        section = `<div class="chips">${[['attention', 'Late & problems'], ['line', 'On the line'], ['ready', 'Packed, waiting for courier'], ['done', 'Shipped & done']].map(([k, l]) => `<a class="chip" href="#/admin/factories?tab=jobs&f=${k}" aria-pressed="${f === k}">${l} <span class="chip-count">${lists[k].length}</span></a>`).join('')}</div>
          ${jobTable(lists[f], f === 'attention' ? 'Nothing late and no open problems' : 'Nothing here')}`;
      } else if (tab === 'shipping') {
        const cur = SH().cfg();
        const list = SH().list();
        section = `<section class="dash-card"><h2 class="h4">Courier</h2><p class="small muted">Super App’s courier contract. Parcels are booked automatically when a factory packs an order. Switching only affects new parcels; the ones already booked stay with their courier.</p>
            <form class="row wrap" data-courier style="--gap:10px;margin-top:12px;align-items:flex-end">
              <label class="field" style="margin:0"><span class="xs muted">Courier</span><select class="select" name="provider">${Object.values(SH().PROVIDERS).map((p) => `<option value="${p.key}" ${cur.provider === p.key ? 'selected' : ''}>${esc(p.name)}${p.note ? ` — ${esc(p.note)}` : ''}</option>`).join('')}</select></label>
              <label class="field" style="margin:0"><span class="xs muted">Mode</span><select class="select" name="mode"><option value="test" ${cur.mode === 'test' ? 'selected' : ''}>Test</option><option value="live" ${cur.mode === 'live' ? 'selected' : ''}>Live</option></select></label>
              <button class="btn btn-primary btn-sm" type="submit">Save</button></form>
            <p class="xs muted" style="margin-top:8px">${icon('info', 12)} Demo: there is no courier connection yet. With the backend, each courier is one adapter (create parcel, pickup, tracking updates) behind the same switch.</p></section>
          <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Parcels</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Airway bill</th><th>Order</th><th>Customer</th><th class="r">Collect</th><th>Status</th><th></th></tr></thead><tbody>
            ${list.slice(0, 60).map((s) => `<tr><td class="small"><b class="num">${esc(s.awb)}</b><span class="xs muted" style="display:block">${esc(s.providerName)}${s.mode === 'test' ? ' · <span>Test</span>' : ''} · <span>${timeAgo(s.createdAt)}</span></span></td><td class="small num">${esc(s.orderId)}<span class="xs muted" style="display:block">${s.items.length} item${s.items.length > 1 ? 's' : ''}</span></td><td class="small">${esc(s.customer?.name || '')}<span class="xs muted" style="display:block">${esc(s.customer?.city || '')}</span></td>
              <td class="r num">${s.cod ? money(s.cod) : '—'}</td><td><span class="badge ${SH_TONE[s.status]}">${SH().STATUS_LABEL[s.status]}</span></td><td class="r">${['delivered', 'refused', 'cancelled'].includes(s.status) ? '' : `<button class="btn btn-ghost btn-sm" data-courier-next="${s.id}" title="Demo">Courier update</button>`}</td></tr>`).join('') || '<tr><td colspan="6" class="small muted">No parcels yet.</td></tr>'}
          </tbody></table></div></section>`;
      } else if (tab === 'prices') {
        const reqs = F().priceRequests(null).sort((a, b) => b.createdAt - a.createdAt);
        section = `<section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Price change requests</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Product</th><th>Factory</th><th class="r">Now</th><th class="r">Asked</th><th>Why</th><th>Status</th><th></th></tr></thead><tbody>
            ${reqs.map((r) => { const cat = db().find('podCatalog', r.catalogId); return `<tr><td class="small"><b>${esc(cat?.name || r.catalogId)}</b><span class="xs muted" style="display:block"><span>Shop base price</span> ${money(cat?.base || 0)}</span></td><td class="small">${esc(F().get(r.factoryId)?.name || '')}<span class="xs muted" style="display:block">${esc(r.by)} · ${timeAgo(r.createdAt)}</span></td><td class="r num">${money(r.from)}</td><td class="r num"><b>${money(r.to)}</b><span class="xs muted" style="display:block">${r.to > r.from ? '+' : ''}${Math.round(((r.to - r.from) / r.from) * 100)}%</span></td><td class="xs">${esc(r.note || '—')}</td>
              <td><span class="badge ${r.status === 'pending' ? 'badge-gold' : r.status === 'approved' ? 'badge-turf' : ''}">${r.status === 'pending' ? 'Waiting' : r.status === 'approved' ? 'Approved' : r.status === 'replaced' ? 'Replaced' : 'Rejected'}</span></td>
              <td class="r">${r.status === 'pending' ? `<button class="btn btn-primary btn-sm" data-price-ok="${r.id}">Approve</button><button class="btn btn-ghost btn-sm" data-price-no="${r.id}">Reject</button>` : ''}</td></tr>`; }).join('') || '<tr><td colspan="7" class="small muted">No requests.</td></tr>'}
          </tbody></table></div></section>
          <section class="dash-card"><h2 class="h4">Current prices per piece</h2><p class="xs muted">What Super App pays the factory. The shop base price and sellers’ profit are set in the <a class="link" href="#/admin/catalog">print catalogue</a>.</p>
            <ul class="fac-list">${SA.services.pod.catalog({ all: true }).map((cat) => `<li class="row-between"><span class="small">${esc(cat.name)}</span><span class="small num">${money(cat.partnerCost)} <span class="xs muted">· <span>Margin</span> ${money(cat.base - cat.partnerCost)}</span></span></li>`).join('')}</ul></section>`;
      } else if (tab === 'money') {
        section = fs.map((f) => {
          const m = F().money(f.id);
          return `<section class="dash-card"><div class="row-between wrap" style="--gap:10px"><div><h2 class="h4">${esc(f.name)}</h2><p class="small"><span>Owed now:</span> <b class="num">${money(m.owed)}</b></p><p class="xs muted"><span>Earned</span> ${money(m.earned)} · <span>Deductions</span> ${money(m.deductions)} · <span>Bonuses</span> ${money(m.bonuses)} · <span>Paid</span> ${money(m.paid)}</p></div>
              <span class="row wrap" style="--gap:6px"><button class="btn btn-primary btn-sm" data-settle ${m.owed > 0 ? '' : 'disabled'}>${icon('wallet', 14)} Pay ${money(m.owed)}</button><button class="btn btn-ghost btn-sm" data-ledger="${f.id}" data-type="deduction">Deduction</button><button class="btn btn-ghost btn-sm" data-ledger="${f.id}" data-type="bonus">Bonus</button></span></div>
            <div class="table-wrap" style="margin-top:12px"><table class="table dash-table"><thead><tr><th>Date</th><th>Entry</th><th class="r">Amount</th></tr></thead><tbody>
              ${[...m.ledger.map((l) => ({ at: l.createdAt, text: `${l.type === 'deduction' ? 'Deduction' : 'Bonus'} — ${l.reason}`, amt: l.type === 'deduction' ? -l.amount : l.amount })), ...m.payouts.map((p) => ({ at: p.paidAt || p.createdAt, text: `Paid · ${p.ref || p.method}`, amt: -p.amount }))].sort((a, b) => b.at - a.at).slice(0, 30).map((r) => `<tr><td class="xs muted">${fmtDate(new Date(r.at))}</td><td class="small">${esc(r.text)}</td><td class="r num">${r.amt < 0 ? '−' : '+'}${money(Math.abs(r.amt))}</td></tr>`).join('') || '<tr><td colspan="3" class="small muted">No payments, deductions or bonuses yet.</td></tr>'}
            </tbody></table></div></section>`;
        }).join('');
      } else if (tab === 'team') {
        section = fs.map((f) => `<section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0"><span>Team</span> · ${esc(f.name)}</h2><p class="xs muted" style="padding:2px 18px 0">The factory owner manages the team and PINs. Read-only here.</p><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Name</th><th>Role</th><th>Status</th><th>Last sign-in</th></tr></thead><tbody>
            ${F().staff(f.id).map((x) => `<tr class="${x.active ? '' : 'is-dim'}"><td class="small"><b>${esc(x.name)}</b></td><td><span class="badge ${SA.pages.factoryCommon.ROLE_TONE[x.role]}">${F().ROLES[x.role].label}</span></td><td>${x.active ? '<span class="badge badge-turf">On</span>' : '<span class="badge">Off</span>'}</td><td class="xs muted">${x.lastSeen ? timeAgo(x.lastSeen) : '—'}</td></tr>`).join('')}
          </tbody></table></div></section>`).join('');
      }
      const body = `${A.tabs('/admin/factories', [['', 'Factories', fs.length], ['jobs', 'Production', al.late + al.issues || null], ['shipping', 'Courier & parcels'], ['prices', 'Prices', al.prices || null], ['money', 'Money'], ['team', 'Factory teams']], tab)}${section}`;
      return C.layout('factories', { title: 'Factories', subtitle: `<span>Printing, packing and shipping of print-on-demand orders</span> · <span>Courier:</span> ${esc(SH().current().name)}${SH().cfg().mode === 'test' ? ' · <span>Test</span>' : ''}`, actions: '<a class="btn btn-outline btn-sm" href="#/factory">Open the factory system</a>', body });
    },
    mount(el) {
      el.querySelector('[data-courier]')?.addEventListener('submit', (e) => { e.preventDefault(); try { SH().setProvider(e.target.provider.value, e.target.mode.value); toast('Courier saved'); SA.app.rerender(); } catch (err) { toast(err.message, 'error'); } });
      el.addEventListener('click', async (e) => {
        const t = (s) => e.target.closest(s);
        try {
          if (t('[data-status]')) {
            const b = t('[data-status]'), to = b.dataset.to;
            if (to === 'active') { F().setStatus(b.dataset.status, 'active'); toast('Factory is active'); return SA.app.rerender(); }
            const r = await A.reason({ title: to === 'suspended' ? 'Suspend this factory?' : 'Pause this factory?', text: to === 'suspended' ? 'Nobody at the factory can sign in. Printed products leave the shop until another factory takes them.' : 'The factory stops receiving new jobs. Printed products leave the shop until it is active again.', label: 'Reason the factory sees', confirmLabel: to === 'suspended' ? 'Suspend' : 'Pause', danger: true });
            if (r) { F().setStatus(b.dataset.status, to, r.reason); toast(to === 'suspended' ? 'Factory suspended' : 'Factory paused'); SA.app.rerender(); }
          }
          if (t('[data-strike]')) { const r = await A.reason({ title: 'Send the factory a warning', label: 'Warning', placeholder: 'e.g. 12 late parcels this week — the deadline is 48 hours', confirmLabel: 'Send warning', danger: true }); if (r) { F().addStrike(t('[data-strike]').dataset.strike, r.reason); toast('Warning sent'); SA.app.rerender(); } }
          if (t('[data-resolve]')) { const r = await A.reason({ title: 'Mark the problem resolved', label: 'What was decided?', required: false, confirmLabel: 'Resolved' }); if (r) { F().resolveIssue(t('[data-resolve]').dataset.resolve, r.reason); toast('Resolved'); SA.app.rerender(); } }
          if (t('[data-cancel]')) { const r = await A.reason({ title: 'Cancel this item?', text: 'The customer is refunded if they paid online; the factory is told to stop.', label: 'Reason', confirmLabel: 'Cancel item', danger: true }); if (r) { await AD().cancelPrint(t('[data-cancel]').dataset.cancel, r.reason); toast('Item cancelled'); SA.app.rerender(); } }
          if (t('[data-complaint]')) {
            const id = t('[data-complaint]').dataset.complaint; const o = db().find('podOrders', id);
            const r = await A.reason({ title: `Send ${id} back for a reprint`, text: 'For a quality complaint (faded print, wrong placement, damage). The factory reprints at its own cost and ships again.', label: 'What is wrong?', confirmLabel: 'Send for reprint', danger: true,
              extra: `<div class="field"><label for="cp-d">Deduct from the factory (EGP, optional)</label><input class="input" id="cp-d" name="deduct" type="number" min="0" value="0" /><p class="xs muted">Its price for this item is ${money(o.partnerCost * o.qty)}.</p></div>` });
            if (r) { F().complaintReprint(id, r.reason, { deduct: +r.deduct || 0 }); toast('Sent back for a reprint'); SA.app.rerender(); }
          }
          if (t('[data-move-f]')) { const id = t('[data-move-f]').dataset.moveF; const other = F().list().find((f) => f.id !== db().find('podOrders', id).factoryId); if (other) { F().reassign(id, other.id); toast(`Moved to ${other.name}`); SA.app.rerender(); } }
          if (t('[data-courier-next]')) { SH().next(t('[data-courier-next]').dataset.courierNext); toast('Courier update received'); SA.app.rerender(); }
          if (t('[data-price-ok]')) { F().decidePrice(t('[data-price-ok]').dataset.priceOk, true); toast('Price approved — new jobs use it'); SA.app.rerender(); }
          if (t('[data-price-no]')) { const r = await A.reason({ title: 'Reject this price?', label: 'Reason the factory sees', confirmLabel: 'Reject', danger: true }); if (r) { F().decidePrice(t('[data-price-no]').dataset.priceNo, false, r.reason); toast('Rejected'); SA.app.rerender(); } }
          if (t('[data-settle]')) { const m = F().money(F().list()[0].id); if (await SA.ui.overlay.confirm({ title: `Pay ${money(m.owed)} to ${esc(m.factory.name)}?`, text: 'Recorded as a bank transfer. The factory sees it in its statement.', confirmLabel: 'Pay' })) { AD().settlePrinter({ amount: m.owed }); toast('Payment recorded'); SA.app.rerender(); } }
          if (t('[data-ledger]')) {
            const b = t('[data-ledger]'), type = b.dataset.type;
            const r = await A.reason({ title: type === 'deduction' ? 'Deduct from the factory' : 'Add a bonus', label: 'Reason the factory sees', confirmLabel: type === 'deduction' ? 'Deduct' : 'Add bonus', danger: type === 'deduction',
              extra: '<div class="field"><label for="lg-a">Amount (EGP)</label><input class="input" id="lg-a" name="amount" type="number" min="1" /></div>' });
            if (r) { F().addLedger(b.dataset.ledger, { type, amount: r.amount, reason: r.reason }); toast(type === 'deduction' ? 'Deduction recorded' : 'Bonus recorded'); SA.app.rerender(); }
          }
        } catch (err) { toast(err.message, 'error'); }
      });
    },
  });
})(window.SA);
