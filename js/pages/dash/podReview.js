/** Admin: review print-on-demand designs, handle copyright/content reports and manage sellers (strikes, suspension). */
(function (SA) {
  const { esc, money, timeAgo, fmtDate } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const D = () => SA.services.dash;
  const P = () => SA.services.pod;
  const SL = () => SA.services.sellers;

  const TABS = [['queue', 'Review queue'], ['reports', 'Reports'], ['sellers', 'Sellers']];
  const LANE = { priority: ['badge-gold', 'Priority · Pro'], instant: ['badge-turf', 'Elite'], normal: ['badge', 'Standard'], spot: ['badge-blaze', 'Spot check · already live'] };
  const REASONS = {
    copyright: 'Uses artwork, characters or photos you don’t have the rights to.',
    trademark: 'Shows a club crest, brand logo or trademarked name.',
    quality: 'The file is too small or blurry to print well.',
    offensive: 'Content isn’t allowed on Super App.',
    details: 'Title, category or tags don’t match the design.',
  };
  const REPORT = { copyright: 'Copyright', trademark: 'Trademark', offensive: 'Offensive', other: 'Other' };

  const mock = (d, side = d.placement?.front ? 'front' : 'back') => {
    const col = d.product?.colors.find((k) => k.name === d.colors[0]) || d.product?.colors[0];
    return SA.ui.garmentView({ kind: d.product?.kind, hex: col?.hex, art: side === 'back' ? d.srcBack : d.src, place: d.placement?.[side], side, artRatio: side === 'back' ? d.ratioBack : d.ratioFront, label: d.title });
  };

  const queueItem = (d) => {
    const s = SL().get(d.sellerId);
    const lvl = SL().levelOf(s);
    const [cls, lane] = LANE[d.lane] || LANE.normal;
    const sides = ['front', 'back'].filter((x) => d.placement?.[x]);
    return `<article class="dash-card rv-item" data-design="${d.id}">
      <div class="rv-media">${sides.map((x) => `<div class="rv-mock"><span class="xs muted">${x === 'front' ? 'Front' : 'Back'}</span>${mock(d, x)}</div>`).join('')}
        <div class="rv-art" title="Uploaded file"><img src="${esc(d.src)}" alt="Uploaded artwork for ${esc(d.title)}" /></div></div>
      <div class="rv-body">
        <div class="row-between" style="--gap:8px"><span class="badge ${cls}">${lane}</span><span class="xs muted">${d.submittedAt ? `sent ${timeAgo(d.submittedAt)}` : ''}</span></div>
        <h3 class="h4">${esc(d.title)}</h3>
        <p class="small soft">${esc(d.desc || 'No description')}</p>
        <dl class="kv rv-kv">
          <div><dt>Seller</dt><dd><a href="#/s/${esc(s.handle)}" target="_blank" rel="noopener">${esc(s.name)}</a> · <span class="lvl-chip lvl-${lvl.id}">${esc(lvl.name)}</span>${s.strikes?.length ? ` · <span class="text-danger">${s.strikes.length} strike${s.strikes.length > 1 ? 's' : ''}</span>` : ''}</dd></div>
          <div><dt>Product</dt><dd>${esc(d.product?.name)} · ${d.colors.map(esc).join(', ')}</dd></div>
          <div><dt>Category · tags</dt><dd>${esc(P().category(d.category)?.name || '—')}${d.tags?.length ? ` · ${d.tags.map(esc).join(', ')}` : ''}</dd></div>
          <div><dt>Price · seller earns</dt><dd class="num">${money(d.price)} · ${money(d.profit)}</dd></div>
          <div><dt>File</dt><dd class="num">${d.artW ? `${d.artW}×${d.artH}px` : 'Built-in artwork'}</dd></div>
        </dl>
        <div class="rv-actions">
          <button class="btn btn-primary btn-sm" data-approve="${d.id}">${icon('check', 14, 2.6)} ${d.lane === 'spot' ? 'Looks fine' : 'Approve'}</button>
          <button class="btn btn-outline btn-sm" data-reject="${d.id}" style="color:var(--danger)">${icon('x', 14)} ${d.lane === 'spot' ? 'Take down' : 'Reject'}</button>
        </div>
      </div></article>`;
  };

  const reportItem = (r) => {
    const d = r.design;
    return `<article class="dash-card rv-item">
      <div class="rv-media">${d ? `<div class="rv-mock">${mock(d)}</div>` : ''}</div>
      <div class="rv-body">
        <div class="row-between"><span class="badge ${r.status === 'open' ? 'badge-danger' : 'badge'}">${REPORT[r.reason]} report${r.status === 'open' ? '' : r.status === 'removed' ? ' · removed' : ' · dismissed'}</span><span class="xs muted">${timeAgo(r.createdAt)}</span></div>
        <h3 class="h4">${esc(d?.title || 'Deleted design')}</h3>
        <p class="small">“${esc(r.detail || 'No details given')}”</p>
        ${r.contact ? `<p class="xs muted">Contact: ${esc(r.contact)}</p>` : ''}
        <p class="xs muted">${esc(d?.seller?.name || '')} · ${d ? `${d.status === 'live' ? 'Live in the shop' : d.status}` : ''}</p>
        ${r.status === 'open' ? `<div class="rv-actions"><button class="btn btn-outline btn-sm" data-dismiss="${r.id}">Dismiss</button><button class="btn btn-primary btn-sm" data-takedown="${r.id}" style="background:var(--danger-fill);border-color:var(--danger-fill)">${icon('shield', 14)} Take down${['copyright', 'trademark'].includes(r.reason) ? ' + strike' : ''}</button></div>` : ''}
      </div></article>`;
  };

  const sellersTable = () => `<section class="dash-card table-card"><div class="table-wrap"><table class="table dash-table">
      <thead><tr><th>Seller</th><th>Level</th><th class="r">Items sold</th><th class="r">Rating</th><th class="r">Live designs</th><th>Strikes</th><th>ID check</th><th>Status</th><th></th></tr></thead><tbody>
      ${SL().list().map((s) => {
        const l = SL().levelOf(s);
        const live = P().list({ sellerId: s.id, status: 'live' }).length;
        const active = (s.strikes || []).filter((x) => Date.now() - x.at < 90 * 864e5).length;
        return `<tr><td><b class="small">${esc(s.name)}</b><span class="xs muted" style="display:block">/s/${esc(s.handle)} · ${s.mode === 'pod' ? 'Print on demand' : s.mode === 'store' ? 'Own store' : 'Both'}</span></td>
          <td><span class="lvl-chip lvl-${l.id}">${esc(l.name)}</span>${l.frozen ? ` ${icon('lock', 12)}` : ''}</td><td class="r num">${l.stats.sales}</td><td class="r num">${l.stats.rating ? l.stats.rating.toFixed(1) : '—'}</td><td class="r num">${live}</td>
          <td>${active ? `<span class="text-danger small">${active} active</span>` : '<span class="xs muted">None</span>'}</td>
          <td>${s.kyc === 'pending' ? `<span class="row" style="--gap:6px"><button class="btn btn-primary btn-sm" data-kyc-ok="${s.id}">Verify</button><button class="btn btn-ghost btn-sm" data-kyc-no="${s.id}">Reject</button></span><span class="xs muted" style="display:block">${esc(s.kycInfo?.fullName || '')} · ID ••••${esc(s.kycInfo?.idLast4 || '')}</span>` : `<span class="badge ${s.kyc === 'verified' ? 'badge-turf' : s.kyc === 'rejected' ? 'badge-danger' : 'badge'}">${s.kyc === 'verified' ? 'Verified' : s.kyc === 'rejected' ? 'Rejected' : 'Not sent'}</span>`}</td>
          <td><span class="badge ${s.status === 'active' ? 'badge-turf' : 'badge-danger'}">${s.status === 'active' ? 'Active' : 'Suspended'}</span></td>
          <td class="r"><button class="btn btn-outline btn-sm" data-seller-status="${s.id}" data-to="${s.status === 'active' ? 'suspended' : 'active'}">${s.status === 'active' ? 'Suspend' : 'Reactivate'}</button></td></tr>`;
      }).join('')}</tbody></table></div></section>`;

  const rejectDialog = (d) => SA.ui.overlay.open({
    title: d.lane === 'spot' ? `Take down “${esc(d.title)}”` : `Reject “${esc(d.title)}”`, subtitle: 'The seller sees your reason and can fix the design and send it again.',
    body: `<form id="rj-form" class="stack" style="--gap:10px" novalidate>
      ${Object.entries(REASONS).map(([k, v], i) => `<label class="choice"><input type="radio" name="tpl" value="${k}" ${i === 0 ? 'checked' : ''} /><span class="radio-dot"></span><span class="grow small">${esc(v)}</span></label>`).join('')}
      <div class="field"><label for="rj-note">Message to the seller</label><textarea class="textarea" id="rj-note" name="reason" maxlength="300">${esc(REASONS.copyright)}</textarea></div>
      <label class="choice"><input type="checkbox" name="strike" /><span class="radio-dot" style="border-radius:6px"></span><span class="grow small"><b>Add a copyright strike</b><span class="xs muted" style="display:block">Freezes the seller’s level for 90 days. Three strikes suspend the account.</span></span></label>
      <p class="field-error" data-err></p></form>`,
    footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-primary" form="rj-form" type="submit">${d.lane === 'spot' ? 'Take down' : 'Reject design'}</button>`,
    onMount(m, close) {
      const f = m.querySelector('#rj-form');
      f.addEventListener('change', (e) => { if (e.target.name === 'tpl') { f.reason.value = REASONS[e.target.value]; f.strike.checked = ['copyright', 'trademark'].includes(e.target.value); } });
      f.strike.checked = true;
      f.addEventListener('submit', (e) => {
        e.preventDefault();
        try { P().reject(d.id, f.reason.value, { strike: f.strike.checked }); close(); SA.ui.overlay.toast(`“${d.title}” rejected`); SA.app.rerender(); }
        catch (err) { m.querySelector('[data-err]').textContent = err.message; }
      });
    },
  });

  SA.pages.dashPodReview = C.guard({
    title: 'Design review — Super App Admin',
    render(ctx) {
      if (!D().isAdmin()) return C.layout('pod-review', { title: 'Design review', body: `<div class="dash-card">${SA.ui.c.empty({ icon: 'lock', title: 'Super App admins only', text: 'This section is only available to the Super App team.' })}</div>` });
      const tab = TABS.some(([k]) => k === ctx.query.tab) ? ctx.query.tab : 'queue';
      const queue = P().queue();
      const reports = P().reports().sort((a, b) => (a.status === 'open' ? 0 : 1) - (b.status === 'open' ? 0 : 1) || b.createdAt - a.createdAt);
      const openReports = reports.filter((r) => r.status === 'open').length;
      const counts = { queue: queue.length, reports: openReports, sellers: SL().list().length };
      const body = `<div class="chips" role="tablist" aria-label="Sections">${TABS.map(([k, l]) => `<a class="chip" role="tab" href="#/dash/pod-review?tab=${k}" aria-pressed="${tab === k}" aria-selected="${tab === k}">${l} <span class="chip-count">${counts[k]}</span></a>`).join('')}</div>
        ${tab === 'queue' ? (queue.length ? `<div class="rv-list">${queue.map(queueItem).join('')}</div>` : `<div class="dash-card">${SA.ui.c.empty({ icon: 'check', title: 'All caught up', text: 'New designs show up here as soon as sellers send them.' })}</div>`) : ''}
        ${tab === 'reports' ? (reports.length ? `<div class="rv-list">${reports.map(reportItem).join('')}</div>` : `<div class="dash-card">${SA.ui.c.empty({ icon: 'shield', title: 'No reports', text: 'Customers and rights holders report designs from the product page.' })}</div>`) : ''}
        ${tab === 'sellers' ? sellersTable() : ''}`;
      return C.layout('pod-review', { title: 'Design review', subtitle: 'Every print-on-demand design is checked before it reaches the shop', body });
    },
    mount(el) {
      el.addEventListener('click', async (e) => {
        const ap = e.target.closest('[data-approve]');
        if (ap) { const d = P().approve(ap.dataset.approve); SA.ui.overlay.toast(`“${d.title}” is live`); SA.app.rerender(); return; }
        const rj = e.target.closest('[data-reject]');
        if (rj) { const d = P().queue().find((x) => x.id === rj.dataset.reject); if (d) rejectDialog(d); return; }
        const dm = e.target.closest('[data-dismiss]');
        if (dm) { P().resolveReport(dm.dataset.dismiss, 'dismiss'); SA.ui.overlay.toast('Report dismissed'); SA.app.rerender(); return; }
        const td = e.target.closest('[data-takedown]');
        if (td && await SA.ui.overlay.confirm({ title: 'Take this design down?', text: 'It leaves the shop straight away and the seller is told why. Copyright and trademark reports also add a strike.', confirmLabel: 'Take down', danger: true })) {
          P().resolveReport(td.dataset.takedown, 'takedown'); SA.ui.overlay.toast('Design taken down'); SA.app.rerender(); return;
        }
        const ko = e.target.closest('[data-kyc-ok],[data-kyc-no]');
        if (ko) { const ok = !!ko.dataset.kycOk; SL().verifyKyc(ko.dataset.kycOk || ko.dataset.kycNo, ok); SA.ui.overlay.toast(ok ? 'Seller verified — withdrawals unlocked' : 'ID rejected — the seller can send it again'); SA.app.rerender(); return; }
        const ss = e.target.closest('[data-seller-status]');
        if (ss) {
          const s = SL().get(ss.dataset.sellerStatus);
          if (ss.dataset.to === 'suspended' && !(await SA.ui.overlay.confirm({ title: `Suspend ${esc(s.name)}?`, text: 'All their designs and products leave the shop until you reactivate them.', confirmLabel: 'Suspend seller', danger: true }))) return;
          SL().setStatus(s.id, ss.dataset.to); SA.ui.overlay.toast(ss.dataset.to === 'active' ? 'Seller reactivated' : 'Seller suspended'); SA.app.rerender();
        }
      });
    },
  });
})(window.SA);
