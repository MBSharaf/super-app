/**
 * Admin console (#/admin): a separate system for the Super App team, with its own sign-in and session.
 * It reuses the dashboard shell (layout, bell, charts, forms) from SA.pages.dashCommon; in admin mode the shell shows
 * this navigation, the admin to-do list and admin links.
 */
(function (SA) {
  const { esc } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const D = () => SA.services.dash;

  const NAV = [
    { key: 'overview', page: 'adminOverview', label: 'Overview', href: '#/admin', icon: 'grid' },
    { group: 'Money' },
    { key: 'payouts', page: 'adminPayouts', label: 'Withdrawals', href: '#/admin/payouts', icon: 'wallet', badge: 'payouts' },
    { key: 'finance-approvals', page: 'adminFinanceApprovals', label: 'Finance approvals', href: '#/admin/finance-approvals', icon: 'check', badge: 'fapprovals' },
    { key: 'revenue', page: 'adminRevenue', label: 'Revenue', href: '#/admin/revenue', icon: 'percent' },
    { group: 'People' },
    { key: 'customers', page: 'adminCustomers', label: 'Customers', href: '#/admin/customers', icon: 'user' },
    { key: 'partners', page: 'adminPartners', label: 'Pitch partners', href: '#/admin/partners', icon: 'pitch' },
    { key: 'sellers', page: 'adminSellers', label: 'Sellers', href: '#/admin/sellers', icon: 'users', badge: 'kyc' },
    { group: 'Operations' },
    { key: 'approvals', page: 'adminApprovals', label: 'Approvals', href: '#/admin/approvals', icon: 'check', badge: 'approvals' },
    { key: 'orders', page: 'adminOrders', label: 'Orders & bookings', href: '#/admin/orders', icon: 'ticket' },
    { key: 'tournaments', page: 'dashTournaments', label: 'Tournaments', href: '#/admin/tournaments', icon: 'trophy' },
    { key: 'support', page: 'adminSupport', label: 'Support & disputes', href: '#/admin/support', icon: 'message', badge: 'support' },
    { key: 'store', page: 'dashStore', label: 'Super App Store', href: '#/admin/store', icon: 'bag', badge: 'stock' },
    { key: 'reviews', page: 'dashReviews', label: 'Reviews', href: '#/admin/reviews', icon: 'star', badge: 'reviews' },
    { group: 'Print on demand' },
    { key: 'pod-review', page: 'dashPodReview', label: 'Design review', href: '#/admin/designs', icon: 'image', badge: 'designs' },
    { key: 'levels', page: 'adminLevels', label: 'Level review', href: '#/admin/levels', icon: 'trend', badge: 'levels' },
    { key: 'samples', page: 'adminSamples', label: 'Samples', href: '#/admin/samples', icon: 'shirt', badge: 'samples' },
    { key: 'factories', page: 'adminFactories', label: 'Factories & shipping', href: '#/admin/factories', icon: 'truck', badge: 'factories' },
    { key: 'pod-catalog', page: 'dashPodCatalog', label: 'Print catalogue', href: '#/admin/catalog', icon: 'tag' },
    { group: 'Marketing & content' },
    { key: 'community', page: 'adminCommunity', label: 'Communities', href: '#/admin/community', icon: 'users', badge: 'community' },
    { key: 'games', page: 'adminGames', label: 'Free slots & games', href: '#/admin/games', icon: 'pitch' },
    { key: 'broadcast', page: 'adminBroadcast', label: 'Broadcasts', href: '#/admin/broadcast', icon: 'megaphone' },
    { key: 'promos', page: 'adminPromos', label: 'Promo codes', href: '#/admin/promos', icon: 'gift' },
    { key: 'content', page: 'adminContent', label: 'Site content', href: '#/admin/content', icon: 'news' },
    { key: 'fantasy', page: 'adminFantasy', label: 'Fantasy', href: '#/admin/fantasy', icon: 'trophy' },
    { group: 'Insights' },
    { key: 'analytics', page: 'adminAnalytics', label: 'Analytics', href: '#/admin/analytics', icon: 'chart' },
    { key: 'audit', page: 'adminAudit', label: 'Audit log', href: '#/admin/audit', icon: 'clock' },
    { key: 'settings', page: 'dashSettings', label: 'Platform settings', href: '#/admin/settings', icon: 'settings' },
  ];
  // Pages are added chunk by chunk; links to pages that don't exist yet stay hidden
  // Team roles (set by the CEO): each admin account only sees its role's sections
  const mine = (n) => { const a = D().me(); return !SA.services.ceo || !a || SA.services.ceo.canSee(a, n.key); };
  const live = () => {
    const list = NAV.filter((n) => n.group !== undefined || (SA.pages[n.page] && mine(n)));
    return list.filter((n, i) => n.group === undefined || (list[i + 1] && list[i + 1].group === undefined));
  };

  /* ---------- Sign-in ---------- */
  SA.pages.adminLogin = {
    title: 'Admin console — Super App',
    render() {
      const accs = SA.services.db.all('accounts').filter((a) => a.role === 'admin' && a.teamRole !== 'ceo');
      const CE = SA.services.ceo;
      const acc = (a, i) => { const r = CE?.roleOfAccount(a); const paused = CE?.blockedAccount(a); return `<button class="dl-acc" data-signin="${a.id}" style="--i:${i}" ${paused ? 'disabled' : ''}>${SA.ui.c.avatar({ name: a.name, avatar: a.avatar })}<span class="grow"><b>${esc(a.name)}</b><span class="xs muted" style="display:block">${r ? esc(r.title) : esc(a.email)}</span></span><span class="badge ${paused ? 'badge-danger' : a.agent ? 'badge-gold' : 'badge-blaze'}">${paused ? 'Paused' : a.agent ? 'AI agent' : 'Admin'}</span>${icon('arrowRight', 16)}</button>`; };
      const people = accs.filter((a) => !a.agent), agents = accs.filter((a) => a.agent);
      return `<div class="dash-login adm-login">
        <section class="dl-copy">
          <a class="dash-brand" href="#/"><span class="brand-mark"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2.6" stroke-linecap="round"><path d="M17 7.2C15.9 6 14.3 5.4 12.4 5.4c-2.7 0-4.5 1.4-4.5 3.4 0 4.6 9.2 2.6 9.2 7 0 2-1.9 3.4-4.8 3.4-2.1 0-3.9-.7-5.3-2"/></svg></span><span><b>SUPER APP</b><span class="xs">Admin console</span></span></a>
          <div class="dl-prefs">${SA.ui.shell.prefButtons()}</div>
          <span class="badge badge-blaze adm-tag">${icon('lock', 13)} Super App team only</span>
          <h1 class="display dl-title">Run the whole platform from one place.</h1>
          <p class="lead">Money and withdrawals, customers, partners and sellers, every order and booking, support, content, fantasy and promotions — with a full audit log.</p>
          <h2 class="h4" style="margin-top:28px">Sign in as</h2>
          <p class="xs muted" style="margin-top:4px">${icon('info', 12)} Demo account — admin sign-in with email, password and two-step verification arrives with the backend.</p>
          <div class="dl-accounts">${people.map(acc).join('')}</div>
          ${agents.length ? `<details class="adm-agents"><summary class="small"><b>${icon('bolt', 14)} AI agents</b> <span class="muted">· ${agents.length} — each sees only its role’s sections</span></summary><div class="dl-accounts">${agents.map(acc).join('')}</div></details>` : ''}
          <p class="small muted" style="margin-top:22px">Pitch owner or seller? <a class="link" href="#/dash">Go to the partner dashboard</a> · Factory team? <a class="link" href="#/factory">Factory system</a> · CEO? <a class="link" href="#/ceo">Command Center</a></p>
        </section>
        <section class="dl-visual adm-visual" aria-hidden="true">
          <div class="adm-orbit adm-3d" data-tilt3d>
            <div class="float-card adm-f1"><span class="xs">Platform revenue · 30 days</span><b class="num">412,860 EGP</b><span class="dl-spark"><i style="--h:40%"></i><i style="--h:52%"></i><i style="--h:47%"></i><i style="--h:66%"></i><i style="--h:71%"></i><i style="--h:84%"></i><i style="--h:92%"></i></span></div>
            <div class="float-card adm-f2">${icon('wallet', 16)}<span><span class="xs">Withdrawals to approve</span><b class="num">7</b></span></div>
            <div class="float-card adm-f3">${icon('shield', 16)}<span><span class="xs">Audit log</span><b>Every change, signed</b></span></div>
            <div class="adm-cube-wrap"><div class="adm-cube">${[['wallet', 'Money'], ['users', 'People'], ['ticket', 'Orders'], ['news', 'Content'], ['trophy', 'Fantasy'], ['chart', 'Insights']].map(([ic, l], i) => `<div class="adm-face f${i}">${icon(ic, 34)}<b>${l}</b></div>`).join('')}</div></div>
            <div class="adm-ring" aria-hidden="true"><i></i><i></i><i></i></div>
          </div>
        </section>
      </div>`;
    },
    mount(el) {
      const off = SA.core.motion.tilt(el.querySelector('[data-tilt3d]'), { max: 12 });
      el.querySelectorAll('[data-signin]').forEach((b) => b.addEventListener('click', () => {
        try { D().auth.signIn(b.dataset.signin); } catch (e) { SA.ui.overlay.toast(e.message, { type: 'error' }); return; }
        SA.ui.overlay.toast(`Welcome, ${esc(D().me().name.split(' ')[0])}`);
        const h = SA.core.router.location();
        SA.core.router.go(h.startsWith('/admin/') ? h : '/admin', { replace: true });
      }));
      return off;
    },
  };

  /** Admin pages: the dashboard guard shows the admin sign-in when there is no admin session */
  const guard = (page) => C.guard({ ...page, title: page.title || 'Admin console — Super App' });

  /** Small helpers shared by admin pages */
  const tabs = (base, list, cur, extra = {}) => `<div class="chips" role="tablist" aria-label="Sections">${list.map(([k, l, n]) => `<a class="chip" role="tab" href="${SA.core.router.href(base, { ...extra, tab: k || undefined })}" aria-pressed="${cur === k}" aria-selected="${cur === k}">${l}${n != null ? ` <span class="chip-count">${n}</span>` : ''}</a>`).join('')}</div>`;
  const reason = async ({ title, text = '', label = 'Reason', confirmLabel = 'Confirm', danger = false, placeholder = '', required = true, extra = '' }) => new Promise((resolve) => {
    let done = false;
    SA.ui.overlay.open({
      label: title, title, subtitle: text,
      body: `<form class="stack" style="--gap:12px" data-reason novalidate>${extra}<div class="field"><label for="adm-reason">${label}</label><textarea class="input" id="adm-reason" name="reason" rows="3" maxlength="400" placeholder="${esc(placeholder)}"></textarea><p class="field-error" data-err="reason"></p></div>
        <div class="row" style="--gap:8px;justify-content:flex-end"><button type="button" class="btn btn-ghost" data-cancel>Cancel</button><button class="btn ${danger ? 'btn-blaze' : 'btn-primary'}" type="submit">${confirmLabel}</button></div></form>`,
      onMount(m, close) {
        const f = m.querySelector('[data-reason]');
        m.querySelector('[data-cancel]').addEventListener('click', () => close());
        f.addEventListener('submit', (e) => {
          e.preventDefault();
          const data = SA.core.utils.formData(f);
          if (required && (data.reason || '').trim().length < 3) { f.querySelector('[data-err="reason"]').textContent = 'Write a short reason (it’s saved in the audit log).'; return; }
          done = true; close(); resolve({ ...data, reason: (data.reason || '').trim() });
        });
        setTimeout(() => f.querySelector('textarea')?.focus(), 50);
      },
      onClose() { if (!done) resolve(null); },
    });
  });

  /** The admin section a path belongs to, and whether this admin's role may open it */
  const sectionOf = (path) => {
    const p = path.split('?')[0];
    let best = null;
    NAV.forEach((n) => { if (!n.href) return; const h = n.href.slice(1); if ((p === h || (h !== '/admin' && p.startsWith(`${h}/`))) && (!best || h.length > best.href.length - 1)) best = n; });
    return best;
  };
  const denied = (path) => { const n = sectionOf(path); return n && !mine(n) ? n : null; };
  const deniedBody = (n) => `<div class="dash-card">${SA.ui.c.empty({ icon: 'lock', title: `${esc(n.label)} isn’t part of your role`, text: `${esc(SA.services.ceo?.roleOfAccount(D().me())?.title || '')} — the CEO decides which sections each role opens.`, action: (live().find((x) => x.href) ? `<a class="btn btn-primary" href="${live().find((x) => x.href).href}">Go to ${esc(live().find((x) => x.href).label)}</a>` : '') })}</div>`;

  SA.pages.adminCommon = { NAV, live, guard, tabs, reason, denied, deniedBody };
})(window.SA);
