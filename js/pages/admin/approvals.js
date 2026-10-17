/** Admin console: approvals — new pitches (before they go live) and new sellers (when approval is switched on). */
(function (SA) {
  const { esc, money, timeAgo } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const A = SA.pages.adminCommon;
  const AD = () => SA.services.admin;
  const db = () => SA.services.db;
  const media = SA.data.media;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });
  const RV = { pending: ['badge-gold', 'Waiting'], rejected: ['badge-danger', 'Not approved'] };

  /* ---------- API mode: real applications from the server (pitch partners and sellers) ---------- */
  const API = () => SA.services.api;
  const live = () => API()?.isLive();
  const AS = { pending: ['badge-gold', 'Waiting'], approved: ['badge-turf', 'Approved'], rejected: ['badge-danger', 'Not approved'] };
  const filesOf = (a, purpose) => a.files.filter((f) => f.purpose === purpose).map((f) => f.url);
  const idPhotos = (a) => { const ids = [...filesOf(a, 'IdFront'), ...filesOf(a, 'IdBack')]; return ids.length ? `<details class="adm-id"><summary class="small">${icon('shield', 14)} ID photos</summary>${ids.map((u) => `<img src="${esc(u)}" alt="ID photo" loading="lazy" />`).join('')}</details>` : ''; };
  const decide = (a) => (a.status === 'Pending' ? `<button class="btn btn-ghost btn-sm" data-reject-live="${esc(a.code)}">Don’t approve</button><button class="btn btn-primary btn-sm" data-approve-live="${esc(a.code)}">${icon('check', 14)} Approve</button>` : '');
  const liveCard = (a, areaName) => {
    const x = a.answers, st = AS[a.status.toLowerCase()] || AS.pending, isPitch = a.type === 'PitchOwner';
    const title = isPitch ? x.pitchName : x.storeName;
    const facts = isPitch
      ? `${esc(x.address)}, ${esc(areaName(x.areaId))} · ${x.count} pitch${x.count > 1 ? 'es' : ''} · ${x.formats.map((f) => `${f}-a-side`).join(', ')} · ${money(x.price)}/hour`
      : `${{ pod: 'Print on demand', store: 'Own store', both: 'Both' }[x.mode] || ''}${x.bio ? ` · ${esc(x.bio)}` : ''}`;
    return `<article class="dash-card adm-approve">
      ${isPitch ? `<div class="adm-approve-img">${filesOf(a, 'VenuePhoto').slice(0, 3).map((u) => `<img src="${esc(u)}" alt="" loading="lazy" />`).join('')}</div>` : ''}
      <div class="row-between wrap" style="--gap:6px"><h2 class="h4">${esc(title)}</h2><span class="badge ${st[0]}">${st[1]}</span></div>
      <p class="small soft">${facts}</p>
      <dl class="kv"><div><dt>Owner</dt><dd>${esc(x.ownerName || x.fullName || a.applicant.fullName)}</dd></div><div><dt>Contact</dt><dd class="num">${esc(x.phone || '')} · ${esc(a.applicant.email)}</dd></div><div><dt>National ID</dt><dd class="num">${esc(x.nationalId || '')}</dd></div><div><dt>Payout</dt><dd>${esc(x.payoutMethod)} · ${esc(x.payoutDetail)}</dd></div><div><dt>Sent</dt><dd>${timeAgo(new Date(a.createdAt).getTime())}</dd></div></dl>
      ${idPhotos(a)}${x.note ? `<p class="small">${esc(x.note)}</p>` : ''}${a.rejectReason ? `<p class="xs" style="color:var(--danger)">${icon('alert', 12)} ${esc(a.rejectReason)}</p>` : ''}
      <div class="row wrap" style="--gap:8px;justify-content:flex-end">${isPitch ? `<a class="btn btn-ghost btn-sm" href="https://www.google.com/maps?q=${x.lat},${x.lng}" target="_blank" rel="noopener">${icon('map', 14)} Map</a>` : ''}${decide(a)}</div>
    </article>`;
  };
  /** Both live lists at once (the tab counts need them); null when this account may not see a type */
  const loadLive = async () => {
    const get = (type) => API().get(`/admin/approvals?type=${type}&pageSize=100`).then((r) => r.items).catch(() => null);
    const [partners, sellers, areas] = await Promise.all([get('PitchOwner'), get('Seller'), API().get('/lookups/areas')]);
    const names = Object.fromEntries(areas.map((x) => [x.id, SA.i18n?.isAr ? x.nameAr : x.nameEn]));
    const pitchCount = await API().get('/admin/pitches?status=WaitingForReview&pageSize=1').then((r) => r.meta.total).catch(() => null);
    return { partners, sellers, pitchCount, areaName: (id) => names[id] || '' };
  };
  const liveSection = (list, areaName, emptyIcon, emptyTitle) => (list == null
    ? `<div class="dash-card">${SA.ui.c.empty({ icon: 'lock', title: 'Not part of your permissions', text: 'Ask the CEO for the approvals permission for this type.' })}</div>`
    : list.length ? `<div class="adm-approve-grid">${list.map((a) => liveCard(a, areaName)).join('')}</div>`
      : `<div class="dash-card">${SA.ui.c.empty({ icon: emptyIcon, title: emptyTitle, text: 'New applications wait here.' })}</div>`);
  const pendingCount = (list) => (list || []).filter((a) => a.status === 'Pending').length;

  /** A pitch waiting for review (a demo record, or one built from GET /admin/pitches/{id} in API mode) */
  const pitchCard = (p, owner, liveTag = '') => `<article class="dash-card adm-approve">
            <div class="adm-approve-img">${(p.images || []).slice(0, 3).map((im) => `<img src="${media.photo(im, 360, 240)}" alt="" loading="lazy" />`).join('')}</div>
            <div class="row-between wrap" style="--gap:6px"><h2 class="h4">${esc(p.name)}</h2><span class="badge ${RV[p.review][0]}">${RV[p.review][1]}</span></div>
            <p class="small soft">${esc(p.address)}, ${esc(p.area)}, ${esc(p.city)} · ${esc(p.format)} · ${p.indoor ? 'Indoor' : 'Outdoor'} · ${esc(p.surface)}</p>
            <dl class="kv"><div><dt>Owner</dt><dd>${esc(owner)}</dd></div><div><dt>Price</dt><dd class="num">${money(p.price)} · peak ${money(p.peakPrice)}</dd></div><div><dt>Hours</dt><dd class="num">${p.open[0]}:00 – ${p.open[1] % 24}:00</dd></div><div><dt>Sent</dt><dd>${timeAgo(p.submittedAt || p.createdAt)}</dd></div></dl>
            <p class="small">${esc(p.about)}</p>
            ${p.review === 'rejected' ? `<p class="xs" style="color:var(--danger)">${icon('alert', 12)} ${esc(p.reviewReason || '')}</p>` : ''}
            <div class="row wrap" style="--gap:8px;justify-content:flex-end"><a class="btn btn-ghost btn-sm" href="https://www.google.com/maps?q=${p.lat},${p.lng}" target="_blank" rel="noopener">${icon('map', 14)} Map</a>${p.review === 'pending' ? `<button class="btn btn-outline btn-sm" data-reject-pitch${liveTag}="${p.id}">Don’t approve</button>` : ''}<button class="btn btn-primary btn-sm" data-approve-pitch${liveTag}="${p.id}">${icon('check', 14, 2.6)} Approve and publish</button></div>
          </article>`;
  /** API mode: the server's pitch → the fields the card reads */
  const fromReview = (r) => {
    const d = r.pitch, day = d.openingHours.find((h) => !h.isClosed) || { openHour: 0, closeHour: 0 };
    return { id: r.summary.id, name: d.name, address: r.address, area: r.summary.venueName, city: r.summary.areaEn, format: `${d.format}-a-side`, indoor: d.isIndoor,
      surface: d.surface.replace(/([a-z])([A-Z])/g, '$1 $2'), images: d.photos.map((ph) => (ph.url ? `${location.origin}${ph.url}` : ph.key)),
      price: d.pricePerHour, peakPrice: d.peakPricePerHour ?? d.pricePerHour, open: [day.openHour, day.closeHour], submittedAt: Date.parse(r.summary.createdAt),
      about: d.descriptionEn || '', review: { WaitingForReview: 'pending', Rejected: 'rejected' }[d.status] || 'approved', reviewReason: d.rejectReason, lat: r.lat, lng: r.lng };
  };
  /** Pitches waiting for review with their whole form; null without the approvals.pitches permission */
  const loadLivePitches = async () => {
    try {
      const rows = (await API().get('/admin/pitches?status=WaitingForReview&pageSize=50')).items;
      return await Promise.all(rows.map((x) => API().get(`/admin/pitches/${x.id}`)));
    } catch { return null; }
  };

  SA.pages.adminApprovals = A.guard({
    title: 'Approvals — Super App Admin',
    async render(ctx) {
      const tab = ctx.query.tab || '';
      const pitches = AD().pendingPitches(), sellers = AD().pendingSellers();
      const apps = SA.services.partners.list();
      const on = !!db().settings().sellerApproval;
      const L = live() ? await loadLive() : null;
      let section;
      if (L && tab === 'partners') {
        section = liveSection(L.partners, L.areaName, 'pitch', 'No applications');
      } else if (L && tab === 'sellers') {
        section = liveSection(L.sellers, L.areaName, 'users', 'No sellers waiting');
      } else if (!tab) {
        const LP = L ? await loadLivePitches() : null;
        section = L && LP == null ? `<div class="dash-card">${SA.ui.c.empty({ icon: 'lock', title: 'Not part of your permissions', text: 'Ask the CEO for the approvals permission for this type.' })}</div>`
          : (L ? LP.length : pitches.length) ? `<div class="adm-approve-grid">${L ? LP.map((r) => pitchCard(fromReview(r), r.summary.businessName, '-live')).join('') : pitches.map((p) => { const o = db().find('accounts', p.ownerId); return pitchCard(p, `${o?.company || ''} · ${o?.name || ''}`); }).join('')}</div>`
          : `<div class="dash-card">${SA.ui.c.empty({ icon: 'pitch', title: 'No pitches waiting', text: 'New pitches partners add wait here until you approve them.' })}</div>`;
      } else if (tab === 'partners') {
        const AS = { pending: ['badge-gold', 'Waiting'], approved: ['badge-turf', 'Approved'], rejected: ['badge-danger', 'Not approved'] };
        section = apps.length ? `<div class="adm-approve-grid">${apps.map((a) => `<article class="dash-card adm-approve">
            <div class="adm-approve-img">${a.photos.slice(0, 3).map((im) => `<img src="${im}" alt="" loading="lazy" />`).join('')}</div>
            <div class="row-between wrap" style="--gap:6px"><h2 class="h4">${esc(a.pitchName)}</h2><span class="badge ${AS[a.status][0]}">${AS[a.status][1]}</span></div>
            <p class="small soft">${esc(a.address)}, ${esc(a.area)}, ${esc(a.city)} · ${a.count} pitch${a.count > 1 ? 'es' : ''} · ${a.formats.join(', ')} · ${money(a.price)}/hour</p>
            <dl class="kv"><div><dt>Owner</dt><dd>${esc(a.ownerName)}</dd></div><div><dt>Contact</dt><dd class="num">${esc(a.phone)} · ${esc(a.email)}</dd></div><div><dt>National ID</dt><dd class="num">•••• ${esc(a.idLast4)}</dd></div><div><dt>Payout</dt><dd>${esc(a.payout.method)} · ${esc(a.payout.detail)}</dd></div><div><dt>Sent</dt><dd>${timeAgo(a.createdAt)}</dd></div></dl>
            ${a.idPhoto ? `<details class="adm-id"><summary class="small">${icon('shield', 14)} ID photo</summary><img src="${a.idPhoto}" alt="ID photo" /></details>` : ''}
            ${a.note ? `<p class="small">${esc(a.note)}</p>` : ''}${a.status === 'rejected' ? `<p class="xs" style="color:var(--danger)">${icon('alert', 12)} ${esc(a.reason || '')}</p>` : ''}
            <div class="row wrap" style="--gap:8px;justify-content:flex-end"><a class="btn btn-ghost btn-sm" href="https://www.google.com/maps?q=${a.lat},${a.lng}" target="_blank" rel="noopener">${icon('map', 14)} Map</a>${a.status === 'pending' ? `<button class="btn btn-ghost btn-sm" data-reject-app="${a.id}">Don’t approve</button><button class="btn btn-primary btn-sm" data-approve-app="${a.id}">${icon('check', 14)} Approve</button>` : ''}</div>
          </article>`).join('')}</div>`
          : `<div class="dash-card">${SA.ui.c.empty({ icon: 'pitch', title: 'No applications', text: 'Customers apply to list their pitch from their profile. Applications wait here.' })}</div>`;
      } else {
        section = `<section class="dash-card"><label class="choice"><input type="checkbox" data-seller-approval ${on ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span><span class="grow"><b>New sellers need approval</b><span class="xs muted" style="display:block">When on, a new seller’s store and designs stay hidden until you approve them. Sellers already approved aren’t affected.</span></span></label></section>
          <section class="dash-card table-card"><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Seller</th><th>Sells</th><th>Owner</th><th>Started</th><th>Status</th><th></th></tr></thead><tbody>
          ${sellers.map((s) => { const a = db().find('accounts', s.accountId); return `<tr><td class="small"><b>${esc(s.name)}</b><span class="xs muted" style="display:block">@${esc(s.handle)}${s.bio ? ` · ${esc(s.bio.slice(0, 60))}` : ''}</span>${s.kycInfo ? `<span class="xs" style="display:block">${icon('shield', 11)} ${esc(s.kycInfo.fullName)} · ID •••• ${esc(s.kycInfo.idLast4)}${s.payout ? ` · ${esc(s.payout.method)} ${esc(s.payout.detail)}` : ''}</span>${s.kycInfo.photo ? `<details class="adm-id"><summary class="xs">ID photo</summary><img src="${s.kycInfo.photo}" alt="ID photo" /></details>` : ''}` : ''}</td><td class="small">${{ pod: 'Print on demand', store: 'Own store', both: 'Both' }[s.mode]}</td><td class="small">${esc(a?.name || '')}<span class="xs muted" style="display:block">${esc(a?.email || '')}</span></td><td class="small">${timeAgo(s.createdAt)}</td>
            <td><span class="badge ${RV[s.status][0]}">${RV[s.status][1]}</span>${s.reviewReason ? `<span class="xs muted" style="display:block">${esc(s.reviewReason)}</span>` : ''}</td>
            <td class="r"><div class="row" style="--gap:6px;justify-content:flex-end">${s.status === 'pending' ? `<button class="btn btn-ghost btn-sm" data-reject-seller="${s.id}">Don’t approve</button>` : ''}<button class="btn btn-primary btn-sm" data-approve-seller="${s.id}">Approve</button></div></td></tr>`; }).join('')
            || `<tr><td colspan="6">${SA.ui.c.empty({ icon: 'users', title: 'No sellers waiting', text: on ? 'New sellers wait here until you approve them.' : 'Approval is off — new sellers go live straight away.' })}</td></tr>`}
          </tbody></table></div></section>`;
      }
      const body = `${A.tabs('/admin/approvals', [['', 'New pitches', L ? (L.pitchCount ?? 0) : pitches.filter((p) => p.review === 'pending').length], ['partners', 'Pitch partners', L ? pendingCount(L.partners) : apps.filter((a) => a.status === 'pending').length], ['sellers', 'New sellers', L ? pendingCount(L.sellers) : sellers.filter((s) => s.status === 'pending').length]], tab)}${section}`;
      return C.layout('approvals', { title: 'Approvals', subtitle: 'Check new pitches and sellers before they go live on Super App', body });
    },
    mount(el) {
      el.querySelector('[data-seller-approval]')?.addEventListener('change', (e) => { AD().setSellerApproval(e.target.checked); toast(e.target.checked ? 'New sellers now need approval' : 'New sellers go live straight away', 'info'); });
      el.addEventListener('click', async (e) => {
        const t = (s) => e.target.closest(s);
        try {
          if (t('[data-approve-pitch]')) { AD().approvePitch(t('[data-approve-pitch]').dataset.approvePitch); toast('Pitch approved — it’s live'); SA.app.rerender(); return; }
          if (t('[data-reject-pitch]')) { const r = await A.reason({ title: 'Don’t approve this pitch?', text: 'The owner sees the reason and can fix the pitch and send it again.', label: 'Reason the owner sees', placeholder: 'e.g. The photos don’t show the pitch itself.', confirmLabel: 'Don’t approve', danger: true }); if (!r) return; AD().rejectPitch(t('[data-reject-pitch]').dataset.rejectPitch, r.reason); toast('Pitch not approved', 'info'); SA.app.rerender(); return; }
          if (t('[data-approve-pitch-live]')) { await API().post(`/admin/pitches/${t('[data-approve-pitch-live]').dataset.approvePitchLive}/approve`); toast('Pitch approved — it’s live'); SA.app.rerender(); return; }
          if (t('[data-reject-pitch-live]')) { const id = t('[data-reject-pitch-live]').dataset.rejectPitchLive; const r = await A.reason({ title: 'Don’t approve this pitch?', text: 'The owner sees the reason and can fix the pitch and send it again.', label: 'Reason the owner sees', placeholder: 'e.g. The photos don’t show the pitch itself.', confirmLabel: 'Don’t approve', danger: true }); if (!r) return; await API().post(`/admin/pitches/${id}/reject`, { reason: r.reason }); toast('Pitch not approved', 'info'); SA.app.rerender(); return; }
          if (t('[data-approve-live]')) { await API().post(`/admin/approvals/applications/${t('[data-approve-live]').dataset.approveLive}/approve`); toast('Approved — the applicant was notified'); SA.app.rerender(); return; }
          if (t('[data-reject-live]')) { const r = await A.reason({ title: 'Don’t approve this application?', text: 'The applicant sees the reason and can fix it and send it again.', label: 'Reason the applicant sees', placeholder: 'e.g. The ID photo isn’t clear', confirmLabel: 'Don’t approve', danger: true }); if (!r) return; await API().post(`/admin/approvals/applications/${t('[data-reject-live]').dataset.rejectLive}/reject`, { reason: r.reason }); toast('Applicant told', 'info'); SA.app.rerender(); return; }
          if (t('[data-approve-app]')) { SA.services.partners.approve(t('[data-approve-app]').dataset.approveApp); toast('Approved — the pitch partner account and the pitch are live'); SA.app.rerender(); return; }
          if (t('[data-reject-app]')) { const r = await A.reason({ title: 'Don’t approve this application?', text: 'The applicant sees the reason and can apply again.', label: 'Reason the applicant sees', placeholder: 'e.g. The ID photo isn’t clear', confirmLabel: 'Don’t approve', danger: true }); if (!r) return; SA.services.partners.reject(t('[data-reject-app]').dataset.rejectApp, r.reason); toast('Applicant told', 'info'); SA.app.rerender(); return; }
          if (t('[data-approve-seller]')) { AD().approveSeller(t('[data-approve-seller]').dataset.approveSeller); toast('Seller approved — the store is live'); SA.app.rerender(); return; }
          if (t('[data-reject-seller]')) { const r = await A.reason({ title: 'Don’t approve this seller?', label: 'Reason the seller sees', confirmLabel: 'Don’t approve', danger: true }); if (!r) return; AD().rejectSeller(t('[data-reject-seller]').dataset.rejectSeller, r.reason); toast('Seller not approved', 'info'); SA.app.rerender(); }
        } catch (err) { toast(err.message, 'error'); }
      });
    },
  });
})(window.SA);
