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
          <img src="${media.photo(p.images[0], 720, 480)}" alt="${esc(p.name)} ${p.sport === 'padel' ? 'padel court' : 'pitch'}" loading="lazy" />
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
              <p class="small muted truncate">${esc(p.area)}, ${esc(p.city)} · ${p.sport === 'padel' ? `Padel · ${p.panoramic ? 'Panoramic' : 'Classic'}${p.indoor ? ' · Indoor' : ' · Outdoor'}` : `${esc(p.format)}${p.indoor ? ' · Indoor' : ''}`}</p>
            </div>
            ${c.rating(p.rating, p.reviews, 13)}
          </div>
          <div class="pitch-card-foot">
            <div><span class="price">${money(p.price)}</span><span class="xs muted"> / hour</span></div>
            ${avail === null ? '' : avail > 0
              ? `<span class="avail ${avail <= 3 ? 'is-low' : ''}"><i></i>${p.sport === 'padel' ? `${avail} start time${avail > 1 ? 's' : ''} today` : `${avail} slot${avail > 1 ? 's' : ''} today`}</span>`
              : `<span class="avail is-none"><i></i>${nextTxt ? 'Next: ' + nextTxt : 'Fully booked'}</span>`}
          </div>
        </div>
      </article>`;
    },

    match(m) {
      // Football: Beginner…Competitive. Padel: Level 1–7 (1–2 new, 3–4 club, 5–6 strong, 7 pro)
      const levelBadge = (l) => {
        const n = +String(l).replace('Level ', '');
        if (n) return n <= 2 ? 'badge-turf' : n <= 4 ? 'badge-info' : n <= 6 ? 'badge-blaze' : 'badge-danger';
        return l === 'Beginner' ? 'badge-turf' : l === 'Competitive' ? 'badge-danger' : l === 'Advanced' ? 'badge-blaze' : 'badge-info';
      };
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
            ${m.sport === 'padel' ? `<span class="badge badge-ink">${icon('padel', 12, 2)} Padel</span>` : ''}
            <span class="badge ${levelBadge(m.level)}">${m.level}</span>
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

    /** Shop card (storefront style): 4:5 photo on a soft background, small sale tag, round quick-add, brand · name · price */
    product(p, { size = '' } = {}) {
      const off = p.was && p.was > p.price ? Math.round((1 - p.price / p.was) * 100) : 0;
      return `<article class="product-card ${size} ${p.pod ? 'is-pod' : ''}" data-product="${p.id}">
        <div class="media product-media">
          <a href="#/shop/${p.id}" tabindex="-1" aria-hidden="true"><img src="${media.u(p.images[0], 640, 800)}" alt="" loading="lazy" data-fly /></a>
          ${p.images[1] ? `<img class="alt" src="${media.u(p.images[1], 640, 800)}" alt="" loading="lazy" aria-hidden="true" />` : ''}
          <div class="media-top">
            ${p.soldOut ? '<span class="pc-tag is-ink">Sold out</span>' : off ? `<span class="pc-tag is-sale">−${off}%</span>` : p.badges?.[0] ? `<span class="pc-tag">${esc(p.badges[0])}</span>` : '<span></span>'}
            <button class="icon-btn fav-btn ${p.wished ? 'is-on' : ''}" data-action="wish" data-id="${p.id}" aria-pressed="${p.wished}" aria-label="${p.wished ? 'Remove from' : 'Add to'} wishlist">${icon(p.wished ? 'heartFill' : 'heart', 18)}</button>
          </div>
          ${p.soldOut ? '' : `<button class="quick-add" data-action="quick-add" data-id="${p.id}" aria-label="${p.sizes.length > 1 ? 'Choose size' : 'Add to cart'}">${icon('plus', 18, 2.2)}<span>${p.sizes.length > 1 ? 'Choose size' : 'Add to cart'}</span></button>`}
        </div>
        <div class="product-body">
          <p class="pc-brand">${esc(p.brand)}</p>
          <h3 class="product-name"><a href="#/shop/${p.id}">${esc(p.name)}</a></h3>
          <div class="pc-price"><b class="${off ? 'is-sale' : ''}">${money(p.price)}</b>${off ? `<s>${money(p.was)}</s>` : ''}</div>
          ${p.rating ? `<div class="pc-rating">${icon('star', 12)}<span>${Number(p.rating).toFixed(1)}</span>${p.reviews ? `<span class="muted">(${p.reviews})</span>` : ''}</div>` : ''}
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
