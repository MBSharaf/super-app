(function (SA) {
  const { esc, money, fmtKm, fmtHour, fmtRange, isoDate, addDays, today, fmtDate, DAY, timeAgo } = SA.core.utils;
  const DN = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
  const daysLabel = (days) => { const k = [...days].sort().join(','); return k === '0,1,2,3,4' ? 'Sun–Thu' : k === '5,6' ? 'Fri–Sat' : k === '0,1,2,3,4,5,6' ? 'every day' : days.map((d) => DN[d]).join(', '); };
  /** Policy as a 3-step timeline: full refund → 50% → none */
  const policyBlock = (pol) => `<div class="pol-line" role="img" aria-label="${esc(pol.summary)}">
      <span class="pl-seg pl-full" style="--w:${pol.half ? 50 : 70}%"><b>Full refund</b><i class="xs">until ${pol.full} h before</i></span>
      <span class="pl-seg pl-half" style="--w:${pol.half ? 30 : 30}%"><b>50%</b><i class="xs">${pol.half ? `until ${pol.half} h before` : 'until kick-off'}</i></span>
      ${pol.half ? '<span class="pl-seg pl-none" style="--w:20%"><b>No refund</b><i class="xs">last hours</i></span>' : ''}
      <span class="pl-kick xs">${SA.ui.icon('ball', 14)} Kick-off</span></div>
    <p class="small soft" style="margin-top:10px"><b>${esc(pol.name)} policy.</b> ${esc(pol.summary)} If the venue cancels, you always get everything back.</p>`;
  const { icon } = SA.ui;
  const c = SA.ui.c;
  const media = SA.data.media;
  const S = SA.services;

  const DURATIONS = [[1, '60 min'], [1.5, '90 min'], [2, '2 hours']];

  SA.pages.pitchDetail = {
    title: 'Pitch — Super App',
    async render(ctx) {
      const p = await S.pitches.get(ctx.params.id);
      const reviews = await S.pitches.reviews(p.id);
      this.title = `${p.name} — Book on Super App`;
      const F = SA.data.pitches.facilities;
      return `
      <div class="container has-action-bar">
        <div class="page-head" style="padding-bottom:16px">
          <nav class="crumbs" aria-label="Breadcrumb"><a href="#/">Home</a>${icon('chevronRight', 14)}<a href="#/pitches">Book a pitch</a>${icon('chevronRight', 14)}<span aria-current="page">${esc(p.name)}</span></nav>
        </div>
        <div class="gallery">
          <button class="gallery-main media" data-gallery-open aria-label="View photos"><img src="${media.photo(p.images[0], 1400, 900)}" alt="${esc(p.name)}" data-main /></button>
          <div class="gallery-thumbs">${p.images.slice(0, 4).map((im, i) => `<button class="gallery-thumb media ${i === 0 ? 'is-active' : ''}" data-img="${im}" aria-label="Photo ${i + 1}"><img src="${media.photo(im, 400, 300)}" alt="" loading="lazy" /></button>`).join('')}</div>
        </div>

        <div class="detail-layout">
          <div class="detail-main">
            <div class="row wrap" style="--gap:8px">${p.tags.map((t) => `<span class="badge badge-ink">${esc(t)}</span>`).join('')}${p.tags.includes(p.format) ? '' : `<span class="badge">${esc(p.format)}</span>`}<span class="badge">${p.indoor ? 'Indoor' : 'Outdoor'}</span></div>
            <h1 class="h1" style="margin-top:14px">${esc(p.name)}</h1>
            <div class="row wrap detail-meta">
              ${c.rating(p.rating, p.reviews)}
              <span class="small soft">${icon('pin', 15)} ${esc(p.address)}, ${esc(p.area)}</span>
              <span class="small soft">${icon('locate', 15)} ${fmtKm(p.distanceKm)} away · about ${p.etaMin} min by car</span>
            </div>
            <div class="spec-grid">
              <div><span class="xs muted">Format</span><b>${esc(p.format)} · ${p.players} players</b></div>
              <div><span class="xs muted">Surface</span><b>${esc(p.surface)}</b></div>
              <div><span class="xs muted">Opening hours</span><b>${fmtHour(p.open[0])} – ${fmtHour(p.open[1])}</b></div>
              <div><span class="xs muted">Price per hour</span><b>${money(p.price)} <span class="xs muted">· ${money(p.peakPrice)} after 6 PM & weekends</span></b></div>
            </div>
            <p class="lead" style="margin-top:24px">${esc(p.about)}</p>

            <h2 class="h3 sub-title">Facilities</h2>
            <ul class="facility-grid">${p.facilities.map((f) => `<li>${icon(c.facilityIcon(f), 20)}<span>${F[f]}</span></li>`).join('')}</ul>

            ${S.tournaments.forPitch(p.id).map((t) => `<a class="tour-strip" href="#/tournaments/${t.id}">${icon('trophy', 20)}<span class="grow"><b>${esc(t.name)}</b><span class="small soft" style="display:block">${t.regOpen ? `Registration open · ${t.spotsLeft} spot${t.spotsLeft === 1 ? '' : 's'} left · ${money(t.fee)} per team` : t.status === 'running' ? `In progress · next match ${t.next ? fmtDate(t.next.date) : 'soon'}` : 'Full'}</span></span>${icon('chevronRight', 18)}</a>`).join('')}
            <h2 class="h3 sub-title">Cancellation policy</h2>
            ${policyBlock(S.pitches.policyOf(p.cancelPolicy))}

            <h2 class="h3 sub-title">Location</h2>
            <div class="mini-map" id="detail-map" role="region" aria-label="Pitch location"></div>
            <p class="small soft" style="margin-top:10px">${esc(p.address)}, ${esc(p.area)}, ${esc(p.city)}</p>

            <div class="row-between sub-title"><h2 class="h3">Reviews</h2>${c.rating(p.rating, p.reviews)}</div>
            <div class="review-list">${reviews.map((r) => `<article class="review">
              <div class="row">${c.avatar({ name: r.customer.name, avatar: r.customer.avatar })}<div class="grow"><b class="small">${esc(r.customer.name)}</b><span class="xs muted" style="display:block">${timeAgo(r.createdAt)} · verified booking</span></div>${c.stars(r.rating, 13)}</div>
              <p class="small soft" style="margin-top:10px">${esc(r.text)}</p>
              ${r.reply ? `<div class="owner-reply"><b class="xs">${SA.ui.icon('message', 12)} Response from ${esc(SA.services.db.find('accounts', p.ownerId)?.company || 'the venue')}</b><p class="small">${esc(r.reply.text)}</p></div>` : ''}</article>`).join('') || '<p class="small muted">No reviews yet — players can review after their game.</p>'}</div>
          </div>

          <aside class="book-panel panel" id="book-panel" aria-label="Book this pitch">
            <div class="row-between"><div><span class="price" style="font-size:1.4rem">${money(p.price)}</span><span class="small muted"> / hour</span></div>
              <button class="icon-btn fav-btn ${S.pitches.isFavorite(p.id) ? 'is-on' : ''}" data-action="fav-pitch" data-id="${p.id}" aria-label="Save pitch">${icon(S.pitches.isFavorite(p.id) ? 'heartFill' : 'heart', 18)}</button></div>
            ${p.status === 'closed' ? `<div class="notice blaze" style="margin-top:14px">${icon('alert', 18)}<span><b>Temporarily closed for booking.</b> Check back soon or pick another pitch nearby.</span></div>` : ''}
            ${S.pitches.activePromos(p).map((x) => `<div class="deal-note">${icon('tag', 16)}<span><b>${Math.round(x.pct * 100)}% off — ${esc(x.label)}</b><span class="xs" style="display:block">${daysLabel(x.days)}, ${fmtHour(x.hourFrom)}–${fmtHour(x.hourTo)} · until ${fmtDate(x.to)}</span></span></div>`).join('')}
            <h2 class="field-label" style="margin-top:18px">1. Pick a date</h2>
            <div class="date-strip compact" data-dates>${Array.from({ length: 14 }, (_, i) => { const d = addDays(today(), i), iso = isoDate(d); return `<button class="date-chip" data-date="${iso}" aria-pressed="false"><span class="xs">${i === 0 ? 'Today' : DAY[d.getDay()]}</span><b class="num">${d.getDate()}</b></button>`; }).join('')}</div>
            <h2 class="field-label" style="margin-top:18px">2. How long?</h2>
            <div class="tabs dur-tabs" role="radiogroup" aria-label="Duration">${DURATIONS.map(([v, l]) => `<button class="tab" role="radio" data-dur="${v}" aria-checked="false" aria-selected="false">${l}</button>`).join('')}</div>
            <div class="row-between" style="margin-top:18px"><h2 class="field-label">3. Choose a start time</h2><span class="xs muted" data-free></span></div>
            <div class="slot-legend xs"><span><i class="lg-av"></i>Available</span><span><i class="lg-sel"></i>Selected</span><span><i class="lg-bk"></i>Booked — tap for waitlist</span>${S.pitches.activePromos(p).length ? '<span><i class="lg-deal"></i>Offer price</span>' : ''}</div>
            <div class="slot-grid" data-slots role="listbox" aria-label="Start times"></div>
            <p class="field-error" data-slot-err style="margin-top:8px"></p>
            ${p.weekly?.enabled ? `<div class="repeat-box" data-repeat>
              <div class="row-between"><h2 class="field-label">4. Play every week?</h2>${p.weekly.discount ? `<span class="badge badge-turf">Save ${Math.round(p.weekly.discount * 100)}%</span>` : ''}</div>
              <div class="tabs" role="radiogroup" aria-label="Repeat">${[[0, 'Just once'], ...S.bookings.WEEK_OPTIONS.map((w) => [w, `${w} weeks`])].map(([v, l]) => `<button class="tab" role="radio" data-weeks="${v}" aria-checked="false" aria-selected="false">${l}</button>`).join('')}</div>
            </div>` : ''}
            <div class="book-summary" data-summary></div>
          </aside>
        </div>
      </div>
      <div class="action-bar" data-mobile-bar></div>`;
    },
    mount(el, ctx) {
      const p = S.pitches.getSync(ctx.params.id);
      const sel = { date: ctx.query.date || isoDate(today()), duration: +ctx.query.dur || 1, hour: ctx.query.hour ? +ctx.query.hour : null, weeks: +ctx.query.weeks || 0 };
      const pol = S.pitches.policyOf(p.cancelPolicy);
      const slotsEl = el.querySelector('[data-slots]');
      const err = el.querySelector('[data-slot-err]');

      // Gallery
      const main = el.querySelector('[data-main]');
      el.querySelectorAll('[data-img]').forEach((b) => b.addEventListener('click', () => {
        el.querySelectorAll('[data-img]').forEach((x) => x.classList.toggle('is-active', x === b));
        main.style.opacity = 0;
        setTimeout(() => { main.src = media.photo(b.dataset.img, 1400, 900); main.onload = () => (main.style.opacity = 1); }, 160);
      }));
      el.querySelector('[data-gallery-open]').addEventListener('click', () => {
        SA.ui.overlay.open({ wide: true, title: esc(p.name), body: `<div class="lightbox">${p.images.map((im) => `<img src="${media.photo(im, 1400, 900)}" alt="" loading="lazy" />`).join('')}</div>` });
      });

      // Map
      this._map = null;
      SA.ui.map.ready().then((ok) => {
        const node = el.querySelector('#detail-map');
        if (!ok || !node.isConnected) return;
        const m = SA.ui.map.create(node, { center: p, zoom: 14 });
        m?.setPitches([{ ...p, freeSlots: 1 }]);
        const loc = S.location.current(); if (loc) m?.setUser(loc);
        this._map = m;
      });

      const fits = (slots, h) => {
        for (let x = h; x < h + sel.duration; x++) { const s = slots.find((y) => y.hour === Math.floor(x)); if (!s || s.status !== 'available') return false; }
        return true;
      };

      const draw = (animate = false) => {
        el.querySelectorAll('[data-date]').forEach((b) => { const on = b.dataset.date === sel.date; b.classList.toggle('is-active', on); b.setAttribute('aria-pressed', on); });
        el.querySelectorAll('[data-dur]').forEach((b) => { const on = +b.dataset.dur === sel.duration; b.setAttribute('aria-selected', on); b.setAttribute('aria-checked', on); });
        const slots = S.pitches.slotsSync(p.id, sel.date);
        if (sel.hour != null && !fits(slots, sel.hour)) sel.hour = null;
        const free = slots.filter((s) => s.status === 'available').length;
        el.querySelector('[data-free]').textContent = free ? `${free} free on ${fmtDate(sel.date)}` : 'Fully booked';
        slotsEl.classList.remove('animate'); void slotsEl.offsetWidth; slotsEl.classList.toggle('animate', animate);
        slotsEl.innerHTML = slots.map((s, si) => {
          const inSel = sel.hour != null && s.hour >= sel.hour && s.hour < sel.hour + sel.duration;
          const st = inSel ? 'selected' : s.status;
          const label = { available: 'Available', booked: 'Booked', past: 'Passed', yours: 'Your booking', selected: 'Selected', blocked: 'Unavailable', closed: 'Closed' }[st];
          return `<button class="slot is-${st} ${s.deal && st === 'available' ? 'has-deal' : ''}" style="--i:${si}" role="option" data-hour="${s.hour}" aria-selected="${inSel}" ${['booked', 'blocked'].includes(s.status) ? `data-wait="1" aria-disabled="true"` : s.status !== 'available' ? 'disabled' : ''} aria-label="${fmtHour(s.hour)}, ${label}${['booked', 'blocked'].includes(s.status) ? ', tap to join the waitlist' : ''}${s.peak ? ', peak price' : ''}${s.deal ? `, ${s.deal}% off` : ''}">
            ${['booked', 'blocked'].includes(s.status) && S.waitlist.entryFor(p.id, sel.date, s.hour) ? `<i class="wl-on" title="You’re on the waitlist">${icon('bell', 11, 2.4)}</i>` : ''}<b class="num">${fmtHour(s.hour)}</b><span class="xs">${st === 'available' ? (s.deal ? `<s>${money(s.base).replace(' EGP', '')}</s> ${money(s.price).replace(' EGP', '')}` : money(s.price).replace(' EGP', '')) : label}</span>${s.deal && st === 'available' ? `<i class="deal-tag">−${s.deal}%</i>` : s.peak && st === 'available' ? '<i class="peak" title="Peak price"></i>' : ''}</button>`;
        }).join('') || '<p class="small muted">Closed on this day.</p>';
        if (!free) slotsEl.insertAdjacentHTML('afterend', '');
        drawSummary();
      };

      const drawSummary = () => {
        const sum = el.querySelector('[data-summary]');
        const bar = el.querySelector('[data-mobile-bar]');
        if (sel.hour == null) {
          sum.innerHTML = `<div class="notice">${icon('info', 18)}<span>Tap a green time to start. Booked times are greyed out.</span></div><button class="btn btn-primary btn-block btn-lg" disabled style="margin-top:14px">Select a time</button>`;
          bar.innerHTML = `<div class="grow"><b>${money(p.price)}</b><span class="xs muted"> / hour</span><span class="xs muted" style="display:block">Pick a date and time</span></div><button class="btn btn-primary" data-scroll-book>Choose time</button>`;
          return;
        }
        el.querySelectorAll('[data-weeks]').forEach((b) => { const on = +b.dataset.weeks === sel.weeks; b.setAttribute('aria-checked', on); b.setAttribute('aria-selected', on); });
        const weekly = sel.weeks > 0 && p.weekly?.enabled;
        const href = `#/book/${p.id}?date=${sel.date}&hour=${sel.hour}&dur=${sel.duration}${weekly ? `&weeks=${sel.weeks}` : ''}`;
        if (weekly) {
          const qs = S.bookings.quoteSeries({ pitchId: p.id, date: sel.date, hour: sel.hour, duration: sel.duration, weeks: sel.weeks });
          sum.innerHTML = `<div class="sel-card">
              <div class="row-between"><span class="small soft">Every ${DAY[SA.core.utils.parseISO(sel.date).getDay()]} from ${fmtDate(sel.date)}</span><button class="link small" data-clear>Change</button></div>
              <b class="h4 num">${fmtRange(sel.hour, sel.duration)} · ${qs.booked} games</b>
              <div class="week-dots" aria-label="${qs.booked} of ${qs.sessions.length} weeks free">${qs.sessions.map((x) => `<i class="${x.available ? 'is-ok' : 'is-off'}" title="${fmtDate(x.date)} — ${x.available ? 'free' : 'taken'}"></i>`).join('')}</div>
              ${qs.skipped ? `<p class="xs muted">${qs.skipped} week${qs.skipped === 1 ? ' is' : 's are'} already taken and will be skipped.</p>` : ''}
              <dl class="kv" style="margin-top:12px"><div><dt>${qs.booked} games</dt><dd class="num">${money(qs.listPrice)}</dd></div>${qs.discountAmount ? `<div><dt>Weekly discount (${Math.round(qs.discount * 100)}%)</dt><dd class="num" style="color:var(--turf)">−${money(qs.discountAmount)}</dd></div>` : ''}<div><dt>Service fee</dt><dd class="num">${money(qs.serviceFee)}</dd></div><div class="total"><dt>Total</dt><dd class="num">${money(qs.total)}</dd></div></dl>
              <p class="xs muted" style="margin-top:8px">${money(Math.round(qs.total / Math.max(1, qs.booked)))} per game. Cancel any single game under the ${esc(pol.name.toLowerCase())} policy.</p>
            </div>
            ${qs.booked >= 2 ? c.cta(`Review ${qs.booked} games`, { href, size: 'lg', attrs: 'style="width:100%;justify-content:space-between"' }) : '<p class="field-error">Fewer than two of those weeks are free — try another time.</p>'}`;
          bar.innerHTML = `<div class="grow"><b class="num">${money(qs.total)}</b><span class="xs muted" style="display:block">${qs.booked} weekly games · ${fmtRange(sel.hour, sel.duration)}</span></div>${qs.booked >= 2 ? c.cta('Review', { href }) : ''}`;
          sum.querySelector('[data-clear]').addEventListener('click', () => { sel.hour = null; draw(); });
          return;
        }
        const q = S.bookings.quote({ pitchId: p.id, date: sel.date, hour: sel.hour, duration: sel.duration });
        sum.innerHTML = `<div class="sel-card">
            <div class="row-between"><span class="small soft">${fmtDate(sel.date, 'long')}</span><button class="link small" data-clear>Change</button></div>
            <b class="h4 num">${fmtRange(sel.hour, sel.duration)}</b>
            <dl class="kv" style="margin-top:12px"><div><dt>Pitch (${sel.duration * 60} min)</dt><dd class="num">${money(q.pitchTotal)}</dd></div><div><dt>Service fee</dt><dd class="num">${money(q.serviceFee)}</dd></div><div class="total"><dt>Total</dt><dd class="num">${money(q.total)}</dd></div></dl>
            <p class="xs muted" style="margin-top:8px">≈ ${money(q.perPlayer)} each for ${p.players} players. Full refund if you cancel up to ${pol.full} hours before.</p>
          </div>
          ${c.cta('Review booking', { href, size: 'lg', attrs: 'style="width:100%;justify-content:space-between"' })}`;
        bar.innerHTML = `<div class="grow"><b class="num">${money(q.total)}</b><span class="xs muted" style="display:block">${fmtDate(sel.date)} · ${fmtRange(sel.hour, sel.duration)}</span></div>${c.cta('Review', { href })}`;
        sum.querySelector('[data-clear]').addEventListener('click', () => { sel.hour = null; draw(); });
      };

      /** Booked hour → offer the waitlist */
      const waitDialog = (h) => {
        const entry = S.waitlist.entryFor(p.id, sel.date, h);
        SA.ui.overlay.open({
          title: entry ? 'You’re on the waitlist' : `${fmtHour(h)} is booked`,
          subtitle: `${esc(p.name)} · ${fmtDate(sel.date, 'long')}`,
          body: `<div class="wl-box"><span class="wl-bell">${icon('bell', 30)}</span><p>${entry ? 'We’ll notify you the moment this hour frees up. First to book gets it.' : 'Join the waitlist and we’ll notify you straight away if the booking is cancelled. It’s free and you can leave any time.'}</p>
            ${S.waitlist.countFor(p.id, sel.date, h) ? `<p class="xs muted">${S.waitlist.countFor(p.id, sel.date, h)} player${S.waitlist.countFor(p.id, sel.date, h) === 1 ? ' is' : 's are'} waiting.</p>` : ''}</div>`,
          footer: entry ? `<button class="btn btn-outline" data-close>Close</button><button class="btn btn-ghost" data-leave style="color:var(--danger)">Leave waitlist</button>` : `<button class="btn btn-outline" data-close>Not now</button><button class="btn btn-primary" data-join>${icon('bell', 15)} Notify me</button>`,
          onMount(m, close) {
            m.querySelector('[data-join]')?.addEventListener('click', () => { try { S.waitlist.join({ pitchId: p.id, date: sel.date, hour: h }); close(); SA.ui.overlay.toast('You’re on the waitlist — we’ll notify you', { type: 'success' }); draw(); } catch (err) { close(); SA.ui.overlay.toast(err.message, { type: err.code === 'available' ? 'info' : 'error' }); draw(); } });
            m.querySelector('[data-leave]')?.addEventListener('click', () => { S.waitlist.leave(entry.id); close(); SA.ui.overlay.toast('Removed from the waitlist'); draw(); });
          },
        });
      };
      el.addEventListener('click', (e) => { if (e.target.closest('[data-scroll-book]')) el.querySelector('#book-panel').scrollIntoView({ behavior: 'smooth', block: 'start' }); });
      el.querySelector('[data-dates]').addEventListener('click', (e) => { const b = e.target.closest('[data-date]'); if (b) { sel.date = b.dataset.date; err.textContent = ''; draw(true); } });
      el.querySelector('.dur-tabs').addEventListener('click', (e) => { const b = e.target.closest('[data-dur]'); if (b) { sel.duration = +b.dataset.dur; err.textContent = ''; draw(true); } });
      const syncWeeks = () => el.querySelectorAll('[data-weeks]').forEach((x) => { const on = +x.dataset.weeks === sel.weeks; x.setAttribute('aria-checked', on); x.setAttribute('aria-selected', on); });
      el.querySelector('[data-repeat]')?.addEventListener('click', (e) => { const b = e.target.closest('[data-weeks]'); if (b) { sel.weeks = +b.dataset.weeks; syncWeeks(); drawSummary(); } });
      syncWeeks();
      slotsEl.addEventListener('click', (e) => {
        const b = e.target.closest('[data-hour]'); if (!b) return;
        const h = +b.dataset.hour;
        if (b.dataset.wait) { waitDialog(h); return; }
        const slots = S.pitches.slotsSync(p.id, sel.date);
        if (!fits(slots, h)) { err.textContent = `There isn’t ${sel.duration * 60} minutes free from ${fmtHour(h)}. Pick an earlier start or a shorter game.`; b.classList.add('shake'); setTimeout(() => b.classList.remove('shake'), 400); return; }
        err.textContent = '';
        sel.hour = h; draw();
        if (matchMedia('(max-width: 860px)').matches) return;
        el.querySelector('[data-summary]').scrollIntoView({ behavior: 'smooth', block: 'nearest' });
      });
      draw(true);
      el.querySelector('[data-dates] .is-active')?.scrollIntoView({ inline: 'center', block: 'nearest' });
      return () => this._map?.destroy();
    },
  };
})(window.SA);
