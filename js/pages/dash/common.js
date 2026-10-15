/** Partner Dashboard: shared layout, guard, sign-in screen, charts and form helpers. */
(function (SA) {
  const { esc, money, isoDate, addDays, today, parseISO, fmtDate, MON } = SA.core.utils;
  const { icon } = SA.ui;
  const c = SA.ui.c;
  const D = () => SA.services.dash;

  const NAV = [
    { key: 'overview', label: 'Overview', href: '#/dash', icon: 'grid' },
    { group: 'Pitches' },
    { key: 'pitches', label: 'My pitches', href: '#/dash/pitches', icon: 'pitch' },
    { key: 'availability', label: 'Availability', href: '#/dash/availability', icon: 'calendar' },
    { key: 'checkin', label: 'Check-in', href: '#/dash/checkin', icon: 'qr', badge: 'arrivals' },
    { key: 'bookings', label: 'Bookings', href: '#/dash/bookings', icon: 'ticket' },
    { key: 'teams', label: 'Teams & memberships', href: '#/dash/teams', icon: 'repeat' },
    { key: 'reviews', label: 'Reviews', href: '#/dash/reviews', icon: 'star', badge: 'reviews' },
    { key: 'tournaments', label: 'Tournaments', href: '#/dash/tournaments', icon: 'trophy', badge: 'tournaments' },
    { group: 'Money' },
    { key: 'finance', label: 'Finance', href: '#/dash/finance', icon: 'percent' },
    { key: 'wallet', label: 'Wallet', href: '#/dash/wallet', icon: 'wallet' },
    { key: 'insights', label: 'Forecast & offers', href: '#/dash/insights', icon: 'trend' },
    { key: 'codes', label: 'Discount codes', href: '#/dash/codes', icon: 'tag' },
    { group: 'Store' },
    { key: 'store', label: 'My store', href: '#/dash/store', icon: 'bag', badge: 'stock' },
    { key: 'orders', label: 'Orders', href: '#/dash/orders', icon: 'package', badge: 'orders' },
    { group: '' },
    { key: 'activity', label: 'Activity', href: '#/dash/activity', icon: 'clock' },
  ];
  // Roles that can sign in to the partner dashboard (admins use the separate admin console at #/admin)
  const SIGNIN_ROLES = ['owner', 'seller', 'printer'];
  const isAdminUI = () => D().adminMode();
  const A = () => SA.pages.adminCommon;
  /** Pages shared with the admin console keep working there: their dashboard links point to the admin routes */
  const ADMIN_LINKS = [['#/dash/notifications', '#/admin/notifications'], ['#/dash/pod-review', '#/admin/designs'], ['#/dash/pod-catalog', '#/admin/catalog'], ['#/dash/print', '#/admin/print'],
    ['#/dash/settings', '#/admin/settings'], ['#/dash/partners', '#/admin/partners'], ['#/dash/reviews', '#/admin/reviews'], ['#/dash/activity', '#/admin/audit'], ['#/dash/store', '#/admin/store'], ['#/dash/orders', '#/admin/store-orders']];
  const adminLinks = (html) => ADMIN_LINKS.reduce((h, [a, b]) => h.split(a).join(b), html);
  const ROLE_LABEL = { admin: ['badge-blaze', 'Admin'], owner: ['badge-turf', 'Owner'], printer: ['badge-gold', 'Print partner'], seller: ['badge-turf', 'Seller'] };
  /** Navigation per role; pages outside it send the user to the role's home page */
  const ROLE_NAV = {
    printer: [{ key: 'print', label: 'Print queue', href: '#/dash/print', icon: 'package', badge: 'print' }],
    seller: (a) => {
      const s = SA.services.sellers?.byAccount(a.id);
      return [
        { key: 'seller', label: 'Overview', href: '#/dash/seller', icon: 'grid' },
        { key: 'seller-store', label: 'Store settings', href: '#/dash/seller/store', icon: 'settings' },
        { key: 'seller-calendar', label: 'Marketing calendar', href: '#/dash/seller/calendar', icon: 'calendar' },
        { key: 'seller-analytics', label: 'Analytics', href: '#/dash/seller/analytics', icon: 'chart' },
        ...(s?.mode !== 'store' ? [{ group: 'Print on demand' }, { key: 'seller-designs', label: 'Designs', href: '#/dash/seller/designs', icon: 'image' }, { key: 'seller-orders', label: 'Print orders', href: '#/dash/seller/orders', icon: 'package' }] : []),
        ...(s?.storeId ? [{ group: 'My store' }, { key: 'store', label: 'Products', href: '#/dash/store', icon: 'bag', badge: 'stock' }, { key: 'orders', label: 'Store orders', href: '#/dash/orders', icon: 'truck', badge: 'orders' }] : []),
        { group: 'Money' }, { key: 'seller-wallet', label: 'Wallet', href: '#/dash/seller/wallet', icon: 'wallet' }, ...(s?.storeId ? [{ key: 'codes', label: 'Discount codes', href: '#/dash/codes', icon: 'tag' }] : []),
      ];
    },
  };
  const ROLE_HOME = { printer: () => SA.pages.dashPrint, seller: () => SA.pages.dashSeller };
  const navFor = (a) => { if (isAdminUI()) return A().live(); const n = ROLE_NAV[a.role]; return typeof n === 'function' ? n(a) : n || NAV; };

  /* ---------- Admin owner filter (persists for the session) ---------- */
  const ownerFilter = () => { try { return sessionStorage.getItem('dash.owner') || ''; } catch { return ''; } };
  const setOwnerFilter = (v) => { try { sessionStorage.setItem('dash.owner', v); } catch { /* ignore */ } };
  const scope = () => (D().isAdmin() && !isAdminUI() ? { ownerId: ownerFilter() || undefined } : {});

  /* ---------- Date ranges ---------- */
  const PRESETS = [['7', 'Last 7 days'], ['30', 'Last 30 days'], ['90', 'Last 90 days'], ['month', 'This month'], ['lastmonth', 'Last month']];
  const range = (q, def = '30') => {
    const t = today();
    if (q.from && q.to && q.from <= q.to) return { from: q.from, to: q.to, preset: 'custom' };
    const p = q.range || def;
    if (p === 'month') return { from: isoDate(new Date(t.getFullYear(), t.getMonth(), 1)), to: isoDate(t), preset: p };
    if (p === 'lastmonth') return { from: isoDate(new Date(t.getFullYear(), t.getMonth() - 1, 1)), to: isoDate(new Date(t.getFullYear(), t.getMonth(), 0)), preset: p };
    const n = Math.max(1, +p || 30);
    return { from: isoDate(addDays(t, -(n - 1))), to: isoDate(t), preset: String(n) };
  };
  const rangeLabel = (r) => `${fmtDate(r.from)} – ${fmtDate(r.to)}`;
  const rangePicker = (r, base, extra = {}) => `<div class="dr">
      <div class="chips" role="group" aria-label="Date range">${PRESETS.map(([v, l]) => `<a class="chip" href="${SA.core.router.href(base, { ...extra, range: v })}" aria-pressed="${r.preset === v}">${l}</a>`).join('')}</div>
      <form class="dr-custom" data-range-form><input class="input" type="date" name="from" value="${r.from}" aria-label="From" /><span class="muted">→</span><input class="input" type="date" name="to" value="${r.to}" aria-label="To" /><button class="btn btn-outline btn-sm" type="submit">Apply</button></form>
    </div>`;
  const bindRange = (el, base, extra = {}) => el.querySelector('[data-range-form]')?.addEventListener('submit', (e) => {
    e.preventDefault();
    const f = SA.core.utils.formData(e.target);
    if (!f.from || !f.to || f.to < f.from) return SA.ui.overlay.toast('Choose a start date before the end date.', { type: 'error' });
    SA.core.router.go(SA.core.router.href(base, { ...extra, from: f.from, to: f.to }).slice(1));
  });

  /* ---------- Layout ---------- */
  /** Action items for the bell and sidebar badges */
  const alertsNow = () => { try { return isAdminUI() ? SA.services.admin.alerts() : D().alerts(scope()); } catch (e) { console.warn(e); return []; } };
  const navLink = (n, i, active, al) => {
    const b = n.badge && al.find((x) => x.key === n.badge);
    return `<a href="${n.href}" style="--i:${i}" ${n.key === active ? 'aria-current="page"' : ''}>${icon(n.icon, 18)}<span class="grow">${n.label}</span><span class="nav-badge ${b ? `nb-${b.tone}` : ''}" data-badge="${n.badge || ''}" ${b ? '' : 'hidden'}>${b ? b.count : ''}</span></a>`;
  };
  const sideNav = (active, a, al = alertsNow()) => { const adm = isAdminUI(), home = adm ? '#/admin' : '#/dash'; return `
    <a class="dash-brand" href="${home}"><span class="brand-mark"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2.6" stroke-linecap="round"><path d="M17 7.2C15.9 6 14.3 5.4 12.4 5.4c-2.7 0-4.5 1.4-4.5 3.4 0 4.6 9.2 2.6 9.2 7 0 2-1.9 3.4-4.8 3.4-2.1 0-3.9-.7-5.3-2"/></svg></span><span><b>SUPER APP</b><span class="xs">${adm ? 'Admin console' : 'Partner dashboard'}</span></span></a>
    <div class="dash-acct">${c.avatar({ name: a.name, avatar: a.avatar })}<span class="grow"><b class="small truncate">${esc(a.name)}</b><span class="xs truncate">${esc(a.company)}</span></span><span class="badge ${(ROLE_LABEL[a.role] || ROLE_LABEL.owner)[0]}">${(ROLE_LABEL[a.role] || ROLE_LABEL.owner)[1]}</span></div>
    <nav class="dash-nav" aria-label="Dashboard">
      ${navFor(a).map((n, i) => (n.group !== undefined ? (n.group ? `<span class="dash-nav-label xs">${n.group}</span>` : '<span class="dash-nav-sep"></span>') : navLink(n, i, active, al))).join('')}
    </nav>
    ${(() => { const u = unreadNow(); return `<nav class="dash-nav dash-nav-notes" aria-label="Notifications"><a href="${home}/notifications" ${active === 'notifications' ? 'aria-current="page"' : ''}>${icon('bell', 18)}<span class="grow">Notifications</span><span class="nav-badge ${u ? 'nb-blaze' : ''}" data-badge="notifications" ${u ? '' : 'hidden'}>${u || ''}</span></a>${adm ? '' : `<a href="#/dash/support" ${active === 'support' ? 'aria-current="page"' : ''}>${icon('help', 18)}<span class="grow">Help & support</span></a>`}</nav>`; })()}
    <div class="dash-side-foot">
      <a href="#/" class="dash-foot-link">${icon('home', 16)} View the site</a>
      <button class="dash-foot-link" data-action="toggle-theme">${icon(SA.core.theme.current() === 'dark' ? 'sun' : 'moon', 16)} ${SA.core.theme.current() === 'dark' ? 'Light mode' : 'Dark mode'}</button>
      <button class="dash-foot-link" data-action="toggle-lang" data-i18n-skip>${icon('globe', 16)} ${SA.i18n.isAr ? 'English' : 'العربية'}</button>
      <button class="dash-foot-link" data-dash-signout>${icon('logout', 16)} Sign out</button>
    </div>`; };

  // Bell = unread notifications (SA.services.notify) + to-do items (dashService.alerts)
  const N = () => SA.services.notify;
  const unreadNow = () => { const a = D().me(); return a && N() ? N().unread(a.id) : 0; };
  const bellCount = (al) => al.reduce((t, x) => t + x.count, 0) + unreadNow();
  const notifItem = (n) => `<li><a href="${esc(n.link || '#/dash/notifications')}" class="bp-item ${n.read ? '' : 'is-unread'}" data-notif="${n.id}"><span class="bp-ico t-${n.tone || 'info'}">${icon(n.icon || 'bell', 16)}</span><span class="grow"><b class="small">${esc(n.title)}</b><span class="xs muted" style="display:block">${esc(n.body)}</span><span class="xs muted bp-time">${SA.core.utils.timeAgo(n.at)}</span></span>${n.read ? '' : '<i class="bp-unread" aria-label="Unread"></i>'}</a></li>`;
  const bellPop = (al) => {
    const a = D().me();
    const notes = a && N() ? N().list(a.id).slice(0, 6) : [];
    const unread = unreadNow();
    return `<div class="row-between"><b>Notifications</b>${unread ? `<button class="link xs" data-read-all>Mark all as read</button>` : '<span class="xs muted">All read</span>'}</div>
      <ul>${notes.map(notifItem).join('') || `<li class="bp-empty">${icon('bell', 18)}<span class="small">No notifications yet.</span></li>`}</ul>
      ${al.length ? `<div class="row-between bp-sec"><b>To do</b><span class="xs muted">${al.reduce((t, x) => t + x.count, 0)}</span></div>
      <ul>${al.map((x) => `<li><a href="${x.href}" class="bp-item"><span class="bp-ico t-${x.tone}">${icon(x.icon, 16)}</span><span class="grow"><b class="small">${esc(x.title)}</b><span class="xs muted" style="display:block">${esc(x.text)}</span></span>${icon('chevronRight', 14)}</a></li>`).join('')}</ul>` : ''}
      <a class="btn btn-ghost btn-sm btn-block bp-all" href="${isAdminUI() ? '#/admin' : '#/dash'}/notifications">See all notifications</a>`;
  };
  const bell = (al) => `<div class="bell-wrap"><button class="icon-btn dash-bell" data-bell aria-expanded="false" aria-haspopup="true" aria-label="Notifications">${icon('bell', 19)}<span class="bell-dot" ${bellCount(al) ? '' : 'hidden'}>${bellCount(al)}</span></button>
      <div class="bell-pop" data-bell-pop hidden>${bellPop(al)}</div></div>`;
  const layout = (active, { title, subtitle = '', actions = '', body }) => {
    const a = D().me();
    const al = alertsNow();
    const adm = isAdminUI();
    const owners = D().isAdmin() && !adm ? SA.services.db.all('accounts').filter((x) => x.role === 'owner') : [];
    const out = `<div class="dash ${adm ? 'is-admin' : ''}" data-dash-active="${active}">
      <aside class="dash-side">${sideNav(active, a, al)}</aside>
      <section class="dash-main">
        <header class="dash-top">
          <button class="icon-btn dash-menu" data-dash-menu aria-label="Open dashboard menu">${icon('list')}</button>
          <div class="grow dash-titles"><h1 class="h2 dash-title">${title}</h1>${subtitle ? `<p class="small muted">${subtitle}</p>` : ''}</div>
          ${owners.length ? `<label class="dash-owner"><span class="xs muted">Viewing</span><select class="select" data-owner-filter>${[['', 'All partners'], ...owners.map((o) => [o.id, `${o.name} · ${o.company}`])].map(([v, l]) => `<option value="${v}" ${ownerFilter() === v ? 'selected' : ''}>${esc(l)}</option>`).join('')}</select></label>` : ''}
          <div class="dash-actions">${actions}<span class="dash-prefs">${SA.ui.shell.prefButtons()}</span>${bell(al)}</div>
        </header>
        <div class="dash-body">${body}</div>
      </section>
    </div>`;
    return adm ? adminLinks(out) : out;
  };

  /** Wire the common layout behaviours; returns cleanup */
  const bindLayout = (el) => {
    const home = isAdminUI() ? '/admin' : '/dash';
    el.querySelector('[data-dash-signout]')?.addEventListener('click', () => { const adm = isAdminUI(); D().auth.signOut(); SA.ui.overlay.toast(adm ? 'Signed out of the admin console' : 'Signed out of the partner dashboard', { type: 'info' }); SA.core.router.go(home); });
    el.querySelector('[data-owner-filter]')?.addEventListener('change', (e) => { setOwnerFilter(e.target.value); SA.app.rerender(); });
    el.querySelector('[data-dash-menu]')?.addEventListener('click', () => {
      const a = D().me();
      SA.ui.overlay.open({ drawer: true, side: 'left', label: 'Dashboard menu', body: `<div class="dash-drawer ${isAdminUI() ? 'is-admin' : ''}">${(isAdminUI() ? adminLinks : (x) => x)(sideNav(el.querySelector('.dash')?.dataset.dashActive, a))}</div>`,
        onMount(m, close) { m.querySelectorAll('a').forEach((x) => x.addEventListener('click', close)); m.querySelector('[data-dash-signout]')?.addEventListener('click', () => { close(); D().auth.signOut(); SA.core.router.go(home); }); } });
    });
    // Bell dropdown
    const btn = el.querySelector('[data-bell]'), pop = el.querySelector('[data-bell-pop]');
    const closePop = () => { if (pop && !pop.hidden) { pop.hidden = true; btn.setAttribute('aria-expanded', 'false'); } };
    btn?.addEventListener('click', (e) => {
      e.stopPropagation(); pop.hidden = !pop.hidden; btn.setAttribute('aria-expanded', String(!pop.hidden));
      // Phones: pin the panel under the top bar, full width
      pop.style.top = innerWidth < 760 ? `${Math.round(btn.getBoundingClientRect().bottom + 10)}px` : '';
    });
    pop?.addEventListener('click', (e) => {
      if (e.target.closest('[data-read-all]')) { e.preventDefault(); N()?.markAll(D().me().id); return; }
      const n = e.target.closest('[data-notif]'); if (n) { N()?.markRead(n.dataset.notif); closePop(); }
    });
    const onDoc = (e) => { if (pop && !pop.contains(e.target)) closePop(); };
    const onKey = (e) => { if (e.key === 'Escape') closePop(); };
    document.addEventListener('click', onDoc); document.addEventListener('keydown', onKey);
    // Live updates: bookings and orders made on the site (in this tab or another one) show up here
    const db = SA.services.db;
    const seen = { sales: new Set(db.all('sales').map((x) => x.id)), bookings: new Set(db.all('bookings').map((x) => x.id)), notes: new Set((db.all('notifications') || []).map((x) => x.id)) };
    const mine = () => { try { const sc = scope(); return { pitches: new Set(D().myPitches(sc).map((p) => p.id)), stores: new Set(D().myStores(sc).map((x) => x.id)) }; } catch { return { pitches: new Set(), stores: new Set() }; } };
    let t = 0;
    const off = db.subscribe((col) => {
      if (!D().me() || !['sales', 'bookings', 'reviews', 'products', 'notifications', 'podOrders', 'designs', '*'].includes(col)) return;
      const m = mine();
      // New notifications for this account pop up as toasts (also from another tab) — and as a system notification if allowed
      if (col === 'notifications' || col === '*') db.all('notifications').forEach((x) => {
        if (seen.notes.has(x.id)) return; seen.notes.add(x.id);
        if (x.to !== D().me().id || x.read) return;
        SA.ui.overlay.toast(x.title, { action: x.link ? 'View' : '', onAction: () => { N()?.markRead(x.id); if (x.link) location.hash = x.link; } });
        try { if (document.hidden && 'Notification' in window && Notification.permission === 'granted' && N()?.prefs(D().me().id).browser) new Notification(x.title, { body: x.body, tag: x.id }); } catch { /* not supported */ }
      });
      if (col === 'sales' || col === '*') db.all('sales').forEach((x) => { seen.sales.add(x.id); });
      if (col === 'bookings' || col === '*') db.all('bookings').forEach((x) => { if (seen.bookings.has(x.id)) return; seen.bookings.add(x.id); if (m.pitches.has(x.pitchId) && x.source === 'app') SA.ui.overlay.toast(`New booking ${x.id} — ${SA.core.utils.fmtDate(x.date)} at ${SA.core.utils.fmtHour(x.hour)}`, { action: 'View', onAction: () => SA.pages.dashBookings.openBooking(x.id) }); });
      // Refresh badges and the bell without re-rendering the page (batched)
      clearTimeout(t);
      t = setTimeout(() => {
        const al = alertsNow();
        document.querySelectorAll('[data-badge]').forEach((n) => { const k = n.dataset.badge; const b = k && al.find((x) => x.key === k); n.hidden = !b; n.textContent = b ? b.count : ''; n.className = `nav-badge ${b ? `nb-${b.tone}` : ''}`; });
        const dot = el.querySelector('.bell-dot'); const total = bellCount(al);
        if (dot) { dot.hidden = !total; dot.textContent = total; }
        if (pop) pop.innerHTML = isAdminUI() ? adminLinks(bellPop(al)) : bellPop(al);
        document.querySelectorAll('[data-badge="notifications"]').forEach((n) => { const u = unreadNow(); n.hidden = !u; n.textContent = u || ''; n.className = `nav-badge ${u ? 'nb-blaze' : ''}`; });
      }, 120);
    });
    return () => { off(); clearTimeout(t); document.removeEventListener('click', onDoc); document.removeEventListener('keydown', onKey); };
  };

  /** Wrap a dashboard page so it requires a signed-in partner */
  const guard = (page) => ({
    ...page,
    title: page.title || 'Partner dashboard — Super App',
    async render(ctx) {
      if (!D().me()) return (isAdminUI() ? SA.pages.adminLogin : SA.pages.dashLogin).render(ctx);
      // Roles with their own area (print partner, seller) only see their pages; activity is shared
      const home = ROLE_HOME[D().me().role]?.();
      const allow = this.roles || page.roles || [];
      this._as = home && !(typeof allow === 'function' ? allow(D().me()) : allow.includes(D().me().role)) ? home : null;
      if (this._as) return this._as.render(ctx);
      try { return await page.render.call(page, ctx); }
      catch (err) {
        if (!['forbidden', 'not_found'].includes(err.code)) throw err;
        this._failed = true;
        return layout(ctx.path.split('/')[2] || 'overview', { title: err.code === 'forbidden' ? 'No access' : 'Not found', body: `<div class="dash-card">${SA.ui.c.empty({ icon: 'lock', title: err.code === 'forbidden' ? 'This belongs to another partner' : 'We couldn’t find that', text: SA.core.utils.esc(err.message), action: `<a class="btn btn-primary" href="${isAdminUI() ? '#/admin' : '#/dash'}">Back to overview</a>` })}</div>` });
      }
    },
    mount(el, ctx) {
      if (!D().me()) return (isAdminUI() ? SA.pages.adminLogin : SA.pages.dashLogin).mount(el, ctx);
      if (this._as) { const as = this._as; this._as = null; return as.mount(el, ctx); }
      if (this._failed) { this._failed = false; return bindLayout(el); }
      const offs = [bindLayout(el), page.mount?.call(page, el, ctx)];
      return () => offs.forEach((f) => typeof f === 'function' && f());
    },
  });

  /* ---------- Widgets ---------- */
  const kpi = ({ label, value, sub = '', ic = 'grid', tone = '', count = null, prefix = '', suffix = '', decimals = 0 }) => `
    <div class="kpi ${tone}">
      <span class="kpi-ico">${icon(ic, 18)}</span>
      <span class="xs kpi-label">${label}</span>
      <b class="kpi-value num" ${count !== null ? `data-countup="${count}" data-prefix="${prefix}" data-suffix="${suffix}" data-decimals="${decimals}"` : ''}>${value}</b>
      ${sub ? `<span class="xs kpi-sub">${sub}</span>` : ''}
    </div>`;

  /** Stacked daily bars: bookings (turf) + store (blaze) */
  const barChart = (rows, { height = 220 } = {}) => {
    if (!rows.length) return '';
    const max = Math.max(1, ...rows.map((r) => r.bookingsNet + r.storeNet));
    const w = 100 / rows.length;
    const step = rows.length > 45 ? 14 : rows.length > 20 ? 7 : rows.length > 10 ? 2 : 1;
    // Text alternative: totals and the best day, for screen readers
    const totB = rows.reduce((t, r) => t + r.bookingsNet, 0), totS = rows.reduce((t, r) => t + r.storeNet, 0);
    const best = rows.reduce((b, r) => (r.bookingsNet + r.storeNet > b.bookingsNet + b.storeNet ? r : b), rows[0]);
    const alt = `Daily net earnings from ${fmtDate(rows[0].date)} to ${fmtDate(rows[rows.length - 1].date)}: bookings ${money(Math.round(totB))}, store ${money(Math.round(totS))}. Best day ${fmtDate(best.date)} with ${money(Math.round(best.bookingsNet + best.storeNet))}.`;
    return `<div class="bar-chart grow-in" style="--h:${height}px" role="img" aria-label="${esc(alt)}">
      <div class="bc-grid">${[1, 0.75, 0.5, 0.25].map((f) => `<span style="bottom:${f * 100}%"><i>${money(Math.round(max * f)).replace(' EGP', '')}</i></span>`).join('')}</div>
      <div class="bc-bars">${rows.map((r, i) => {
        const tb = (r.bookingsNet / max) * 100, ts = (r.storeNet / max) * 100;
        return `<div class="bc-col" style="--i:${i};width:${w}%" data-tip="${fmtDate(r.date)} — Bookings ${money(Math.round(r.bookingsNet))} · Store ${money(Math.round(r.storeNet))} · ${r.bookings} booking${r.bookings === 1 ? '' : 's'}">
          <span class="bc-s" style="height:${ts}%"></span><span class="bc-b" style="height:${tb}%"></span>
          ${i % step === 0 ? `<i class="bc-x">${parseISO(r.date).getDate()} ${MON[parseISO(r.date).getMonth()]}</i>` : ''}</div>`;
      }).join('')}</div>
      <div class="bc-tip" hidden></div>
    </div>
    <div class="legend xs" aria-hidden="true"><span><i style="background:var(--turf)"></i>Bookings (net) — lower part</span><span><i class="lg-hatch"></i>Store (net) — striped top</span></div>`;
  };
  const bindChart = (el) => {
    el.querySelectorAll('.bar-chart').forEach((ch) => {
      const tip = ch.querySelector('.bc-tip');
      ch.addEventListener('pointermove', (e) => {
        const col = e.target.closest('.bc-col'); if (!col) { tip.hidden = true; return; }
        const r = ch.getBoundingClientRect();
        tip.textContent = col.dataset.tip; tip.hidden = false;
        tip.style.left = `${Math.min(r.width - 230, Math.max(0, e.clientX - r.left - 115))}px`;
      });
      ch.addEventListener('pointerleave', () => (tip.hidden = true));
    });
  };

  const heatmap = (hm) => {
    const DAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
    const hours = []; for (let h = hm.minH; h < hm.maxH; h++) hours.push(h);
    if (!hours.length) return '<p class="small muted">No opening hours yet.</p>';
    let top = { r: -1 };
    [0, 1, 2, 3, 4, 5, 6].forEach((d) => hours.forEach((h) => { const cap = hm.cap[d][h] || 0; const r = cap ? (hm.grid[d][h] || 0) / cap : 0; if (r > top.r) top = { r, d, h }; }));
    const alt = `Occupancy by weekday and hour. Busiest slot: ${DAYS[top.d]} at ${SA.core.utils.fmtHour(top.h)}, ${Math.round(top.r * 100)}% booked. Darker green means busier.`;
    return `<div class="heatmap" style="--cols:${hours.length}" role="img" aria-label="${esc(alt)}">
      <span></span>${hours.map((h, i) => `<span class="hm-h xs">${i % 2 === 0 ? SA.core.utils.fmtHour(h).replace(':00', '').replace(' ', '') : ''}</span>`).join('')}
      ${[6, 0, 1, 2, 3, 4, 5].map((d) => `<span class="hm-d xs">${DAYS[d]}</span>${hours.map((h) => {
        const cap = hm.cap[d][h] || 0, v = hm.grid[d][h] || 0, rate = cap ? v / cap : 0;
        return `<span class="hm-c" style="--a:${rate.toFixed(2)};--i:${d * 2 + (h % 7)}" title="${DAYS[d]} ${SA.core.utils.fmtHour(h)} — ${Math.round(rate * 100)}% booked"></span>`;
      }).join('')}`).join('')}
    </div><div class="legend xs" style="margin-top:10px"><span>Less busy</span><span class="hm-scale"></span><span>Fully booked</span></div>`;
  };

  const STATUS = {
    upcoming: ['badge-info', 'Upcoming'], 'in-progress': ['badge-blaze', 'Playing now'], 'checked-in': ['badge-turf', 'Checked in'], completed: ['badge-turf', 'Completed'],
    cancelled: ['badge-danger', 'Cancelled'], 'no-show': ['badge-gold', 'No-show'],
  };
  const statusBadge = (s) => { const [cls, l] = STATUS[s] || ['', s]; return `<span class="badge ${cls}">${l}</span>`; };

  /* ---------- Forms ---------- */
  const showErrors = (form, errors = {}) => {
    // Clear a field's message as soon as the person edits it
    if (!form.dataset.errWired) {
      form.dataset.errWired = '1';
      const clear = (e) => { const n = e.target.name; if (!n) return; e.target.removeAttribute('aria-invalid'); const m = form.querySelector(`[data-err="${n}"]`); if (m) m.textContent = ''; };
      form.addEventListener('input', clear); form.addEventListener('change', clear);
    }
    form.querySelectorAll('[data-err]').forEach((e) => (e.textContent = errors[e.dataset.err] || ''));
    form.querySelectorAll('[aria-invalid]').forEach((i) => i.removeAttribute('aria-invalid'));
    Object.keys(errors).forEach((k) => form.querySelector(`[name="${k}"]`)?.setAttribute('aria-invalid', 'true'));
    const first = form.querySelector('[data-err]:not(:empty)');
    first?.closest('.field, .dash-card, fieldset')?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  };

  /** Read an image file, downscale it and return a compressed JPEG data URL */
  const readImage = (file, max = 1200, quality = 0.82) => new Promise((resolve, reject) => {
    if (!file || !/^image\//.test(file.type)) return reject(new Error('Choose an image file (JPG, PNG or WebP).'));
    if (file.size > 10 * 1024 * 1024) return reject(new Error('That image is larger than 10 MB. Choose a smaller one.'));
    const url = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => {
      const k = Math.min(1, max / Math.max(img.width, img.height));
      const cv = document.createElement('canvas'); cv.width = Math.round(img.width * k); cv.height = Math.round(img.height * k);
      cv.getContext('2d').drawImage(img, 0, 0, cv.width, cv.height);
      URL.revokeObjectURL(url);
      resolve(cv.toDataURL('image/jpeg', quality));
    };
    img.onerror = () => { URL.revokeObjectURL(url); reject(new Error('We couldn’t read that image. Try another file.')); };
    img.src = url;
  });

  /** Photo manager: stock library + uploads, reorder, remove. Returns { get(), el } */
  const photoManager = (host, { images = [], library = [], max = 6, aspect = '4 / 3', name = 'images' } = {}) => {
    let list = images.slice();
    const media = SA.data.media;
    const draw = () => {
      host.innerHTML = `<div class="pm">
        ${list.map((im, i) => `<figure class="pm-item ${i === 0 ? 'is-cover' : ''}" style="aspect-ratio:${aspect}"><img src="${media.u(im, 400, 300)}" alt="" />
          ${i === 0 ? '<span class="badge badge-ink pm-cover">Cover</span>' : ''}
          <span class="pm-tools">${i > 0 ? `<button type="button" class="pm-btn" data-left="${i}" aria-label="Move left">${icon('chevronLeft', 14)}</button>` : ''}<button type="button" class="pm-btn" data-del="${i}" aria-label="Remove photo">${icon('trash', 14)}</button></span></figure>`).join('')}
        ${list.length < max ? `<label class="pm-add" style="aspect-ratio:${aspect}">${icon('plus', 22)}<span class="xs">Upload photo</span><input type="file" accept="image/*" multiple hidden data-upload /></label>` : ''}
      </div>
      ${library.length && list.length < max ? `<details class="pm-lib"><summary class="small strong">${icon('grid', 15)} Or choose from the Super App photo library</summary><div class="pm-lib-grid">${library.map((id) => `<button type="button" class="pm-lib-item ${list.includes(id) ? 'is-on' : ''}" data-lib="${id}"><img src="${media.u(id, 240, 180)}" alt="" loading="lazy" /></button>`).join('')}</div></details>` : ''}
      <p class="field-error" data-err="${name}"></p>`;
    };
    host.addEventListener('click', (e) => {
      const del = e.target.closest('[data-del]'); if (del) { list.splice(+del.dataset.del, 1); draw(); return; }
      const left = e.target.closest('[data-left]'); if (left) { const i = +left.dataset.left; [list[i - 1], list[i]] = [list[i], list[i - 1]]; draw(); return; }
      const lib = e.target.closest('[data-lib]');
      if (lib) { const id = lib.dataset.lib; list = list.includes(id) ? list.filter((x) => x !== id) : list.length < max ? [...list, id] : list; const open = host.querySelector('details')?.open; draw(); if (open && host.querySelector('details')) host.querySelector('details').open = true; }
    });
    host.addEventListener('change', async (e) => {
      if (!e.target.matches('[data-upload]')) return;
      const files = [...e.target.files].slice(0, max - list.length);
      for (const f of files) {
        try { list.push(await readImage(f)); } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
      }
      draw();
    });
    draw();
    return { get: () => list.slice() };
  };

  /* ---------- Excel helpers ---------- */
  const xlsx = (name, sheets) => { SA.core.xlsx.download(name, sheets); SA.ui.overlay.toast(`Downloaded ${name}`, { type: 'success' }); };

  /* ---------- Sign-in screen ---------- */
  SA.pages.dashLogin = {
    title: 'Partner dashboard — Super App',
    render() {
      const accs = SA.services.db.all('accounts').filter((a) => SIGNIN_ROLES.includes(a.role));
      return `<div class="dash-login">
        <section class="dl-copy">
          <a class="dash-brand" href="#/"><span class="brand-mark"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2.6" stroke-linecap="round"><path d="M17 7.2C15.9 6 14.3 5.4 12.4 5.4c-2.7 0-4.5 1.4-4.5 3.4 0 4.6 9.2 2.6 9.2 7 0 2-1.9 3.4-4.8 3.4-2.1 0-3.9-.7-5.3-2"/></svg></span><span><b>SUPER APP</b><span class="xs">Partner dashboard</span></span></a>
          <div class="dl-prefs">${SA.ui.shell.prefButtons()}</div>
          <h1 class="display dl-title">Run your pitches. Open your store. Get paid.</h1>
          <p class="lead">One place for pitch owners and the Super App team: bookings, availability, money, payouts and your own shop on Super App.</p>
          <h2 class="h4" style="margin-top:28px">Sign in as</h2>
          <p class="xs muted" style="margin-top:4px">${icon('info', 12)} Demo accounts — partner sign-in with email and password arrives with the backend.</p>
          <div class="dl-accounts">${accs.map((a, i) => `<button class="dl-acc" data-signin="${a.id}" style="--i:${i}" ${a.status === 'suspended' ? 'disabled aria-disabled="true"' : ''}>${c.avatar({ name: a.name, avatar: a.avatar })}<span class="grow"><b>${esc(a.name)}</b><span class="xs muted" style="display:block">${esc(a.company)}${a.role === 'owner' ? ` · ${SA.services.db.all('pitches').filter((p) => p.ownerId === a.id && !p.deleted).length} pitches` : ''}</span></span>${a.status === 'suspended' ? '<span class="badge badge-danger">Suspended</span>' : `<span class="badge ${a.id === 'acc-new' ? 'badge-gold' : (ROLE_LABEL[a.role] || ROLE_LABEL.owner)[0]}">${a.id === 'acc-new' ? 'New partner' : (ROLE_LABEL[a.role] || ROLE_LABEL.owner)[1]}</span>`}${icon('arrowRight', 16)}</button>`).join('')}</div>
        </section>
        <section class="dl-visual" aria-hidden="true">
          <div class="hero-stage dl-stage" data-tilt>
            <div class="pitch3d"><div class="pitch3d-grass"></div>
              <svg class="pitch3d-lines" viewBox="0 0 400 260" preserveAspectRatio="none"><g fill="none" stroke="rgba(255,255,255,.85)" stroke-width="2"><rect x="6" y="6" width="388" height="248" rx="2"/><line x1="200" y1="6" x2="200" y2="254"/><circle cx="200" cy="130" r="34"/><rect x="6" y="70" width="52" height="120"/><rect x="342" y="70" width="52" height="120"/></g></svg>
              <div class="pitch3d-glow"></div></div>
            <div class="float-card dl-f1"><span class="xs">Revenue this month</span><b class="num">186,420 EGP</b><span class="dl-spark"><i style="--h:30%"></i><i style="--h:55%"></i><i style="--h:42%"></i><i style="--h:70%"></i><i style="--h:62%"></i><i style="--h:88%"></i><i style="--h:76%"></i></span></div>
            <div class="float-card dl-f2"><span class="xs">Occupancy tonight</span><b class="num">82%</b><span class="meter"><span style="width:82%"></span></span></div>
            <div class="float-card dl-f3">${icon('wallet', 16)}<span><span class="xs">Next payout</span><b class="num">Monday</b></span></div>
          </div>
        </section>
      </div>`;
    },
    mount(el) {
      const off = SA.core.motion.tilt(el.querySelector('[data-tilt]'), { max: 7 });
      el.querySelectorAll('[data-signin]').forEach((b) => b.addEventListener('click', () => {
        try { D().auth.signIn(b.dataset.signin); } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); return; }
        setOwnerFilter('');
        SA.ui.overlay.toast(`Welcome, ${esc(D().me().name.split(' ')[0])}`);
        SA.core.router.go(location.hash.replace(/^#/, '').startsWith('/dash/') ? location.hash.slice(1) : '/dash', { replace: true });
      }));
      return off;
    },
  };

  SA.pages.dashCommon = { SIGNIN_ROLES, ROLE_LABEL, layout, guard, bindLayout, adminLinks, isAdminUI, kpi, barChart, bindChart, heatmap, statusBadge, range, rangeLabel, rangePicker, bindRange, scope, ownerFilter, showErrors, readImage, photoManager, xlsx, STATUS };
})(window.SA);
