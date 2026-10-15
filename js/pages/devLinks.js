/**
 * TEMPORARY — "Sharaf" test section on the home page: every page of the site, the partner dashboard and the admin
 * console in one place, with one-tap sign-in for the demo accounts, so everything can be tested from a phone.
 * To remove it: delete this file, its <script> line in index.html, the ${SA.ui.devLinks?.() || ''} line in
 * js/pages/home.js and the "Sharaf test section" block at the end of css/pages/admin.css.
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
    { title: 'Shop', icon: 'bag', links: [
      ['Shop', '#/shop'], ['Designs', '#/shop?cat=designs'], ['Product', '#/shop/sh-predator'], ['Wishlist', '#/wishlist'], ['Cart', '#/cart'], ['Checkout', '#/checkout'], ['Orders', '#/orders'],
    ] },
    { title: 'Fantasy', icon: 'trophy', links: [
      ['Fantasy home', '#/fantasy'], ['Pick squad', '#/fantasy/squad'], ['Leagues & Pass', '#/fantasy/leagues'], ['Fixtures', '#/fantasy/fixtures'], ['Stats', '#/fantasy/stats'],
    ] },
    { title: 'Sell', icon: 'shirt', links: [
      ['Start selling', '#/sell'], ['Design studio', '#/sell/studio'], ['Store: Koora Ink', '#/s/koora-ink'], ['Store: Nile Lines', '#/s/nile-lines'], ['Store: Cairo Otaku', '#/s/cairo-otaku'],
    ] },
    { title: 'Pitch owner dashboard', icon: 'pitch', as: [['acc-o1', 'Karim · Turf 22'], ['acc-new', 'New partner']], links: [
      ['Overview', '#/dash'], ['My pitches', '#/dash/pitches'], ['Add a pitch', '#/dash/pitches/new'], ['Availability', '#/dash/availability'], ['Check-in', '#/dash/checkin'], ['Bookings', '#/dash/bookings'],
      ['Teams', '#/dash/teams'], ['Reviews', '#/dash/reviews'], ['Tournaments', '#/dash/tournaments'], ['Finance', '#/dash/finance'], ['Wallet', '#/dash/wallet'], ['Forecast & offers', '#/dash/insights'],
      ['Discount codes', '#/dash/codes'], ['My store', '#/dash/store'], ['Store orders', '#/dash/orders'], ['Notifications', '#/dash/notifications'], ['Help & support', '#/dash/support'], ['Activity', '#/dash/activity'],
    ] },
    { title: 'Seller dashboard', icon: 'image', as: [['acc-s1', 'Koora Ink · Elite'], ['acc-s2', 'Nile Lines · Pro'], ['acc-s3', 'Cairo Otaku · Rising']], links: [
      ['Overview', '#/dash/seller'], ['Store settings', '#/dash/seller/store'], ['Marketing calendar', '#/dash/seller/calendar'], ['Analytics', '#/dash/seller/analytics'], ['Designs & samples', '#/dash/seller/designs'],
      ['Print orders', '#/dash/seller/orders'], ['Wallet', '#/dash/seller/wallet'], ['Notifications', '#/dash/notifications'],
    ] },
    { title: 'Print partner', icon: 'package', as: [['acc-print', 'Print partner']], links: [['Print queue', '#/dash/print'], ['Notifications', '#/dash/notifications']] },
    { title: 'Admin console', icon: 'shield', as: [['acc-admin', 'Super App HQ']], links: [
      ['Overview', '#/admin'], ['Withdrawals', '#/admin/payouts'], ['Revenue', '#/admin/revenue'], ['Customers', '#/admin/customers'], ['Partners', '#/admin/partners'], ['Sellers', '#/admin/sellers'],
      ['Approvals', '#/admin/approvals'], ['Communities', '#/admin/community'], ['Orders & bookings', '#/admin/orders'], ['Tournaments', '#/admin/tournaments'], ['Support', '#/admin/support'], ['Super App Store', '#/admin/store'], ['Reviews', '#/admin/reviews'],
      ['Design review', '#/admin/designs'], ['Level review', '#/admin/levels'], ['Samples', '#/admin/samples'], ['Print orders', '#/admin/print'], ['Print catalogue', '#/admin/catalog'], ['Broadcasts', '#/admin/broadcast'],
      ['Promo codes', '#/admin/promos'], ['Site content', '#/admin/content'], ['Fantasy', '#/admin/fantasy'], ['Analytics', '#/admin/analytics'], ['Audit log', '#/admin/audit'], ['Platform settings', '#/admin/settings'],
    ] },
  ];

  SA.ui.devLinks = () => `<section class="container section-tight dev-links" data-i18n-skip>
    <div class="dev-head"><span class="badge badge-blaze">Temporary · test only</span><h2 class="h2">Sharaf</h2><p class="small soft">Every page in one place for testing on the phone. Dashboards: tap an account first to sign in as it, then open the pages.</p></div>
    ${GROUPS.map((g, i) => `<details class="dev-group" ${i === 0 ? 'open' : ''}><summary>${icon(g.icon, 18)}<b class="grow">${g.title}</b><span class="xs muted">${g.links.length}</span>${icon('chevronDown', 16)}</summary>
      ${g.seller ? `<div class="dev-as"><span class="xs muted">Seller-only communities need a seller account:</span><button class="chip" data-dev-seller>Make me a test seller</button><button class="chip" data-dev-as="acc-admin" data-dev-go="#/admin/community">Sign in as admin</button></div>` : ''}
      ${g.as ? `<div class="dev-as"><span class="xs muted">Sign in as:</span>${g.as.map(([id, l]) => `<button class="chip" data-dev-as="${id}" data-dev-go="${g.links[0][1]}">${l}</button>`).join('')}</div>` : ''}
      <div class="dev-grid">${g.links.map(([l, h]) => `<a class="dev-link" href="${h}"><span>${l}</span><span class="xs muted">${h.replace('#', '')}</span></a>`).join('')}</div></details>`).join('')}
    <p class="xs muted" style="margin-top:10px">Data is stored on this device. Reset it from Admin → Platform settings → Reset platform data.</p>
  </section>`;

  // One-tap sign-in for the demo accounts (partner dashboard and admin console have separate sessions)
  document.addEventListener('click', (e) => {
    if (e.target.closest('[data-dev-seller]')) {
      // Turns this device's shopper into an approved seller (test only)
      try {
        const S = SA.services.sellers, db = SA.services.db;
        let s = S.mine();
        if (!s) s = S.start({ mode: 'both', name: 'Sharaf Test Store', handle: `sharaf-test-${Math.random().toString(36).slice(2, 5)}`, agree: true });
        db.update('sellers', s.id, { status: 'active' }); if (s.storeId) db.update('stores', s.storeId, { status: 'active' });
        SA.ui.overlay.toast(`You’re a seller now: ${s.name}`); location.hash = '#/community/creators';
      } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
      return;
    }
    const b = e.target.closest('[data-dev-as]'); if (!b) return;
    try { SA.services.dash.auth.signIn(b.dataset.devAs); SA.ui.overlay.toast(`Signed in as ${b.textContent}`); location.hash = b.dataset.devGo; }
    catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
  });
})(window.SA);
