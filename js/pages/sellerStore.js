/**
 * Site: a seller's public store page — #/s/:handle — designs and own products, follow, level badge.
 * The owner also gets a way back to the seller dashboard and sees their designs that aren't in the shop yet
 * (in review, drafts, not approved, paused) — only approved designs are public.
 */
(function (SA) {
  const { esc, fmtNum, plural } = SA.core.utils;
  const { icon, cards } = SA.ui;
  const c = SA.ui.c;
  const S = SA.services;
  const media = SA.data.media;

  const STATUS = { review: ['In review', 'gold'], draft: ['Draft', ''], rejected: ['Not approved', 'danger'], paused: ['Paused', ''] };
  /** The owner's designs that customers can't see yet */
  const pending = (s) => {
    const list = S.pod.list({ sellerId: s.id, status: ['review', 'draft', 'rejected', 'paused'] });
    if (!list.length) return '';
    return `<section class="panel panel-pad sf-pending">
      <div class="row-between"><div><h2 class="h4">${icon('eye', 16)} Only you can see this</h2><p class="xs muted">These designs aren’t in your store yet. A design shows here for customers once the Super App team approves it (usually within 24 hours).</p></div>
        <a class="btn btn-ghost btn-sm" href="#/seller/designs">All my designs</a></div>
      <div class="sf-pending-list">${list.map((d) => {
        const side = d.placement?.front ? 'front' : 'back';
        const col = d.product?.colors.find((k) => d.colors.includes(k.name)) || d.product?.colors[0];
        const [label, tone] = STATUS[d.status] || [d.status, ''];
        return `<a class="sf-pending-item" href="#/sell/studio/${d.id}"><span class="sf-pm">${SA.ui.garmentView({ kind: d.product?.kind, hex: col?.hex, art: side === 'back' ? d.srcBack : d.src, place: d.placement?.[side], side, artRatio: side === 'back' ? d.ratioBack : d.ratioFront, label: d.title })}</span>
          <span class="grow"><b class="small">${esc(d.title || 'Untitled design')}</b><span class="xs muted" style="display:block">${esc(d.product?.name || '')}</span>${d.status === 'rejected' && d.reason ? `<span class="xs text-danger" style="display:block">${esc(d.reason)}</span>` : ''}</span>
          <span class="badge ${tone ? `badge-${tone}` : ''}">${label}</span></a>`;
      }).join('')}</div></section>`;
  };

  SA.pages.sellerStore = {
    title: 'Store — Super App',
    async render(ctx) {
      const s = S.sellers.byHandle(ctx.params.handle);
      if (!s || s.status !== 'active') {
        this.title = 'Store not found — Super App';
        return `${c.pageHead({ crumbs: [['Shop', '#/shop'], ['Store']], title: 'Store not found' })}<div class="container"><div class="panel">${c.empty({ icon: 'bag', title: s ? 'This store is closed for now' : 'We couldn’t find that store', text: 'Browse designs from other creators instead.', action: c.cta('Browse designs', { href: '#/shop?cat=designs' }) })}</div></div>`;
      }
      this.title = `${s.name} — Super App`;
      const tab = ctx.query.tab === 'products' ? 'products' : 'designs';
      const all = await S.shop.list({ seller: s.id, sort: ctx.query.sort || 'featured' });
      // The pinned design goes first
      const designs = all.filter((p) => p.pod).sort((a, b) => (b.designId === s.featured) - (a.designId === s.featured)), products = all.filter((p) => !p.pod);
      const shown = s.mode === 'store' || !designs.length || (tab === 'products' && products.length) ? products : designs;   // no designs (print on demand off): the products
      const lvl = S.sellers.levelOf(s);
      const following = S.sellers.isFollowing(s.id);
      const mine = S.sellers.current()?.id === s.id || S.sellers.mine()?.id === s.id;
      const brk = S.sellers.onBreak(s);
      const accent = s.accent || '#17795A';
      const cover = s.cover ? media.u(s.cover, 1600, 560) : '';
      return `<section class="container storefront seller-front" style="--accent:${accent}">
        <div class="sf-cover ${cover ? '' : 'sf-cover-plain'}" ${cover ? `style="background-image:url('${cover}');background-position:center ${s.coverY ?? 50}%"` : ''}>
          ${mine ? `<button class="btn btn-sm ss-glass sf-cover-edit" data-quick="cover">${icon('camera', 14)} ${cover ? 'Change cover' : 'Add a cover'}</button>` : ''}</div>
        <div class="sf-info">
          <span class="sf-logo ${mine ? 'is-mine' : ''}">${s.logo ? `<img src="${esc(s.logo)}" alt="" />` : `<span class="sf-initials">${esc(SA.core.utils.initials(s.name))}</span>`}${mine ? `<button class="sf-logo-edit" data-quick="logo" aria-label="Change logo">${icon('camera', 14)}</button>` : ''}</span>
          <div class="grow">
            <span class="xs muted">${(s.tags || []).length ? '' : 'Creator on Super App · '}<span class="lvl-chip lvl-${lvl.id}">${esc(lvl.name)}</span></span>${(s.tags || []).length ? `<span class="tag-row">${s.tags.map((t) => `<a class="tag-chip" href="#/shop?tag=${encodeURIComponent(t)}" data-tag="${esc(t)}">#${esc(t)}</a>`).join('')}</span>` : ''}
            <h1 class="h1">${esc(s.name)}</h1>
            <p class="soft">${esc(s.bio || '')}</p>
            <div class="sf-stats small"><span><b class="num">${fmtNum(lvl.stats.sales)}</b> sold</span>${lvl.stats.rating ? `<span>${icon('star', 13)} <b class="num">${lvl.stats.rating.toFixed(1)}</b></span>` : ''}<span><b class="num" data-followers>${fmtNum(s.followers || 0)}</b> followers</span></div>
          </div>
          <div class="sf-actions">${mine ? `<button class="btn btn-outline" data-dash>${icon('grid', 15)} Back to dashboard</button><button class="btn btn-primary sf-accent-btn" data-manage>${icon('edit', 15)} Edit store</button>` : `<button class="btn ${following ? 'btn-outline' : 'btn-primary sf-accent-btn'}" data-follow aria-pressed="${following}">${following ? `${icon('check', 15, 2.6)} Following` : `${icon('plus', 15)} Follow`}</button>`}
            <button class="icon-btn" data-share aria-label="Copy store link">${icon('share', 18)}</button></div>
        </div>
        ${brk ? `<div class="sf-break">${icon('clock', 18)}<div><b>${mine ? 'Your store is on a break' : `${esc(s.name)} is taking a break`}</b><span class="small soft" style="display:block">${esc(s.vacationNote || 'Designs are hidden for now.')}${s.vacationUntil ? ` · Back ${SA.core.utils.fmtDate(s.vacationUntil)}` : ''}</span></div>${mine ? '<button class="btn btn-outline btn-sm" data-manage>Open the store</button>' : ''}</div>` : ''}
      </section>
      <div class="container section-tight">
        ${mine ? pending(s) : ''}
        ${designs.length && products.length ? `<div class="chips" role="tablist" aria-label="Store sections" style="margin-bottom:16px">
          <a class="chip" role="tab" href="#/s/${esc(s.handle)}" aria-pressed="${tab === 'designs'}" aria-selected="${tab === 'designs'}">Designs <span class="chip-count">${designs.length}</span></a>
          <a class="chip" role="tab" href="#/s/${esc(s.handle)}?tab=products" aria-pressed="${tab === 'products'}" aria-selected="${tab === 'products'}">Products <span class="chip-count">${products.length}</span></a></div>` : ''}
        <p class="small muted" style="margin-bottom:14px">${plural(shown.length, 'item')}</p>
        ${shown.length ? `<div class="product-grid">${shown.map((p) => cards.product(p).replace('<div class="media-top">', p.designId && p.designId === s.featured ? `<div class="media-top"><span class="badge sf-feat">${icon('star', 11)} Featured</span>` : '<div class="media-top">')).join('')}</div>`
          : brk ? '' : c.empty({ icon: 'shirt', title: 'Nothing here yet', text: mine ? 'Your approved designs and products show up here.' : 'This creator hasn’t published anything yet. Follow to hear when they do.', action: mine ? c.cta('Create a design', { href: '#/sell/studio' }) : '' })}
      </div>`;
    },
    mount(el, ctx) {
      const s = S.sellers.byHandle(ctx.params.handle);
      if (!s) return;
      el.querySelector('[data-follow]')?.addEventListener('click', () => {
        const on = S.sellers.follow(s.id);
        SA.ui.overlay.toast(on ? `You’re following ${s.name}` : `Unfollowed ${s.name}`);
        SA.app.rerender();
      });
      // Owner: full settings in the seller dashboard, or a quick cover/logo change right here
      // Owner: back to the seller dashboard (signed in as this store's seller account)
      el.querySelector('[data-dash]')?.addEventListener('click', () => {
        if (SA.services.dash.auth.sellerAccount()?.id !== s.accountId) SA.services.dash.auth.signIn(s.accountId, { as: 'seller' });
        SA.core.router.go('/seller');
      });
      el.querySelectorAll('[data-manage]').forEach((b) => b.addEventListener('click', () => {
        if (SA.services.dash.me()?.role !== 'seller' || SA.services.dash.me()?.id !== s.accountId) SA.services.dash.auth.signIn(s.accountId, { as: 'seller' });
        SA.core.router.go('/dash/seller/store');
      }));
      el.querySelectorAll('[data-quick]').forEach((b) => b.addEventListener('click', async () => {
        const logo = b.dataset.quick === 'logo';
        try {
          const url = await SA.ui.pickImage(logo ? { aspect: 1, max: 400, alpha: true } : { aspect: 16 / 5.6, max: 1600 });
          if (!url) return;
          S.sellers.update(s.id, { [logo ? 'logo' : 'cover']: url });
          SA.ui.overlay.toast(logo ? 'Logo updated' : 'Cover updated', { type: 'success' });
          SA.app.rerender();
        } catch (err) { SA.ui.overlay.toast(err.code === 'storage_full' || /storage/.test(err.message) ? 'That image is too big for this device — try a smaller one.' : err.message, { type: 'error' }); }
      }));
      el.querySelector('[data-share]')?.addEventListener('click', async () => {
        const url = SA.core.router.url(`/s/${s.handle}`);
        try { await navigator.clipboard.writeText(url); SA.ui.overlay.toast('Store link copied'); } catch { SA.ui.overlay.toast(url); }
      });
    },
  };
})(window.SA);
