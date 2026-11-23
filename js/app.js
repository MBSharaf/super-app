/** Bootstrap: seed local data, register routes, render pages into #app. */
(function (SA) {
  const { router } = SA.core;
  const P = SA.pages;
  const appEl = document.getElementById('app');
  let cleanup = null;
  let renderToken = 0;
  /** An old address that moved: open the new one in its place (keeps the query string) */
  P.moved = (to) => ({ title: 'Super Malaab', render(ctx) { const t = typeof to === 'function' ? to(ctx) : to; const q = SA.core.router.location().split('?')[1]; setTimeout(() => router.go(`${t}${q ? `?${q}` : ''}`, { replace: true })); return ''; } });

  /* ---------- Paused parts: their pages show "Coming soon", their links are hidden ----------
     Store paused (SA.config.storeEnabled = false): shop, cart, orders, sellers, factory.
     Print on demand paused (SA.config.podEnabled = false, the store selling sellers' own products only): studio, designs,
     the print catalogue and design review, samples, the factory. */
  const STORE_PATHS = /^\/(shop|cart|checkout|orders?|wishlist|sell|s|seller|factory)(\/|$)|^\/dash\/(orders|pod-catalog|pod-review|print|store|seller)(\/|$)|^\/admin\/(returns|designs|catalog|store|store-orders|sellers|orders)(\/|$)/;
  const POD_PATHS = /^\/(sell\/studio|sell\/bulk|seller\/designs|factory)(\/|$)|^\/dash\/(pod-catalog|pod-review|print)(\/|$)|^\/admin\/(designs|catalog|print|samples)(\/|$)/;
  const soon = (title, text, ic = 'bag') => ({
    title: `${title} — Super Malaab`,
    render: () => `<div class="container section-tight"><section class="panel panel-pad" style="text-align:center;max-width:560px;margin:48px auto">
      <span class="service-ico" style="margin:0 auto">${SA.ui.icon(ic, 26)}</span>
      <h1 class="h2" style="margin-top:16px">${title}</h1>
      <p class="lead soft" style="margin-top:10px">${text}</p>
      <div class="row wrap" style="--gap:10px;justify-content:center;margin-top:20px"><a class="btn btn-primary" href="#/pitches">Book a pitch</a><a class="btn btn-outline" href="#/">Home</a></div></section></div>`,
  });
  const pause = (test, page, paths, extra = '') => {
    const add = router.add.bind(router);
    router.add = (path, p, opts) => add(path, test.test(path) ? page : p, opts);
    const css = paths.map((x) => `a[href="#/${x}"],a[href^="#/${x}/"],a[href^="#/${x}?"]`).join(',');
    document.head.insertAdjacentHTML('beforeend', `<style>${css}${extra}{display:none!important}</style>`);
  };
  // Fantasy closed (SA.config.fantasyEnabled = false): its pages, links, the home fantasy band and its pop-up ads
  const FANTASY_PATHS = /^\/fantasy(\/|$)/;
  const MATCHES_PATHS = /^\/matches(\/|$)/;
  SA.core.isPaused = (path) => {
    const p = String(path || '').replace(/^#/, '').split('?')[0];
    return (!SA.config.storeEnabled && STORE_PATHS.test(p)) || (!SA.config.podEnabled && POD_PATHS.test(p)) || (!SA.config.fantasyEnabled && FANTASY_PATHS.test(p))
      || (!SA.config.matchesEnabled && MATCHES_PATHS.test(p));
  };
  if (!SA.config.fantasyEnabled) {
    P.fantasySoon = soon('Fantasy is coming soon', 'We’re getting Super Malaab fantasy ready. Meanwhile, book a pitch or join a game.', 'trophy');
    pause(FANTASY_PATHS, P.fantasySoon, ['fantasy'], ',.fantasy-band,.fc-fantasy,[data-fantasy-only]');
  }
  // Match schedule closed (GitHub Pages demo for the Paymob review): its page, links and the home "Live & next up" band
  if (!SA.config.matchesEnabled) {
    P.matchesSoon = soon('Live scores are coming soon', 'We’re getting the match schedule ready. Meanwhile, book a pitch or join a game.', 'calendar');
    pause(MATCHES_PATHS, P.matchesSoon, ['matches'], ',.matchday-band');
  }
  if (!SA.config.storeEnabled) {
    P.comingSoon = soon('The Super Malaab store is coming soon', 'We’re getting the shop, sellers’ designs and delivery ready. Meanwhile, book a pitch or join a game.');
    pause(STORE_PATHS, P.comingSoon, ['shop', 'cart', 'checkout', 'order', 'orders', 'wishlist', 'sell', 's/', 'seller', 'factory', 'dash/orders', 'dash/pod-catalog', 'dash/pod-review', 'dash/print', 'dash/store', 'dash/seller', 'admin/returns', 'admin/designs', 'admin/catalog', 'admin/store', 'admin/sellers', 'admin/orders'],
      ',[data-cart-target],.fc-product');
  } else if (!SA.config.podEnabled) {
    P.podSoon = soon('Print on demand is coming soon', 'Sellers sell their own products for now. Printing your designs on tees and hoodies comes later.');
    pause(POD_PATHS, P.podSoon, ['sell/studio', 'sell/bulk', 'seller/designs', 'dash/seller/designs', 'factory', 'dash/pod-catalog', 'dash/pod-review', 'dash/print', 'admin/designs', 'admin/catalog', 'admin/print', 'admin/samples'], ',[data-pod-only]');
  }

  router
    .add('/', P.home)
    .add('/pitches', P.pitches)
    .add('/pitches/:id', P.pitchDetail)
    .add('/book/:id', P.bookingCheckout)
    .add('/booking/:id', P.bookingDetail)
    .add('/bookings', P.bookings)
    .add('/players', P.players)
    .add('/players/new', P.publishMatch)
    .add('/players/:id', P.matchDetail)
    .add('/fantasy', P.fantasy)
    .add('/fantasy/squad', P.fantasySquad)
    .add('/fantasy/leagues', P.fantasyLeagues)
    .add('/fantasy/league/:id', P.fantasyLeague)
    .add('/fantasy/fixtures', P.fantasyFixtures)
    .add('/fantasy/stats', P.fantasyStats)
    .add('/shop', P.shop)
    .add('/shop/:id', P.product)
    .add('/cart', P.cart)
    .add('/checkout', P.checkout)
    .add('/order/:id', P.order)
    .add('/orders', P.orders)
    .add('/wishlist', P.wishlist)
    .add('/profile', P.profile)
    // Real accounts (API mode)
    .add('/auth/sign-in', P.authSignIn)
    .add('/auth/sign-up', P.authSignUp)
    .add('/auth/2fa', P.authTwoFactor)
    .add('/auth/2fa-setup', P.authTwoFactorSetup)
    .add('/auth/forgot', P.authForgot)
    .add('/auth/reset', P.authReset)
    .add('/auth/confirm', P.authConfirm)
    .add('/auth/invite', P.authInvite)
    .add('/wallet', P.wallet)
    .add('/matches', P.matches)
    .add('/tournaments', P.tournaments)
    .add('/tournaments/:id', P.tournament)
    .add('/sell', P.sell)
    .add('/sell/studio', P.studio)
    .add('/sell/bulk', P.bulkUpload)
    .add('/sell/studio/:id', P.studio)
    .add('/s/:handle', P.sellerStore)
    .add('/support', P.support)
    .add('/community', P.communities)
    .add('/alerts', P.alerts)
    .add('/community/:slug', P.community)
    .add('/community/:slug/:postId', P.communityPost)
    .add('/support/:id', P.supportTicket)
    // Partner dashboard (admins + pitch owners)
    .add('/dash', P.dashOverview)
    .add('/dash/pitches', P.dashPitches)
    .add('/dash/pitches/new', P.dashPitchEdit)
    .add('/dash/pitches/:id', P.dashPitchEdit)
    .add('/dash/availability', P.dashAvailability)
    .add('/dash/checkin', P.dashCheckin)
    .add('/dash/teams', P.dashTeams)
    .add('/dash/reviews', P.dashReviews)
    .add('/dash/orders', P.moved('/seller/store-orders'))
    .add('/dash/insights', P.dashInsights)
    .add('/dash/slots', P.dashSlots)
    .add('/dash/codes', P.dashCodes)
    .add('/dash/tournaments', P.dashTournaments)
    .add('/dash/tournaments/:id', P.dashTournament)
    .add('/dash/pod-catalog', P.dashPodCatalog)
    .add('/dash/pod-review', P.dashPodReview)
    .add('/dash/print', P.factoryJobs)
    .add('/seller', P.dashSeller)
    .add('/seller/designs', P.dashSellerDesigns)
    .add('/seller/orders', P.dashSellerOrders)
    .add('/seller/wallet', P.dashSellerWallet)
    .add('/seller/calendar', P.dashSellerCalendar)
    .add('/seller/analytics', P.dashSellerAnalytics)
    .add('/seller/store', P.dashSellerStore)
    .add('/seller/products', P.dashStore)
    .add('/seller/products/product/new', P.dashProductEdit)
    .add('/seller/products/product/:id', P.dashProductEdit)
    .add('/seller/store-orders', P.dashOrders)
    .add('/seller/returns', P.sellerReturns)
    .add('/seller/codes', P.dashCodes)
    .add('/seller/notifications', P.dashNotifications)
    .add('/seller/support', P.dashSupport)
    .add('/seller/support/:id', P.dashSupportTicket)
    .add('/dash/sell', P.dashSell)
    .add('/dash/seller', P.moved('/seller'))
    .add('/dash/seller/:page', P.moved((c) => `/seller/${c.params.page}`))
    .add('/dash/notifications', P.dashNotifications)
    .add('/dash/support', P.dashSupport)
    .add('/dash/support/:id', P.dashSupportTicket)
    .add('/dash/bookings', P.dashBookings)
    .add('/dash/finance', P.dashFinance)
    .add('/dash/wallet', P.dashWallet)
    .add('/dash/store', P.moved('/seller/products'))
    .add('/dash/store/product/new', P.moved('/seller/products/product/new'))
    .add('/dash/store/product/:id', P.moved((c) => `/seller/products/product/${c.params.id}`))
    .add('/dash/partners', P.dashPartners)
    .add('/dash/settings', P.dashSettings)
    .add('/dash/activity', P.dashActivity)
    // Admin console (separate system for the Super Malaab team)
    .add('/admin', P.adminOverview)
    .add('/admin/audit', P.adminAudit)
    .add('/admin/payouts', P.adminPayouts)
    .add('/admin/finance-approvals', P.adminFinanceApprovals)
    .add('/admin/returns', P.adminReturns)
    .add('/admin/cancellations', P.adminCancellations)
    .add('/admin/revenue', P.adminRevenue)
    .add('/admin/settlements', P.adminSettlements)
    .add('/admin/notifications', P.dashNotifications)
    .add('/admin/designs', P.dashPodReview)
    .add('/admin/catalog', P.dashPodCatalog)
    .add('/admin/print', P.adminFactories)
    .add('/admin/factories', P.adminFactories)
    .add('/admin/reviews', P.dashReviews)
    .add('/admin/partners', P.adminPartners)
    .add('/admin/customers', P.adminCustomers)
    .add('/admin/orders', P.adminOrders)
    .add('/admin/broadcast', P.adminBroadcast)
    .add('/admin/promos', P.adminPromos)
    .add('/admin/content', P.adminContent)
    .add('/admin/fantasy', P.adminFantasy)
    .add('/admin/levels', P.adminLevels)
    .add('/admin/samples', P.adminSamples)
    .add('/admin/analytics', P.adminAnalytics)
    .add('/admin/approvals', P.adminApprovals)
    .add('/admin/community', P.adminCommunity)
    .add('/admin/games', P.adminGames)
    .add('/admin/tournaments', P.dashTournaments)
    .add('/admin/tournaments/:id', P.dashTournament)
    .add('/admin/support', P.adminSupport)
    .add('/admin/support/:id', P.adminTicket)
    .add('/admin/sellers', P.adminSellers)
    .add('/admin/settings', P.dashSettings)
    .add('/admin/store', P.dashStore)
    .add('/admin/store/product/new', P.dashProductEdit)
    .add('/admin/store/product/:id', P.dashProductEdit)
    .add('/admin/store-orders', P.dashOrders)
    .add('/help', P.help)
    .add('/legal', P.legalIndex)
    .add('/legal/:key', P.legalDoc)
    .add('/partner/apply', P.partnerApply)
    .add('/my-games', P.myGames)
    .add('/ceo', P.ceoOverview)
    .add('/ceo/briefing', P.ceoBriefing)
    .add('/ceo/approvals', P.ceoApprovals)
    .add('/ceo/departments', P.ceoDepartments)
    .add('/ceo/department/:key', P.ceoDepartment)
    .add('/ceo/agents', P.ceoAgents)
    .add('/ceo/agent/:id', P.ceoAgent)
    .add('/ceo/team', P.ceoTeam)
    .add('/ceo/team/:id', P.ceoTeamMember)
    .add('/ceo/roles', P.ceoRoles)
    .add('/ceo/goals', P.ceoGoals)
    .add('/ceo/budgets', P.ceoBudgets)
    .add('/ceo/calendar', P.ceoCalendar)
    .add('/ceo/reports', P.ceoReports)
    .add('/ceo/whatif', P.ceoWhatIf)
    .add('/ceo/control', P.ceoControl)
    .add('/ceo/systems', P.ceoSystems)
    .add('/ceo/integrations', P.ceoIntegrations)
    .add('/ceo/apis', P.ceoApis)
    .add('/ceo/audit', P.ceoAudit)
    .add('/ceo/sitemap', P.ceoSiteMap)
    .add('/ceo/legal', P.ceoLegal)
    .add('/factory', P.factoryOverview)
    .add('/factory/jobs', P.factoryJobs)
    .add('/factory/job/:id', P.factoryJob)
    .add('/factory/scan', P.factoryScan)
    .add('/factory/shipping', P.factoryShipping)
    .add('/factory/team', P.factoryTeam)
    .add('/factory/money', P.factoryMoney)
    .add('/factory/reports', P.factoryReports)
    .add('/factory/settings', P.factorySettings)
    .add('/factory/activity', P.factoryActivity)
    .add('*', P.notFound);

  /* ---------- Business areas on the public domain ----------
     On supermalaab.com the staff and partner areas (CEO system, admin console, partner, seller and factory dashboards)
     open only when the real server answers: it checks the account, the role and the 2-factor code on every request.
     Without it (static hosting, demo data in the browser) a PIN in the page is no protection, so the areas stay closed.
     GitHub Pages and local copies keep the demo PINs for testing. */
  const BUSINESS_PATHS = /^\/(ceo|admin|dash|seller|factory)(\/|$)/;
  const PUBLIC_HOST = /(^|\.)supermalaab\.com$/i.test(location.hostname);
  P.businessLocked = {
    title: 'Staff area — Super Malaab',
    render: () => `<div class="container section-tight"><section class="panel panel-pad" style="text-align:center;max-width:560px;margin:48px auto">
      <span class="service-ico" style="margin:0 auto">${SA.ui.icon('lock', 26)}</span>
      <h1 class="h2" style="margin-top:16px">This area isn’t open yet</h1>
      <p class="lead soft" style="margin-top:10px">Dashboards for staff and partners open with secure sign-in when Super Malaab launches.</p>
      <div class="row wrap" style="--gap:10px;justify-content:center;margin-top:20px"><a class="btn btn-primary" href="#/pitches">Book a pitch</a><a class="btn btn-outline" href="#/">Home</a></div></section></div>`,
  };
  const lockedHere = async (path) => PUBLIC_HOST && BUSINESS_PATHS.test(path) && (await SA.services.api.ready) !== 'api';
  if (PUBLIC_HOST) document.head.insertAdjacentHTML('beforeend', '<style>html:not([data-backend="api"]) :is(a[href^="#/ceo"],a[href^="#/admin"],a[href^="#/dash"],a[href^="#/seller"],a[href^="#/factory"]){display:none!important}</style>');

  const render = async (match, { keepScroll = false } = {}) => {
    const token = ++renderToken;
    const page = (await lockedHere(match.path)) ? P.businessLocked : match.route.page;
    if (token !== renderToken) return;
    const ctx = { params: match.params, query: match.query, path: match.path };
    SA.ui.shell.render(match.path);
    document.body.classList.add('is-loading');
    let html;
    try { html = await page.render(ctx); }
    catch (err) {
      console.error(err);
      html = `<div class="container section">${SA.ui.c.empty({ icon: 'alert', title: err.code === 'not_found' ? 'Not found' : 'Something went wrong', text: SA.core.utils.esc(err.message || 'Please try again.'), action: '<a class="btn btn-primary" href="#/">Back to home</a>' })}</div>`;
    }
    if (token !== renderToken) return; // a newer navigation won
    document.body.classList.remove('is-loading');
    if (typeof cleanup === 'function') cleanup();
    SA.ui.overlay.closeAll();
    const scroll = scrollY;
    let swapped = false;
    const swap = () => {
      // A newer render already ran (a view transition can call this late): never put an older page back
      if (swapped || token !== renderToken) return;
      swapped = true;
      const view = document.createElement('div');
      view.className = 'view';
      view.innerHTML = html;
      appEl.replaceChildren(view);
      window.saSplashDone?.(); // the first page is on screen: hide the boot splash (index.html)
      document.title = (page.title || 'Super Malaab').replace(/Super Malaab Partner$/, match.path.startsWith('/admin') ? 'Super Malaab Admin' : 'Super Malaab Partner');
      if (keepScroll) window.scrollTo(0, scroll);
      else window.scrollTo(0, 0);
      cleanup = page.mount ? page.mount(view, ctx) : null;
      // In-place re-renders keep what is already on screen visible (no re-reveal flicker)
      if (keepScroll) view.classList.add('no-motion');
      SA.core.motion.enhance(view);
      if (!keepScroll && !match.query.new) appEl.focus({ preventScroll: true });
    };
    const motion = SA.core.motion.enabled();
    if (!keepScroll && motion && document.startViewTransition) {
      // Cross-page transition: old page lifts away, new page rises in (css/motion.css)
      const vt = document.startViewTransition(swap);
      // A transition can be skipped (hidden tab, rapid navigation); the page still swaps, so ignore those rejections
      vt.ready.catch(() => {}); vt.finished.catch(() => {});
      // Safety net: if the browser never runs the transition callback, swap anyway
      await Promise.race([vt.updateCallbackDone.catch(() => {}), new Promise((r) => setTimeout(r, 1500))]);
      if (!swapped && token === renderToken) swap();
    } else {
      swap();
      if (!keepScroll && motion) { appEl.classList.remove('is-entering'); void appEl.offsetWidth; appEl.classList.add('is-entering'); }
    }
  };

  SA.app = {
    /** Re-render the current route in place (after a state change) */
    rerender: () => render(router.current(), { keepScroll: true }),
  };

  SA.services.user.ensureSeeded();
  SA.ui.shell.init();
  SA.i18n.start();
  // Real product photos for print-on-demand items: load in the background, then swap out the drawn previews
  // (skipped while someone is editing — the studio swaps its own canvas)
  if (SA.ui.photoMock) {
    let t = 0;
    SA.ui.photoMock.onReady(() => {
      clearTimeout(t);
      t = setTimeout(() => {
        const m = router.current();
        if (!m || /^\/sell\/studio/.test(m.path) || document.querySelector('form :focus')) return;
        if (document.querySelector('.garment-skel, svg.garment:not([data-photo]), img[src^="data:image/svg"]')) SA.app.rerender();
      }, 160);
    });
    SA.ui.photoMock.load();
  }
  SA.ui.chatbot.init();
  SA.ui.promoPop?.init();
  SA.ui.pointsPop?.init();
  // Real mode (the server's site): clear the demo records the services added while loading, before the first page
  SA.services.db.scrubDemo();
  router.onChange((m) => render(m));
  router.start();
})(window.SA);
