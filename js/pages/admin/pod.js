/** Admin console: monthly seller level review (with a grace month) and sample requests. */
(function (SA) {
  const { esc, money, fmtDate, timeAgo } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const A = SA.pages.adminCommon;
  const AD = () => SA.services.admin;
  const db = () => SA.services.db;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });
  const ACT = { ok: ['badge-turf', 'Keeps level'], up: ['badge-info', 'Moves up'], grace: ['badge-gold', 'Grace month'], down: ['badge-danger', 'Goes down'] };
  const monthName = (m) => new Date(`${m}-01T12:00:00`).toLocaleDateString('en-GB', { month: 'long', year: 'numeric' });

  SA.pages.adminLevels = A.guard({
    title: 'Level review — Super App Admin',
    render() {
      const L = SA.services.sellers.levels();
      const preview = AD().previewLevelReview();
      const history = (db().all('levelReviews') || []);
      const month = new Date().toISOString().slice(0, 7);
      const due = AD().levelReviewDue();
      const body = `<section class="dash-card adm-howto"><h2 class="h4">How the review works</h2>
          <ol class="small soft"><li><b>Moving up is instant</b> — as soon as a seller reaches the sales and rating of the next level.</li>
            <li><b>Going down waits for the review.</b> Once a month we check every seller. Anyone below their level’s rating gets a <b>grace month</b> and keeps their perks.</li>
            <li>If they’re still below at the next review, the level goes down. Sales never go down, so only the rating (or a copyright freeze) can cost a level.</li></ol>
          <div class="row wrap" style="--gap:10px;margin-top:14px"><button class="btn ${due ? 'btn-primary' : 'btn-outline'}" data-run>${icon('trend', 15)} ${due ? `Run the ${monthName(month)} review` : 'Run the review again'}</button><span class="xs muted">${due ? 'Due — not run this month yet.' : `Done for ${monthName(month)}.`}</span></div></section>
        <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">If you run it now</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Seller</th><th class="r">Items sold</th><th class="r">Rating</th><th>Reviewed level</th><th>Earned now</th><th>Result</th></tr></thead><tbody>
          ${preview.map((r) => `<tr><td class="small"><b>${esc(r.s.name)}</b><span class="xs muted" style="display:block">@${esc(r.s.handle)}${r.s.grace ? ` · in grace since ${monthName(r.s.grace.since)}` : ''}</span></td><td class="r num">${r.sales}</td><td class="r num">${r.rating ? r.rating.toFixed(1) : '—'}</td>
            <td><span class="lvl-chip lvl-${L[r.kept].id}">${esc(L[r.kept].name)}</span></td><td><span class="lvl-chip lvl-${L[r.earned].id}">${esc(L[r.earned].name)}</span></td><td><span class="badge ${ACT[r.action][0]}">${ACT[r.action][1]}</span></td></tr>`).join('')}
        </tbody></table></div></section>
        ${history.map((h) => `<section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">${monthName(h.month)} review</h2><p class="xs muted" style="padding:2px 18px 0">${fmtDate(new Date(h.at))} · by ${esc(h.by)}</p><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Seller</th><th>From</th><th>To</th><th>Result</th></tr></thead><tbody>
          ${h.results.map((r) => `<tr><td class="small">${esc(r.name)}</td><td class="small">${esc(r.from)}</td><td class="small">${esc(r.to)}</td><td><span class="badge ${ACT[r.action][0]}">${ACT[r.action][1]}</span></td></tr>`).join('')}</tbody></table></div></section>`).join('')}`;
      return C.layout('levels', { title: 'Level review', subtitle: 'Monthly check of seller levels — a grace month before anyone goes down', body });
    },
    mount(el) {
      el.querySelector('[data-run]')?.addEventListener('click', async () => {
        const p = AD().previewLevelReview();
        if (!(await SA.ui.overlay.confirm({ title: 'Run the level review?', text: `${p.filter((r) => r.action === 'grace').length} seller(s) get a grace month and ${p.filter((r) => r.action === 'down').length} go down. Sellers are notified.`, confirmLabel: 'Run review' }))) return;
        AD().runLevelReview(); toast('Level review done'); SA.app.rerender();
      });
    },
  });

  const SST = { requested: ['badge-gold', 'Waiting'], approved: ['badge-info', 'In the print queue'], delivered: ['badge-turf', 'Delivered'], rejected: ['badge-danger', 'Rejected'] };
  SA.pages.adminSamples = A.guard({
    title: 'Samples — Super App Admin',
    render(ctx) {
      const tab = ctx.query.tab || 'requested';
      const all = SA.services.pod.samples();
      const list = all.filter((x) => tab === 'all' || x.status === tab);
      const body = `<p class="small soft">Sellers order samples of their designs to check the print. Starter and Rising pay the base price, Pro gets 30% off and Elite gets one free a month. Approving charges the seller’s balance and sends the job to the print partner.</p>
        ${A.tabs('/admin/samples', [['requested', 'Waiting', all.filter((x) => x.status === 'requested').length], ['approved', 'Printing', all.filter((x) => x.status === 'approved').length], ['delivered', 'Delivered', all.filter((x) => x.status === 'delivered').length], ['rejected', 'Rejected', all.filter((x) => x.status === 'rejected').length], ['all', 'All', all.length]], tab)}
        <section class="dash-card table-card"><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Design</th><th>Seller</th><th>Requested</th><th>Ship to</th><th class="r">Price</th><th>Status</th><th></th></tr></thead><tbody>
        ${list.map((x) => { const s = db().find('sellers', x.sellerId); const lv = s ? SA.services.sellers.levelOf(s) : null; return `<tr><td><span class="row" style="--gap:10px"><span class="sl-thumb"><img src="${SA.data.media.u(`pod:${x.designId}:${x.color}`)}" alt="" /></span><span><b class="small">${esc(x.title)}</b><span class="xs muted" style="display:block">${esc(x.color)} · ${esc(x.size)}</span></span></span></td>
          <td class="small">${esc(s?.name || '')}${lv ? ` <span class="lvl-chip lvl-${lv.id}">${esc(lv.name)}</span>` : ''}</td><td class="small">${timeAgo(x.createdAt)}</td><td class="xs">${esc(x.address)}</td><td class="r num">${x.free ? 'Free' : money(x.price)}</td>
          <td><span class="badge ${SST[x.status][0]}">${SST[x.status][1]}</span>${x.reason ? `<span class="xs muted" style="display:block">${esc(x.reason)}</span>` : ''}</td>
          <td class="r">${x.status === 'requested' ? `<div class="row" style="--gap:6px;justify-content:flex-end"><button class="btn btn-ghost btn-sm" data-reject="${x.id}">Reject</button><button class="btn btn-primary btn-sm" data-approve="${x.id}">Approve</button></div>` : ''}</td></tr>`; }).join('')
          || `<tr><td colspan="7">${SA.ui.c.empty({ icon: 'shirt', title: 'No sample requests here', text: 'Sellers order samples from their Designs page.' })}</td></tr>`}
        </tbody></table></div></section>`;
      return C.layout('samples', { title: 'Samples', subtitle: 'Approve sample prints sellers order of their own designs', body });
    },
    mount(el) {
      el.addEventListener('click', async (e) => {
        const ap = e.target.closest('[data-approve]');
        if (ap) { try { AD().approveSample(ap.dataset.approve); toast('Sample approved — sent to the print partner'); SA.app.rerender(); } catch (err) { toast(err.message, 'error'); } return; }
        const rj = e.target.closest('[data-reject]');
        if (rj) { const r = await A.reason({ title: 'Reject this sample?', label: 'Reason the seller sees', confirmLabel: 'Reject', danger: true }); if (!r) return; try { AD().rejectSample(rj.dataset.reject, r.reason); toast('Sample rejected', 'info'); SA.app.rerender(); } catch (err) { toast(err.message, 'error'); } }
      });
    },
  });
})(window.SA);
