/** Admin console: withdrawals (approve / reject, print partner settlement, balance adjustments) and the platform revenue report. */
(function (SA) {
  const { esc, money, isoDate, addDays, today, timeAgo, fmtDate, parseISO } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const A = SA.pages.adminCommon;
  const AD = () => SA.services.admin;
  const db = () => SA.services.db;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });

  const KIND = { partner: ['badge-info', 'Pitch owner'], seller: ['badge-turf', 'Seller'], printer: ['badge-gold', 'Print partner'] };
  const who = (p) => `<span class="row" style="--gap:10px">${SA.ui.c.avatar({ name: p.name, avatar: p.account?.avatar }, 'sm')}<span><b class="small">${esc(p.company || p.name)}</b><span class="xs muted" style="display:block">${esc(p.name)} · <span class="badge ${KIND[p.kind][0]}">${KIND[p.kind][1]}</span></span></span></span>`;
  const checkBadges = (p) => { const c = AD().checks(p); return c.length ? c.map(([t, l]) => `<span class="badge badge-${t === 'danger' ? 'danger' : 'gold'}">${l}</span>`).join(' ') : `<span class="badge badge-turf">${icon('check', 12, 2.6)} Ready</span>`; };

  /* ======================= Withdrawals ======================= */
  SA.pages.adminPayouts = A.guard({
    title: 'Withdrawals — Super App Admin',
    render(ctx) {
      const tab = ctx.query.tab || '';
      const all = AD().payouts();
      const waiting = all.filter((p) => p.status === 'processing');
      const month = isoDate(new Date(today().getFullYear(), today().getMonth(), 1));
      const paidM = all.filter((p) => p.status === 'paid' && isoDate(new Date(p.paidAt || p.createdAt)) >= month);
      const pr = AD().printerOwed();
      const ready = waiting.filter((p) => !AD().checks(p).some(([t]) => t === 'danger'));
      const adj = AD().adjustments();
      const tabs = A.tabs('/admin/payouts', [['', 'Waiting', waiting.length], ['paid', 'Paid', all.filter((p) => p.status === 'paid').length], ['rejected', 'Rejected', all.filter((p) => p.status === 'rejected').length], ['print', 'Print partner'], ['adjust', 'Adjustments', adj.length]], tab);
      let section = '';
      if (!tab) {
        section = `<section class="dash-card table-card"><div class="row-between" style="padding:18px 18px 0"><h2 class="h4">Waiting for approval</h2>${ready.length > 1 ? `<button class="btn btn-primary btn-sm" data-approve-all>${icon('check', 14, 2.6)} Approve ${ready.length} ready</button>` : ''}</div>
          <div class="table-wrap"><table class="table dash-table"><thead><tr><th>Requested</th><th>Who</th><th>Send to</th><th>Checks</th><th class="r">Amount</th><th></th></tr></thead><tbody>
          ${waiting.map((p) => `<tr><td class="small nowrap">${fmtDate(new Date(p.createdAt))}<span class="xs muted" style="display:block">${timeAgo(p.createdAt)}</span></td><td>${who(p)}</td><td class="small">${esc(p.method)}<span class="xs muted" style="display:block">${esc(p.detail || '—')}</span></td><td class="adm-checks">${checkBadges(p)}</td><td class="r num strong">${money(p.amount)}</td>
            <td class="r"><div class="row" style="--gap:6px;justify-content:flex-end"><button class="btn btn-ghost btn-sm" data-reject="${p.id}">Reject</button><button class="btn btn-primary btn-sm" data-approve="${p.id}">Approve</button></div></td></tr>`).join('')
            || `<tr><td colspan="6">${SA.ui.c.empty({ icon: 'wallet', title: 'No withdrawals waiting', text: 'Requests from pitch owners and sellers show up here. Money only leaves after you approve it.' })}</td></tr>`}
          </tbody></table></div></section>`;
      } else if (tab === 'paid' || tab === 'rejected') {
        const list = all.filter((p) => p.status === tab);
        section = `<section class="dash-card table-card"><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Requested</th><th>Who</th><th>Send to</th><th>${tab === 'paid' ? 'Sent' : 'Reason'}</th><th class="r">Amount</th></tr></thead><tbody>
          ${list.map((p) => `<tr><td class="small nowrap">${fmtDate(new Date(p.createdAt))}</td><td>${who(p)}</td><td class="small">${esc(p.method)}<span class="xs muted" style="display:block">${esc(p.detail || '—')}</span></td>
            <td class="small">${tab === 'paid' ? `${p.paidAt ? fmtDate(new Date(p.paidAt)) : '—'}${p.ref ? `<span class="xs muted mono" style="display:block">${esc(p.ref)}</span>` : ''}${p.settlement ? '<span class="xs muted" style="display:block">Print settlement</span>' : ''}` : esc(p.reason || '')}</td><td class="r num strong">${money(p.amount)}</td></tr>`).join('')
            || `<tr><td colspan="5" class="small muted" style="padding:18px">Nothing here yet.</td></tr>`}
          </tbody></table></div></section>`;
      } else if (tab === 'print') {
        const recent = pr.orders.slice().sort((a, b) => b.updatedAt - a.updatedAt).slice(0, 12);
        const hist = all.filter((p) => p.kind === 'printer');
        section = `<div class="dash-grid g-1-1">
          <section class="dash-card"><h2 class="h4">${esc(pr.account?.company || 'Print partner')}</h2><p class="small soft" style="margin-top:6px">Super App pays the partner cost of every item it printed and shipped (refused deliveries included — we absorb them). Reprints for print faults are on the partner.</p>
            <dl class="kv" style="margin-top:14px"><div><dt>Printed items not settled yet</dt><dd class="num">${pr.items}</dd></div><div><dt>Partner cost earned</dt><dd class="num">${money(pr.earned)}</dd></div><div><dt>Already paid</dt><dd class="num">${money(pr.paid)}</dd></div><div class="total"><dt>Owed now</dt><dd class="num">${money(pr.owed)}</dd></div></dl>
            <button class="btn btn-primary" data-settle style="margin-top:14px" ${pr.owed ? '' : 'disabled'}>${icon('wallet', 15)} Pay ${money(pr.owed)}</button></section>
          <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Settlements</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Date</th><th>Reference</th><th class="r">Amount</th></tr></thead><tbody>
            ${hist.map((p) => `<tr><td class="small">${fmtDate(new Date(p.paidAt || p.createdAt))}</td><td class="xs mono">${esc(p.ref || '')}</td><td class="r num">${money(p.amount)}</td></tr>`).join('') || '<tr><td colspan="3" class="small muted" style="padding:18px">No settlements yet.</td></tr>'}</tbody></table></div></section>
        </div>
        <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Latest printed items</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Item</th><th>Design</th><th>Status</th><th class="r">Partner cost</th></tr></thead><tbody>
          ${recent.map((o) => `<tr><td class="small mono">${esc(o.id)}</td><td class="small">${esc(o.title)} · ${esc(o.color)} · ${esc(o.size)}</td><td><span class="badge">${esc(o.status)}</span></td><td class="r num">${money(o.partnerCost * (o.qty || 1))}</td></tr>`).join('') || '<tr><td colspan="4" class="small muted" style="padding:18px">Items printed from now on appear here.</td></tr>'}</tbody></table></div></section>`;
      } else {
        section = `<section class="dash-card table-card"><div class="row-between" style="padding:18px 18px 0"><h2 class="h4">Balance adjustments</h2><button class="btn btn-primary btn-sm" data-adjust>${icon('plus', 14, 2.6)} New adjustment</button></div>
          <p class="xs muted" style="padding:4px 18px 0">Credits and debits on a partner’s or seller’s wallet. They see the reason in their wallet.</p>
          <div class="table-wrap"><table class="table dash-table"><thead><tr><th>Date</th><th>Account</th><th>Reason</th><th>Type</th><th class="r">Amount</th></tr></thead><tbody>
          ${adj.map((x) => `<tr><td class="small nowrap">${fmtDate(new Date(x.at))}</td><td class="small"><b>${esc(x.account?.company || x.account?.name || '')}</b><span class="xs muted" style="display:block">${esc(x.account?.name || '')}</span></td><td class="small">${esc(x.reason)}</td><td><span class="badge">${esc({ manual: 'Manual', refund: 'Refund share', sample: 'Sample' }[x.kind] || x.kind)}</span></td><td class="r num strong" style="color:${x.amount > 0 ? 'var(--turf)' : 'var(--danger)'}">${x.amount > 0 ? '+' : '−'}${money(Math.abs(x.amount))}</td></tr>`).join('')
            || `<tr><td colspan="5" class="small muted" style="padding:18px">No adjustments yet.</td></tr>`}
          </tbody></table></div></section>`;
      }
      const body = `<div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'Waiting for approval', value: money(Math.round(waiting.reduce((t, p) => t + p.amount, 0))), ic: 'clock', tone: waiting.length ? 'k-blaze' : '', sub: `${waiting.length} request${waiting.length === 1 ? '' : 's'}` })}
          ${C.kpi({ label: 'Sent this month', value: money(Math.round(paidM.reduce((t, p) => t + p.amount, 0))), ic: 'check', tone: 'k-turf', sub: `${paidM.length} transfer${paidM.length === 1 ? '' : 's'}` })}
          ${C.kpi({ label: 'Owed to the print partner', value: money(pr.owed), ic: 'package', sub: `${pr.items} printed item${pr.items === 1 ? '' : 's'}` })}
          ${C.kpi({ label: 'Adjustments this month', value: money(Math.round(adj.filter((x) => isoDate(new Date(x.at)) >= month).reduce((t, x) => t + x.amount, 0))), ic: 'swap', sub: 'Credits minus debits' })}
        </div>${tabs}${section}`;
      return C.layout('payouts', { title: 'Withdrawals', subtitle: 'Approve or reject withdrawal requests, pay the print partner and correct balances', body });
    },
    mount(el) {
      const approve = (id) => new Promise((resolve) => {
        const p = AD().payouts().find((x) => x.id === id);
        SA.ui.overlay.open({
          title: `Send ${money(p.amount)}?`, subtitle: `${esc(p.company || p.name)} · ${esc(p.method)} ${esc(p.detail || '')}`,
          body: `<form class="stack" style="--gap:12px" data-ok novalidate><div class="field"><label for="po-ref">Transfer reference (optional)</label><input class="input mono" id="po-ref" name="ref" maxlength="40" placeholder="Bank or InstaPay reference" /><span class="field-hint">Leave empty and we’ll create one.</span></div><p class="field-error" data-err="form"></p>
            <div class="row" style="--gap:8px;justify-content:flex-end"><button type="button" class="btn btn-ghost" data-close>Cancel</button><button class="btn btn-primary" type="submit">${icon('check', 15, 2.6)} Mark as sent</button></div></form>`,
          onMount(m, close) {
            m.querySelector('[data-ok]').addEventListener('submit', (e) => {
              e.preventDefault();
              try { AD().approvePayout(id, { ref: e.target.ref.value }); close(); toast(`${money(p.amount)} marked as sent`); resolve(true); }
              catch (err) { m.querySelector('[data-err="form"]').textContent = err.message; }
            });
          },
          onClose() { resolve(false); },
        });
      });
      el.addEventListener('click', async (e) => {
        const ap = e.target.closest('[data-approve]'); if (ap) { if (await approve(ap.dataset.approve)) SA.app.rerender(); return; }
        const rj = e.target.closest('[data-reject]');
        if (rj) {
          const p = AD().payouts().find((x) => x.id === rj.dataset.reject);
          const r = await A.reason({ title: `Reject ${money(p.amount)}?`, text: `${esc(p.company || p.name)} sees this reason. The money goes back to their available balance.`, label: 'Reason they’ll see', placeholder: 'e.g. The account name doesn’t match the ID on file.', confirmLabel: 'Reject withdrawal', danger: true });
          if (!r) return;
          try { AD().rejectPayout(p.id, r.reason); toast('Withdrawal rejected', 'info'); SA.app.rerender(); } catch (err) { toast(err.message, 'error'); }
          return;
        }
        if (e.target.closest('[data-approve-all]')) {
          const ready = AD().pendingPayouts().filter((p) => !AD().checks(p).some(([t]) => t === 'danger'));
          const sum = ready.reduce((t, p) => t + p.amount, 0);
          if (!(await SA.ui.overlay.confirm({ title: `Approve ${ready.length} withdrawals?`, text: `${money(sum)} in total. Only requests that passed every check are included.`, confirmLabel: 'Approve all' }))) return;
          ready.forEach((p) => { try { AD().approvePayout(p.id); } catch (err) { console.warn(err); } });
          toast(`${ready.length} withdrawals approved`); SA.app.rerender(); return;
        }
        if (e.target.closest('[data-settle]')) {
          const o = AD().printerOwed();
          SA.ui.overlay.open({
            title: 'Pay the print partner', subtitle: `Owed now: ${money(o.owed)}`,
            body: `<form class="form-grid" data-pay novalidate><div class="field"><label for="st-a">Amount (EGP)</label><input class="input num" id="st-a" name="amount" type="number" min="1" max="${o.owed}" value="${o.owed}" /></div><div class="field"><label for="st-r">Transfer reference</label><input class="input mono" id="st-r" name="ref" maxlength="40" /></div><p class="field-error span-2" data-err="form"></p>
              <div class="span-2 row" style="--gap:8px;justify-content:flex-end"><button type="button" class="btn btn-ghost" data-close>Cancel</button><button class="btn btn-primary" type="submit">Record payment</button></div></form>`,
            onMount(m, close) { m.querySelector('[data-pay]').addEventListener('submit', (ev) => { ev.preventDefault(); try { AD().settlePrinter({ amount: ev.target.amount.value, ref: ev.target.ref.value }); close(); toast('Print partner paid'); SA.app.rerender(); } catch (err) { m.querySelector('[data-err="form"]').textContent = err.message; } }); },
          });
          return;
        }
        if (e.target.closest('[data-adjust]')) adjustDialog();
      });
    },
  });

  /** New balance adjustment (also used from the partner and seller pages) */
  const adjustDialog = (accountId = '') => {
    const accs = db().all('accounts').filter((a) => ['owner', 'seller', 'printer'].includes(a.role));
    SA.ui.overlay.open({
      title: 'Adjust a balance', subtitle: 'Credit or debit a partner’s or seller’s wallet. They see the reason.',
      body: `<form class="form-grid" data-adj novalidate>
        <div class="field span-2"><label for="aj-a">Account</label><select class="select" id="aj-a" name="accountId">${accs.map((a) => `<option value="${a.id}" ${a.id === accountId ? 'selected' : ''}>${esc(a.company)} · ${esc(a.name)}</option>`).join('')}</select></div>
        <div class="field"><span class="field-label">Type</span><div class="seg" role="radiogroup"><label class="seg-opt"><input type="radio" name="sign" value="1" checked /><span>Credit (+)</span></label><label class="seg-opt"><input type="radio" name="sign" value="-1" /><span>Debit (−)</span></label></div></div>
        <div class="field"><label for="aj-m">Amount (EGP)</label><input class="input num" id="aj-m" name="amount" type="number" min="1" max="100000" /></div>
        <div class="field span-2"><label for="aj-r">Reason they’ll see</label><input class="input" id="aj-r" name="reason" maxlength="160" placeholder="e.g. Goodwill credit for the delayed payout" /></div>
        <p class="field-error span-2" data-err="form"></p>
        <div class="span-2 row" style="--gap:8px;justify-content:flex-end"><button type="button" class="btn btn-ghost" data-close>Cancel</button><button class="btn btn-primary" type="submit">Save adjustment</button></div></form>`,
      onMount(m, close) {
        m.querySelector('[data-adj]').addEventListener('submit', (e) => {
          e.preventDefault();
          const f = SA.core.utils.formData(e.target);
          try { AD().adjust({ accountId: f.accountId, amount: +f.sign * Math.abs(+f.amount), reason: f.reason }); close(); toast('Balance adjusted'); SA.app.rerender(); }
          catch (err) { m.querySelector('[data-err="form"]').textContent = err.message; }
        });
      },
    });
  };
  A.adjustDialog = adjustDialog;

  /* ======================= Revenue ======================= */
  const pct = (a, b) => (b ? Math.round(((a - b) / b) * 100) : 0);
  SA.pages.adminRevenue = A.guard({
    title: 'Revenue — Super App Admin',
    render(ctx) {
      const r = C.range(ctx.query, '30');
      const len = Math.round((parseISO(r.to) - parseISO(r.from)) / 864e5) + 1;
      const prevTo = isoDate(addDays(parseISO(r.from), -1)), prevFrom = isoDate(addDays(parseISO(r.from), -len));
      const cur = AD().revenue(r), prev = AD().revenue({ from: prevFrom, to: prevTo });
      const t = cur.total, p = prev.total;
      const chg = (a, b) => { const c = pct(a, b); return b ? `<span class="adm-delta ${c >= 0 ? 'up' : 'down'}">${c >= 0 ? '▲' : '▼'} ${Math.abs(c)}%</span>` : '<span class="xs muted">—</span>'; };
      // Leaders
      const byOwner = {};
      cur.summary.ledger.forEach((b) => { const o = (byOwner[b.ownerId] ||= { gross: 0, comm: 0, n: 0 }); o.gross += b.money.gross; o.comm += b.money.commission; if (b.status !== 'cancelled') o.n++; });
      const owners = Object.entries(byOwner).map(([id, v]) => ({ a: db().find('accounts', id), ...v })).filter((x) => x.a).sort((a, b) => b.comm - a.comm).slice(0, 6);
      const bySeller = {};
      (db().all('podOrders') || []).forEach((o) => { if (o.sample || !(isoDate(new Date(o.placedAt)) >= r.from && isoDate(new Date(o.placedAt)) <= r.to) || ['refused', 'cancelled'].includes(o.status)) return; const s = (bySeller[o.sellerId] ||= { items: 0, margin: 0, sales: 0 }); s.items += o.qty || 1; s.margin += (o.base - o.partnerCost) * (o.qty || 1); s.sales += o.price * (o.qty || 1); });
      const sellers = Object.entries(bySeller).map(([id, v]) => ({ s: db().find('sellers', id), ...v })).filter((x) => x.s).sort((a, b) => b.margin - a.margin).slice(0, 6);
      const plans = { egy: 0, epl: 0, both: 0 }; cur.passes.forEach((x) => { plans[x.plan] = (plans[x.plan] || 0) + 1; });
      const body = `${C.rangePicker(r, '/admin/revenue')}
        <div class="kpi-grid">
          ${C.kpi({ label: 'Income', value: money(t.income), count: t.income, suffix: ' EGP', ic: 'trend', tone: 'k-turf', sub: `${chg(t.income, p.income)} vs previous ${len} days` })}
          ${C.kpi({ label: 'Costs', value: money(t.cost), count: t.cost, suffix: ' EGP', ic: 'alert', sub: `${chg(t.cost, p.cost)} vs previous ${len} days` })}
          ${C.kpi({ label: 'Net platform revenue', value: money(t.net), count: t.net, suffix: ' EGP', ic: 'percent', tone: 'k-blaze', sub: `${chg(t.net, p.net)} vs previous ${len} days` })}
          ${C.kpi({ label: 'Take rate', value: `${t.gmv ? ((t.net / t.gmv) * 100).toFixed(1) : '0.0'}%`, ic: 'swap', sub: `of ${money(t.gmv)} through Super App` })}
        </div>
        <section class="dash-card"><div class="row-between"><h2 class="h4">Income by day</h2><span class="xs muted">${C.rangeLabel(r)}</span></div><div style="margin-top:14px">${A.revChart(cur.daily, AD().STREAMS)}</div></section>
        <div class="dash-grid g-1-1">
          <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Income by stream</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Stream</th><th class="r">Amount</th><th class="r">Share</th><th class="r">Change</th></tr></thead><tbody>
            ${AD().STREAMS.map(([k, l, tone]) => `<tr><td class="small"><i class="adm-dot" style="background:${A.TONE[tone]}"></i>${l}</td><td class="r num strong">${money(t[k])}</td><td class="r num">${t.income ? ((t[k] / t.income) * 100).toFixed(1) : '0.0'}%</td><td class="r">${chg(t[k], p[k])}</td></tr>`).join('')}
            </tbody><tfoot><tr><th>Total income</th><th class="r num">${money(t.income)}</th><th class="r">100%</th><th class="r">${chg(t.income, p.income)}</th></tr></tfoot></table></div></section>
          <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Costs</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Cost</th><th class="r">Amount</th><th class="r">Change</th></tr></thead><tbody>
            ${AD().COSTS.map(([k, l]) => `<tr><td class="small">${l}</td><td class="r num strong">${money(t[k])}</td><td class="r">${chg(t[k], p[k])}</td></tr>`).join('')}
            </tbody><tfoot><tr><th>Total costs</th><th class="r num">${money(t.cost)}</th><th class="r">${chg(t.cost, p.cost)}</th></tr></tfoot></table></div>
            <p class="xs muted" style="padding:0 18px 18px">Withdrawals aren’t a cost: they are the partners’ and sellers’ own money leaving their wallets.</p></section>
        </div>
        <div class="dash-grid g-1-1">
          <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Top pitch owners by commission</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Partner</th><th class="r">Bookings</th><th class="r">Gross</th><th class="r">Commission</th></tr></thead><tbody>
            ${owners.map((x) => `<tr><td class="small"><b>${esc(x.a.company)}</b></td><td class="r num">${x.n}</td><td class="r num">${money(Math.round(x.gross))}</td><td class="r num strong">${money(Math.round(x.comm))}</td></tr>`).join('') || '<tr><td colspan="4" class="small muted" style="padding:18px">No bookings in this period.</td></tr>'}</tbody></table></div></section>
          <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Top sellers by print margin</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Seller</th><th class="r">Items</th><th class="r">Sales</th><th class="r">Our margin</th></tr></thead><tbody>
            ${sellers.map((x) => `<tr><td class="small"><b>${esc(x.s.name)}</b></td><td class="r num">${x.items}</td><td class="r num">${money(x.sales)}</td><td class="r num strong">${money(x.margin)}</td></tr>`).join('') || '<tr><td colspan="4" class="small muted" style="padding:18px">No print orders in this period.</td></tr>'}</tbody></table></div></section>
        </div>
        <section class="dash-card"><h2 class="h4">Fantasy Pass</h2><div class="adm-counts" style="grid-template-columns:repeat(4,minmax(0,1fr))">${[['trophy', cur.passes.length, 'Passes sold'], ['flag', plans.egy, 'Egyptian league'], ['globe', plans.epl, 'Premier League'], ['sparkle', plans.both, 'Both leagues']].map(([ic, n, l]) => `<div class="adm-count"><span class="kpi-ico">${icon(ic, 18)}</span><b class="h3 num">${n.toLocaleString()}</b><span class="xs muted">${l}</span></div>`).join('')}</div>
          <p class="xs muted" style="margin-top:10px">${money(t.passes)} from subscriptions. Prizes come from sponsors — Super App pays no cash prizes.</p></section>`;
      return C.layout('revenue', { title: 'Revenue', subtitle: 'What Super App earns, where it comes from and what it costs', actions: `<button class="btn btn-outline btn-sm" data-export>${icon('file', 15)} Export Excel</button>`, body });
    },
    mount(el, ctx) {
      C.bindRange(el, '/admin/revenue');
      A.bindRevChart(el);
      el.querySelector('[data-export]')?.addEventListener('click', () => {
        const r = C.range(ctx.query, '30');
        const cur = AD().revenue(r), t = cur.total;
        const S = AD().STREAMS, K = AD().COSTS;
        C.xlsx(`super-app-revenue-${r.from}-to-${r.to}.xlsx`, [
          { name: 'Summary', columns: [{ header: 'Line', width: 38 }, { header: 'Amount', width: 16, type: 'money' }],
            rows: [...S.map(([k, l]) => [l, t[k]]), ['Total income', t.income], ...K.map(([k, l]) => [l, -t[k]]), ['Total costs', -t.cost], ['Money through Super App (GMV)', t.gmv]], totals: ['Net platform revenue', t.net] },
          { name: 'Daily', columns: [{ header: 'Date', width: 12 }, ...S.map(([, l]) => ({ header: l, width: 16, type: 'money' })), ...K.map(([, l]) => ({ header: l, width: 16, type: 'money' })), { header: 'Net', width: 14, type: 'money' }, { header: 'GMV', width: 14, type: 'money' }],
            rows: cur.daily.map((d) => [d.date, ...S.map(([k]) => Math.round(d[k])), ...K.map(([k]) => -Math.round(d[k])), Math.round(d.net), Math.round(d.gmv)]),
            totals: ['Total', ...S.map(([k]) => t[k]), ...K.map(([k]) => -t[k]), t.net, t.gmv] },
          { name: 'Fantasy Pass', columns: [{ header: 'Date', width: 12 }, { header: 'Plan', width: 14 }, { header: 'Customer', width: 22 }, { header: 'Amount', width: 12, type: 'money' }],
            rows: cur.passes.map((x) => [isoDate(new Date(x.at)), { egy: 'Egyptian league', epl: 'Premier League', both: 'Both leagues' }[x.plan], x.customer, x.amount]), totals: ['Total', `${cur.passes.length} passes`, '', t.passes] },
          { name: 'Print orders', columns: [{ header: 'Item', width: 14 }, { header: 'Date', width: 12 }, { header: 'Seller', width: 18 }, { header: 'Design', width: 28 }, { header: 'Status', width: 11 }, { header: 'Price', width: 11, type: 'money' }, { header: 'Base', width: 11, type: 'money' }, { header: 'Partner cost', width: 13, type: 'money' }, { header: 'Our margin', width: 13, type: 'money' }],
            rows: (db().all('podOrders') || []).filter((o) => !o.sample && isoDate(new Date(o.placedAt)) >= r.from && isoDate(new Date(o.placedAt)) <= r.to).map((o) => [o.id, isoDate(new Date(o.placedAt)), db().find('sellers', o.sellerId)?.name || '', o.title, o.status, o.price * (o.qty || 1), o.base * (o.qty || 1), o.partnerCost * (o.qty || 1), ['refused', 'cancelled'].includes(o.status) ? -(o.loss || 0) : (o.base - o.partnerCost) * (o.qty || 1)]) },
        ]);
        AD().log('export', `Exported the revenue report ${r.from} to ${r.to}`);
      });
    },
  });
})(window.SA);
