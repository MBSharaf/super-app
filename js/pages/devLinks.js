/**
 * "Every page" — the map of every page in every system, with one-tap sign-in shortcuts for the demo accounts.
 * It lives in the CEO system (#/ceo/sitemap): the CEO sees and opens everything from there. Links and shortcuts open
 * in a new tab so the Command Center stays open. (It used to be the temporary "Sharaf" section on the home page.)
 */
(function (SA) {
  const { icon } = SA.ui;
  const GROUPS = [
    { title: 'Site', icon: 'home', links: [
      ['Home', '#/'], ['Pitches', '#/pitches'], ['Pitch page', '#/pitches/p-zayed-22'], ['Book a pitch', '#/book/p-zayed-22'], ['My bookings', '#/bookings'],
      ['Find players', '#/players'], ['Post a game', '#/players/new'], ['Matches', '#/matches'], ['News', '#/news'], ['News story', '#/news/n-derby-date'],
      ['Tournaments', '#/tournaments'], ['Tournament page', '#/tournaments/tr-autumn-cup'], ['Profile', '#/profile'], ['Wallet & points', '#/wallet'], ['Help & support', '#/support'],
    ] },
    { title: 'Communities', icon: 'users', seller: true, links: [
      ['All communities', '#/community'], ['Players', '#/community/players'], ['Fantasy Talk', '#/community/fantasy'], ['Creators (sellers)', '#/community/creators'], ['Pro & Elite Lounge', '#/community/pro-lounge'],
      ['Admin: reports', '#/admin/community'], ['Admin: all posts', '#/admin/community?tab=posts'], ['Admin: post as Super App Admin', '#/admin/community?tab=new'], ['Admin: communities', '#/admin/community?tab=communities'], ['Admin: safety & muted', '#/admin/community?tab=safety'],
    ] },
    { title: 'Pitch community & games', icon: 'pitch', games: true, links: [
      ['Pitches & free slots', '#/community/pitches'], ['Free-slot alerts', '#/alerts'], ['Find players', '#/players'], ['Post your match', '#/players/new'], ['A game (auto-accept)', '#/players/m-1001'], ['A game (host decides)', '#/players/m-1002'],
      ['Owner: post free slots', '#/dash/slots'], ['Admin: free slots', '#/admin/games'], ['Admin: games & requests', '#/admin/games?tab=games'], ['Admin: player records', '#/admin/games?tab=players'],
    ] },
    { title: 'Shop', icon: 'bag', links: [
      ['Shop', '#/shop'], ['Designs', '#/shop?cat=designs'], ['Product', '#/shop/sh-predator'], ['Wishlist', '#/wishlist'], ['Cart', '#/cart'], ['Checkout', '#/checkout'], ['Orders', '#/orders'],
    ] },
    { title: 'Fantasy', icon: 'trophy', links: [
      ['Fantasy home', '#/fantasy'], ['Pick squad', '#/fantasy/squad'], ['Leagues & Pass', '#/fantasy/leagues'], ['Fixtures', '#/fantasy/fixtures'], ['Stats', '#/fantasy/stats'],
    ] },
    { title: 'Help, legal & returns', icon: 'help', links: [
      ['Help center', '#/help'], ['Help: refunds', '#/help?topic=returns'], ['All legal', '#/legal'], ['Terms of use', '#/legal/terms'], ['Privacy policy', '#/legal/privacy'], ['Refunds & returns policy', '#/legal/refunds'],
      ['Injury & conduct waiver', '#/legal/waiver'], ['Seller agreement', '#/legal/seller'], ['Pitch partner agreement', '#/legal/partner'], ['Cookies', '#/legal/cookies'], ['Order history (return or exchange)', '#/orders'], ['Contact support', '#/support'],
      ['Seller: returns', '#/seller/returns'], ['Admin: returns', '#/admin/returns'], ['CEO: legal documents', '#/ceo/legal'],
    ] },
    { title: 'Sell', icon: 'shirt', links: [
      ['Bulk upload (many designs)', '#/sell/bulk'],
      ['Start selling', '#/sell'], ['Design studio', '#/sell/studio'], ['Store: Koora Ink', '#/s/koora-ink'], ['Store: Nile Lines', '#/s/nile-lines'], ['Store: Cairo Otaku', '#/s/cairo-otaku'],
    ] },
    { title: 'Pitch owner dashboard', icon: 'pitch', as: [['acc-o1', 'Karim · Turf 22'], ['acc-new', 'New partner']], links: [
      ['Overview', '#/dash'], ['My pitches', '#/dash/pitches'], ['Add a pitch', '#/dash/pitches/new'], ['Availability', '#/dash/availability'], ['Check-in', '#/dash/checkin'], ['Bookings', '#/dash/bookings'],
      ['Teams', '#/dash/teams'], ['Reviews', '#/dash/reviews'], ['Tournaments', '#/dash/tournaments'], ['Finance', '#/dash/finance'], ['Wallet', '#/dash/wallet'], ['Forecast & offers', '#/dash/insights'],
      ['Discount codes', '#/dash/codes'], ['Sell on Super App', '#/dash/sell'], ['Notifications', '#/dash/notifications'], ['Help & support', '#/dash/support'], ['Activity', '#/dash/activity'],
    ] },
    { title: 'Seller dashboard', icon: 'image', as: [['acc-s1', 'Koora Ink · Elite'], ['acc-s2', 'Nile Lines · Pro'], ['acc-s3', 'Cairo Otaku · Rising']], links: [
      ['Overview', '#/seller'], ['Store settings', '#/seller/store'], ['Marketing calendar', '#/seller/calendar'], ['Analytics', '#/seller/analytics'], ['Designs & samples', '#/seller/designs'],
      ['Print orders', '#/seller/orders'], ['Products (own store)', '#/seller/products'], ['Store orders', '#/seller/store-orders'], ['Returns', '#/seller/returns'], ['Bulk upload', '#/sell/bulk'], ['Wallet', '#/seller/wallet'], ['Notifications', '#/seller/notifications'], ['Seller sign-in', '#/seller'],
    ] },
    { title: 'Grow: become a seller or a pitch partner', icon: 'sparkle', links: [
      ['Profile (grow cards)', '#/profile'], ['Apply to sell', '#/sell'], ['Apply as a pitch partner', '#/partner/apply'], ['Pitch partner: apply to sell', '#/dash/sell'], ['Karim’s shop (seller dashboard)', '#/seller/products'],
      ['Admin: pitch partner applications', '#/admin/approvals?tab=partners'], ['Admin: seller applications', '#/admin/approvals?tab=sellers'],
    ] },
    { title: 'Real accounts (when the site runs on the Super App server)', icon: 'lock', links: [
      ['Sign in', '#/auth/sign-in'], ['Create an account', '#/auth/sign-up'], ['Two-step code', '#/auth/2fa'], ['Set up two-step sign-in', '#/auth/2fa-setup'],
      ['Forgot password', '#/auth/forgot'], ['Choose a new password (from the email)', '#/auth/reset'], ['Confirm email (from the email)', '#/auth/confirm'], ['Team invitation (from the email)', '#/auth/invite'],
    ] },
    { title: 'CEO system (Command Center)', icon: 'crown', ceo: true, links: [
      ['CEO sign-in (PIN 2026)', '#/ceo'], ['Command overview', '#/ceo'], ['Morning briefing', '#/ceo/briefing'], ['Approvals inbox', '#/ceo/approvals'], ['Departments', '#/ceo/departments'], ['Org chart', '#/ceo/departments?view=chart'],
      ['A department (Finance)', '#/ceo/department/finance'], ['AI workforce', '#/ceo/agents'], ['An agent (Payables)', '#/ceo/agent/payables'], ['Goals (OKRs)', '#/ceo/goals'], ['Budgets', '#/ceo/budgets'], ['Compliance calendar', '#/ceo/calendar'],
      ['Reports', '#/ceo/reports'], ['AI agents report', '#/ceo/reports?kind=agents'], ['What-if simulator', '#/ceo/whatif'], ['Control room (kill switches)', '#/ceo/control'], ['All systems (open as anyone)', '#/ceo/systems'], ['Company audit log', '#/ceo/audit'],
      ['Legal documents', '#/ceo/legal'], ['Every page', '#/ceo/sitemap'], ['Integrations (server)', '#/ceo/integrations'], ['API catalogue (server)', '#/ceo/apis'], ['Team (server)', '#/ceo/team'], ['Roles & permissions (server)', '#/ceo/roles'], ['Admin console sign-in (agents list)', '#/admin'], ['Admin: finance approvals (CFO)', '#/admin/finance-approvals'],
    ] },
    { title: 'Factory (print-on-demand)', icon: 'truck', factory: [['fs-owner', '1111', 'Owner · Hany'], ['fs-sup', '2222', 'Supervisor · Reem'], ['fs-pr1', '3333', 'Printer · Mahmoud'], ['fs-qc', '4444', 'Quality · Nour'], ['fs-pack', '5555', 'Packing · Islam']], links: [
      ['Factory sign-in', '#/factory'], ['Overview', '#/factory'], ['Production', '#/factory/jobs'], ['New jobs & batches', '#/factory/jobs?tab=new'], ['Quality check', '#/factory/jobs?tab=qc'], ['Packing', '#/factory/jobs?tab=packing'], ['Late', '#/factory/jobs?tab=late'], ['Problems', '#/factory/jobs?tab=issues'],
      ['A job (with a problem)', '#/factory/job/PO-F706'], ['Scan a job', '#/factory/scan?code=PO-F708'], ['Shipping & courier', '#/factory/shipping'], ['Partly packed orders', '#/factory/shipping?tab=partial'], ['Team', '#/factory/team'], ['Money', '#/factory/money'], ['Reports', '#/factory/reports'], ['Settings & prices', '#/factory/settings'], ['Activity', '#/factory/activity'],
      ['Admin: factories', '#/admin/factories'], ['Admin: late & problems', '#/admin/factories?tab=jobs'], ['Admin: courier', '#/admin/factories?tab=shipping'], ['Admin: price requests', '#/admin/factories?tab=prices'], ['Admin: factory money', '#/admin/factories?tab=money'],
    ] },
    { title: 'Admin console', icon: 'shield', as: [['acc-admin', 'Super App HQ']], links: [
      ['Overview', '#/admin'], ['Withdrawals', '#/admin/payouts'], ['Finance approvals', '#/admin/finance-approvals'], ['Returns', '#/admin/returns'], ['Revenue', '#/admin/revenue'], ['Customers', '#/admin/customers'], ['Partners', '#/admin/partners'], ['Sellers', '#/admin/sellers'],
      ['Approvals', '#/admin/approvals'], ['Communities', '#/admin/community'], ['Orders & bookings', '#/admin/orders'], ['Tournaments', '#/admin/tournaments'], ['Support', '#/admin/support'], ['Super App Store', '#/admin/store'], ['Reviews', '#/admin/reviews'],
      ['Design review', '#/admin/designs'], ['Level review', '#/admin/levels'], ['Samples', '#/admin/samples'], ['Print orders', '#/admin/print'], ['Print catalogue', '#/admin/catalog'], ['Broadcasts', '#/admin/broadcast'],
      ['Promo codes', '#/admin/promos'], ['Site content', '#/admin/content'], ['Pop-up ads', '#/admin/content?tab=popups'], ['Fantasy', '#/admin/fantasy'], ['Analytics', '#/admin/analytics'], ['Audit log', '#/admin/audit'], ['Platform settings', '#/admin/settings'],
    ] },
  ];

  const open = (to) => window.open(SA.core.router.url(String(to).replace(/^#/, '')), '_blank', 'noopener');
  // English on purpose: a technical map of routes for testing (labels match the route names)
  SA.ui.siteMap = () => `<section class="dev-links" data-i18n-skip>
    ${GROUPS.map((g, i) => `<details class="dev-group" ${i === 0 ? 'open' : ''}><summary>${icon(g.icon, 18)}<b class="grow">${g.title}</b><span class="xs muted">${g.links.length}</span>${icon('chevronDown', 16)}</summary>
      ${g.games ? `<div class="dev-as"><span class="xs muted">Shortcuts:</span><button class="chip" data-dev-as="acc-o1" data-dev-go="#/dash/slots">Sign in as Karim (owner)</button><button class="chip" data-dev-game>Create a finished game to rate</button></div>` : ''}
      ${g.seller ? `<div class="dev-as"><span class="xs muted">Seller-only communities need a seller account:</span><button class="chip" data-dev-seller>Make me a test seller</button><button class="chip" data-dev-as="acc-admin" data-dev-go="#/admin/community">Sign in as admin</button></div>` : ''}
      ${g.ceo ? `<div class="dev-as"><span class="xs muted">Shortcuts:</span><button class="chip" data-dev-agent="acc-ag-payables">Admin console as the Payables agent</button><button class="chip" data-dev-agent="acc-ag-support-agent">Admin console as Echo (support)</button><button class="chip" data-dev-big>Send a 60,000 EGP test request (two approvals)</button></div>` : ''}
      ${g.factory ? `<div class="dev-as"><span class="xs muted">Sign in to the factory as:</span>${g.factory.map(([id, pin, l]) => `<button class="chip" data-dev-fac="${id}" data-pin="${pin}">${l}</button>`).join('')}<button class="chip" data-dev-job>Send a test order to the factory</button></div>` : ''}
      ${g.as ? `<div class="dev-as"><span class="xs muted">Sign in as:</span>${g.as.map(([id, l]) => `<button class="chip" data-dev-as="${id}" data-dev-go="${g.links[0][1]}">${l}</button>`).join('')}</div>` : ''}
      <div class="dev-grid">${g.links.map(([l, h]) => `<a class="dev-link" href="${h}" target="_blank" rel="noopener"><span>${l}</span><span class="xs muted">${h.replace('#', '')}</span></a>`).join('')}</div></details>`).join('')}
    <p class="xs muted" style="margin-top:10px">Data is stored on this device. Reset it from Admin → Platform settings → Reset platform data.</p>
  </section>`;
  SA.ui.siteMapGroups = GROUPS;

  // One-tap sign-in for the demo accounts (partner dashboard and admin console have separate sessions)
  document.addEventListener('click', (e) => {
    if (e.target.closest('[data-dev-game]')) {
      // A game you hosted that ended an hour ago, with 3 accepted players — so the rating step can be tried (test only)
      try {
        const st = SA.core.store, u = st.get().user, now = new Date();
        const hour = Math.max(1, now.getHours() - 2), id = `m-test-${Date.now().toString(36).slice(-4)}`, date = SA.core.utils.isoDate(now);
        st.set((s) => ({ publishedMatches: [{ id, pitchId: 'p-maadi-cage', date, hour, duration: 1, total: 10, filled: 7, participants: [{ name: u.name, avatar: u.avatar, role: 'Host', photo: true, isMine: true }, ...['Friend 1', 'Friend 2', 'Friend 3'].map((n, i) => ({ name: n, avatar: i + 4, role: 'Player', photo: false }))],
          level: 'Intermediate', gender: 'Men', type: '5 v 5', pricePerPlayer: 70, notes: 'Test game', needsGK: false, isMine: true, hostName: u.name, bookingId: '', postedAt: Date.now() - 864e5, autoAccept: true, closed: true }, ...s.publishedMatches] }), 'match');
        ['Ziad Khaled', 'Amr Sherif', 'Belal Ehab'].forEach((n, i) => { const p = SA.services.games.simPlayer(n); SA.services.db.insert('gameRequests', { matchId: id, hostName: u.name, pitchName: 'Maadi Cage', date, hour, player: p, position: p.position, message: '', status: 'accepted', by: 'auto', amount: 70, paid: true, at: Date.now() - (5 - i) * 36e5 }, 'gr'); });
        SA.ui.overlay.toast('Test game created — rate the players'); open(`/players/${id}`);
      } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
      return;
    }
    if (e.target.closest('[data-dev-seller]')) {
      // Turns this device's shopper into an approved seller (test only)
      try {
        const S = SA.services.sellers, db = SA.services.db;
        let s = S.mine();
        if (!s) s = S.start({ mode: 'both', name: 'Sharaf Test Store', handle: `sharaf-test-${Math.random().toString(36).slice(2, 5)}`, agree: true });
        db.update('sellers', s.id, { status: 'active' }); if (s.storeId) db.update('stores', s.storeId, { status: 'active' });
        SA.ui.overlay.toast(`You’re a seller now: ${s.name}`); open('/community/creators');
      } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
      return;
    }
    if (e.target.closest('[data-dev-ceo]')) { try { SA.services.ceo.auth.signIn('2026'); SA.core.router.go('/ceo'); } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); } return; }
    const ag = e.target.closest('[data-dev-agent]');
    if (ag) { try { localStorage.setItem('superapp.admin.session', ag.dataset.devAgent); open('/admin'); } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); } return; }
    if (e.target.closest('[data-dev-big]')) {
      // A big balance credit asked by the admin manager: it waits for the CEO and the CFO (test only)
      const prev = localStorage.getItem('superapp.admin.session');
      const back = SA.core.router.location();
      SA.core.router.go('/admin', { replace: true });
      try { localStorage.setItem('superapp.admin.session', 'acc-admin'); SA.services.admin.adjust({ accountId: 'acc-o1', amount: 60000, reason: 'Test: tournament prize money' }); }
      catch (err) { SA.ui.overlay.toast(err.message, { type: err.code === 'needs_approval' ? 'info' : 'error' }); }
      finally { if (prev) localStorage.setItem('superapp.admin.session', prev); }
      setTimeout(() => SA.core.router.go(back.startsWith('/ceo') ? back : '/ceo/approvals', { replace: true }), 300);
      return;
    }
    const fb = e.target.closest('[data-dev-fac]');
    if (fb) { try { SA.services.factory.auth.signIn(fb.dataset.devFac, fb.dataset.pin); SA.ui.overlay.toast(`Factory: ${fb.textContent}`); open('/factory'); } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); } return; }
    if (e.target.closest('[data-dev-job]')) {
      // A two-item printed order from this device's shopper, straight to the factory (test only)
      try {
        const live = SA.services.pod.live({}).slice(0, 12), u = SA.core.store.get().user || {};
        const pick = [live[Math.floor(Math.random() * live.length)], live[Math.floor(Math.random() * live.length)]];
        const orderId = `ORD-T${Date.now().toString(36).toUpperCase().slice(-5)}`;
        SA.services.pod.place({ orderId, customer: { id: u.id || 'u-001', name: u.name || 'Test customer', city: 'Cairo', phone: u.phone || '', address: '14 Test St., Nasr City' }, payment: 'cod',
          lines: pick.map((d) => ({ designId: d.id, color: d.colors[0], size: d.product?.sizes?.[1] || 'M', qty: 1, title: d.title })) });
        SA.ui.overlay.toast(`Test order ${orderId} sent to the factory`);
      } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
      return;
    }
    const b = e.target.closest('[data-dev-as]'); if (!b) return;
    try { SA.services.dash.auth.signIn(b.dataset.devAs, { as: /^#?\/seller/.test(b.dataset.devGo) ? 'seller' : '' }); SA.ui.overlay.toast(`Signed in as ${b.textContent}`); open(b.dataset.devGo); }
    catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
  });
})(window.SA);
