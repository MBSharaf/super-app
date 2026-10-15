/**
 * Factory system (#/factory): shared layout, staff sign-in (name + PIN), job cards, job ticket and shipping label PDFs.
 * A separate system with its own session (SA.services.factory.auth); it reuses the dashboard look (css/dash.css).
 */
(function (SA) {
  const { esc, money, timeAgo } = SA.core.utils;
  const { icon } = SA.ui;
  const c = SA.ui.c;
  const F = () => SA.services.factory;
  const P = () => SA.services.pod;
  const N = () => SA.services.notify;
  const H = 36e5;

  const NAV = [
    { key: 'overview', label: 'Overview', href: '#/factory', icon: 'grid' },
    { key: 'jobs', label: 'Production', href: '#/factory/jobs', icon: 'package', badge: 'jobs' },
    { key: 'scan', label: 'Scan a job', href: '#/factory/scan', icon: 'scan' },
    { key: 'shipping', label: 'Shipping', href: '#/factory/shipping', icon: 'truck', perm: 'ship', badge: 'pickup' },
    { group: 'Manage', perm: 'reports' },
    { key: 'reports', label: 'Reports', href: '#/factory/reports', icon: 'chart', perm: 'reports' },
    { key: 'team', label: 'Team', href: '#/factory/team', icon: 'users', perm: 'team' },
    { key: 'money', label: 'Money', href: '#/factory/money', icon: 'wallet', perm: 'money' },
    { key: 'settings', label: 'Settings', href: '#/factory/settings', icon: 'settings', perm: ['settings', 'intake'] },
    { key: 'activity', label: 'Activity', href: '#/factory/activity', icon: 'clock', perm: 'reports' },
  ];
  const allowed = (n, s) => !n.perm || [].concat(n.perm).some((p) => F().can(p, s));
  const ROLE_TONE = { owner: 'badge-blaze', supervisor: 'badge-turf', printer: 'badge-gold', qc: 'badge-info', packer: 'badge' };
  const STAGE_TONE = { new: 'badge-blaze', printing: 'badge-gold', qc: 'badge-info', packing: 'badge-turf', ready: 'badge-turf', shipped: 'badge-info', delivered: 'badge-turf', refused: 'badge-danger', cancelled: 'badge-danger' };
  const stageBadge = (st) => `<span class="badge ${STAGE_TONE[st] || ''}">${F().STAGE_LABEL[st] || st}</span>`;
  const hrs = (ms) => { const h = Math.abs(ms) / H; return h >= 24 ? `${Math.floor(h / 24)}d ${Math.round(h % 24)}h` : h >= 1 ? `${Math.round(h)}h` : `${Math.max(1, Math.round(h * 60))} min`; };
  const dueChip = (o) => (!F().ACTIVE.includes(o.stage) ? '' : o.late ? `<span class="fj-due is-late">${icon('alert', 12)} Late by ${hrs(o.left)}</span>` : `<span class="fj-due ${o.left < 6 * H ? 'is-soon' : ''}">${icon('clock', 12)} Due in ${hrs(o.left)}</span>`);
  // Badges for the side nav: open jobs for this person's part of the line, parcels waiting for pickup
  const MY_STAGES = { printer: ['new', 'printing'], qc: ['qc'], packer: ['packing'] };
  const badges = (s) => {
    const fid = s.factoryId;
    const mine = MY_STAGES[s.role] || F().ACTIVE;
    return { jobs: F().jobs({ factoryId: fid, stage: mine }).length, pickup: SA.services.shipping.list({ factoryId: fid, status: 'awaiting_pickup' }).length };
  };

  /* ---------- Sign-in: pick your name, type your PIN ---------- */
  const login = {
    title: 'Factory — Super App',
    render() {
      const fs = F().list();
      return `<div class="dash-login fac-login">
        <section class="dl-copy">
          ${brand('Factory')}
          <div class="dl-prefs">${SA.ui.shell.prefButtons()}</div>
          <span class="badge badge-gold">${icon('package', 13)} Production floor</span>
          <h1 class="display dl-title">Print it. Check it. Ship it.</h1>
          <p class="lead">Every printed order from Super App in one queue — job tickets with a QR code, quality checks, and the courier booked for you when an order is packed.</p>
          ${fs.map((f) => `<h2 class="h4" style="margin-top:26px">${esc(f.name)} <span class="xs muted">· ${esc(f.city)}</span></h2>
            <div class="dl-accounts fac-people">${F().staff(f.id).filter((x) => x.active).map((x, i) => `<button class="dl-acc" data-who="${x.id}" style="--i:${i}">${c.avatar({ name: x.name, avatar: x.avatar })}<span class="grow"><b>${esc(x.name)}</b><span class="xs muted" style="display:block">${F().ROLES[x.role].label}</span></span>${icon('lock', 15)}</button>`).join('')}</div>`).join('')}
          <p class="xs muted" style="margin-top:14px">${icon('info', 12)} Demo PINs: owner 1111 · supervisor 2222 · printers 3333 / 3344 · quality 4444 · packing 5555</p>
          <p class="small muted" style="margin-top:18px">Pitch owner or seller? <a class="link" href="#/dash">Partner dashboard</a> · Super App team? <a class="link" href="#/admin">Admin console</a></p>
        </section>
        <section class="dl-visual fac-visual" aria-hidden="true">
          <div class="fac-belt">
            <div class="float-card fac-f1">${icon('qr', 18)}<span><span class="xs">Job ticket</span><b class="num">PO-F702</b></span></div>
            <div class="float-card fac-f2">${icon('shield', 16)}<span><span class="xs">Quality check</span><b>Passed</b></span></div>
            <div class="float-card fac-f3">${icon('truck', 16)}<span><span class="xs">Courier booked</span><b class="num">BST-48120931</b></span></div>
            <div class="fac-core">${icon('shirt', 44)}</div>
          </div>
        </section>
      </div>`;
    },
    mount(el) {
      el.querySelectorAll('[data-who]').forEach((b) => b.addEventListener('click', () => pinPad(b.dataset.who)));
    },
  };
  const pinPad = (staffId) => {
    const x = SA.services.db.find('factoryStaff', staffId);
    let pin = '';
    SA.ui.overlay.open({
      label: 'Enter your PIN', title: esc(x.name), subtitle: `${F().ROLES[x.role].label} · enter your 4-digit PIN`,
      body: `<div class="pin">
        <div class="pin-dots" aria-live="polite" data-dots>${'<i></i>'.repeat(4)}</div>
        <p class="field-error" data-err style="text-align:center;min-height:1.2em"></p>
        <input class="sr-only" inputmode="numeric" maxlength="4" autocomplete="off" aria-label="PIN" data-pin-input />
        <div class="pin-keys">${[1, 2, 3, 4, 5, 6, 7, 8, 9, '', 0, '⌫'].map((k) => (k === '' ? '<span></span>' : `<button type="button" class="pin-key" data-k="${k}" aria-label="${k === '⌫' ? 'Delete' : k}">${k}</button>`)).join('')}</div>
      </div>`,
      onMount(m, close) {
        const dots = m.querySelector('[data-dots]'), err = m.querySelector('[data-err]'), inp = m.querySelector('[data-pin-input]');
        const paint = () => dots.querySelectorAll('i').forEach((d, i) => d.classList.toggle('on', i < pin.length));
        const press = (k) => {
          if (k === '⌫') pin = pin.slice(0, -1); else if (pin.length < 4 && /\d/.test(k)) pin += k;
          err.textContent = ''; paint();
          if (pin.length === 4) {
            try { F().auth.signIn(staffId, pin); close(); SA.ui.overlay.toast(`Welcome, ${esc(x.name.split(' ')[0])}`); const h = location.hash.slice(1); SA.core.router.go(h.startsWith('/factory/') ? h : '/factory', { replace: true }); SA.app.rerender(); }
            catch (e) { err.textContent = e.message; dots.classList.remove('shake'); void dots.offsetWidth; dots.classList.add('shake'); pin = ''; setTimeout(paint, 250); }
          }
        };
        m.querySelectorAll('[data-k]').forEach((b) => b.addEventListener('click', () => press(String(b.dataset.k))));
        inp.addEventListener('keydown', (e) => { if (/^\d$/.test(e.key)) press(e.key); if (e.key === 'Backspace') press('⌫'); e.preventDefault(); });
        setTimeout(() => inp.focus(), 60);
      },
    });
  };

  /* ---------- Layout ---------- */
  const brand = (sub) => `<a class="dash-brand" href="#/factory"><span class="brand-mark"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2.6" stroke-linecap="round"><path d="M17 7.2C15.9 6 14.3 5.4 12.4 5.4c-2.7 0-4.5 1.4-4.5 3.4 0 4.6 9.2 2.6 9.2 7 0 2-1.9 3.4-4.8 3.4-2.1 0-3.9-.7-5.3-2"/></svg></span><span><b>SUPER APP</b><span class="xs">${sub}</span></span></a>`;
  const sideNav = (active, s) => {
    const b = badges(s);
    const list = NAV.filter((n) => allowed(n, s));
    return `${brand('Factory')}
      <div class="dash-acct">${c.avatar({ name: s.name, avatar: s.avatar })}<span class="grow"><b class="small truncate">${esc(s.name)}</b><span class="xs truncate">${esc(s.factory.name)}</span></span><span class="badge ${ROLE_TONE[s.role]}">${F().ROLES[s.role].label}</span></div>
      <nav class="dash-nav" aria-label="Factory">${list.map((n, i) => (n.group ? `<span class="dash-nav-label xs">${n.group}</span>` : `<a href="${n.href}" style="--i:${i}" ${n.key === active ? 'aria-current="page"' : ''}>${icon(n.icon, 18)}<span class="grow">${n.label}</span><span class="nav-badge ${b[n.badge] ? 'nb-blaze' : ''}" ${b[n.badge] ? '' : 'hidden'}>${b[n.badge] || ''}</span></a>`)).join('')}</nav>
      <div class="dash-side-foot">
        <a href="#/" class="dash-foot-link">${icon('home', 16)} View the site</a>
        <button class="dash-foot-link" data-action="toggle-theme">${icon(SA.core.theme.current() === 'dark' ? 'sun' : 'moon', 16)} ${SA.core.theme.current() === 'dark' ? 'Light mode' : 'Dark mode'}</button>
        <button class="dash-foot-link" data-action="toggle-lang" data-i18n-skip>${icon('globe', 16)} ${SA.i18n.isAr ? 'English' : 'العربية'}</button>
        <button class="dash-foot-link" data-fac-signout>${icon('logout', 16)} Switch user</button>
      </div>`;
  };
  const bell = (s) => {
    const list = N()?.list(s.factory.accountId) || [];
    const unread = list.filter((n) => !n.read).length;
    return `<div class="bell-wrap"><button class="icon-btn dash-bell" data-bell aria-expanded="false" aria-label="Messages">${icon('bell', 19)}<span class="bell-dot" ${unread ? '' : 'hidden'}>${unread}</span></button>
      <div class="bell-pop" data-bell-pop hidden><div class="row-between"><b>Messages</b>${unread ? '<button class="link xs" data-read-all>Mark all as read</button>' : '<span class="xs muted">All read</span>'}</div>
      <ul>${list.slice(0, 8).map((n) => `<li><a href="${esc(n.link || '#/factory')}" class="bp-item ${n.read ? '' : 'is-unread'}" data-notif="${n.id}"><span class="bp-ico t-${n.tone || 'info'}">${icon(n.icon || 'bell', 16)}</span><span class="grow"><b class="small">${esc(n.title)}</b><span class="xs muted" style="display:block">${esc(n.body)}</span><span class="xs muted bp-time">${timeAgo(n.at)}</span></span></a></li>`).join('') || `<li class="bp-empty">${icon('bell', 18)}<span class="small">No messages yet.</span></li>`}</ul></div></div>`;
  };
  const layout = (active, { title, subtitle = '', actions = '', body }) => {
    const s = F().me();
    const f = s.factory;
    const banner = f.status === 'paused' ? `<div class="fac-banner is-danger">${icon('alert', 16)} Super App paused this factory${f.statusReason ? `: ${esc(f.statusReason)}` : ''}. No new jobs arrive until it is active again.</div>`
      : !f.intake ? `<div class="fac-banner is-gold">${icon('alert', 16)} New orders are paused${f.intakeNote ? ` — ${esc(f.intakeNote)}` : ''}. Printed products are hidden from the shop.${F().can('intake', s) ? ' <a class="link" href="#/factory/settings">Resume</a>' : ''}</div>` : '';
    return `<div class="dash is-factory" data-dash-active="${active}">
      <aside class="dash-side">${sideNav(active, s)}</aside>
      <section class="dash-main">
        <header class="dash-top">
          <button class="icon-btn dash-menu" data-fac-menu aria-label="Open the factory menu">${icon('list')}</button>
          <div class="grow dash-titles"><h1 class="h2 dash-title">${title}</h1>${subtitle ? `<p class="small muted">${subtitle}</p>` : ''}</div>
          <div class="dash-actions">${actions}<a class="btn btn-outline btn-sm fac-scan-btn" href="#/factory/scan">${icon('scan', 15)} Scan</a><span class="dash-prefs">${SA.ui.shell.prefButtons()}</span>${bell(s)}</div>
        </header>
        <div class="dash-body">${banner}${body}</div>
      </section>
    </div>`;
  };
  const bindLayout = (el) => {
    const out = () => { F().auth.signOut(); SA.ui.overlay.toast('Signed out — next person, sign in', { type: 'info' }); SA.core.router.go('/factory'); SA.app.rerender(); };
    el.querySelector('[data-fac-signout]')?.addEventListener('click', out);
    el.querySelector('[data-fac-menu]')?.addEventListener('click', () => {
      SA.ui.overlay.open({ drawer: true, side: 'left', label: 'Factory menu', body: `<div class="dash-drawer">${sideNav(el.querySelector('.dash')?.dataset.dashActive, F().me())}</div>`,
        onMount(m, close) { m.querySelectorAll('a').forEach((x) => x.addEventListener('click', close)); m.querySelector('[data-fac-signout]')?.addEventListener('click', () => { close(); out(); }); } });
    });
    const btn = el.querySelector('[data-bell]'), pop = el.querySelector('[data-bell-pop]');
    btn?.addEventListener('click', (e) => { e.stopPropagation(); pop.hidden = !pop.hidden; btn.setAttribute('aria-expanded', String(!pop.hidden)); pop.style.top = innerWidth < 760 ? `${Math.round(btn.getBoundingClientRect().bottom + 10)}px` : ''; });
    pop?.addEventListener('click', (e) => {
      if (e.target.closest('[data-read-all]')) { N()?.markAll(F().me().factory.accountId); SA.app.rerender(); return; }
      const n = e.target.closest('[data-notif]'); if (n) N()?.markRead(n.dataset.notif);
    });
    const onDoc = (e) => { if (pop && !pop.hidden && !pop.contains(e.target) && !btn.contains(e.target)) pop.hidden = true; };
    document.addEventListener('click', onDoc);
    // New jobs and courier updates show up without a reload
    let t = 0;
    const off = SA.services.db.subscribe((col) => {
      if (!['podOrders', 'shipments', 'notifications', 'factories', '*'].includes(col)) return;
      clearTimeout(t);
      t = setTimeout(() => { if (!document.querySelector('.overlay, dialog[open]') && document.activeElement?.tagName !== 'INPUT' && document.activeElement?.tagName !== 'TEXTAREA' && document.activeElement?.tagName !== 'SELECT') SA.app.rerender(); }, 400);
    });
    return () => { off(); clearTimeout(t); document.removeEventListener('click', onDoc); };
  };

  /** Wrap a factory page: sign-in first, then the role check */
  const guard = (page) => ({
    ...page,
    title: page.title || 'Factory — Super App',
    async render(ctx) {
      const s = F().me();
      if (!s) { this._login = true; return login.render(ctx); }
      this._login = false;
      if (page.perm && ![].concat(page.perm).some((p) => F().can(p, s))) { this._denied = true; return layout('', { title: 'Not for your role', body: `<div class="dash-card">${c.empty({ icon: 'lock', title: 'Ask your supervisor', text: 'This part of the factory system belongs to another role.', action: '<a class="btn btn-primary" href="#/factory/jobs">Go to production</a>' })}</div>` }); }
      this._denied = false;
      try { return await page.render.call(page, ctx, s); }
      catch (err) { if (err.code !== 'not_found') throw err; this._denied = true; return layout('', { title: 'Not found', body: `<div class="dash-card">${c.empty({ icon: 'search', title: 'We couldn’t find that', text: esc(err.message), action: '<a class="btn btn-primary" href="#/factory/scan">Scan another job</a>' })}</div>` }); }
    },
    mount(el, ctx) {
      if (this._login) return login.mount(el, ctx);
      const offs = [bindLayout(el), this._denied ? null : page.mount?.call(page, el, ctx, F().me())];
      return () => offs.forEach((f) => typeof f === 'function' && f());
    },
  });

  /* ---------- Jobs ---------- */
  const mock = (o, side, size = '') => {
    const d = o.design; if (!d) return '';
    const col = d.product?.colors.find((k) => k.name === o.color) || d.product?.colors[0];
    return `<div class="pr-mock ${size}">${SA.ui.garmentView({ kind: d.product?.kind, hex: col?.hex, art: d.src, place: d.placement?.[side], side, artRatio: d.artW ? d.artH / d.artW : 1, label: `${d.title} ${side}` })}<span class="xs muted">${side === 'front' ? 'Front' : 'Back'}</span></div>`;
  };
  const sides = (o) => (o.design ? ['front', 'back'].filter((x) => o.design.placement?.[x]) : []);
  /** The buttons this person may press on a job right now */
  const actions = (o, s, { big = false } = {}) => {
    const nx = F().NEXT[o.stage];
    const sz = big ? 'btn-lg' : 'btn-sm';
    const out = [];
    if (o.status !== 'cancelled' && nx && F().can(nx.perm, s)) out.push(`<button class="btn btn-primary ${sz}" data-move="${o.id}" data-to="${nx.to}">${icon(nx.icon, big ? 18 : 13)} ${nx.label}</button>`);
    if (o.stage === 'qc' && F().can('qc', s)) out.push(`<button class="btn btn-outline ${sz}" data-qcfail="${o.id}" style="color:var(--danger)">${icon('x', big ? 16 : 13)} Failed — reprint</button>`);
    return out.join('');
  };
  const jobCard = (o, s, { compact = false } = {}) => {
    const sameOrder = SA.services.db.all('podOrders').filter((x) => x.orderId === o.orderId && x.factoryId === o.factoryId && x.status !== 'cancelled');
    return `<article class="dash-card pr-item fj ${o.late ? 'is-late' : ''} ${o.issue && !o.issue.resolved ? 'has-issue' : ''}" data-job="${o.id}">
      <a class="pr-media" href="#/factory/job/${o.id}" aria-label="Open ${o.id}">${sides(o).map((x) => mock(o, x)).join('')}</a>
      <div class="pr-body">
        <div class="row-between" style="--gap:8px"><a class="num strong" href="#/factory/job/${o.id}">${esc(o.id)}</a><span class="row" style="--gap:6px">${o.sample ? '<span class="badge badge-gold">Sample</span>' : ''}${stageBadge(o.stage)}</span></div>
        <p class="small strong">${o.qty} × ${esc(o.product?.name || o.catalogId)} · ${esc(o.color)} · ${esc(o.size)}</p>
        <p class="xs muted">“${esc(o.title)}” · ${esc(o.orderId)}${sameOrder.length > 1 ? ` (${sameOrder.length} items)` : ''} · ${esc(o.customer?.city || '')}${o.reprints ? ` · reprinted ${o.reprints}×` : ''}</p>
        <div class="row wrap" style="--gap:6px;margin-top:4px">${dueChip(o)}${o.assignee ? `<span class="fj-who">${icon('user', 12)} ${esc(o.assignee.name)}</span>` : ''}</div>
        ${o.issue && !o.issue.resolved ? `<p class="fj-issue xs">${icon('alert', 12)} <b>${esc(o.issue.type)}</b>${o.issue.note ? ` — ${esc(o.issue.note)}` : ''} <span class="muted">· ${esc(o.issue.by)}</span></p>` : ''}
        ${o.status === 'cancelled' ? `<p class="fj-issue xs">${icon('x', 12)} Cancelled by Super App — stop work on it.</p>` : ''}
        ${compact ? '' : `<div class="pr-actions">${actions(o, s)}${o.design ? `<button class="btn btn-ghost btn-sm" data-file="${o.id}">${icon('file', 13)} Print file</button>` : ''}<button class="btn btn-ghost btn-sm" data-ticket="${o.id}">${icon('qr', 13)} Ticket</button></div>`}
      </div></article>`;
  };

  /** Print file: the artwork plus a JSON sheet with the exact placement (DTF transfer) */
  const download = (o) => {
    const d = o.design || P().get(o.designId); if (!d) return;
    const job = { job: o.id, order: o.orderId, product: d.product?.name, technique: 'DTF', colour: o.color, size: o.size, qty: o.qty,
      placement: Object.fromEntries(['front', 'back'].filter((x) => d.placement?.[x]).map((x) => [x, { centreX: d.placement[x].x, centreY: d.placement[x].y, width: d.placement[x].w, unit: 'fraction of the 400×440 garment template' }])),
      artwork: { width: d.artW || 'vector', height: d.artH || 'vector' } };
    const ext = /^data:image\/(\w+)/.exec(d.src)?.[1]?.replace('svg+xml', 'svg') || 'png';
    const a = document.createElement('a'); a.href = d.src; a.download = `${o.id}-artwork.${ext}`; a.click();
    const b = document.createElement('a'); b.href = URL.createObjectURL(new Blob([JSON.stringify(job, null, 2)], { type: 'application/json' })); b.download = `${o.id}-job.json`; b.click();
    setTimeout(() => URL.revokeObjectURL(b.href), 2000);
  };

  /* ---------- PDFs: job ticket (goes with the garment) and shipping label (on the parcel) ---------- */
  const INK = '#0D1411', SOFT = '#46514B', TURF = '#0B6E4F', BLAZE = '#FF5B14';
  const ticketPdf = async (ids) => {
    const doc = SA.core.pdf.create({ title: `Job tickets ${ids[0]}` });
    ids.forEach((id) => {
      const o = F().job(id); const p = doc.page();
      p.rect(0, 0, p.W, 70, { fill: INK });
      p.logo(40, 20, 28);
      p.text('SUPER APP · JOB TICKET', 78, 40, { size: 13, weight: 800, color: '#fff', spacing: 1 });
      p.text(`${o.product?.name || ''} · DTF`, p.W - 40, 40, { size: 11, color: '#9FB3A9', align: 'right' });
      p.text(o.id, 40, 130, { size: 40, weight: 800 });
      p.qr(o.id, p.W - 40 - 150, 92, 150);
      p.text(`${o.qty} × ${o.color} · size ${o.size}`, 40, 168, { size: 18, weight: 700, color: TURF });
      p.text(`“${o.title}”`, 40, 194, { size: 12, color: SOFT, maxWidth: 330 });
      const rows = [['Order', `${o.orderId}${o.sample ? ' · SAMPLE for the seller' : ''}`], ['Placed', new Date(o.placedAt).toLocaleString('en-GB')], ['Due to ship', new Date(o.dueAt).toLocaleString('en-GB')],
        ['Print', sides(o).map((x) => `${x} (centre ${Math.round(o.design.placement[x].x * 100)}% / ${Math.round(o.design.placement[x].y * 100)}%, width ${Math.round(o.design.placement[x].w * 100)}%)`).join(' + ') || '—'], ['Ship to', `${o.customer?.name || ''} · ${o.customer?.city || ''}`]];
      rows.forEach(([k, v], i) => { p.text(k.toUpperCase(), 40, 270 + i * 34, { size: 8, weight: 700, color: TURF, spacing: 0.8 }); p.text(v, 40, 286 + i * 34, { size: 11, maxWidth: p.W - 80 }); });
      p.rect(40, 460, p.W - 80, 150, { stroke: INK, lw: 1, r: 8 });
      ['Printed by', 'Quality check', 'Packed by'].forEach((k, i) => { p.text(k, 56 + i * ((p.W - 112) / 3), 486, { size: 9, color: SOFT }); p.line(56 + i * ((p.W - 112) / 3), 590, 40 + (i + 1) * ((p.W - 112) / 3), 590); });
      p.text('Scan the QR code on the factory tablet to move this job to the next step.', 40, 650, { size: 9, color: SOFT });
    });
    await doc.save(`job-tickets-${ids[0]}${ids.length > 1 ? `-and-${ids.length - 1}-more` : ''}.pdf`);
    SA.ui.overlay.toast(`Downloaded ${ids.length} ticket${ids.length > 1 ? 's' : ''}`, { type: 'success' });
  };
  const labelPdf = async (shId) => {
    const sh = SA.services.shipping.get(shId);
    const f = F().get(sh.factoryId);
    const items = sh.items.map((id) => SA.services.db.find('podOrders', id)).filter(Boolean);
    const doc = SA.core.pdf.create({ title: `Label ${sh.awb}` });
    const p = doc.page();
    p.rect(30, 30, p.W - 60, p.H - 60, { stroke: INK, lw: 2, r: 10 });
    p.text(sh.providerName.toUpperCase(), 50, 74, { size: 26, weight: 800 });
    if (sh.mode === 'test') p.text('TEST SHIPMENT', p.W - 50, 74, { size: 11, weight: 800, color: BLAZE, align: 'right' });
    p.qr(sh.awb, 50, 100, 140);
    p.text('AIRWAY BILL', 210, 116, { size: 8, weight: 700, color: TURF, spacing: 1 });
    p.text(sh.awb, 210, 146, { size: 24, weight: 800 });
    p.text(`Order ${sh.orderId} · ${items.reduce((t, o) => t + o.qty, 0)} item(s)`, 210, 170, { size: 11, color: SOFT });
    p.text(sh.cod ? `COLLECT ${sh.cod.toLocaleString('en-US')} EGP` : 'PAID — collect nothing', 210, 214, { size: 18, weight: 800, color: sh.cod ? BLAZE : TURF });
    p.line(50, 270, p.W - 50, 270);
    p.text('SHIP TO', 50, 300, { size: 8, weight: 700, color: TURF, spacing: 1 });
    p.text(sh.customer?.name || 'Customer', 50, 330, { size: 22, weight: 800 });
    p.text(sh.customer?.address || '', 50, 356, { size: 13, color: SOFT, maxWidth: p.W - 100 });
    p.text(`${sh.customer?.city || ''}${sh.customer?.phone ? ` · ${sh.customer.phone}` : ''}`, 50, 378, { size: 13, color: SOFT });
    p.line(50, 410, p.W - 50, 410);
    p.text('FROM', 50, 440, { size: 8, weight: 700, color: TURF, spacing: 1 });
    p.text(`${f?.name || ''} for Super App`, 50, 462, { size: 12, weight: 700 });
    p.text(`${f?.address || ''} · ${f?.phone || ''}`, 50, 480, { size: 10, color: SOFT, maxWidth: p.W - 100 });
    items.forEach((o, i) => p.text(`☐ ${o.id} — ${o.qty} × ${o.title} (${o.color}, ${o.size})`, 50, 530 + i * 18, { size: 10 }));
    await doc.save(`label-${sh.awb}.pdf`);
    SA.ui.overlay.toast(`Downloaded the label ${sh.awb}`, { type: 'success' });
  };

  /** One delegated handler for job buttons on any factory page */
  const bindJobs = (el) => {
    const go = (fn, msg) => { try { fn(); if (msg) SA.ui.overlay.toast(msg); SA.app.rerender(); } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); } };
    const onClick = async (e) => {
      const mv = e.target.closest('[data-move]');
      if (mv) {
        try {
          const o = F().move(mv.dataset.move, mv.dataset.to);
          SA.ui.overlay.toast(mv.dataset.to !== 'ready' ? `${o.id} → ${F().STAGE_LABEL[mv.dataset.to]}` : o.shipmentId ? `Packed — ${SA.services.shipping.get(o.shipmentId).providerName} booked for ${o.orderId}` : 'Packed — waiting for the rest of the order', { type: 'success' });
          SA.app.rerender();
        } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
        return;
      }
      const qf = e.target.closest('[data-qcfail]');
      if (qf) {
        const r = await SA.pages.adminCommon?.reason?.({ title: `Failed quality check · ${qf.dataset.qcfail}`, text: 'It goes back to printing. Reprints are on the factory and count in the defect rate.', label: 'What is wrong?', placeholder: 'e.g. print is off-centre, colour faded, stain on the sleeve', confirmLabel: 'Send back to printing', danger: true });
        if (r) go(() => F().qcFail(qf.dataset.qcfail, r.reason), 'Sent back to printing');
        return;
      }
      const fl = e.target.closest('[data-file]'); if (fl) return download(F().job(fl.dataset.file));
      const tk = e.target.closest('[data-ticket]'); if (tk) return ticketPdf(tk.dataset.ticket.split(','));
      const lb = e.target.closest('[data-label]'); if (lb) return labelPdf(lb.dataset.label);
    };
    el.addEventListener('click', onClick);
    return () => el.removeEventListener('click', onClick);
  };

  SA.pages.factoryLogin = login;
  SA.pages.factoryCommon = { NAV, layout, guard, jobCard, actions, mock, sides, stageBadge, dueChip, hrs, bindJobs, ticketPdf, labelPdf, download, MY_STAGES, ROLE_TONE };
})(window.SA);
