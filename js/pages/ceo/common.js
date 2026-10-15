/**
 * CEO system "Command Center" (#/ceo): shared layout, PIN sign-in and small widgets.
 * A separate system with its own session (SA.services.ceo.auth); it reuses the dashboard look (css/dash.css).
 */
(function (SA) {
  const { esc, money, timeAgo } = SA.core.utils;
  const { icon } = SA.ui;
  const c = SA.ui.c;
  const CE = () => SA.services.ceo;
  const N = () => SA.services.notify;

  const NAV = [
    { key: 'overview', label: 'Command overview', href: '#/ceo', icon: 'grid' },
    { key: 'briefing', label: 'Morning briefing', href: '#/ceo/briefing', icon: 'sun' },
    { key: 'approvals', label: 'Approvals', href: '#/ceo/approvals', icon: 'check', badge: 'approvals' },
    { group: 'Company' },
    { key: 'departments', label: 'Departments', href: '#/ceo/departments', icon: 'users' },
    { key: 'agents', label: 'AI workforce', href: '#/ceo/agents', icon: 'bolt', badge: 'paused' },
    { key: 'goals', label: 'Goals (OKRs)', href: '#/ceo/goals', icon: 'target' },
    { key: 'budgets', label: 'Budgets', href: '#/ceo/budgets', icon: 'wallet' },
    { key: 'calendar', label: 'Compliance calendar', href: '#/ceo/calendar', icon: 'calendar', badge: 'deadlines' },
    { group: 'Insight & control' },
    { key: 'reports', label: 'Reports', href: '#/ceo/reports', icon: 'chart' },
    { key: 'whatif', label: 'What-if simulator', href: '#/ceo/whatif', icon: 'sparkle' },
    { key: 'control', label: 'Control room', href: '#/ceo/control', icon: 'settings', badge: 'off' },
    { key: 'systems', label: 'All systems', href: '#/ceo/systems', icon: 'globe' },
    { key: 'audit', label: 'Company audit log', href: '#/ceo/audit', icon: 'clock' },
  ];
  const badges = () => {
    try {
      return { approvals: CE().approvals({ status: 'pending' }).length, paused: CE().roles().filter((r) => r.holder === 'ai' && r.status === 'paused').length,
        deadlines: CE().calendar().filter((x) => ['overdue', 'soon'].includes(x.state)).length, off: CE().switches().filter((s) => !s.on).length };
    } catch { return {}; }
  };
  const TONES = { turf: 'var(--turf)', info: 'var(--info)', blaze: 'var(--blaze)', ink: 'var(--ink-3)', gold: 'var(--gold)', violet: '#7C4DDB', danger: 'var(--danger)' };

  /* ---------- Sign-in ---------- */
  const brand = (sub) => `<a class="dash-brand" href="#/ceo"><span class="brand-mark"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2.6" stroke-linecap="round"><path d="M17 7.2C15.9 6 14.3 5.4 12.4 5.4c-2.7 0-4.5 1.4-4.5 3.4 0 4.6 9.2 2.6 9.2 7 0 2-1.9 3.4-4.8 3.4-2.1 0-3.9-.7-5.3-2"/></svg></span><span><b>SUPER APP</b><span class="xs">${sub}</span></span></a>`;
  const login = {
    title: 'Command Center — Super App',
    render() {
      const K = (() => { try { return { depts: SA.data.org.DEPTS.length, agents: SA.data.org.ROLES.filter((r) => r.holder === 'ai').length }; } catch { return { depts: 8, agents: 0 }; } })();
      return `<div class="dash-login ceo-login">
        <section class="dl-copy">
          ${brand('Command Center')}
          <div class="dl-prefs">${SA.ui.shell.prefButtons()}</div>
          <span class="badge badge-gold">${icon('crown', 13)} CEO only</span>
          <h1 class="display dl-title">The whole company on one screen.</h1>
          <p class="lead">${K.depts} departments, ${K.agents} AI agents and every system — with your approvals, goals, budgets, deadlines, reports and kill switches.</p>
          <button class="dl-acc ceo-acc" data-ceo-signin style="--i:0">${c.avatar({ name: 'Sharaf', avatar: 4 })}<span class="grow"><b>Sharaf</b><span class="xs muted" style="display:block">Chief executive (CEO)</span></span>${icon('lock', 15)}</button>
          <p class="xs muted" style="margin-top:12px">${icon('info', 12)} Demo PIN: 2026 — sign-in with two-step verification arrives with the backend.</p>
          <p class="small muted" style="margin-top:18px">Super App team? <a class="link" href="#/admin">Admin console</a> · Factory? <a class="link" href="#/factory">Factory system</a></p>
        </section>
        <section class="dl-visual ceo-visual" aria-hidden="true">
          <div class="ceo-stage" data-tilt3d>
            <div class="ceo-globe"><i></i><i></i><i></i><b>${icon('crown', 34)}</b></div>
            ${SA.data.org.DEPTS.map((d, i) => `<span class="ceo-sat" style="--a:${(360 / SA.data.org.DEPTS.length) * i}deg;--t:${TONES[d.tone] || 'var(--turf)'}">${icon(d.icon, 16)}</span>`).join('')}
            <div class="float-card ceo-f1">${icon('check', 16)}<span><span class="xs">Approvals</span><b>Only what needs you</b></span></div>
            <div class="float-card ceo-f2">${icon('bolt', 16)}<span><span class="xs">AI workforce</span><b class="num">${K.agents} agents</b></span></div>
          </div>
        </section>
      </div>`;
    },
    mount(el) {
      el.querySelector('[data-ceo-signin]')?.addEventListener('click', () => pinPad({ title: 'Sharaf', subtitle: 'Chief executive · enter your 4-digit PIN', check: (pin) => CE().auth.signIn(pin), done: () => { SA.ui.overlay.toast('Welcome back, Sharaf'); const h = SA.core.router.location(); SA.core.router.go(h.startsWith('/ceo/') ? h : '/ceo', { replace: true }); SA.app.rerender(); } }));
      return SA.core.motion.tilt(el.querySelector('[data-tilt3d]'), { max: 10 });
    },
  };
  /** 4-digit PIN pad (CEO sign-in, the CFO's second approval) */
  const pinPad = ({ title, subtitle, check, done }) => {
    let pin = '';
    SA.ui.overlay.open({
      label: 'Enter the PIN', title: esc(title), subtitle,
      body: `<div class="pin">
        <div class="pin-dots" aria-live="polite" data-dots>${'<i></i>'.repeat(4)}</div>
        <p class="field-error" data-err style="text-align:center;min-height:1.2em"></p>
        <input class="sr-only" inputmode="numeric" maxlength="4" autocomplete="off" aria-label="PIN" data-pin-input />
        <div class="pin-keys">${[1, 2, 3, 4, 5, 6, 7, 8, 9, '', 0, '⌫'].map((k) => (k === '' ? '<span></span>' : `<button type="button" class="pin-key" data-k="${k}" aria-label="${k === '⌫' ? 'Delete' : k}">${k}</button>`)).join('')}</div>
      </div>`,
      onMount(m, close) {
        const dots = m.querySelector('[data-dots]'), err = m.querySelector('[data-err]'), inp = m.querySelector('[data-pin-input]');
        const paint = () => dots.querySelectorAll('i').forEach((d, i) => d.classList.toggle('on', i < pin.length));
        const press = async (k) => {
          if (k === '⌫') pin = pin.slice(0, -1); else if (pin.length < 4 && /\d/.test(k)) pin += k;
          err.textContent = ''; paint();
          if (pin.length === 4) {
            try { await check(pin); close(); done?.(); }
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
  const sideNav = (active, a) => {
    const b = badges();
    return `${brand('Command Center')}
      <div class="dash-acct">${c.avatar({ name: a.name, avatar: a.avatar })}<span class="grow"><b class="small truncate">${esc(a.name)}</b><span class="xs truncate">Super App Egypt</span></span><span class="badge badge-gold">CEO</span></div>
      <nav class="dash-nav" aria-label="Command Center">${NAV.map((n, i) => (n.group ? `<span class="dash-nav-label xs">${n.group}</span>` : `<a href="${n.href}" style="--i:${i}" ${n.key === active ? 'aria-current="page"' : ''}>${icon(n.icon, 18)}<span class="grow">${n.label}</span><span class="nav-badge ${b[n.badge] ? 'nb-blaze' : ''}" ${b[n.badge] ? '' : 'hidden'}>${b[n.badge] || ''}</span></a>`)).join('')}</nav>
      <div class="dash-side-foot">
        <a href="#/" class="dash-foot-link dash-site-link">${icon('home', 16)} <span class="grow">Back to Super App</span>${icon('arrowUpRight', 14)}</a>
        <button class="dash-foot-link" data-action="toggle-theme">${icon(SA.core.theme.current() === 'dark' ? 'sun' : 'moon', 16)} ${SA.core.theme.current() === 'dark' ? 'Light mode' : 'Dark mode'}</button>
        <button class="dash-foot-link" data-action="toggle-lang" data-i18n-skip>${icon('globe', 16)} ${SA.i18n.isAr ? 'English' : 'العربية'}</button>
        <button class="dash-foot-link" data-ceo-signout>${icon('logout', 16)} Sign out</button>
      </div>`;
  };
  const bell = (a) => {
    const list = N()?.list(a.id) || [];
    const unread = list.filter((n) => !n.read).length;
    return `<div class="bell-wrap"><button class="icon-btn dash-bell" data-bell aria-expanded="false" aria-label="Notifications">${icon('bell', 19)}<span class="bell-dot" ${unread ? '' : 'hidden'}>${unread}</span></button>
      <div class="bell-pop" data-bell-pop hidden><div class="row-between"><b>Notifications</b>${unread ? '<button class="link xs" data-read-all>Mark all as read</button>' : '<span class="xs muted">All read</span>'}</div>
      <ul>${list.slice(0, 8).map((n) => `<li><a href="${esc(n.link || '#/ceo')}" class="bp-item ${n.read ? '' : 'is-unread'}" data-notif="${n.id}"><span class="bp-ico t-${n.tone || 'info'}">${icon(n.icon || 'bell', 16)}</span><span class="grow"><b class="small">${esc(n.title)}</b><span class="xs muted" style="display:block">${esc(n.body)}</span><span class="xs muted bp-time">${timeAgo(n.at)}</span></span></a></li>`).join('') || `<li class="bp-empty">${icon('bell', 18)}<span class="small">Nothing new.</span></li>`}</ul></div></div>`;
  };
  const layout = (active, { title, subtitle = '', actions = '', body }) => {
    const a = CE().me();
    const off = CE().switches().filter((s) => !s.on);
    const stopped = CE().roles().some((r) => r.holder === 'ai' && r.status === 'paused' && CE().cfg().agents?.[r.id]?.stopAll);
    const banner = [stopped ? `<div class="fac-banner is-danger">${icon('alert', 16)} <span><span>Emergency stop is on — AI agents are paused.</span> <a class="link" href="#/ceo/agents">Manage</a></span></div>` : '',
      off.length ? `<div class="fac-banner is-gold">${icon('ban', 16)} <span><span>Switched off:</span> ${off.map((s) => esc(s.label)).join(', ')}. <a class="link" href="#/ceo/control">Control room</a></span></div>` : ''].join('');
    return `<div class="dash is-ceo" data-dash-active="${active}">
      <aside class="dash-side">${sideNav(active, a)}</aside>
      <section class="dash-main">
        <header class="dash-top">
          <button class="icon-btn dash-menu" data-ceo-menu aria-label="Open the menu">${icon('list')}</button>
          <div class="grow dash-titles"><h1 class="h2 dash-title">${title}</h1>${subtitle ? `<p class="small muted">${subtitle}</p>` : ''}</div>
          <div class="dash-actions">${actions}<span class="dash-prefs">${SA.ui.shell.prefButtons()}</span>${bell(a)}</div>
        </header>
        <div class="dash-body">${banner}${body}</div>
      </section>
    </div>`;
  };
  const bindLayout = (el) => {
    const out = () => { CE().auth.signOut(); SA.ui.overlay.toast('Signed out of the Command Center', { type: 'info' }); SA.core.router.go('/ceo'); SA.app.rerender(); };
    el.querySelector('[data-ceo-signout]')?.addEventListener('click', out);
    el.querySelector('[data-ceo-menu]')?.addEventListener('click', () => {
      SA.ui.overlay.open({ drawer: true, side: 'left', label: 'Menu', body: `<div class="dash-drawer">${sideNav(el.querySelector('.dash')?.dataset.dashActive, CE().me())}</div>`,
        onMount(m, close) { m.querySelectorAll('a').forEach((x) => x.addEventListener('click', close)); m.querySelector('[data-ceo-signout]')?.addEventListener('click', () => { close(); out(); }); } });
    });
    const btn = el.querySelector('[data-bell]'), pop = el.querySelector('[data-bell-pop]');
    btn?.addEventListener('click', (e) => { e.stopPropagation(); pop.hidden = !pop.hidden; btn.setAttribute('aria-expanded', String(!pop.hidden)); pop.style.top = innerWidth < 760 ? `${Math.round(btn.getBoundingClientRect().bottom + 10)}px` : ''; });
    pop?.addEventListener('click', (e) => {
      if (e.target.closest('[data-read-all]')) { N()?.markAll(CE().me().id); SA.app.rerender(); return; }
      const n = e.target.closest('[data-notif]'); if (n) N()?.markRead(n.dataset.notif);
    });
    const onDoc = (e) => { if (pop && !pop.hidden && !pop.contains(e.target) && !btn.contains(e.target)) pop.hidden = true; };
    document.addEventListener('click', onDoc);
    // New approval requests and changes from other systems show up without a reload
    let t = 0;
    const off = SA.services.db.subscribe((col) => {
      if (!['ceoApprovals', 'notifications', 'settings', '*'].includes(col)) return;
      clearTimeout(t);
      t = setTimeout(() => { if (!document.querySelector('.overlay, dialog[open]') && !['INPUT', 'TEXTAREA', 'SELECT'].includes(document.activeElement?.tagName)) SA.app.rerender(); }, 500);
    });
    return () => { off(); clearTimeout(t); document.removeEventListener('click', onDoc); };
  };
  /** Wrap a CEO page: sign-in first */
  const guard = (page) => ({
    ...page,
    title: page.title || 'Command Center — Super App',
    async render(ctx) {
      if (!CE().auth.current()) { this._login = true; return login.render(ctx); }
      this._login = false;
      try { this._failed = false; return await page.render.call(page, ctx); }
      catch (err) { if (!['not_found', 'forbidden'].includes(err.code)) throw err; this._failed = true; return layout('', { title: 'Not found', body: `<div class="dash-card">${c.empty({ icon: 'search', title: 'We couldn’t find that', text: esc(err.message), action: '<a class="btn btn-primary" href="#/ceo">Back to the overview</a>' })}</div>` }); }
    },
    mount(el, ctx) {
      if (this._login) return login.mount(el, ctx);
      const offs = [bindLayout(el), this._failed ? null : page.mount?.call(page, el, ctx)];
      return () => offs.forEach((f) => typeof f === 'function' && f());
    },
  });

  /* ---------- Widgets ---------- */
  const egp = (n) => money(Math.round(n || 0));
  const fmtVal = (v, type) => (type === 'money' ? egp(v) : (Math.round((+v || 0) * 100) / 100).toLocaleString());
  /** Score ring 0–100 */
  const ring = (score, tone, size = 64) => `<span class="ceo-ring" style="--p:${score};--c:${TONES[tone] || TONES.turf};--s:${size}px" role="img" aria-label="Health ${score} out of 100"><b class="num">${score}</b></span>`;
  const meter = (pct, tone = 'turf') => `<span class="meter" aria-hidden="true"><span style="width:${Math.max(0, Math.min(100, pct))}%;background:${TONES[tone] || tone}"></span></span>`;
  /** Simple daily columns (one value) with a hover tip */
  const columns = (rows, { key = 'net', label = 'Net', height = 180 } = {}) => {
    if (!rows.length) return '';
    const max = Math.max(1, ...rows.map((r) => Math.abs(r[key])));
    const tot = rows.reduce((t, r) => t + r[key], 0);
    return `<div class="ceo-cols" style="--h:${height}px" role="img" aria-label="${esc(`${label} per day: ${egp(tot)} in total`)}">${rows.map((r, i) => `<i style="--v:${Math.max(2, (Math.abs(r[key]) / max) * 100)}%;--i:${i}" class="${r[key] < 0 ? 'neg' : ''}" title="${SA.core.utils.fmtDate(r.date)} — ${egp(r[key])}"></i>`).join('')}</div>`;
  };
  /** People get their photo; AI agents get a gold tile with their initial (they aren't people) */
  const face = (r, size = '') => (r.holder === 'ai' ? `<span class="ceo-av ${size}" aria-hidden="true" data-i18n-skip>${esc(r.name[0])}${icon('bolt', 10)}</span>` : c.avatar({ name: r.name, avatar: SA.services.db.find('accounts', r.accountId || '')?.avatar ?? 4 }, size));
  const roleChip = (r) => `<span class="ceo-chip ${r.holder === 'ai' ? 'is-ai' : ''}">${icon(r.holder === 'ai' ? 'bolt' : 'user', 12)} ${r.holder === 'ai' ? 'AI agent' : 'Human'}</span>`;
  const statusChip = (r) => (r.status === 'paused' ? '<span class="badge badge-danger">Paused</span>' : '<span class="badge badge-turf">Working</span>');
  const run = async (fn, msg) => { try { const r = await fn(); if (msg) SA.ui.overlay.toast(typeof msg === 'function' ? msg(r) : msg); SA.app.rerender(); return r ?? true; } catch (e) { SA.ui.overlay.toast(e.message, { type: 'error' }); return false; } };

  SA.pages.ceoCommon = { NAV, guard, layout, pinPad, ring, meter, columns, egp, face, fmtVal, roleChip, statusChip, run, TONES };
})(window.SA);
