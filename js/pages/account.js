(function (SA) {
  const { esc, money, fmtHour, relDay, timeAgo, fmtNum } = SA.core.utils;
  const { icon, cards } = SA.ui;
  const c = SA.ui.c;
  const S = SA.services;
  const store = SA.core.store;

  SA.pages.profile = {
    title: 'Profile — Super App',
    async render() {
      const u = S.user.me(), s = store.get();
      const [{ upcoming }, mine] = await Promise.all([S.bookings.list(), S.matches.mine()]);
      const team = S.fantasy.team();
      const tot = S.fantasy.totals();
      const favs = s.favoritesPitches.map((id) => S.pitches.getSync(id)).filter(Boolean);
      const tiles = [
        ['ticket', 'My bookings', `${upcoming.length} upcoming`, '#/bookings'],
        ['users', 'My matches', `${mine.joined.length} joined · ${mine.hosting.length} hosting`, '#/players'],
        ['trophy', 'Fantasy team', team ? `${esc(team.name)} · ${tot.total} pts` : 'Not created yet', '#/fantasy'],
        ['package', 'Orders', `${s.orders.length} order${s.orders.length === 1 ? '' : 's'}`, '#/orders'],
        ['heart', 'Wishlist', `${s.wishlist.length} saved`, '#/wishlist'],
        ['wallet', 'Wallet', money(s.wallet.balance), '#/wallet'],
        ['sparkle', 'Super Points', `${S.loyalty.summary().balance.toLocaleString()} points · ${S.loyalty.summary().tier.name}`, '#/wallet'],
        ['users', 'Communities', `${(store.get().communities || []).length} joined`, '#/community'],
        ['help', 'Help & support', `${S.support.mine().filter((t) => t.status !== 'resolved').length} open request${S.support.mine().filter((t) => t.status !== 'resolved').length === 1 ? '' : 's'}`, '#/support'],
        ['trophy', 'Tournaments', `${(SA.services.db.all('tournaments') || []).filter((t) => t.status !== 'cancelled' && t.teams.some((x) => x.userId === u.id)).length} team entries`, '#/tournaments'],
      ];
      return `<div class="container">
        <section class="profile-hero">
          ${c.avatar(u, 'xl')}
          <div class="grow"><h1 class="h1">${esc(u.name)}</h1><p class="soft">${esc(u.position)} · ${esc(u.level)} · ${esc(u.foot)} foot · ${esc(u.area)}, ${esc(u.city)}</p>
            <div class="profile-stats"><div><b class="num">${u.matchesPlayed + mine.joined.length}</b><span class="xs muted">matches</span></div><div><b class="num">${u.goals}</b><span class="xs muted">goals</span></div><div><b class="num">${u.rating}</b><span class="xs muted">player rating</span></div><div><b class="num">${new Date(u.memberSince).getFullYear()}</b><span class="xs muted">member since</span></div></div></div>
          <button class="btn btn-outline" data-edit>${icon('edit', 16)} Edit profile</button>
        </section>
        <div class="tile-grid">${tiles.map(([ic, t, sub, h]) => `<a class="card card-link account-tile" href="${h}"><span class="service-ico">${icon(ic, 20)}</span><span class="grow"><b>${t}</b><span class="small muted" style="display:block">${sub}</span></span>${icon('chevronRight', 18)}</a>`).join('')}</div>

        <section class="section-tight">
          <div class="sec-head"><h2 class="h3">Joined matches</h2><a class="btn btn-ghost btn-sm" href="#/players">Find more</a></div>
          ${mine.joined.length ? `<div class="stack" style="--gap:12px">${mine.joined.map((m) => cards.match(m)).join('')}</div>` : `<div class="panel">${c.empty({ icon: 'users', title: 'You haven’t joined a match yet', text: 'Browse games near you that need players.', action: '<a class="btn btn-primary" href="#/players">Find a game</a>' })}</div>`}
        </section>
        ${mine.hosting.length ? `<section class="section-tight"><div class="sec-head"><h2 class="h3">Matches you’re hosting</h2></div><div class="stack" style="--gap:12px">${mine.hosting.map((m) => cards.match(m)).join('')}</div></section>` : ''}
        <section class="section-tight">
          <div class="sec-head"><h2 class="h3">Upcoming bookings</h2><a class="btn btn-ghost btn-sm" href="#/bookings">All bookings</a></div>
          ${upcoming.length ? `<div class="stack" style="--gap:12px">${upcoming.slice(0, 3).map((b) => cards.bookingRow(b)).join('')}</div>` : `<div class="panel">${c.empty({ icon: 'calendar', title: 'No upcoming bookings', action: '<a class="btn btn-primary" href="#/pitches">Book a pitch</a>' })}</div>`}
        </section>
        ${favs.length ? `<section class="section-tight"><div class="sec-head"><h2 class="h3">Favourite pitches</h2></div><div class="scroller" style="--col:300px">${favs.map((p) => cards.pitch({ ...p, distanceKm: SA.core.utils.haversineKm(S.location.current() || SA.config.fallbackLocation, p) })).join('')}</div></section>` : ''}
        <section class="section-tight"><div class="panel panel-pad row-between wrap"><div><h2 class="h4">Demo data</h2><p class="small soft">Everything here is stored on this device. Reset to start the demo from scratch.</p></div><button class="btn btn-outline" data-action="reset-demo">${icon('refresh', 16)} Reset demo</button></div></section>
      </div>`;
    },
    mount(el) {
      el.querySelector('[data-edit]').addEventListener('click', () => {
        const u = S.user.me();
        SA.ui.overlay.open({
          title: 'Edit profile',
          body: `<form id="pf" class="form-grid">
            <div class="field span-2"><label for="pf-n">Full name</label><input class="input" id="pf-n" name="name" value="${esc(u.name)}" required /></div>
            <div class="field"><label for="pf-p">Position</label><select class="select" id="pf-p" name="position">${['Goalkeeper', 'Defender', 'Midfielder', 'Winger', 'Forward'].map((x) => `<option ${x === u.position ? 'selected' : ''}>${x}</option>`).join('')}</select></div>
            <div class="field"><label for="pf-l">Level</label><select class="select" id="pf-l" name="level">${SA.data.matches.levels.map((x) => `<option ${x === u.level ? 'selected' : ''}>${x}</option>`).join('')}</select></div>
            <div class="field"><label for="pf-f">Stronger foot</label><select class="select" id="pf-f" name="foot">${['Right', 'Left', 'Both'].map((x) => `<option ${x === u.foot ? 'selected' : ''}>${x}</option>`).join('')}</select></div>
            <div class="field"><label for="pf-a">Area</label><input class="input" id="pf-a" name="area" value="${esc(u.area)}" /></div></form>`,
          footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-primary" form="pf" type="submit">Save changes</button>`,
          onMount(m, close) {
            m.querySelector('#pf').addEventListener('submit', async (e) => {
              e.preventDefault();
              const d = SA.core.utils.formData(e.target);
              if (d.name.trim().length < 3) return;
              await S.user.update({ ...d, name: d.name.trim(), firstName: d.name.trim().split(' ')[0] });
              close(); SA.app.rerender(); SA.ui.overlay.toast('Profile saved');
            });
          },
        });
      });
    },
  };


  /** Super Points card + history (wallet page) */
  const pointsSection = () => {
    const L = S.loyalty.summary();
    return `<section class="points-wrap">
      <div class="points-card tier-${L.tier.key}" data-tilt>
        <div class="row-between"><span class="pc-label">${icon('sparkle', 16)} Super Points</span><span class="pc-tier">${L.tier.name}</span></div>
        <b class="pc-bal num" data-countup="${L.balance}">${L.balance.toLocaleString()}</b>
        <span class="small pc-sub">Worth <b>${money(Math.floor(L.value))}</b> in vouchers</span>
        <div class="pc-prog"><span style="--w:${Math.round(L.progress * 100)}%"></span></div>
        <span class="xs pc-sub">${L.next ? `${(L.next.min - L.lifetime).toLocaleString()} points to ${L.next.name} — earn ×${L.next.mult} on everything` : 'Top tier — you earn ×1.5 on everything'}</span>
        <button class="btn btn-sm pc-btn" data-redeem ${L.balance >= L.cfg.minRedeem ? '' : 'disabled'}>${icon('gift', 14)} Get a voucher</button>
        <span class="pc-coin" aria-hidden="true">S</span>
      </div>
      <div class="panel panel-pad points-how">
        <h2 class="h4">How you earn</h2>
        <ul class="how-list">
          <li>${icon('pitch', 18)}<span><b>1 point for every ${L.cfg.earnPer} EGP</b> on games you play, orders delivered and tournament entries</span></li>
          <li>${icon('star', 18)}<span><b>+20 points</b> for each review after a game</span></li>
          <li>${icon('trophy', 18)}<span><b>Silver ×1.25 · Gold ×1.5</b> — your tier rises with lifetime points (${L.lifetime.toLocaleString()} so far)</span></li>
          <li>${icon('gift', 18)}<span><b>${Math.round(1 / L.cfg.pointValue)} points = 1 EGP</b> as a voucher, from ${L.cfg.minRedeem.toLocaleString()} points (${money(Math.round(L.cfg.minRedeem * L.cfg.pointValue))}). Use it on bookings, games, the shop, printed designs, tournaments and the Fantasy Pass — points can’t be turned into cash.</span></li>
        </ul>
        ${S.loyalty.vouchers().length ? `<h3 class="small strong" style="margin-top:16px">Your vouchers</h3><ul class="tx-list">${S.loyalty.vouchers().map((v) => `<li><span class="tx-ico credit">${icon('gift', 14, 2.2)}</span><span class="grow"><b class="small num">${esc(v.code)}</b><span class="xs muted" style="display:block">${v.balance ? `${money(v.balance)} left of ${money(v.amount)}` : `Used — ${money(v.amount)}`}</span></span>${v.balance ? '<span class="badge badge-turf">Ready</span>' : '<span class="badge">Used</span>'}</li>`).join('')}</ul>` : ''}
        <h3 class="small strong" style="margin-top:16px">Points history</h3>
        <ul class="tx-list">${L.history.slice(0, 8).map((h) => `<li><span class="tx-ico ${h.points >= 0 ? 'credit' : 'debit'}">${icon(h.kind === 'redeem' ? 'wallet' : h.kind === 'bonus' ? 'gift' : 'plus', 14, 2.2)}</span><span class="grow"><b class="small">${esc(h.label)}</b><span class="xs muted" style="display:block">${timeAgo(h.at)}</span></span><b class="num small" style="color:${h.points >= 0 ? 'var(--turf)' : 'var(--ink)'}">${h.points >= 0 ? '+' : ''}${h.points.toLocaleString()}</b></li>`).join('')}</ul>
      </div>
    </section>`;
  };

  SA.pages.wallet = {
    title: 'Wallet — Super App',
    render() {
      const w = S.user.wallet();
      return `${c.pageHead({ crumbs: [['Profile', '#/profile'], ['Wallet']], title: 'Wallet', text: 'Pay for pitches, matches, fantasy leagues and orders in one tap. Refunds and winnings land here.' })}
      <div class="container">
        <div class="wallet-layout">
          <div class="wallet-card">
            <span class="small" style="opacity:.75">Available balance</span>
            <b class="wallet-bal num">${money(w.balance)}</b>
            <div class="row wrap" style="margin-top:22px">${[200, 500, 1000].map((a) => `<button class="btn btn-sm wallet-quick" data-topup="${a}">+ ${a}</button>`).join('')}<button class="btn btn-blaze btn-sm" data-topup="">${icon('plus', 14)} Top up</button></div>
            <span class="wallet-mark" aria-hidden="true">SUPER APP</span>
          </div>
          <div class="panel panel-pad">
            <h2 class="h4">Activity</h2>
            <ul class="tx-list">${w.transactions.map((t) => `<li><span class="tx-ico ${t.type}">${icon(t.type === 'credit' ? 'plus' : 'minus', 14, 2.4)}</span><span class="grow"><b class="small">${esc(t.label)}</b><span class="xs muted" style="display:block">${timeAgo(t.at)}</span></span><b class="num small" style="color:${t.type === 'credit' ? 'var(--turf)' : 'var(--ink)'}">${t.type === 'credit' ? '+' : '−'}${money(t.amount)}</b></li>`).join('')}</ul>
          </div>
        </div>
        ${pointsSection()}
        <section class="panel panel-pad" id="receipts" style="margin-top:18px">
          <div class="row-between wrap" style="--gap:8px"><h2 class="h4">${icon('file', 18)} Payments & receipts</h2><span class="xs muted">Every payment, refund and top-up — download a PDF for any of them</span></div>
          ${S.payment.receipts().length ? `<ul class="tx-list rc-list">${S.payment.receipts().slice(0, 60).map((r) => `<li><span class="tx-ico ${r.kind === 'payment' ? 'debit' : 'credit'}">${icon(r.kind === 'payment' ? 'minus' : r.kind === 'voucher' ? 'gift' : 'plus', 14, 2.4)}</span>
            <span class="grow"><b class="small">${esc(r.label)}</b><span class="xs muted" style="display:block">${new Date(r.at).toLocaleString()} · ${esc(r.method || '')}</span></span>
            <b class="small num">${r.kind === 'payment' ? '−' : '+'}${money(r.amount)}</b><button class="btn btn-ghost btn-sm" data-receipt="${esc(r.id)}" aria-label="Download the receipt as PDF">${icon('file', 14)} PDF</button></li>`).join('')}</ul>`
            : c.empty({ icon: 'file', title: 'No payments yet', text: 'Your receipts appear here after your first payment.' })}
        </section>
      </div>`;
    },
    mount(el) {
      el.addEventListener('click', (e) => { const b = e.target.closest('[data-receipt]'); if (b) SA.ui.docs.receipt(S.payment.receipts().find((r) => r.id === b.dataset.receipt)); });
      const off = SA.core.motion.tilt(el.querySelector('.points-card'), { max: 10 });
      el.querySelector('[data-redeem]')?.addEventListener('click', () => {
        const L = S.loyalty.summary();
        const max = Math.floor(L.balance / 100) * 100;
        SA.ui.overlay.open({
          title: 'Turn points into a voucher', subtitle: `${L.balance.toLocaleString()} points available · ${Math.round(1 / L.cfg.pointValue)} points = 1 EGP · vouchers only, no cash`,
          body: `<form id="rd" class="stack" style="--gap:12px" novalidate><div class="field"><label for="rd-p">Points to use</label><input class="input num" id="rd-p" name="points" type="number" min="${L.cfg.minRedeem}" max="${max}" step="100" value="${max}" />
            <div class="chips" style="margin-top:8px">${[L.cfg.minRedeem, Math.floor(max / 200) * 100, max].filter((v, i, a) => v >= L.cfg.minRedeem && a.indexOf(v) === i).map((v) => `<button type="button" class="chip" data-pts="${v}">${v.toLocaleString()}</button>`).join('')}</div>
            <span class="field-error" data-err="points"></span></div><p class="h3 num" data-get></p></form>`,
          footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-blaze" form="rd" type="submit">${icon('gift', 15)} Create voucher</button>`,
          onMount(m, close) {
            const f = m.querySelector('#rd');
            const upd = () => { m.querySelector('[data-get]').textContent = `= ${money(Math.floor(Math.floor(+f.points.value / 100) * 100 * L.cfg.pointValue))} voucher`; };
            m.querySelectorAll('[data-pts]').forEach((b) => b.addEventListener('click', () => { f.points.value = b.dataset.pts; upd(); }));
            f.addEventListener('input', upd); upd();
            f.addEventListener('submit', async (e) => {
              e.preventDefault();
              try { const r = await S.loyalty.redeem(f.points.value); close(); SA.core.motion.confetti({ count: 70 }); SA.ui.overlay.toast(`Voucher ${r.code} — ${money(r.amount)} ready to use at checkout`, { type: 'success' }); SA.app.rerender(); }
              catch (err) { m.querySelector('[data-err="points"]').textContent = err.message; }
            });
          },
        });
      });
      el.addEventListener('click', (e) => {
        const b = e.target.closest('[data-topup]');
        if (!b) return;
        const preset = +b.dataset.topup || 500;
        SA.ui.overlay.open({
          title: 'Top up wallet',
          body: `<form id="tu" class="stack" style="--gap:16px">
            <div class="field"><label for="tu-a">Amount (EGP)</label><input class="input num" id="tu-a" name="amount" type="number" min="50" step="50" value="${preset}" /><div class="chips" style="margin-top:8px">${[200, 500, 1000, 2000].map((a) => `<button type="button" class="chip" data-amt="${a}">${a}</button>`).join('')}</div></div>
            <div data-pay>${SA.ui.payment.render({ amount: 0, name: 'tupay' }).replace(/<label class="choice pay-choice"[^]*?value="wallet"[^]*?<\/label>/, '')}</div></form>`,
          footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-primary" form="tu" type="submit">Top up</button>`,
          onMount(m, close) {
            const form = m.querySelector('#tu');
            const pay = m.querySelector('[data-pay]');
            const first = pay.querySelector('input[name="tupay"]'); if (first) { first.checked = true; pay.querySelector('.card-form').hidden = first.value !== 'new'; }
            SA.ui.payment.bind(pay, { name: 'tupay' });
            m.querySelectorAll('[data-amt]').forEach((x) => x.addEventListener('click', () => (form.amount.value = x.dataset.amt)));
            form.addEventListener('submit', async (ev) => {
              ev.preventDefault();
              const p = SA.ui.payment.read(pay, { name: 'tupay' });
              try {
                await SA.ui.payment.process(() => S.payment.topUp(+form.amount.value, p.savedCardId ? { savedCardId: p.savedCardId } : { card: p.card }), { root: pay, label: 'Topping up' });
                close(); SA.ui.overlay.toast(`${money(+form.amount.value)} added to your wallet`); SA.app.rerender();
              } catch { /* shown */ }
            });
          },
        });
      });
    },
  };

  SA.pages.notFound = {
    title: 'Page not found — Super App',
    render: () => `<div class="container section">${c.empty({ icon: 'alert', title: 'This page is off the pitch', text: 'The link may be old or mistyped.', action: '<a class="btn btn-primary" href="#/">Back to home</a>' })}</div>`,
  };
})(window.SA);
