/** Bootstrap: seed local data, register routes, render pages into #app. */
(function (SA) {
  const { router } = SA.core;
  const P = SA.pages;
  const appEl = document.getElementById('app');
  let cleanup = null;
  let renderToken = 0;

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
    .add('/wallet', P.wallet)
    .add('/news', P.news)
    .add('/news/:id', P.newsArticle)
    .add('/matches', P.matches)
    .add('/tournaments', P.tournaments)
    .add('/tournaments/:id', P.tournament)
    .add('/sell', P.sell)
    .add('/sell/studio', P.studio)
    .add('/sell/studio/:id', P.studio)
    .add('/s/:handle', P.sellerStore)
    .add('/support', P.support)
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
    .add('/dash/orders', P.dashOrders)
    .add('/dash/insights', P.dashInsights)
    .add('/dash/codes', P.dashCodes)
    .add('/dash/tournaments', P.dashTournaments)
    .add('/dash/tournaments/:id', P.dashTournament)
    .add('/dash/pod-catalog', P.dashPodCatalog)
    .add('/dash/pod-review', P.dashPodReview)
    .add('/dash/print', P.dashPrint)
    .add('/dash/seller', P.dashSeller)
    .add('/dash/seller/designs', P.dashSellerDesigns)
    .add('/dash/seller/orders', P.dashSellerOrders)
    .add('/dash/seller/wallet', P.dashSellerWallet)
    .add('/dash/seller/calendar', P.dashSellerCalendar)
    .add('/dash/seller/store', P.dashSellerStore)
    .add('/dash/notifications', P.dashNotifications)
    .add('/dash/support', P.dashSupport)
    .add('/dash/support/:id', P.dashSupportTicket)
    .add('/dash/bookings', P.dashBookings)
    .add('/dash/finance', P.dashFinance)
    .add('/dash/wallet', P.dashWallet)
    .add('/dash/store', P.dashStore)
    .add('/dash/store/product/new', P.dashProductEdit)
    .add('/dash/store/product/:id', P.dashProductEdit)
    .add('/dash/partners', P.dashPartners)
    .add('/dash/settings', P.dashSettings)
    .add('/dash/activity', P.dashActivity)
    // Admin console (separate system for the Super App team)
    .add('/admin', P.adminOverview)
    .add('/admin/audit', P.adminAudit)
    .add('/admin/payouts', P.adminPayouts)
    .add('/admin/revenue', P.adminRevenue)
    .add('/admin/notifications', P.dashNotifications)
    .add('/admin/designs', P.dashPodReview)
    .add('/admin/catalog', P.dashPodCatalog)
    .add('/admin/print', P.dashPrint)
    .add('/admin/reviews', P.dashReviews)
    .add('/admin/partners', P.adminPartners)
    .add('/admin/customers', P.adminCustomers)
    .add('/admin/orders', P.adminOrders)
    .add('/admin/support', P.adminSupport)
    .add('/admin/support/:id', P.adminTicket)
    .add('/admin/sellers', P.adminSellers)
    .add('/admin/settings', P.dashSettings)
    .add('*', P.notFound);

  const render = async (match, { keepScroll = false } = {}) => {
    const token = ++renderToken;
    const page = match.route.page;
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
      if (swapped) return;
      swapped = true;
      const view = document.createElement('div');
      view.className = 'view';
      view.innerHTML = html;
      appEl.replaceChildren(view);
      document.title = page.title || 'Super App';
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
        if (document.querySelector('svg.garment:not([data-photo]), img[src^="data:image/svg"]')) SA.app.rerender();
      }, 160);
    });
    setTimeout(() => SA.ui.photoMock.load(), 300);
  }
  SA.ui.whatsapp.init();
  router.onChange((m) => render(m));
  router.start();
})(window.SA);
