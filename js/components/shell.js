/** App shell: header, mobile tab bar, footer, notifications drawer and global delegated actions. */
(function (SA) {
  const { esc, money, timeAgo } = SA.core.utils;
  const { icon } = SA.ui;
  const store = SA.core.store;

  const NAV = [
    { key: 'home', label: 'Home', href: '#/', icon: 'home', match: (p) => p === '/' },
    { key: 'pitches', label: 'Book a pitch', short: 'Book', href: '#/pitches', icon: 'pitch', match: (p) => /^\/(pitches|booking|bookings)/.test(p) },
    { key: 'players', label: 'Find players', short: 'Players', href: '#/players', icon: 'users', match: (p) => /^\/players/.test(p) },
    { key: 'fantasy', label: 'Fantasy', href: '#/fantasy', icon: 'trophy', match: (p) => /^\/fantasy/.test(p) },
    { key: 'shop', label: 'Shop', href: '#/shop', icon: 'bag', match: (p) => /^\/(shop|cart|checkout|order|wishlist)/.test(p) },
  ];
  // Media sections: in the desktop menu, and in the menu drawer on mobile
  const EXTRA = [
    { key: 'tournaments', label: 'Tournaments', href: '#/tournaments', icon: 'trophy', match: (p) => /^\/tournaments/.test(p) },
    { key: 'matches', label: 'Matches', href: '#/matches', icon: 'calendar', match: (p) => /^\/matches/.test(p) },
    { key: 'news', label: 'News', href: '#/news', icon: 'info', match: (p) => /^\/news/.test(p) },
  ];

  const brand = `<a class="brand" href="#/" aria-label="Super App home"><span class="brand-mark"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2.6" stroke-linecap="round"><path d="M17 7.2C15.9 6 14.3 5.4 12.4 5.4c-2.7 0-4.5 1.4-4.5 3.4 0 4.6 9.2 2.6 9.2 7 0 2-1.9 3.4-4.8 3.4-2.1 0-3.9-.7-5.3-2"/></svg></span><span>SUPER APP</span></a>`;

  /** Theme + language switches (header, dashboard, menu) */
  const prefButtons = (cls = '') => `<button class="icon-btn ${cls}" data-action="toggle-theme" aria-label="${SA.core.theme.current() === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}" title="${SA.core.theme.current() === 'dark' ? 'Light mode' : 'Dark mode'}">${icon(SA.core.theme.current() === 'dark' ? 'sun' : 'moon', 19)}</button><button class="icon-btn lang-btn ${cls}" data-action="toggle-lang" aria-label="${SA.i18n.isAr ? 'English' : 'العربية'}" title="${SA.i18n.isAr ? 'English' : 'العربية'}" data-i18n-skip>${SA.i18n.isAr ? 'EN' : 'ع'}</button>`;

  const header = (path) => {
    const s = store.get();
    const cartCount = s.cart.reduce((a, l) => a + l.qty, 0);
    const unread = s.notifications.filter((n) => !n.read).length;
    const u = s.user;
    const mt = SA.services.db?.settings().maintenance;
    const an = mt && (mt.bookings || mt.shop) ? { text: `${mt.bookings && mt.shop ? 'Bookings and shop checkout are' : mt.bookings ? 'Pitch bookings are' : 'Shop checkout is'} paused for maintenance. ${mt.message || ''}`.trim(), tone: 'alert', v: `m${mt.v || 0}` } : SA.services.content?.announcement();
    const bar = an && !SA.services.content.dismissed(an) ? `<div class="announce t-${an.tone || 'info'}" role="status"><div class="container announce-inner">${icon(an.tone === 'promo' ? 'gift' : an.tone === 'alert' ? 'alert' : 'megaphone', 16)}<span class="grow small">${SA.core.utils.esc(an.text)}${an.link ? ` <a class="announce-link" href="${SA.core.utils.esc(an.link)}">${SA.core.utils.esc(an.cta || 'Learn more')}</a>` : ''}</span><button class="icon-btn announce-x" data-announce-x aria-label="Hide announcement">${icon('x', 14)}</button></div></div>` : '';
    return `${bar}<div class="container header-inner">
      ${brand}
      <nav class="main-nav" aria-label="Main">${[...NAV, ...EXTRA].map((n) => `<a href="${n.href}" ${n.match(path) ? 'aria-current="page"' : ''}>${n.label}</a>`).join('')}</nav>
      <div class="header-actions">
        ${prefButtons('hdr-pref')}
        <a class="wallet-chip" href="#/wallet" aria-label="Wallet balance ${money(s.wallet.balance)}"><span class="w-ico">${icon('wallet', 15, 2)}</span><span class="num">${money(s.wallet.balance)}</span></a>
        <button class="icon-btn" data-action="open-notifications" aria-label="Notifications${unread ? `, ${unread} unread` : ''}">${icon('bell')}${unread ? `<span class="dot-count">${unread}</span>` : ''}</button>
        <a class="icon-btn" href="#/cart" data-cart-target aria-label="Cart${cartCount ? `, ${cartCount} items` : ''}">${icon('cart')}${cartCount ? `<span class="dot-count" data-cart-count>${cartCount}</span>` : ''}</a>
        <a class="header-avatar" href="#/profile" aria-label="Your profile">${SA.ui.c.avatar(u)}</a>
        <button class="icon-btn menu-btn" data-action="open-menu" aria-label="Open menu">${icon('grid')}</button>
      </div>
    </div>`;
  };

  const tabbar = (path) => NAV.map((n) => {
    let badge = '';
    if (n.key === 'shop') { const c = store.get().cart.reduce((a, l) => a + l.qty, 0); if (c) badge = `<span class="tab-badge" data-cart-target>${c}</span>`; }
    return `<a href="${n.href}" ${n.match(path) ? 'aria-current="page"' : ''}>${icon(n.icon, 22)}<span>${n.short || n.label}</span>${badge}</a>`;
  }).join('');

  const footer = () => `<div class="container">
    <div class="footer-grid">
      <div class="footer-brand">${brand}<p>Book pitches, fill your squad, play Egyptian Premier League fantasy and gear up — all from one account and one wallet.</p>${SA.ui.social.render({ variant: 'dark' })}</div>
      <div><h4>Play</h4><a href="#/pitches">Book a pitch</a><a href="#/players">Find players</a><a href="#/tournaments">Tournaments</a><a href="#/players/new">Post a match</a><a href="#/bookings">My bookings</a></div>
      <div><h4>Fantasy</h4><a href="#/fantasy">My team</a><a href="#/fantasy/leagues">Leagues</a><a href="#/fantasy/fixtures">Fixtures</a><a href="#/fantasy/stats">Player stats</a></div>
      <div><h4>Shop</h4><a href="#/shop?cat=boots">Football boots</a><a href="#/shop?cat=kits">Jerseys & kits</a><a href="#/shop?sale=1">Sale</a><a href="#/orders">Order history</a><a href="#/sell">Sell on Super App</a></div>
      <div><h4>Account</h4><a href="#/profile">Profile</a><a href="#/wallet">Wallet</a><a href="#/wishlist">Wishlist</a><a href="#/support">Help & support</a><a href="#/dash">Partner dashboard</a><a href="#" data-action="reset-demo">Reset demo data</a><button class="footer-link" data-action="toggle-motion" aria-pressed="false">Animations: <b data-motion-label>On</b></button><button class="footer-link" data-action="cycle-theme">Theme: <b data-theme-label>${SA.core.theme.label()}</b></button><button class="footer-link" data-action="toggle-lang" data-i18n-skip>${SA.i18n.isAr ? 'English' : 'العربية'}</button></div>
    </div>
    <div class="footer-base"><span>© 2026 Super App. Cairo, Egypt.</span><span>Frontend demo: payments, bookings and fantasy scores are simulated on this device.</span></div>
  </div>`;

  const openNotifications = () => {
    const list = store.get().notifications;
    const kindIcon = { fantasy: 'trophy', match: 'users', booking: 'pitch', shop: 'bag' };
    const m = SA.ui.overlay.open({
      drawer: true, title: 'Notifications', subtitle: list.some((n) => !n.read) ? `${list.filter((n) => !n.read).length} unread` : 'You’re all caught up',
      body: list.length ? `<ul class="notif-list">${list.map((n) => `<li><a href="#${n.link || '/'}" class="notif ${n.read ? '' : 'is-unread'}" data-nid="${n.id}">
          <span class="notif-ico k-${n.kind}">${icon(kindIcon[n.kind] || 'bell', 18)}</span>
          <span class="grow"><b>${esc(n.title)}</b><span class="small soft" style="display:block">${esc(n.body)}</span><span class="xs muted">${timeAgo(n.at)}</span></span></a></li>`).join('')}</ul>`
        : SA.ui.c.empty({ icon: 'bell', title: 'No notifications yet', text: 'Booking confirmations, match updates and fantasy deadlines will show up here.' }),
      footer: list.length ? `<button class="btn btn-outline btn-block" data-mark-all>Mark all as read</button>` : '',
      onMount: (el, close) => {
        el.querySelectorAll('[data-nid]').forEach((a) => a.addEventListener('click', () => { SA.services.user.markRead(a.dataset.nid); close(); }));
        el.querySelector('[data-mark-all]')?.addEventListener('click', () => { SA.services.user.markAllRead(); close(); SA.ui.overlay.toast('All notifications marked as read'); });
      },
    });
    return m;
  };

  /** Size picker used by "quick add" on product cards */
  const quickAdd = (id, fromEl) => {
    const p = SA.services.shop.getSync(id);
    const add = (size) => {
      try { SA.services.shop.addToCart(id, { size }); } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); return; }
      SA.core.motion.flyTo(fromEl?.closest('[data-product]')?.querySelector('[data-fly]'));
      SA.ui.overlay.toast(`Added to cart — ${esc(p.name)}`, { action: 'View cart', onAction: () => SA.core.router.go('/cart') });
    };
    if (p.sizes.length <= 1) return add(p.sizes[0]);
    SA.ui.overlay.open({
      title: 'Choose a size', subtitle: esc(p.name),
      body: `<div class="size-grid">${p.sizes.map((s) => `<button class="size-btn" data-size="${esc(s)}">${esc(s)}</button>`).join('')}</div><p class="xs muted" style="margin-top:14px">Not sure? Open the product for the size guide.</p>`,
      onMount: (el, close) => el.querySelectorAll('[data-size]').forEach((b) => b.addEventListener('click', () => { close(); add(b.dataset.size); })),
    });
  };

  /** Global click delegation for actions that appear on many pages. */
  const bindGlobal = () => {
    document.addEventListener('click', async (e) => {
      if (e.target.closest('[data-announce-x]')) { const an = SA.services.content?.announcement(); if (an) SA.services.content.dismiss(an); render(); return; }
      const t = e.target.closest('[data-action]');
      if (!t) return;
      const { action, id } = t.dataset;
      switch (action) {
        case 'open-notifications': e.preventDefault(); openNotifications(); break;
        case 'open-menu': {
          e.preventDefault();
          const path = SA.core.router.current()?.path || '/';
          const live = SA.services.schedule.listSync(SA.core.utils.isoDate(SA.core.utils.today())).filter((m) => m.status === 'live').length;
          SA.ui.overlay.open({
            drawer: true, title: 'Menu',
            body: `<nav class="menu-list" aria-label="All sections">${[...NAV, ...EXTRA].map((n, i) => `<a href="${n.href}" class="menu-item ${n.match(path) ? 'is-active' : ''}" style="--i:${i}" data-close>
                <span class="menu-ico">${icon(n.icon, 20)}</span><span class="grow">${n.label}</span>${n.key === 'matches' && live ? `<span class="badge badge-danger"><span class="live-dot"></span>${live} live</span>` : n.key === 'news' ? '<span class="badge badge-blaze">New</span>' : icon('chevronRight', 18)}</a>`).join('')}</nav>
              <div class="menu-sub">${[['#/wallet', 'wallet', 'Wallet'], ['#/bookings', 'ticket', 'My bookings'], ['#/orders', 'package', 'Orders'], ['#/profile', 'user', 'Profile'], ['#/dash', 'grid', 'Partner dashboard']].map(([h, ic, l]) => `<a href="${h}" data-close>${icon(ic, 18)} ${l}</a>`).join('')}</div>
              <div class="menu-prefs"><button class="btn btn-outline btn-sm" data-action="toggle-theme">${icon(SA.core.theme.current() === 'dark' ? 'sun' : 'moon', 16)} ${SA.core.theme.current() === 'dark' ? 'Light mode' : 'Dark mode'}</button><button class="btn btn-outline btn-sm" data-action="toggle-lang" data-i18n-skip>${icon('globe', 16)} ${SA.i18n.isAr ? 'English' : 'العربية'}</button></div>
              ${SA.ui.social.render({ variant: 'light' })}`,
          });
          break;
        }
        case 'toggle-theme': {
          e.preventDefault();
          const next = SA.core.theme.toggle();
          document.querySelectorAll('[data-action="toggle-theme"]').forEach((b) => { const svg = b.querySelector('svg'); if (svg) svg.outerHTML = icon(next === 'dark' ? 'sun' : 'moon', b.classList.contains('btn') ? 16 : 19); });
          render();
          break;
        }
        case 'cycle-theme': { e.preventDefault(); const p = SA.core.theme.cycle(); SA.ui.overlay.toast(`Theme: ${SA.core.theme.label()}`, { type: 'info' }); render(); void p; break; }
        case 'toggle-lang': e.preventDefault(); SA.i18n.toggle(); break;
        case 'toggle-motion': {
          e.preventDefault();
          const next = SA.core.motion.enabled() ? 'reduced' : 'full';
          SA.core.motion.setPref(next);
          document.querySelectorAll('[data-motion-label]').forEach((b) => (b.textContent = next === 'full' ? 'On' : 'Reduced'));
          SA.ui.overlay.toast(next === 'full' ? 'Animations turned on' : 'Animations reduced', { type: 'info' });
          SA.app.rerender();
          break;
        }
        case 'fav-pitch': {
          e.preventDefault(); e.stopPropagation();
          SA.services.pitches.toggleFavorite(id);
          const on = SA.services.pitches.isFavorite(id);
          t.classList.toggle('is-on', on); t.setAttribute('aria-pressed', on); t.innerHTML = icon(on ? 'heartFill' : 'heart', 18);
          if (on) SA.core.motion.burst(t);
          SA.ui.overlay.toast(on ? 'Saved to your favourite pitches' : 'Removed from favourites', { type: 'info', duration: 2200 });
          break;
        }
        case 'wish': {
          e.preventDefault(); e.stopPropagation();
          const on = SA.services.shop.toggleWish(id);
          document.querySelectorAll(`[data-action="wish"][data-id="${id}"]`).forEach((b) => { b.classList.toggle('is-on', on); b.setAttribute('aria-pressed', on); b.innerHTML = icon(on ? 'heartFill' : 'heart', 18); });
          if (on) SA.core.motion.burst(t);
          SA.ui.overlay.toast(on ? 'Saved to your wishlist' : 'Removed from wishlist', { type: 'info', duration: 2200, action: on ? 'View' : '', onAction: () => SA.core.router.go('/wishlist') });
          break;
        }
        case 'quick-add': e.preventDefault(); e.stopPropagation(); quickAdd(id, t); break;
        case 'join-match': e.preventDefault(); e.stopPropagation(); SA.pages.players.openJoin(id); break;
        case 'reset-demo': {
          e.preventDefault();
          if (await SA.ui.overlay.confirm({ title: 'Reset demo data?', text: 'This clears bookings, orders, your fantasy team and wallet changes on this device.', confirmLabel: 'Reset', danger: true })) {
            store.reset(); SA.core.router.go('/'); SA.ui.overlay.toast('Demo data reset');
          }
          break;
        }
      }
    });
  };

  let lastPath = '/';
  const render = (path = lastPath) => {
    lastPath = path;
    document.getElementById('site-header').innerHTML = header(path);
    document.getElementById('tabbar').innerHTML = tabbar(path);
    document.body.classList.toggle('dash-mode', path.startsWith('/dash') || path.startsWith('/admin'));
    requestAnimationFrame(() => SA.core.motion.syncIndicators());
  };

  const init = () => {
    document.getElementById('site-footer').innerHTML = footer();
    document.querySelectorAll('[data-motion-label]').forEach((b) => (b.textContent = SA.core.motion.enabled() ? 'On' : 'Reduced'));
    SA.core.motion.init();
    window.addEventListener('resize', SA.core.utils.debounce(() => SA.core.motion.syncIndicators(), 120));
    bindGlobal();
    let prevCart = 0;
    store.subscribe((s, meta) => {
      render();
      const c = s.cart.reduce((a, l) => a + l.qty, 0);
      if (c > prevCart) document.querySelectorAll('[data-cart-count]').forEach((el) => el.classList.add('bump'));
      prevCart = c;
    });
    const hdr = document.getElementById('site-header');
    window.addEventListener('scroll', () => hdr.classList.toggle('is-scrolled', scrollY > 8), { passive: true });
  };

  SA.ui.shell = { init, render, openNotifications, quickAdd, NAV, prefButtons };
})(window.SA);
