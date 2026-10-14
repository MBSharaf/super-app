(function (SA) {
  const { esc, money, fmtHour, relDay, fmtRange, fmtDate, fmtKm, timeAgo } = SA.core.utils;
  const { icon } = SA.ui;
  const c = SA.ui.c;
  const media = SA.data.media;
  const S = SA.services;

  SA.pages.matchDetail = {
    title: 'Match — Super App',
    async render(ctx) {
      const m = await S.matches.get(ctx.params.id);
      const req = SA.core.store.get().matchRequests[m.id];
      const justPublished = ctx.query.published;
      const open = Array.from({ length: m.spotsLeft });
      const actionBlock = m.isMine
        ? `<div class="stack" style="--gap:10px"><button class="btn btn-primary btn-lg btn-block" data-share>${icon('share', 16)} Share invite link</button><a class="btn btn-outline btn-block" href="#/booking/${m.bookingId}">View booking</a><button class="btn btn-ghost btn-block" data-unpublish style="color:var(--danger)">Remove listing</button></div>`
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
