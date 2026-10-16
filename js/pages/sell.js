/** Site: "Sell on Super App" — switch on seller mode (print on demand, own store, or both) and see your level. */
(function (SA) {
  const { esc, money, fmtNum } = SA.core.utils;
  const { icon } = SA.ui;
  const c = SA.ui.c;
  const S = SA.services;

  const MODES = [
    { id: 'pod', icon: 'shirt', title: 'Print on demand', text: 'Upload designs on our hoodies, tees and caps. Our print partner makes and ships every order — no stock, no upfront cost.' },
    { id: 'store', icon: 'bag', title: 'My own store', text: 'Sell products you already have. You keep the stock and ship the orders; we take a small commission on each sale.' },
    { id: 'both', icon: 'sparkle', title: 'Both', text: 'Run your designs and your own products from one store page and one wallet.' },
  ];
  const PAYOUTS = ['InstaPay', 'Vodafone Cash', 'Bank transfer'];
  const RULES = [
    'Only upload artwork you made or have the rights to use.',
    'No club crests, brand logos, players’ photos or other people’s characters without written permission.',
    'Nothing hateful, sexual or offensive. Every design is reviewed before it goes live.',
    'Copyright reports are taken seriously: a confirmed report freezes your level for 90 days, three strikes suspend the account.',
  ];

  const pct = (v) => `${Math.round(v * 100)}%`;
  const lim = (v) => (v == null ? 'Unlimited' : fmtNum(v));

  /** Level ladder (shared with the seller dashboard) */
  const ladder = (current) => {
    const L = S.sellers.levels();
    const rows = [
      ['Delivered items to reach', (l) => (l.sales ? fmtNum(l.sales) : 'Start')],
      ['Minimum rating', (l) => (l.rating ? `${l.rating.toFixed(1)} ★` : '—')],
      ['Active designs', (l) => lim(l.designs)],
      ['Off the base price', (l) => (l.baseDiscount ? money(l.baseDiscount) : '—')],
      ['Store commission', (l) => pct(l.commission)],
      ['Discount codes', (l) => lim(l.codes)],
      ['Design review', (l) => ({ normal: 'Normal', priority: 'Priority', instant: 'Instant' }[l.review])],
      ['Fantasy subscription', (l) => (l.fantasyDiscount >= 1 ? 'Free' : l.fantasyDiscount ? `${pct(l.fantasyDiscount)} off` : '—')],
      ['Team kits orders', (l) => (l.teamKits ? icon('check', 14, 2.6) : '—')],
    ];
    return `<div class="table-wrap"><table class="table lvl-table">
      <thead><tr><th></th>${L.map((l, i) => `<th class="${i === current ? 'is-current' : ''}"><span class="lvl-chip lvl-${l.id}">${esc(l.name)}</span></th>`).join('')}</tr></thead>
      <tbody>${rows.map(([label, f]) => `<tr><th scope="row">${label}</th>${L.map((l, i) => `<td class="${i === current ? 'is-current' : ''}">${f(l)}</td>`).join('')}</tr>`).join('')}</tbody></table></div>`;
  };

  /** Level card with progress to the next level */
  const levelCard = (s) => {
    const l = S.sellers.levelOf(s);
    return `<div class="lvl-card lvl-${l.id}">
      <div class="lvl-card-top"><span class="lvl-medal">${icon(l.index === 3 ? 'crown' : 'star', 22)}</span>
        <div class="grow"><p class="xs lvl-kicker">Your level</p><h3 class="h3">${esc(l.name)}</h3></div>
        <div class="lvl-stat"><b class="num">${fmtNum(l.stats.sales)}</b><span class="xs">items sold</span></div></div>
      ${l.grace ? `<p class="small lvl-grace">${icon('clock', 14)} <b>Grace month</b> — your rating is ${l.stats.rating.toFixed(1)}; ${esc(l.name)} needs ${l.rating.toFixed(1)}. Bring it back up before the next monthly review to keep your perks.</p>` : ''}
      ${l.next ? `<div class="lvl-progress"><span class="meter"><span style="width:${Math.round(l.progress * 100)}%"></span></span>
        <p class="small">${l.toNext ? `<b class="num">${fmtNum(l.toNext)}</b> more delivered items to reach <b>${esc(l.next.name)}</b>` : `Sales target for <b>${esc(l.next.name)}</b> reached`}${l.ratingOk ? '' : ` · needs a ${l.next.rating.toFixed(1)} ★ rating (now ${l.stats.rating ? l.stats.rating.toFixed(1) : '—'})`}</p></div>`
        : '<p class="small">Top level — every perk is unlocked.</p>'}
      ${l.frozen ? `<p class="small lvl-frozen">${icon('lock', 14)} Level frozen after a copyright report until ${SA.core.utils.fmtDate(new Date(l.frozenUntil))}.</p>` : ''}
    </div>`;
  };
  const DSTATUS = { draft: ['badge', 'Draft'], review: ['badge-gold', 'In review'], live: ['badge-turf', 'Live'], paused: ['badge', 'Paused'], rejected: ['badge-danger', 'Rejected'] };
  const designStatus = (d) => { const [cls, l] = DSTATUS[d.status] || ['badge', d.status]; return `<span class="badge ${cls}">${l}</span>`; };
  /** Seller's own designs with status and quick actions */
  const designsPanel = (s) => {
    const list = S.pod.list({ sellerId: s.id, status: ['draft', 'review', 'live', 'paused', 'rejected'] }).sort((a, b) => b.updatedAt - a.updatedAt);
    return `<section class="panel panel-pad"><div class="row-between"><h2 class="h4">My designs</h2><a class="btn btn-ghost btn-sm" href="#/sell/studio">${icon('plus', 14)} New</a></div>
      ${list.length ? `<div class="my-designs">${list.map((d) => {
        const col = d.product?.colors.find((k) => k.name === d.colors[0]) || d.product?.colors[0];
        const side = d.placement?.front ? 'front' : 'back';
        return `<article class="my-design">
          <a class="md-media" href="#/sell/studio/${d.id}">${SA.ui.garmentView({ kind: d.product?.kind, hex: col?.hex, art: d.src, place: d.placement?.[side], side, artRatio: d.artW ? d.artH / d.artW : 1, label: d.title })}</a>
          <div class="md-body"><div class="row-between" style="--gap:6px"><b class="small truncate">${esc(d.title || 'Untitled')}</b>${designStatus(d)}</div>
            <span class="xs muted">${esc(d.product?.name || '')} · ${money(d.price)} · you earn ${money(d.profit)}</span>
            ${d.status === 'rejected' ? `<span class="xs text-danger">${esc(d.reason)}</span>` : ''}
            ${d.status === 'live' ? `<span class="xs muted">${d.sales || 0} sold · ${d.views || 0} views</span>` : ''}
            <div class="row md-actions" style="--gap:6px"><a class="btn btn-outline btn-sm" href="#/sell/studio/${d.id}">${icon('edit', 13)} Edit</a>
              ${d.status === 'live' || d.status === 'paused' ? `<button class="btn btn-ghost btn-sm" data-pause="${d.id}" data-to="${d.status === 'live' ? 1 : 0}">${d.status === 'live' ? 'Pause' : 'Resume'}</button>` : ''}
              <button class="btn btn-ghost btn-sm" data-del-design="${d.id}" aria-label="Delete ${esc(d.title || 'design')}">${icon('trash', 13)}</button></div></div>
        </article>`;
      }).join('')}</div>` : `<p class="small soft" style="margin-top:8px">No designs yet. Upload your first one in the studio.</p>`}</section>`;
  };
  SA.ui.seller = { ladder, levelCard, MODES, designStatus };

  /** Example earnings: base price vs. the seller's price */
  const calc = () => {
    const items = S.pod.catalog();
    const first = items.find((x) => x.id === 'pod-hoodie') || items[0];
    return `<div class="panel panel-pad sell-calc" data-calc>
      <h2 class="h4">How much do I make?</h2>
      <p class="small soft">You choose the price. Everything above our base price is yours.</p>
      <div class="form-grid" style="margin-top:14px">
        <div class="field"><label for="sc-item">Product</label><select class="select" id="sc-item">${items.map((x) => `<option value="${x.id}" ${x.id === first.id ? 'selected' : ''}>${esc(x.name)}</option>`).join('')}</select></div>
        <div class="field"><label for="sc-price">Your price (EGP)</label><input class="input num" id="sc-price" type="number" inputmode="numeric" value="${first.base + 200}" /></div>
      </div>
      <div class="sell-calc-out" data-calc-out></div>
    </div>`;
  };

  SA.pages.sell = {
    title: 'Sell on Super App',
    render() {
      const mine = S.sellers.mine();
      if (mine) return SA.pages.sell.welcome(mine);
      const user = S.user.me();
      const suggested = S.sellers.slug(`${user.firstName || user.name}-designs`);
      return `<section class="sell-hero">
        <div class="container sell-hero-in">
          <div>
            <p class="eyebrow">${icon('sparkle', 14)} New on Super App</p>
            <h1 class="display">Sell your designs<br />to Egypt’s players and fans</h1>
            <p class="lead">Put your art on hoodies and tees with no stock and no upfront cost, or open a store for products you already have. One account, one wallet.</p>
            <div class="sell-points">
              <span>${icon('shirt', 18)} We print &amp; ship</span><span>${icon('wallet', 18)} Profit in your wallet</span><span>${icon('trend', 18)} Levels with real perks</span>
            </div>
          </div>
          <div class="sell-hero-art" aria-hidden="true">${['nights', 'yalla', 'press'].map((k, i) => `<span class="sell-tile" style="--i:${i}"><img src="${SA.data.pod.art[k]}" alt="" /></span>`).join('')}</div>
        </div>
      </section>
      <div class="container sell-layout">
        <form id="sell-form" class="stack" style="--gap:20px" novalidate>
          <section class="panel panel-pad">
            <h2 class="h4">1. How do you want to sell?</h2>
            <div class="sell-modes" role="radiogroup" aria-label="How you sell">${MODES.map((m, i) => `<label class="sell-mode"><input type="radio" name="mode" value="${m.id}" ${i === 0 ? 'checked' : ''} />
              <span class="sell-mode-ic">${icon(m.icon, 22)}</span><b>${m.title}</b><span class="small soft">${m.text}</span><span class="radio-dot"></span></label>`).join('')}</div>
          </section>
          <section class="panel panel-pad">
            <h2 class="h4">2. Your store</h2>
            <div class="form-grid" style="margin-top:14px">
              <div class="field"><label for="sl-name">Store name</label><input class="input" id="sl-name" name="name" maxlength="40" required placeholder="e.g. Koora Ink" /><span class="field-hint">Your store link is made from the name — you can change it later.</span><p class="field-error" data-err="name"></p></div>
              <div class="field span-2"><label for="sl-bio">About your store <span class="muted">(optional)</span></label><textarea class="textarea" id="sl-bio" name="bio" maxlength="280" placeholder="What do you make? Who is it for?"></textarea><span class="field-hint" data-count>0 / 280</span></div>
            </div>
          </section>
          ${SA.ui.sellerIdFields(user)}
          <section class="panel panel-pad">
            <h2 class="h4">3. How we pay you</h2>
            <div class="form-grid" style="margin-top:14px">
              <div class="field"><label for="sl-pm">Method</label><select class="select" id="sl-pm" name="payoutMethod">${PAYOUTS.map((p) => `<option>${p}</option>`).join('')}</select></div>
              <div class="field"><label for="sl-pd">Account</label><input class="input" id="sl-pd" name="payoutDetail" maxlength="60" placeholder="Phone, email or IBAN" dir="ltr" /><p class="field-error" data-err="payoutDetail"></p></div>
            </div>
            <p class="small soft sell-note">${icon('info', 14)} Profit becomes available ${S.pod.cfg().holdDays} days after delivery. You can withdraw from <b>${money(S.pod.cfg().minWithdraw)}</b>.</p>
          </section>
          <section class="panel panel-pad">
            <h2 class="h4">4. Seller rules</h2>
            <ul class="sell-rules">${RULES.map((r) => `<li>${icon('shield', 16)}<span>${r}</span></li>`).join('')}</ul>
            <label class="choice" style="margin-top:14px"><input type="checkbox" name="agree" /><span class="radio-dot" style="border-radius:6px"></span><span class="grow"><b>I agree to the <a class="link" href="#/legal/seller" target="_blank" rel="noopener">seller agreement</a></b></span></label><p class="field-error" data-err="agree"></p>
          </section>
          <button class="btn btn-blaze btn-lg sell-submit" type="submit">${icon('sparkle', 18)} Send my application</button>
        </form>
        <aside class="stack sell-aside" style="--gap:16px">
          ${calc()}
          <div class="panel panel-pad">
            <h2 class="h4">Seller levels</h2>
            <p class="small soft">Every delivered item counts. Level up for bigger margins, more designs and more visibility.</p>
            <div style="margin-top:12px">${ladder(-1)}</div>
          </div>
        </aside>
      </div>`;
    },

    welcome(s) {
      const l = S.sellers.levelOf(s);
      const used = S.sellers.activeDesigns(s);
      return `${c.pageHead({ crumbs: [['Shop', '#/shop'], ['Sell on Super App']], title: esc(s.name), text: `superapp.eg/s/${esc(s.handle)} · ${MODES.find((m) => m.id === s.mode)?.title || ''}` })}
      <div class="container sell-layout">
        <div class="stack" style="--gap:20px">
          ${s.status === 'pending' ? `<section class="panel panel-pad ap-status">${icon('clock', 28)}<div><h2 class="h3">We’re reviewing your application</h2><p class="soft">The Super App team checks new sellers within one working day. You can prepare designs and products meanwhile — they go live after approval.</p></div></section>` : s.status === 'rejected' ? `<section class="panel panel-pad ap-status is-bad">${icon('alert', 28)}<div><h2 class="h3">Your application wasn’t approved</h2><p class="soft">${esc(s.reviewReason || '')}</p></div></section>` : ''}
          <div class="row wrap" style="--gap:8px"><button class="btn btn-primary btn-sm" data-open-dash>${icon('grid', 14)} Seller dashboard</button><button class="btn btn-outline btn-sm" data-open-dash="store">${icon('settings', 14)} Store settings</button><a class="btn btn-outline btn-sm" href="#/s/${esc(s.handle)}">${icon('eye', 14)} View my store</a></div>
          ${levelCard(s)}
          <div class="panel panel-pad">
            <h2 class="h4">What’s next</h2>
            <div class="sell-next">
              ${s.mode !== 'store' ? `<div class="sell-next-item">${icon('shirt', 20)}<div><b>${used ? 'Add another design' : 'Create your first design'}</b><p class="small soft">${used} of ${lim(l.designs)} active designs used.</p><a class="btn btn-primary btn-sm" href="#/sell/studio" style="margin-top:8px">${icon('plus', 14)} New design</a></div></div>` : ''}
              ${s.mode !== 'pod' ? `<div class="sell-next-item">${icon('bag', 20)}<div><b>Add your products</b><p class="small soft">Your store is ready for products you stock and ship yourself.</p></div></div>` : ''}
              <div class="sell-next-item">${icon('wallet', 20)}<div><b>Verify your ID</b><p class="small soft">Needed once, before your first withdrawal (minimum ${money(S.pod.cfg().minWithdraw)}).</p></div></div>
            </div>
          </div>
          ${s.mode !== 'store' ? designsPanel(s) : ''}
        </div>
        <aside class="panel panel-pad sell-aside"><h2 class="h4">Seller levels</h2><div style="margin-top:12px">${ladder(l.index)}</div></aside>
      </div>`;
    },

    mount(el) {
      el.addEventListener('click', async (e) => {
        const od = e.target.closest('[data-open-dash]');
        if (od) { S.sellers.openDashboard(); SA.core.router.go(od.dataset.openDash === 'store' ? '/seller/store' : '/seller'); return; }
        const p = e.target.closest('[data-pause]');
        if (p) { try { S.pod.setPaused(p.dataset.pause, p.dataset.to === '1'); SA.ui.overlay.toast(p.dataset.to === '1' ? 'Design paused — hidden from the shop' : 'Design is live again'); SA.app.rerender(); } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); } return; }
        const del = e.target.closest('[data-del-design]');
        if (del && await SA.ui.overlay.confirm({ title: 'Delete this design?', text: 'It disappears from the shop. Orders already placed are still printed and paid.', confirmLabel: 'Delete', danger: true })) {
          S.pod.remove(del.dataset.delDesign); SA.ui.overlay.toast('Design deleted'); SA.app.rerender();
        }
      });
      const form = el.querySelector('#sell-form');
      if (!form) return;
      // Earnings calculator
      const out = el.querySelector('[data-calc-out]');
      const item = el.querySelector('#sc-item'), price = el.querySelector('#sc-price');
      const calcUpdate = () => {
        const x = S.pod.catalogItem(item.value);
        const p = Math.round(+price.value || 0);
        const ok = p >= x.minPrice && p <= x.maxPrice;
        out.innerHTML = `<div class="sell-split"><span style="flex:${x.base}"><b class="num">${money(x.base)}</b><small>Base price</small></span><span class="is-you" style="flex:${Math.max(1, p - x.base)}"><b class="num">${money(Math.max(0, p - x.base))}</b><small>You earn</small></span></div>
          <p class="xs ${ok ? 'muted' : 'text-danger'}">${ok ? `Per item, before any level discount. Price range ${money(x.minPrice)} – ${money(x.maxPrice)}.` : `Set a price between ${money(x.minPrice)} and ${money(x.maxPrice)}.`}</p>`;
      };
      item.addEventListener('change', () => { price.value = S.pod.catalogItem(item.value).base + 200; calcUpdate(); });
      price.addEventListener('input', calcUpdate);
      calcUpdate();

      const idData = { idPhoto: '' };
      SA.ui.bindSellerId(el, idData);
      form.bio.addEventListener('input', () => (el.querySelector('[data-count]').textContent = `${form.bio.value.length} / 280`));

      form.addEventListener('submit', (e) => {
        e.preventDefault();
        const data = SA.core.utils.formData(form);
        try {
          const s = S.sellers.start({ ...data, agree: form.agree.checked, idPhoto: idData.idPhoto, requireId: true });
          SA.ui.overlay.toast(s.status === 'pending' ? 'Application sent — we review it within one working day' : `${s.name} is open — welcome aboard!`, { type: 'success' });
          SA.core.motion?.confetti?.({ count: 90 });
          SA.app.rerender();
        } catch (err) {
          SA.ui.overlay.toast(err.message, { type: 'error' });
          SA.ui.showApplyErrors(form, err.details || {});
        }
      });
    },
  };
})(window.SA);
