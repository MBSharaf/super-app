(function (SA) {
  const { esc, money, fmtHour, relDay, fmtRange, fmtDate, fmtKm, timeAgo } = SA.core.utils;
  const { icon } = SA.ui;
  const c = SA.ui.c;
  const media = SA.data.media;
  const S = SA.services;

  /** A player's record, as the host sees it before accepting */
  const recordLine = (rec) => `<span class="pl-rec">${icon('ball', 12)} ${rec.games} games${rec.attendance != null ? ` · <span class="pl-att is-${rec.reliable}">${Math.round(rec.attendance * 100)}% showed up</span>` : ' · <span class="pl-att is-new">New player</span>'}${rec.rating ? ` · ${icon('star', 12)} ${rec.rating.toFixed(1)}` : ''}${rec.noShows ? ` · ${rec.noShows} no-show${rec.noShows > 1 ? 's' : ''}` : ''}</span>`;
  SA.ui.playerRecord = recordLine;
  const RS = { pending: ['badge-gold', 'Waiting'], accepted: ['badge-turf', 'In'], declined: ['', 'Declined'], withdrawn: ['', 'Withdrew'], removed: ['badge-danger', 'Removed'], waitlist: ['badge-info', 'Waitlist'] };

  /** Host panel: settings, requests (accept / decline / remove) and ratings after kick-off */
  const hostPanel = (m) => {
    const list = S.games.forMatch(m.id);
    const hs = S.games.hostSettings(m);
    const pending = list.filter((r) => r.status === 'pending'), inn = list.filter((r) => r.status === 'accepted'), wait = list.filter((r) => r.status === 'waitlist');
    const rateOpen = S.games.canRate(m);
    const row = (r) => `<li class="pl-req" data-req="${r.id}">${c.avatar({ name: r.player.name, avatar: r.player.avatar })}
      <span class="grow"><span class="row wrap" style="--gap:6px"><b class="small">${esc(r.player.name)}</b><span class="badge">${esc(r.position)}</span><span class="badge">${esc(r.player.level || '')}</span>${(() => { const [cl, l] = RS[r.status]; return `<span class="badge ${cl}">${l}</span>`; })()}${r.by === 'auto' ? '<span class="xs muted">auto</span>' : ''}</span>
        ${recordLine(r.record)}${r.message ? `<span class="xs soft" style="display:block">“${esc(r.message)}”</span>` : ''}</span>
      <span class="pl-act">${r.status === 'pending' ? `<button class="btn btn-ghost btn-sm" data-decline="${r.id}">Decline</button><button class="btn btn-primary btn-sm" data-accept="${r.id}" ${m.spotsLeft ? '' : 'disabled title="The game is full"'}>Accept</button>`
        : r.status === 'accepted' && !rateOpen ? `<button class="btn btn-ghost btn-sm" data-remove="${r.id}">Remove</button>` : r.status === 'waitlist' ? `<button class="btn btn-ghost btn-sm" data-decline="${r.id}">Decline</button>` : ''}</span></li>`;
    return `<section class="panel panel-pad host-panel"><div class="row-between wrap" style="--gap:8px"><h2 class="h3">Players asking to join</h2>
        <span class="row wrap" style="--gap:8px"><label class="choice host-opt"><input type="checkbox" data-auto ${hs.autoAccept ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span><span class="xs"><b>Accept automatically</b></span></label>
        <label class="choice host-opt"><input type="checkbox" data-closed ${hs.closed ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span><span class="xs"><b>Close requests</b></span></label></span></div>
      <p class="xs muted" style="margin-top:6px">Players pay their share online when they ask; Super App holds it. Declined or removed players get it back in full.</p>
      ${pending.length ? `<h3 class="small strong sub-mini">Waiting for you (${pending.length})</h3><ul class="pl-list">${pending.map(row).join('')}</ul>` : ''}
      ${inn.length ? `<h3 class="small strong sub-mini">In the game (${inn.length})</h3><ul class="pl-list">${inn.map(row).join('')}</ul>` : ''}
      ${wait.length ? `<h3 class="small strong sub-mini">Waitlist (${wait.length})</h3><ul class="pl-list">${wait.map(row).join('')}</ul>` : ''}
      ${!list.length ? `<p class="small soft" style="margin-top:12px">${icon('clock', 14)} No requests yet. Share the game to fill it faster.</p>` : ''}
      ${rateOpen && inn.length ? `<form class="rate-form" data-rate><h3 class="small strong sub-mini">${icon('star', 14)} Rate the players</h3><p class="xs muted">Your ratings build each player’s record — hosts see it before accepting them.</p>
        ${inn.map((r) => { const old = S.games.ratingFor(m.id, r.player.id); return `<div class="rate-row" data-rr="${r.id}"><span class="grow small"><b>${esc(r.player.name)}</b></span>
          <label class="choice host-opt"><input type="checkbox" name="showed-${r.id}" ${!old || old.showed ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span><span class="xs">Showed up</span></label>
          <span class="stars-in" role="radiogroup" aria-label="Stars for ${esc(r.player.name)}">${[5, 4, 3, 2, 1].map((n) => `<label><input type="radio" name="stars-${r.id}" value="${n}" ${(old?.stars || 4) === n ? 'checked' : ''} /><span>★</span></label>`).join('')}</span></div>`; }).join('')}
        <button class="btn btn-primary btn-sm" type="submit">Save ratings</button></form>` : ''}
    </section>`;
  };

  SA.pages.matchDetail = {
    title: 'Match — Super App',
    async render(ctx) {
      const m = await S.matches.get(ctx.params.id);
      const req = SA.core.store.get().matchRequests[m.id];
      const myReq = !m.isMine ? S.games.myRequest(m.id) : null;
      const justPublished = ctx.query.published;
      const open = Array.from({ length: m.spotsLeft });
      const actionBlock = m.isMine
        ? `<div class="stack" style="--gap:10px"><button class="btn btn-primary btn-lg btn-block" data-share>${icon('share', 16)} Share invite link</button><button class="btn btn-outline btn-block" data-share-community>${icon('users', 16)} Share in the community</button><a class="btn btn-outline btn-block" href="#/booking/${m.bookingId}">View booking</a><button class="btn btn-ghost btn-block" data-unpublish style="color:var(--danger)">Remove listing</button></div>`
        : myReq && myReq.status !== 'accepted'
          ? `<div class="notice ${myReq.status === 'waitlist' ? '' : 'gold'}">${icon('clock', 18)}<span><b>${myReq.status === 'waitlist' ? 'You’re on the waitlist.' : 'Request sent.'}</b> ${money(myReq.amount)} is held by Super App and comes back in full if you don’t get in.</span></div><button class="btn btn-outline btn-block" style="margin-top:12px" data-withdraw="${myReq.id}">Withdraw request</button>`
          : m.joined
          ? `<div class="notice turf">${icon('check', 18)}<span><b>You’re in this match.</b> ${req?.payment ? `Paid with ${esc(req.payment)}.` : ''}</span></div><button class="btn btn-outline btn-block" style="margin-top:12px" data-leave>Leave match</button>`
          : m.spotsLeft ? c.cta(`Join · ${money(m.pricePerPlayer)}`, { variant: 'blaze', size: 'lg', attrs: `data-action="join-match" data-id="${m.id}" style="width:100%;justify-content:space-between"` }) : `<button class="btn btn-outline btn-block" disabled>Match is full</button>`;

      return `<div class="container has-action-bar">
        <div class="page-head" style="padding-bottom:16px"><a class="back-link" href="#/players">${icon('chevronLeft', 16)} All open matches</a></div>
        ${justPublished ? `<div class="notice turf published-note">${icon('check', 18)}<span><b>Your match is live.</b> Players near ${esc(m.pitch.area)} can now find it and join. Share the link to fill it faster.</span></div>` : ''}
        <div class="match-hero media"><img src="${media.photo(m.pitch.image, 1600, 700)}" alt="" />
          <div class="match-hero-copy">
            <div class="row wrap" style="--gap:6px"><span class="badge badge-glass">${m.level}</span><span class="badge badge-glass">${m.type}</span><span class="badge badge-glass">${m.gender}</span>${m.needsGK ? `<span class="badge badge-gold">${icon('glove', 12, 2)} Keeper wanted</span>` : ''}</div>
            <h1 class="h1" style="color:#fff;margin-top:12px">${relDay(m.date)}, ${fmtHour(m.hour)} at ${esc(m.pitch.name)}</h1>
            <p style="color:rgba(255,255,255,.8)">${esc(m.pitch.area)}, ${esc(m.pitch.city)} · ${fmtKm(m.distanceKm)} away</p>
          </div></div>

        <div class="detail-layout">
          <div class="detail-main">
            <div class="spec-grid">
              <div><span class="xs muted">Date</span><b>${fmtDate(m.date, 'long')}</b></div>
              <div><span class="xs muted">Time</span><b class="num">${fmtRange(m.hour, m.duration)}</b></div>
              <div><span class="xs muted">Pitch</span><b>${esc(m.pitch.format)} · ${esc(m.pitch.surface)}</b></div>
              <div><span class="xs muted">Your share</span><b class="num">${money(m.pricePerPlayer)}</b></div>
            </div>
            ${m.notes ? `<h2 class="h3 sub-title">From the host</h2><blockquote class="host-note">${c.avatar({ name: m.hostName, avatar: m.participants[0].avatar, photo: m.participants[0].photo }, 'sm')} <span>“${esc(m.notes)}”</span></blockquote>` : ''}
            <div class="row-between sub-title"><h2 class="h3">Players</h2><span class="small"><b>${m.filled}</b>/${m.total} · <span style="color:var(--blaze-2)">${m.spotsLeft} open</span></span></div>
            <div class="meter ${m.spotsLeft <= 2 ? 'is-hot' : ''}" style="margin-bottom:16px"><span style="width:${(m.filled / m.total) * 100}%"></span></div>
            <ul class="roster">
              ${m.participants.map((p) => `<li class="${p.isMe ? 'is-me' : ''}">${c.avatar(p)}<span class="grow"><b class="small">${esc(p.isMe ? 'You' : p.name)}</b><span class="xs muted" style="display:block">${esc(p.role)}</span></span>${p.role === 'Host' ? '<span class="badge badge-ink">Host</span>' : ''}</li>`).join('')}
              ${open.map(() => `<li class="is-open"><span class="avatar open-avatar">${icon('plus', 16)}</span><span class="grow"><b class="small">Open spot</b><span class="xs muted" style="display:block">Waiting for a player</span></span></li>`).join('')}
            </ul>
            ${m.isMine ? hostPanel(m) : ''}
            <h2 class="h3 sub-title">Where</h2>
            <div class="mini-map" id="match-map"></div>
            <p class="small soft" style="margin-top:10px">${esc(m.pitch.address)}, ${esc(m.pitch.area)} · <a class="link" href="#/pitches/${m.pitch.id}">View pitch</a></p>
          </div>
          <aside class="book-panel panel">
            <div class="row-between"><span class="price" style="font-size:1.4rem">${money(m.pricePerPlayer)}</span><span class="xs muted">per player</span></div>
            <div style="margin:14px 0">${c.spots(m.filled, m.total)}</div>
            <p class="small soft" style="margin-bottom:16px">Hosted by <b>${esc(m.hostName)}</b>${m.postedHoursAgo != null ? ` · posted ${m.postedHoursAgo ? m.postedHoursAgo + ' h ago' : 'just now'}` : ''}</p>
            ${actionBlock}
            <ul class="assurance xs">
              <li>${icon('shield', 14)} Full refund if the host cancels</li>
              <li>${icon('clock', 14)} Leave up to 6 hours before for a refund</li>
            </ul>
          </aside>
        </div>
      </div>
      <div class="action-bar">${m.isMine ? `<button class="btn btn-primary btn-block" data-share>${icon('share', 16)} Share invite link</button>` : m.joined ? `<span class="badge badge-turf">${icon('check', 12)} You’re in</span><span class="grow"></span><button class="btn btn-outline btn-sm" data-leave>Leave</button>` : `<div class="grow"><b class="num">${money(m.pricePerPlayer)}</b><span class="xs muted" style="display:block">${m.spotsLeft} spots left</span></div>${m.spotsLeft ? `<button class="btn btn-blaze" data-action="join-match" data-id="${m.id}">Join match</button>` : ''}`}</div>`;
    },
    mount(el, ctx) {
      const id = ctx.params.id;
      if (ctx.query.published) setTimeout(() => SA.core.motion.confetti(), 300);
      this._map = null;
      Promise.all([S.matches.get(id), SA.ui.map.ready()]).then(([m, ok]) => {
        const node = el.querySelector('#match-map');
        if (!ok || !node?.isConnected) return;
        const map = SA.ui.map.create(node, { center: m.pitch, zoom: 14 });
        map?.setPitches([{ ...m.pitch, price: m.pricePerPlayer, freeSlots: 1 }]);
        this._map = map;
      });
      el.querySelectorAll('[data-share]').forEach((b) => b.addEventListener('click', async () => {
        const url = `${location.origin}${location.pathname}#/players/${id}`;
        if (navigator.share) { try { await navigator.share({ title: 'Join my football match', url }); return; } catch { /* cancelled */ } }
        navigator.clipboard?.writeText(url);
        SA.ui.overlay.toast('Invite link copied — paste it in your WhatsApp group');
      }));
      // Host: requests, settings, ratings · Player: withdraw
      el.addEventListener('click', async (e) => {
        const t = (s) => e.target.closest(s);
        try {
          if (t('[data-accept]')) { S.games.accept(t('[data-accept]').dataset.accept); SA.ui.overlay.toast('Player accepted — they’re in'); SA.app.rerender(); return; }
          if (t('[data-decline]')) { await S.games.decline(t('[data-decline]').dataset.decline); SA.ui.overlay.toast('Request declined — the player gets a full refund', { type: 'info' }); SA.app.rerender(); return; }
          if (t('[data-remove]')) { if (!(await SA.ui.overlay.confirm({ title: 'Remove this player?', text: 'Their spot opens up and they get a full refund. The first player on the waitlist moves up.', confirmLabel: 'Remove', danger: true }))) return; await S.games.remove(t('[data-remove]').dataset.remove); SA.ui.overlay.toast('Player removed', { type: 'info' }); SA.app.rerender(); return; }
          if (t('[data-withdraw]')) { if (!(await SA.ui.overlay.confirm({ title: 'Withdraw your request?', text: 'Your held share comes back to your wallet.', confirmLabel: 'Withdraw', danger: true }))) return; await S.games.withdraw(t('[data-withdraw]').dataset.withdraw); SA.ui.overlay.toast('Request withdrawn — refunded', { type: 'info' }); SA.app.rerender(); }
        } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
      });
      el.querySelector('[data-auto]')?.addEventListener('change', (e) => { S.games.setHost(id, { autoAccept: e.target.checked }); SA.ui.overlay.toast(e.target.checked ? 'New requests are accepted automatically' : 'You decide each request', { type: 'info' }); });
      el.querySelector('[data-closed]')?.addEventListener('change', (e) => { S.games.setHost(id, { closed: e.target.checked }); SA.ui.overlay.toast(e.target.checked ? 'Requests closed' : 'Requests open', { type: 'info' }); });
      el.querySelector('[data-rate]')?.addEventListener('submit', (e) => {
        e.preventDefault();
        const f = e.target;
        const list = [...f.querySelectorAll('[data-rr]')].map((row) => { const rid = row.dataset.rr; return { requestId: rid, showed: f[`showed-${rid}`].checked, stars: +(f.querySelector(`[name="stars-${rid}"]:checked`)?.value || 4) }; });
        S.games.rate(id, list); SA.ui.overlay.toast('Ratings saved — thanks!'); SA.app.rerender();
      });
      el.querySelectorAll('[data-share-community]').forEach((b) => b.addEventListener('click', () => { try { const p = S.community.shareGame(id); SA.ui.overlay.toast('Shared in Pitches & free slots', { action: 'View', onAction: () => SA.core.router.go(`/community/pitches/${p.id}`) }); } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); } }));
      el.querySelectorAll('[data-leave]').forEach((b) => b.addEventListener('click', async () => {
        if (!(await SA.ui.overlay.confirm({ title: 'Leave this match?', text: 'Your spot opens up for other players. If you paid online, your share is refunded to your wallet.', confirmLabel: 'Leave match', danger: true }))) return;
        await S.matches.leave(id); SA.ui.overlay.toast('You left the match'); SA.app.rerender();
      }));
      el.querySelector('[data-unpublish]')?.addEventListener('click', async () => {
        if (!(await SA.ui.overlay.confirm({ title: 'Remove this listing?', text: 'Your booking stays. Players will no longer be able to find or join this match.', confirmLabel: 'Remove listing', danger: true }))) return;
        await S.matches.unpublish(id); SA.ui.overlay.toast('Listing removed'); SA.core.router.go('/players');
      });
      return () => this._map?.destroy();
    },
  };
})(window.SA);
