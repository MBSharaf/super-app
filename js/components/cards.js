/** Domain cards shared across pages. */
(function (SA) {
  const { esc, money, fmtKm, relDay, fmtHour, fmtRange, fmtDate } = SA.core.utils;
  const { icon } = SA.ui;
  const c = SA.ui.c;
  const media = SA.data.media;

  const cards = {
    pitch(p, { compact = false, date } = {}) {
      const fav = SA.services.pitches.isFavorite(p.id);
      const avail = p.freeSlots ?? null;
      const href = `#/pitches/${p.id}${date ? `?date=${date}` : ''}`;
      const nextTxt = p.next ? `${relDay(p.next.date)}, ${fmtHour(p.next.hour)}` : '';
      return `<article class="card card-link pitch-card ${compact ? 'is-compact' : ''}" data-pitch="${p.id}">
        <a href="${href}" class="pitch-card-link" aria-label="${esc(p.name)}, ${fmtKm(p.distanceKm)} away"></a>
        <div class="media pitch-card-media">
          <img src="${media.photo(p.images[0], 720, 480)}" alt="${esc(p.name)} pitch" loading="lazy" />
          <div class="media-top">
            <span class="badge badge-glass">${icon('pin', 13, 2)} ${fmtKm(p.distanceKm)}</span>
            <button class="icon-btn fav-btn ${fav ? 'is-on' : ''}" data-action="fav-pitch" data-id="${p.id}" aria-pressed="${fav}" aria-label="${fav ? 'Remove from' : 'Save to'} favourites">${icon(fav ? 'heartFill' : 'heart', 18)}</button>
          </div>
          ${p.tags?.[0] ? `<span class="badge badge-ink pitch-tag">${esc(p.tags[0])}</span>` : ''}
          ${(() => { const d = SA.services.pitches.activePromos(p).sort((a, b) => b.pct - a.pct)[0]; return d ? `<span class="badge pitch-deal">${icon('tag', 12, 2)} −${Math.round(d.pct * 100)}% ${esc(d.label)}</span>` : ''; })()}
        </div>
        <div class="pitch-card-body">
          <div class="row-between" style="align-items:flex-start">
            <div class="grow">
              <h3 class="h4 truncate">${esc(p.name)}</h3>
              <p class="small muted truncate">${esc(p.area)}, ${esc(p.city)} · ${esc(p.format)}${p.indoor ? ' · Indoor' : ''}</p>
            </div>
            ${c.rating(p.rating, p.reviews, 13)}
          </div>
          <div class="pitch-card-foot">
            <div><span class="price">${money(p.price)}</span><span class="xs muted"> / hour</span></div>
            ${avail === null ? '' : avail > 0
              ? `<span class="avail ${avail <= 3 ? 'is-low' : ''}"><i></i>${avail} slot${avail > 1 ? 's' : ''} today</span>`
              : `<span class="avail is-none"><i></i>${nextTxt ? 'Next: ' + nextTxt : 'Fully booked'}</span>`}
          </div>
        </div>
      </article>`;
    },

    match(m) {
      const urgent = m.spotsLeft > 0 && m.spotsLeft <= 2;
      return `<article class="card card-link match-card" data-match="${m.id}">
        <a href="#/players/${m.id}" class="pitch-card-link" aria-label="${m.spotsLeft} spots at ${esc(m.pitch.name)}"></a>
        <div class="match-when">
          <span class="match-day">${relDay(m.date)}</span>
          <span class="match-time num">${fmtHour(m.hour)}</span>
          <span class="xs muted">${m.duration === 1 ? '60' : m.duration * 60} min</span>
        </div>
        <div class="match-main">
          <div class="row wrap" style="--gap:6px">
            <span class="badge ${m.level === 'Beginner' ? 'badge-turf' : m.level === 'Competitive' ? 'badge-danger' : m.level === 'Advanced' ? 'badge-blaze' : 'badge-info'}">${m.level}</span>
            <span class="badge">${m.type}</span>
            ${m.gender !== 'Men' ? `<span class="badge">${m.gender}</span>` : ''}
            ${m.needsGK ? `<span class="badge badge-gold">${icon('glove', 12, 2)} Keeper wanted</span>` : ''}
          </div>
          <h3 class="h4 truncate" style="margin-top:10px">${esc(m.pitch.name)}</h3>
          <p class="small muted truncate">${icon('pin', 13, 2)} ${esc(m.pitch.area)} · ${fmtKm(m.distanceKm)} · hosted by ${esc(m.hostName.split(' ')[0])}</p>
          <div class="match-spots">
            ${c.spots(m.filled, m.total)}
            <span class="small ${urgent ? 'strong' : ''}" style="${urgent ? 'color:var(--blaze-2)' : ''}">${m.joined ? 'You’re in' : m.isMine ? 'Your match' : m.spotsLeft ? `${m.spotsLeft} spot${m.spotsLeft > 1 ? 's' : ''} left` : 'Full'}</span>
          </div>
        </div>
        <div class="match-side">
          <span class="price">${money(m.pricePerPlayer)}</span><span class="xs muted">per player</span>
          ${m.joined ? `<span class="badge badge-turf">${icon('check', 12, 2.6)} Joined</span>` : m.isMine ? `<span class="badge badge-ink">Host</span>` : `<button class="btn btn-blaze btn-sm" data-action="join-match" data-id="${m.id}">Join</button>`}
        </div>
      </article>`;
    },

    product(p, { size = '' } = {}) {
      return `<article class="product-card ${size} ${p.pod ? 'is-pod' : ''}" data-product="${p.id}">
        <div class="media product-media">
          <a href="#/shop/${p.id}" tabindex="-1" aria-hidden="true"><img src="${media.u(p.images[0], 640, 640)}" alt="" loading="lazy" data-fly /></a>
          ${p.images[1] ? `<img class="alt" src="${media.u(p.images[1], 640, 640)}" alt="" loading="lazy" aria-hidden="true" />` : ''}
          <div class="media-top">
            ${p.soldOut ? '<span class="badge badge-ink">Sold out</span>' : p.discount ? `<span class="badge badge-danger">−${p.discount}%</span>` : p.badges?.[0] ? `<span class="badge badge-glass">${esc(p.badges[0])}</span>` : '<span></span>'}
            <button class="icon-btn fav-btn ${p.wished ? 'is-on' : ''}" data-action="wish" data-id="${p.id}" aria-pressed="${p.wished}" aria-label="${p.wished ? 'Remove from' : 'Add to'} wishlist">${icon(p.wished ? 'heartFill' : 'heart', 18)}</button>
          </div>
          ${p.soldOut ? '' : `<button class="quick-add btn btn-primary btn-sm" data-action="quick-add" data-id="${p.id}">${icon('plus', 14, 2.4)} ${p.sizes.length > 1 ? 'Choose size' : 'Add to cart'}</button>`}
        </div>
        <div class="product-body">
          <p class="xs muted">${esc(p.brand)}</p>
          <h3 class="product-name"><a href="#/shop/${p.id}">${esc(p.name)}</a></h3>
          <div class="row-between" style="margin-top:8px">${c.price(p.price, p.was)}${c.rating(p.rating, null, 12)}</div>
        </div>
      </article>`;
    },

    bookingRow(b, { showActions = true } = {}) {
      const statusBadge = b.status === 'cancelled' ? '<span class="badge badge-danger">Cancelled</span>' : b.isPast ? '<span class="badge">Played</span>' : '<span class="badge badge-turf">Confirmed</span>';
      return `<article class="card booking-row">
        <a href="#/booking/${b.id}" class="booking-thumb media"><img src="${media.photo(b.image, 300, 300)}" alt="" loading="lazy" /></a>
        <div class="grow">
          <div class="row wrap" style="--gap:8px">${statusBadge}${b.seriesId ? `<span class="badge badge-info">${icon('repeat', 11)} Weekly ${b.sessionNo}/${b.seriesSize}</span>` : ''}<span class="xs muted num">#${b.id}</span></div>
          <h3 class="h4" style="margin-top:6px"><a href="#/booking/${b.id}">${esc(b.pitchName)}</a></h3>
          <p class="small soft">${icon('calendar', 14)} ${fmtDate(b.date)} · ${fmtRange(b.hour, b.duration)} · ${esc(b.area)}</p>
          ${b.publishedMatchId && b.status === 'confirmed' ? `<p class="small" style="margin-top:6px;color:var(--turf)">${icon('users', 14)} Listed on Find Players</p>` : ''}
        </div>
        ${showActions && !b.isPast && b.status === 'confirmed' ? `<div class="booking-actions">
          ${b.publishedMatchId ? `<a class="btn btn-outline btn-sm" href="#/players/${b.publishedMatchId}">View listing</a>` : `<a class="btn btn-primary btn-sm" href="#/players/new?booking=${b.id}">${icon('users', 14)} Find players</a>`}
          <a class="btn btn-ghost btn-sm" href="#/booking/${b.id}">Details</a>
        </div>` : `<div class="booking-actions"><span class="price">${money(b.total)}</span></div>`}
      </article>`;
    },
  };

  SA.ui.cards = cards;
})(window.SA);
