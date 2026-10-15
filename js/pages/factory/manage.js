/**
 * Factory system — shipping, team, money, reports, settings and the activity log.
 */
(function (SA) {
  const { esc, money, fmtNum, timeAgo, fmtDate } = SA.core.utils;
  const { icon } = SA.ui;
  const c = SA.ui.c;
  const F = () => SA.services.factory;
  const SH = () => SA.services.shipping;
  const FC = () => SA.pages.factoryCommon;
  const K = () => SA.pages.dashCommon;
  const db = () => SA.services.db;
  const DAY = 864e5;
  const toast = (m, type) => SA.ui.overlay.toast(m, type ? { type } : undefined);
  const run = (fn, msg) => { try { fn(); if (msg) toast(msg); SA.app.rerender(); return true; } catch (err) { toast(err.message, 'error'); return false; } };
  const SH_TONE = { awaiting_pickup: 'badge-blaze', picked_up: 'badge-info', out_for_delivery: 'badge-gold', delivered: 'badge-turf', refused: 'badge-danger', cancelled: 'badge-danger' };

  /* ---------- Shipping ---------- */
  const shipCard = (sh) => {
    const items = sh.items.map((id) => db().find('podOrders', id)).filter(Boolean);
    const open = !['delivered', 'refused', 'cancelled'].includes(sh.status);
    return `<article class="dash-card fac-ship">
      <div class="row-between wrap" style="--gap:8px"><span><b class="num">${esc(sh.awb)}</b> <span class="xs muted">· ${esc(sh.providerName)}</span>${sh.mode === 'test' ? ' <span class="badge">Test</span>' : ''}</span><span class="badge ${SH_TONE[sh.status]}">${SH().STATUS_LABEL[sh.status]}</span></div>
      <p class="small"><b>${esc(sh.customer?.name || '')}</b> · <span>${esc(sh.customer?.city || '')}</span> · <span>Order</span> <span class="num">${esc(sh.orderId)}</span></p>
      <p class="xs muted">${items.map((o) => `${o.id} (${o.qty} × ${esc(o.color)} ${esc(o.size)})`).join(' · ')}</p>
      <p class="xs">${sh.cod ? `${icon('wallet', 12)} Courier collects <b>${money(sh.cod)}</b>` : `${icon('check', 12)} Paid online — collect nothing`}</p>
      <ol class="fac-track">${SH().FLOW.concat(sh.status === 'refused' ? ['refused'] : []).filter((x) => !(sh.status === 'refused' && x === 'delivered')).map((st) => { const ev = sh.events.find((e) => e.status === st); return `<li class="${ev ? 'is-done' : ''}"><span class="xs">${SH().STATUS_LABEL[st]}</span>${ev ? `<span class="xs muted">${timeAgo(ev.at)}</span>` : ''}</li>`; }).join('')}</ol>
      <div class="pr-actions"><button class="btn btn-outline btn-sm" data-label="${sh.id}">${icon('file', 13)} Label</button>${open ? `<button class="btn btn-ghost btn-sm" data-courier-next="${sh.id}" title="Demo: there is no real courier yet">${icon('refresh', 13)} Courier update (demo)</button>` : ''}</div>
    </article>`;
  };
  SA.pages.factoryShipping = { perm: 'ship', title: 'Shipping — Factory', render: (ctx, s) => {
    const tab = ['pickup', 'partial', 'transit', 'done'].includes(ctx.query.tab) ? ctx.query.tab : 'pickup';
    const all = SH().list({ factoryId: s.factoryId });
    const lists = {
      pickup: all.filter((x) => x.status === 'awaiting_pickup'),
      transit: all.filter((x) => ['picked_up', 'out_for_delivery'].includes(x.status)),
      done: all.filter((x) => ['delivered', 'refused', 'cancelled'].includes(x.status) && x.updatedAt > Date.now() - 30 * DAY),
    };
    // Orders where some items are packed and others are still on the line
    const byOrder = {};
    F().jobs({ factoryId: s.factoryId, stage: ['ready', ...F().ACTIVE] }).filter((o) => !o.shipmentId).forEach((o) => (byOrder[o.orderId] ||= []).push(o));
    const partial = Object.entries(byOrder).filter(([, os]) => os.some((o) => o.stage === 'ready'));
    const p = SH().current();
    const tabs = [['pickup', 'Waiting for pickup', lists.pickup.length], ['partial', 'Partly packed', partial.length], ['transit', 'On the way', lists.transit.length], ['done', 'Delivered & refused', lists.done.length]];
    const body = `
      <section class="dash-card fac-courier"><span class="fac-courier-ico">${icon('truck', 22)}</span><div class="grow"><b>${esc(p.name)}</b>${SH().cfg().mode === 'test' ? ' <span class="badge badge-gold">Test</span>' : ''}<p class="xs muted">Super App’s courier. When every item of an order is packed, the parcel is booked and a pickup is requested automatically — print the label and stick it on.</p></div></section>
      <div class="chips" role="tablist" aria-label="Shipments">${tabs.map(([k, l, n]) => `<a class="chip" role="tab" href="#/factory/shipping?tab=${k}" aria-pressed="${tab === k}">${l}${n ? ` <span class="chip-count">${n}</span>` : ''}</a>`).join('')}</div>
      ${tab === 'partial' ? (partial.length ? `<div class="pr-list">${partial.map(([oid, os]) => `<article class="dash-card"><div class="row-between"><b class="num">${esc(oid)}</b><span class="xs muted">${os.filter((o) => o.stage === 'ready').length} of ${os.length} packed</span></div><ul class="fac-list">${os.map((o) => `<li><a class="row-between" href="#/factory/job/${o.id}"><span class="small num">${o.id} · ${o.qty} × ${esc(o.color)} ${esc(o.size)}</span>${FC().stageBadge(o.stage)}</a></li>`).join('')}</ul><p class="xs muted">The courier is booked when the last item is packed.</p></article>`).join('')}</div>` : `<div class="dash-card">${c.empty({ icon: 'package', title: 'No half-packed orders', text: 'Orders with several items wait here until every item is packed.' })}</div>`)
        : lists[tab].length ? `<div class="pr-list">${lists[tab].slice(0, 60).map(shipCard).join('')}</div>` : `<div class="dash-card">${c.empty({ icon: 'truck', title: tab === 'pickup' ? 'Nothing waiting for the courier' : 'Nothing here', text: 'Packed orders appear here with their airway bill and label.' })}</div>`}`;
    return FC().layout('shipping', { title: 'Shipping', subtitle: `${lists.pickup.length} parcel${lists.pickup.length === 1 ? '' : 's'} waiting for ${esc(p.name)}`, body });
  }, mount(el) {
    el.addEventListener('click', (e) => { const n = e.target.closest('[data-courier-next]'); if (n) run(() => SH().next(n.dataset.courierNext), 'Courier update received'); });
    return FC().bindJobs(el);
  } };

  /* ---------- Team (owner) ---------- */
  const staffForm = (x) => `<form id="st-form" class="form-grid" novalidate>
      <div class="field span-2"><label for="st-n">Full name</label><input class="input" id="st-n" name="name" value="${esc(x?.name || '')}" maxlength="60" /><p class="field-error" data-err="name"></p></div>
      <div class="field"><label for="st-r">Role</label><select class="select" id="st-r" name="role">${Object.entries(F().ROLES).filter(([k]) => k !== 'owner').map(([k, r]) => `<option value="${k}" ${x?.role === k ? 'selected' : ''}>${r.label}</option>`).join('')}</select><p class="field-error" data-err="role"></p></div>
      <div class="field"><label for="st-p">${x ? 'New PIN (leave empty to keep)' : '4-digit PIN'}</label><input class="input" id="st-p" name="pin" inputmode="numeric" maxlength="4" autocomplete="off" /><p class="field-error" data-err="pin"></p></div>
      <p class="xs muted span-2">${icon('info', 12)} Each person signs in on the factory tablet with their name and PIN. Every step they take is saved with their name.</p>
    </form>`;
  const openStaff = (id) => {
    const x = id ? db().find('factoryStaff', id) : null;
    SA.ui.overlay.open({ title: x ? `Edit ${esc(x.name)}` : 'Add a team member', body: staffForm(x),
      footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-primary" type="submit" form="st-form">${x ? 'Save' : 'Add'}</button>`,
      onMount(m, close) {
        m.querySelector('#st-form').addEventListener('submit', (e) => {
          e.preventDefault();
          m.querySelectorAll('[data-err]').forEach((n) => (n.textContent = ''));
          try { F().saveStaff(SA.core.utils.formData(e.target), id); close(); toast(x ? 'Saved' : 'Added — share the PIN with them'); SA.app.rerender(); }
          catch (err) { Object.entries(err.details || {}).forEach(([k, v]) => { const n = m.querySelector(`[data-err="${k}"]`); if (n) n.textContent = v; }); if (!err.details) toast(err.message, 'error'); }
        });
      } });
  };
  SA.pages.factoryTeam = { perm: 'team', title: 'Team — Factory', render: (ctx, s) => {
    const people = F().staff(s.factoryId);
    const st = F().stats(s.factoryId, 7);
    const out = Object.fromEntries(st.people.map((p) => [p.id, p]));
    const body = `
      <section class="dash-card"><div class="row-between wrap" style="--gap:10px"><div><h2 class="h4">Roles</h2><p class="xs muted">Each role sees its own part of the line.</p></div><button class="btn btn-primary btn-sm" data-add>${icon('plus', 14)} Add a team member</button></div>
        <div class="fac-roles">${Object.entries(F().ROLES).map(([k, r]) => `<div><span class="badge ${FC().ROLE_TONE[k]}">${r.label}</span><span class="xs muted">${r.text}</span></div>`).join('')}</div></section>
      <section class="dash-card table-card"><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Name</th><th>Role</th><th class="r">Printed</th><th class="r">Checked</th><th class="r">Packed</th><th class="r">QC fails</th><th>Last sign-in</th><th></th></tr></thead><tbody>
        ${people.map((x) => { const o = out[x.id] || {}; return `<tr class="${x.active ? '' : 'is-dim'}"><td><span class="row" style="--gap:8px">${c.avatar({ name: x.name, avatar: x.avatar })}<b class="small">${esc(x.name)}</b></span></td><td><span class="badge ${FC().ROLE_TONE[x.role]}">${F().ROLES[x.role].label}</span>${x.active ? '' : ' <span class="badge">Off</span>'}</td>
          <td class="r num">${o.printed || 0}</td><td class="r num">${o.checked || 0}</td><td class="r num">${o.packed || 0}</td><td class="r num">${o.failed || 0}</td><td class="xs muted">${x.lastSeen ? timeAgo(x.lastSeen) : '—'}</td>
          <td class="r">${x.role === 'owner' ? '' : `<button class="btn btn-ghost btn-sm" data-edit="${x.id}">Edit</button><button class="btn btn-ghost btn-sm" data-active="${x.id}" data-on="${x.active ? '' : '1'}">${x.active ? 'Turn off' : 'Turn on'}</button>`}</td></tr>`; }).join('')}
      </tbody></table></div><p class="xs muted" style="padding:0 18px 16px">Output: last 7 days. “Printed” counts items sent to quality check.</p></section>`;
    return FC().layout('team', { title: 'Team', subtitle: `${people.filter((x) => x.active).length} people on ${esc(s.factory.name)}`, body });
  }, mount(el) {
    el.addEventListener('click', async (e) => {
      if (e.target.closest('[data-add]')) return openStaff(null);
      const ed = e.target.closest('[data-edit]'); if (ed) return openStaff(ed.dataset.edit);
      const ac = e.target.closest('[data-active]');
      if (ac) { const on = !!ac.dataset.on; if (on || await SA.ui.overlay.confirm({ title: 'Turn off this account?', text: 'They can’t sign in any more. Their open jobs become unassigned.', confirmLabel: 'Turn off', danger: true })) run(() => F().setStaffActive(ac.dataset.active, on), on ? 'Turned on' : 'Turned off'); }
    });
  } };

  /* ---------- Money (owner) ---------- */
  SA.pages.factoryMoney = { perm: 'money', title: 'Money — Factory', render: (ctx, s) => {
    const m = F().money(s.factoryId);
    const rows = [
      ...m.orders.slice(0, 200).map((o) => ({ at: F().stepAt(o, 'ready') || o.placedAt, text: `${o.id} · ${o.qty} × ${o.title} (${o.color}, ${o.size})`, amount: o.partnerCost * o.qty })),
      ...m.ledger.map((l) => ({ at: l.createdAt, text: `${l.type === 'deduction' ? 'Deduction' : 'Bonus'} — ${l.reason}`, amount: l.type === 'deduction' ? -l.amount : l.amount, tone: l.type })),
      ...m.payouts.map((p) => ({ at: p.paidAt || p.createdAt, text: `Payment from Super App · ${p.ref || p.method}`, amount: -p.amount, tone: 'payout' })),
    ].sort((a, b) => b.at - a.at);
    const body = `
      <div class="kpi-grid">
        ${K().kpi({ label: 'Super App owes you', value: money(m.owed), sub: 'paid every week by bank transfer', ic: 'wallet', tone: 'k-turf' })}
        ${K().kpi({ label: 'Earned (not settled yet)', value: money(m.earned), sub: `${fmtNum(m.items)} jobs packed`, ic: 'package' })}
        ${K().kpi({ label: 'Deductions', value: money(m.deductions), sub: 'quality complaints', ic: 'alert', tone: m.deductions ? 'k-danger' : '' })}
        ${K().kpi({ label: 'Paid to you', value: money(m.paid), ic: 'check' })}
      </div>
      <section class="dash-card"><h2 class="h4">How you are paid</h2><p class="small muted">You earn your agreed price for every item you pack and hand to the courier — including parcels the customer refuses (Super App covers those). Reprints after a failed quality check are on the factory. A quality complaint from a customer can be deducted. Prices: <a class="link" href="#/factory/settings">Settings → Prices</a>.</p></section>
      <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Statement</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Date</th><th>Item</th><th class="r">Amount</th></tr></thead><tbody>
        ${rows.slice(0, 120).map((r) => `<tr><td class="xs muted">${fmtDate(new Date(r.at))}</td><td class="small">${esc(r.text)}</td><td class="r num ${r.amount < 0 ? 'neg' : ''}" style="${r.tone === 'deduction' ? 'color:var(--danger)' : ''}">${r.amount < 0 ? '−' : ''}${money(Math.abs(r.amount))}</td></tr>`).join('') || '<tr><td colspan="3" class="small muted">Nothing yet.</td></tr>'}
      </tbody></table></div></section>`;
    return FC().layout('money', { title: 'Money', subtitle: 'Between your factory and Super App', actions: `<button class="btn btn-outline btn-sm" data-xls>${icon('file', 14)} Export Excel</button>`, body });
  }, mount(el, ctx, s) {
    el.querySelector('[data-xls]')?.addEventListener('click', () => {
      const m = F().money(s.factoryId);
      const rows = [
        ...m.orders.map((o) => [new Date(F().stepAt(o, 'ready') || o.placedAt).toISOString().slice(0, 10), 'Job packed', o.id, `${o.qty} × ${o.title} (${o.color}, ${o.size})`, o.product?.name || o.catalogId, o.partnerCost * o.qty]),
        ...m.ledger.map((l) => [new Date(l.createdAt).toISOString().slice(0, 10), l.type === 'deduction' ? 'Deduction' : 'Bonus', l.jobId || '', l.reason, '', l.type === 'deduction' ? -l.amount : l.amount]),
        ...m.payouts.map((p) => [new Date(p.paidAt || p.createdAt).toISOString().slice(0, 10), 'Payment from Super App', p.ref || p.id, p.method || '', '', -p.amount]),
      ];
      SA.pages.dashCommon.xlsx(`factory-money-${new Date().toISOString().slice(0, 10)}.xlsx`, SA.core.xlsx.dashboard({
        title: `${s.factory.name} — Money with Super App`, subtitle: `Owed now ${m.owed.toLocaleString()} EGP`,
        data: { name: 'Statement', columns: [{ header: 'Date', width: 12 }, { header: 'Type', width: 22 }, { header: 'Reference', width: 14 }, { header: 'Details', width: 40 }, { header: 'Product', width: 18 }, { header: 'Amount', width: 14, type: 'money' }], rows },
        kpis: [{ label: 'Balance (earned − deductions − paid)', col: 'Amount', type: 'money' }, { label: 'Earned from jobs', f: 'SUMIF', col: 'Type', match: 'Job packed', sum: 'Amount', type: 'money' }, { label: 'Deductions', f: 'SUMIF', col: 'Type', match: 'Deduction', sum: 'Amount', type: 'money' }, { label: 'Paid by Super App', f: 'SUMIF', col: 'Type', match: 'Payment from Super App', sum: 'Amount', type: 'money' }, { label: 'Jobs', f: 'COUNTIF', col: 'Type', match: 'Job packed' }],
        groups: [{ title: 'Earned by product', by: 'Product', sum: 'Amount', chart: 'bar', type: 'money' }, { title: 'By day', by: 'Date', sum: 'Amount', chart: 'line', type: 'money', limit: 62 }],
      }));
    });
  } };

  /* ---------- Reports ---------- */
  SA.pages.factoryReports = { perm: 'reports', title: 'Reports — Factory', render: (ctx, s) => {
    const days = [7, 30, 90].includes(+ctx.query.days) ? +ctx.query.days : 30;
    const st = F().stats(s.factoryId, days);
    const max = Math.max(1, ...st.daily.map((d) => d.items));
    const fails = F().jobs({ factoryId: s.factoryId }).flatMap((o) => (o.steps || []).filter((x) => ['qc-fail', 'complaint'].includes(x.stage) && x.at > Date.now() - days * DAY).map((x) => ({ ...x, job: o.id }))).sort((a, b) => b.at - a.at);
    const totalItems = st.daily.reduce((t, d) => t + d.items, 0);
    const best = st.daily.reduce((b, d) => (d.items > b.items ? d : b), st.daily[0]);
    const body = `
      <div class="chips" role="group" aria-label="Period">${[7, 30, 90].map((d) => `<a class="chip" href="#/factory/reports?days=${d}" aria-pressed="${days === d}">Last ${d} days</a>`).join('')}</div>
      <div class="kpi-grid">
        ${K().kpi({ label: 'Packed on time', value: `${st.onTimePct}%`, sub: `${st.late} late of ${st.jobs}`, ic: 'clock', tone: st.onTimePct >= 95 ? 'k-turf' : 'k-danger' })}
        ${K().kpi({ label: 'Defect rate', value: `${st.defectPct}%`, sub: `${st.qcFails} failed QC · ${st.complaints} complaints`, ic: 'shield', tone: st.defectPct > 3 ? 'k-danger' : '' })}
        ${K().kpi({ label: 'Average time to pack', value: `${st.avgHours} h`, sub: `target ${s.factory.slaHours} h`, ic: 'bolt' })}
        ${K().kpi({ label: 'Items packed', value: fmtNum(st.items), ic: 'package', tone: 'k-blaze' })}
      </div>
      <section class="dash-card"><h2 class="h4">Items packed per day</h2>
        <div class="fac-bars" role="img" aria-label="Items packed per day for the last ${st.daily.length} days: ${totalItems} in total, best day ${fmtDate(new Date(best.day))} with ${best.items}.">${st.daily.map((d) => `<span class="fac-bar" style="--h:${Math.round((d.items / max) * 100)}%" title="${fmtDate(new Date(d.day))}: ${d.items}"><i>${d.items || ''}</i></span>`).join('')}</div>
        <div class="row-between xs muted"><span>${fmtDate(new Date(st.daily[0].day))}</span><span>Today</span></div>
      </section>
      <div class="dash-grid g-1-1">
        <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">By person</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Name</th><th class="r">Printed</th><th class="r">Checked</th><th class="r">Packed</th><th class="r">QC fails</th></tr></thead><tbody>
          ${st.people.sort((a, b) => b.printed + b.checked + b.packed - (a.printed + a.checked + a.packed)).map((p) => `<tr><td class="small"><b>${esc(p.name)}</b></td><td class="r num">${p.printed}</td><td class="r num">${p.checked}</td><td class="r num">${p.packed}</td><td class="r num">${p.failed}</td></tr>`).join('') || '<tr><td colspan="5" class="small muted">No signed steps in this period.</td></tr>'}
        </tbody></table></div></section>
        <section class="dash-card"><h2 class="h4">Quality problems</h2>
          ${fails.length ? `<ul class="fac-list">${fails.slice(0, 12).map((x) => `<li><a href="#/factory/job/${x.job}"><b class="small"><span>${x.stage === 'complaint' ? 'Customer complaint' : 'Failed QC'}</span> · <span class="num">${x.job}</span></b><span class="xs muted" style="display:block">${esc(x.note || '')} · ${x.name ? `${esc(x.name)} · ` : ''}${timeAgo(x.at)}</span></a></li>`).join('')}</ul>` : c.empty({ icon: 'shield', title: 'No quality problems', text: 'Failed checks and customer complaints show up here.' })}
        </section>
      </div>`;
    return FC().layout('reports', { title: 'Reports', subtitle: `${esc(s.factory.name)} · last ${days} days`, actions: `<button class="btn btn-outline btn-sm" data-xls>${icon('file', 14)} Export Excel</button>`, body });
  }, mount(el, ctx, s) {
    el.querySelector('[data-xls]')?.addEventListener('click', () => {
      const days = [7, 30, 90].includes(+ctx.query.days) ? +ctx.query.days : 30;
      const since = Date.now() - days * 864e5;
      const jobs = F().jobs({ factoryId: s.factoryId }).filter((o) => o.placedAt >= since);
      const name = (o, st) => (o.steps || []).filter((x) => x.stage === st).pop()?.name || '';
      const hours = (o) => { const t = F().stepAt(o, 'ready'); return t ? Math.round(((t - o.placedAt) / 36e5) * 10) / 10 : ''; };
      const rows = jobs.map((o) => [o.id, new Date(o.placedAt).toISOString().slice(0, 10), o.product?.name || o.catalogId, o.color, o.size, o.qty, F().STAGE_LABEL[o.stage], hours(o), F().stepAt(o, 'ready') ? (F().stepAt(o, 'ready') <= o.dueAt ? 'On time' : 'Late') : (o.late ? 'Late' : 'In production'), o.qcFails || 0, o.complaints || 0, name(o, 'qc'), name(o, 'packing'), name(o, 'ready')]);
      SA.pages.dashCommon.xlsx(`factory-report-${days}d-${new Date().toISOString().slice(0, 10)}.xlsx`, SA.core.xlsx.dashboard({
        title: `${s.factory.name} — Production report`, subtitle: `Last ${days} days`,
        data: { name: 'Jobs', columns: [{ header: 'Job', width: 12 }, { header: 'Placed', width: 12 }, { header: 'Product', width: 18 }, { header: 'Colour', width: 14 }, { header: 'Size', width: 8 }, { header: 'Qty', width: 6, type: 'number' }, { header: 'Stage', width: 16 }, { header: 'Hours to pack', width: 13, type: 'number' }, { header: 'Deadline', width: 13 }, { header: 'QC fails', width: 9, type: 'number' }, { header: 'Complaints', width: 11, type: 'number' }, { header: 'Printed by', width: 16 }, { header: 'Checked by', width: 16 }, { header: 'Packed by', width: 16 }], rows },
        kpis: [{ label: 'Jobs', f: 'COUNT', col: 'Job' }, { label: 'Items', col: 'Qty' }, { label: 'On time', f: 'COUNTIF', col: 'Deadline', match: 'On time' }, { label: 'Late', f: 'COUNTIF', col: 'Deadline', match: 'Late' }, { label: 'QC fails', col: 'QC fails' }, { label: 'Average hours to pack', f: 'AVERAGE', col: 'Hours to pack' }],
        groups: [{ title: 'Items by product', by: 'Product', sum: 'Qty', chart: 'bar' }, { title: 'Deadline', by: 'Deadline', chart: 'pie' }, { title: 'Items packed by person', by: 'Packed by', sum: 'Qty', chart: 'bar' }, { title: 'Items by day', by: 'Placed', sum: 'Qty', chart: 'line', limit: 92 }],
      }));
    });
  } };

  /* ---------- Settings ---------- */
  SA.pages.factorySettings = { perm: ['settings', 'intake'], title: 'Settings — Factory', render: (ctx, s) => {
    const f = s.factory;
    const owner = F().can('settings', s);
    const list = F().priceList(s.factoryId);
    const body = `
      <section class="dash-card"><div class="row-between wrap" style="--gap:10px"><div><h2 class="h4">New orders</h2><p class="small muted">${f.intake ? 'Taking new orders. Pause if the line can’t keep up (machine down, holiday) — printed products are hidden from the shop until you resume.' : `<span>Paused</span>${f.intakeNote ? ` — ${esc(f.intakeNote)}` : ''}. <span>Printed products are hidden from the shop.</span>`}</p></div>
        <button class="btn ${f.intake ? 'btn-outline' : 'btn-primary'}" data-intake="${f.intake ? '' : '1'}" ${f.intake ? 'style="color:var(--danger)"' : ''}>${f.intake ? 'Pause new orders' : 'Resume new orders'}</button></div></section>
      ${owner ? `
      <section class="dash-card"><h2 class="h4">Capacity & deadline</h2>
        <form class="form-grid" data-cap novalidate style="max-width:520px">
          <div class="field"><label for="fc-cap">Items per day</label><input class="input" id="fc-cap" name="capacity" type="number" min="1" value="${f.capacity}" /><p class="field-error" data-err="capacity"></p></div>
          <div class="field"><label for="fc-sla">Hours to pack and hand over</label><input class="input" id="fc-sla" name="slaHours" type="number" min="12" max="120" value="${f.slaHours}" /><p class="field-error" data-err="slaHours"></p></div>
          <div class="span-2"><button class="btn btn-primary btn-sm" type="submit">Save</button></div>
        </form></section>
      <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Products & prices</h2><p class="xs muted" style="padding:4px 18px 0">Your price per piece (DTF, front print; a back print adds a little). Pause a product you can’t make for now — its designs leave the shop until you turn it back on. Price changes need Super App’s approval.</p>
        <div class="table-wrap"><table class="table dash-table"><thead><tr><th>Product</th><th class="r">Your price</th><th>Status</th><th></th></tr></thead><tbody>
        ${list.map((p) => { const paused = (f.pausedProducts || []).includes(p.id); return `<tr class="${paused ? 'is-dim' : ''}"><td class="small"><b>${esc(p.name)}</b><span class="xs muted" style="display:block"><span>${p.colors.length} colours</span> · ${p.sizes.join(', ')}</span></td>
          <td class="r num">${money(p.partnerCost)}${p.pending ? `<span class="xs" style="display:block;color:var(--gold-ink, #8a6100)"><span>Waiting for approval:</span> ${money(p.pending.to)}</span>` : ''}</td>
          <td>${p.status !== 'active' ? '<span class="badge">Off in catalogue</span>' : paused ? '<span class="badge badge-danger">Paused</span>' : '<span class="badge badge-turf">Making</span>'}</td>
          <td class="r"><button class="btn btn-ghost btn-sm" data-pause="${p.id}" data-on="${paused ? '' : '1'}">${paused ? 'Resume' : 'Pause'}</button><button class="btn btn-ghost btn-sm" data-price="${p.id}">Change price</button></td></tr>`; }).join('')}
        </tbody></table></div></section>
      <section class="dash-card"><h2 class="h4">Factory</h2><dl class="fac-dl"><dt>Name</dt><dd>${esc(f.name)}</dd><dt>Address</dt><dd>${esc(f.address)}</dd><dt>Phone</dt><dd>${esc(f.phone)}</dd><dt>Printing</dt><dd>${f.techniques.join(', ')}</dd></dl><p class="xs muted">To change these, contact Super App.</p></section>` : ''}`;
    return FC().layout('settings', { title: 'Settings', subtitle: esc(f.name), body });
  }, mount(el, ctx, s) {
    el.querySelector('[data-cap]')?.addEventListener('submit', (e) => {
      e.preventDefault(); el.querySelectorAll('[data-err]').forEach((n) => (n.textContent = ''));
      try { F().saveSettings(SA.core.utils.formData(e.target)); toast('Saved'); SA.app.rerender(); } catch (err) { Object.entries(err.details || {}).forEach(([k, v]) => { const n = el.querySelector(`[data-err="${k}"]`); if (n) n.textContent = v; }); toast(err.message, 'error'); }
    });
    el.addEventListener('click', async (e) => {
      const it = e.target.closest('[data-intake]');
      if (it) {
        if (it.dataset.intake) return run(() => F().setIntake(true), 'New orders resumed — printed products are back in the shop');
        const r = await SA.pages.adminCommon.reason({ title: 'Pause new orders?', text: 'Printed products are hidden from the shop until you resume. Jobs already here stay in production.', label: 'Why?', placeholder: 'e.g. DTF printer under maintenance until tomorrow 10 AM', confirmLabel: 'Pause', danger: true });
        if (r) run(() => F().setIntake(false, r.reason), 'New orders paused');
        return;
      }
      const pz = e.target.closest('[data-pause]'); if (pz) return run(() => F().setProductPaused(pz.dataset.pause, !!pz.dataset.on), pz.dataset.on ? 'Paused — its designs left the shop' : 'Resumed');
      const pr = e.target.closest('[data-price]');
      if (pr) {
        const p = db().find('podCatalog', pr.dataset.price);
        const r = await SA.pages.adminCommon.reason({ title: `Change the price · ${esc(p.name)}`, text: `Now ${p.partnerCost} EGP per piece. Super App reviews the request.`, label: 'Why? (optional)', required: false, confirmLabel: 'Send request',
          extra: `<div class="field"><label for="pr-v">New price per piece (EGP)</label><input class="input" id="pr-v" name="cost" type="number" min="1" value="${p.partnerCost}" /></div>` });
        if (r) run(() => F().requestPrice(p.id, r.cost, r.reason), 'Request sent to Super App');
      }
    });
  } };

  /* ---------- Activity ---------- */
  SA.pages.factoryActivity = { perm: 'reports', title: 'Activity — Factory', render: (ctx, s) => {
    const list = F().activity(s.factoryId).slice(0, 200);
    const body = `<section class="dash-card">${list.length ? `<ul class="fac-list">${list.map((a) => `<li><span class="small">${esc(a.text)}</span><span class="xs muted" style="display:block">${timeAgo(a.at)}${String(a.ref || '').startsWith('PO-') ? ` · <a class="link" href="#/factory/job/${esc(a.ref)}">${esc(a.ref)}</a>` : ''}</span></li>`).join('')}</ul>` : c.empty({ icon: 'clock', title: 'No activity yet', text: 'Every step on the line is recorded here with the person’s name.' })}</section>`;
    return FC().layout('activity', { title: 'Activity', subtitle: 'Every step, signed', body });
  } };

  ['factoryShipping', 'factoryTeam', 'factoryMoney', 'factoryReports', 'factorySettings', 'factoryActivity'].forEach((k) => { SA.pages[k] = FC().guard(SA.pages[k]); });
})(window.SA);
