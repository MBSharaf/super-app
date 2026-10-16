(function (SA) {
  const { esc, money, fmtRange, fmtDate, fmtHour, fmtKm, timeAgo, parseISO, DAY } = SA.core.utils;
  const { icon } = SA.ui;
  const c = SA.ui.c;
  const media = SA.data.media;
  const S = SA.services;
  const STEPS = ['Pitch & time', 'Review & pay', 'Confirmed'];

  /* -------- Review & pay: #/book/:id?date&hour&dur[&weeks] -------- */
  SA.pages.bookingCheckout = {
    title: 'Review booking — Super App',
    async render(ctx) {
      const p = await S.pitches.get(ctx.params.id);
      const { date, hour, dur } = ctx.query;
      if (!date || hour == null) return c.empty({ icon: 'calendar', title: 'Pick a time first', text: 'Choose a date and start time on the pitch page.', action: `<a class="btn btn-primary" href="#/pitches/${p.id}">Back to ${esc(p.name)}</a>` });
      const weeks = S.bookings.WEEK_OPTIONS.includes(+ctx.query.weeks) && p.weekly?.enabled ? +ctx.query.weeks : 0;
      const code = weeks ? '' : String(ctx.query.code || '').toUpperCase();
      let q, codeErr = '';
      if (weeks) q = S.bookings.quoteSeries({ pitchId: p.id, date, hour: +hour, duration: +dur || 1, weeks });
      else { try { q = S.bookings.quote({ pitchId: p.id, date, hour: +hour, duration: +dur || 1, code }); } catch (err) { codeErr = err.message; q = S.bookings.quote({ pitchId: p.id, date, hour: +hour, duration: +dur || 1 }); } }
      const points = S.loyalty.estimate(q.pitchTotal);
      const pol = S.pitches.policyOf(p.cancelPolicy);
      return `<div class="container narrow-checkout">
        <div class="page-head"><a class="back-link" href="#/pitches/${p.id}?date=${date}&hour=${hour}&dur=${dur}${weeks ? `&weeks=${weeks}` : ''}">${icon('chevronLeft', 16)} Change time</a>
          <h1 class="h1" style="margin-top:10px">Review and pay</h1><div style="margin-top:14px">${c.steps(STEPS, 1)}</div></div>
        <div class="checkout-layout">
          <form class="stack" style="--gap:20px" id="book-form" novalidate>
            <section class="panel panel-pad">
              <div class="review-pitch">
                <div class="media review-thumb"><img src="${media.photo(p.images[0], 400, 300)}" alt="" /></div>
                <div class="grow"><h2 class="h3">${esc(p.name)}</h2><p class="small soft">${esc(p.address)}, ${esc(p.area)} · ${fmtKm(p.distanceKm)}</p>
                  <div class="row wrap" style="margin-top:10px;--gap:8px"><span class="badge">${esc(p.format)}</span><span class="badge">${esc(p.surface)}</span>${weeks ? `<span class="badge badge-turf">${icon('repeat', 12)} Weekly</span>` : ''}</div></div>
              </div>
              <div class="when-grid">
                <div>${icon('calendar', 18)}<span><span class="xs muted">${weeks ? 'Starts' : 'Date'}</span><b>${fmtDate(date, 'long')}</b></span></div>
                <div>${icon('clock', 18)}<span><span class="xs muted">Time</span><b class="num">${fmtRange(+hour, +dur || 1)}</b></span></div>
              </div>
              ${weeks ? `<div class="series-list"><div class="row-between"><b class="small">Every ${DAY[parseISO(date).getDay()]} · ${q.booked} games</b>${q.skipped ? `<span class="xs muted">${q.skipped} taken week${q.skipped === 1 ? '' : 's'} skipped</span>` : ''}</div>
                <ul>${q.sessions.map((s, i) => `<li class="${s.available ? '' : 'is-off'}" style="--i:${i}"><b class="num">${fmtDate(s.date)}</b><span class="xs">${s.available ? money(s.pitchTotal) : 'Taken — skipped'}</span></li>`).join('')}</ul></div>` : ''}
              <div class="field" style="margin-top:18px"><label for="bk-players">How many players are coming?</label>
                <select class="select" id="bk-players" name="players">${Array.from({ length: p.players }, (_, i) => i + 2).filter((n) => n <= p.players).map((n) => `<option ${n === p.players ? 'selected' : ''}>${n}</option>`).join('')}</select>
                <span class="field-hint">Short on players? After booking you can post the match and players nearby can join.</span></div>
            </section>
            <section class="panel panel-pad">${SA.ui.payment.render({ amount: q.total, name: 'bkpay' })}</section>
          </form>
          <aside class="panel panel-pad order-summary">
            <h2 class="h4">Price details</h2>
            <dl class="kv" style="margin-top:14px">
              ${weeks ? `<div><dt>${q.booked} games · ${(+dur || 1) * 60} min each</dt><dd class="num">${money(q.listPrice)}</dd></div>
                ${q.discountAmount ? `<div><dt>Weekly discount (${Math.round(q.discount * 100)}%)</dt><dd class="num" style="color:var(--turf)">−${money(q.discountAmount)}</dd></div>` : ''}`
                : `<div><dt>${esc(p.name)} · ${(+dur || 1) * 60} min</dt><dd class="num">${money(q.listPrice)}</dd></div>
                  ${q.promo ? `<div><dt>Code ${esc(q.promo.code)} (${esc(q.promo.label)})</dt><dd class="num" style="color:var(--turf)">−${money(q.promo.amount)}</dd></div>` : ''}`}
              <div><dt>Service fee</dt><dd class="num">${money(q.serviceFee)}</dd></div>
              <div class="total"><dt>Total</dt><dd class="num">${money(q.total)}</dd></div>
            </dl>
            <p class="xs muted" style="margin-top:6px">Prices include VAT ${Math.round(S.db.settings().vat * 100)}%. You can download the invoice after paying.</p>
            ${weeks ? `<p class="xs muted" style="margin-top:8px">${icon('tag', 12)} Promo codes can’t be combined with the weekly discount.</p>` : q.promo ? `<div class="coupon-on" style="margin-top:12px">${icon('tag', 16)}<span class="grow small"><b>${esc(q.promo.code)}</b> applied — you save ${money(q.promo.amount)}</span><a class="link xs" href="${SA.core.router.href(`/book/${p.id}`, { ...ctx.query, code: undefined })}" data-replace>Remove</a></div>`
              : `<form class="coupon" data-code-form style="margin-top:12px"><label class="field-label" for="bk-code">Promo code</label><div class="row" style="--gap:8px;margin-top:6px"><input class="input" id="bk-code" name="code" value="${esc(code)}" placeholder="e.g. TURF22" autocomplete="off" style="text-transform:uppercase" /><button class="btn btn-outline" type="submit">Apply</button></div><p class="field-error">${esc(codeErr)}</p></form>`}
            <div class="points-earn">${icon('sparkle', 16)}<span>You’ll earn <b class="num">${(weeks ? S.loyalty.estimate(q.pitchTotal) : points).toLocaleString()} Super Points</b> ${weeks ? 'across these games' : 'after the game'}.</span></div>
            <div class="notice turf" style="margin-top:16px">${icon('shield', 18)}<span><b>${esc(pol.name)} cancellation:</b> ${esc(pol.summary)}${weeks ? ' Applies to each game separately.' : ''} Refunds go straight to your wallet.</span></div>
            ${weeks && (q.booked < 2 || !q.sessions[0].available) ? `<p class="field-error" style="margin-top:12px">${!q.sessions[0].available ? 'The first game’s time was just booked by someone else.' : 'Fewer than two of those weeks are free.'} <a class="link" href="#/pitches/${p.id}?date=${date}">Pick another time</a></p>` : ''}
            <div style="margin-top:14px">${SA.ui.legal.field()}</div>
            <button class="btn btn-blaze btn-lg btn-block" style="margin-top:8px" form="book-form" type="submit" ${weeks && (q.booked < 2 || !q.sessions[0].available) ? 'disabled' : ''}>${icon('lock', 16)} Pay ${money(q.total)}</button>
            <p class="xs muted" style="margin-top:10px;text-align:center">By paying you agree to the pitch rules, the cancellation policy and our <a class="link" href="#/legal/refunds" target="_blank" rel="noopener">refunds policy</a>.</p>
          </aside>
        </div>
      </div>`;
    },
    mount(el, ctx) {
      const form = el.querySelector('#book-form');
      if (!form) return;
      SA.ui.payment.bind(form, { name: 'bkpay' });
      const weeks = +ctx.query.weeks || 0;
      el.querySelector('[data-code-form]')?.addEventListener('submit', (e) => {
        e.preventDefault();
        const v = e.target.code.value.trim();
        SA.core.router.go(SA.core.router.href(`/book/${ctx.params.id}`, { ...ctx.query, code: v || undefined }).slice(1), { replace: true });
      });
      form.addEventListener('submit', async (e) => {
        e.preventDefault();
        const aside = el.querySelector('.legal-consent')?.parentElement || form;
        if (!SA.ui.legal.check(aside)) return;
        const payment = SA.ui.payment.read(form, { name: 'bkpay' });
        const args = { pitchId: ctx.params.id, date: ctx.query.date, hour: +ctx.query.hour, duration: +ctx.query.dur || 1, payment, players: +form.players.value, waiver: true };
        try {
          if (weeks) {
            const r = await SA.ui.payment.process(() => S.bookings.createSeries({ ...args, weeks }), { root: form, label: `Booking ${weeks} weeks`, methodHint: payment.method });
            SA.core.router.go(`/booking/${r.bookings[0].id}?new=1`, { replace: true });
          } else {
            let code = ctx.query.code || '';
            try { if (code) S.bookings.quote({ ...args, code }); } catch { code = ''; } // an invalid code is shown on the page and ignored here
            const b = await SA.ui.payment.process(() => S.bookings.create({ ...args, code }), { root: form, label: 'Confirming your booking', methodHint: payment.method });
            SA.core.router.go(`/booking/${b.id}?new=1`, { replace: true });
          }
        } catch (err) { if (err.code === 'slot_taken') SA.core.router.go(`/pitches/${ctx.params.id}?date=${ctx.query.date}`); }
      });
    },
  };

  /* -------- Confirmation / booking detail: #/booking/:id -------- */
  const reviewForm = () => `<form class="rate-form" data-rate novalidate>
      <fieldset class="rate-stars" aria-label="Your rating">${[5, 4, 3, 2, 1].map((n) => `<input type="radio" id="rt-${n}" name="rating" value="${n}" /><label for="rt-${n}" title="${n} star${n > 1 ? 's' : ''}">${icon('star', 28, 1.6)}</label>`).join('')}</fieldset>
      <span class="field-error" data-err="rating"></span>
      <textarea class="textarea" name="text" rows="3" maxlength="500" placeholder="How was the pitch, the lights, the staff?" aria-label="Your review"></textarea>
      <span class="field-error" data-err="text"></span>
      <button class="btn btn-primary" type="submit">${icon('star', 16)} Post review</button></form>`;

  SA.pages.bookingDetail = {
    title: 'Booking — Super App',
    async render(ctx) {
      const b = await S.bookings.get(ctx.params.id);
      const p = S.pitches.getSync(b.pitchId) || { images: [b.image], address: '', area: b.area, city: b.city, lat: 0, lng: 0, id: b.pitchId };
      const isNew = ctx.query.new;
      const venue = S.bookings.venueRecord(b.id);
      const checkedIn = venue?.checkedInAt || venue?.checkedIn;
      const cancelled = b.status === 'cancelled';
      const past = b.startsAt + b.duration * 36e5 < Date.now();
      const statusLabel = cancelled ? 'Cancelled' : checkedIn && !past ? 'Checked in' : past ? 'Played' : 'Confirmed';
      const series = b.seriesId ? S.bookings.seriesOf(b.seriesId) : [];
      const live = series.filter((x) => x.status === 'confirmed' && parseISO(x.date).getTime() + x.hour * 36e5 > Date.now());
      const myReview = S.bookings.reviewOf(b);
      const pol = S.pitches.policyOf(b.policy);
      const pk = b.seriesId ? S.bookings.packageStatus(b.seriesId) : null;
      // A weekly package has one QR code for all its games (it expires when none is left)
      const code = pk ? S.bookings.packageCode(b.seriesId) : S.bookings.checkInCode(b.id);
      const pkgPaid = pk ? series.reduce((t, x) => t + (x.total || 0), 0) : 0;
      const ST = { played: ['badge-turf', 'Played'], missed: ['badge-danger', 'Missed — counted'], excused: ['badge-gold', 'Excused'], cancelled: ['', 'Cancelled'], upcoming: ['badge-info', 'Upcoming'] };
      return `<div class="container confirm-wrap">
        ${isNew ? `<div class="confirm-hero"><div class="success-mark"><svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"><path d="m4.5 12.5 4.8 4.8L19.5 7"/></svg></div>
          <h1 class="h1">${series.length ? `${series.length} games booked!` : 'You’re booked!'}</h1><p class="lead" style="text-align:center">Show the QR code at reception — staff scan it to check you in.</p><div style="margin-top:12px">${c.steps(STEPS, 3)}</div></div>`
          : `<div class="page-head"><a class="back-link" href="#/bookings">${icon('chevronLeft', 16)} My bookings</a><h1 class="h1" style="margin-top:10px">Booking details</h1></div>`}
        <article class="ticket">
          <div class="ticket-top">
            <div class="media ticket-img"><img src="${media.photo(p.images[0], 1000, 500)}" alt="" /><span class="badge ${cancelled ? 'badge-danger' : past ? 'badge-glass' : 'badge-turf'} ticket-status">${statusLabel}</span></div>
            <div class="ticket-body">
              <div class="row-between wrap"><div><span class="xs muted">Booking ID</span><b class="ticket-id num">${b.id}</b></div>
                <button class="btn btn-outline btn-sm" data-copy="${b.id}">${icon('copy', 14)} Copy</button></div>
              <h2 class="h2" style="margin-top:14px">${esc(b.pitchName)}</h2>
              <p class="small soft">${esc(p.address)}${p.address ? ', ' : ''}${esc(p.area)}, ${esc(p.city)}</p>
              ${b.seriesId ? `<p class="small" style="margin-top:6px">${icon('repeat', 14)} Weekly booking · game ${b.sessionNo} of ${b.seriesSize}</p>` : ''}
            </div>
          </div>
          <div class="ticket-cut"></div>
          <div class="ticket-grid">
            <div><span class="xs muted">Date</span><b>${fmtDate(b.date, 'long')}</b></div>
            <div><span class="xs muted">Time</span><b class="num">${fmtRange(b.hour, b.duration)}</b></div>
            <div><span class="xs muted">Pitch</span><b>${esc(b.format)} · ${b.players} players</b></div>
            <div><span class="xs muted">Payment</span><b>${esc(b.payment.label)}</b></div>
            <div><span class="xs muted">${pk ? 'Package paid' : 'Amount paid'}</span><b class="num">${money(pk ? pkgPaid : b.total)}</b>${pk ? `<span class="xs muted">${pk.total} games${b.discountAmount ? ` · ${money(series.reduce((t, x) => t + (x.discountAmount || 0), 0))} weekly discount` : ''}</span>` : b.discountAmount ? `<span class="xs" style="color:var(--turf)">${money(b.discountAmount)} weekly discount</span>` : ''}</div>
            <div><span class="xs muted">Status</span><b style="color:${cancelled ? 'var(--danger)' : 'var(--turf)'}">${statusLabel}${cancelled && b.refund ? ` · ${money(b.refund)} refunded` : ''}${checkedIn ? ` · ${new Date(venue.checkedInAt || Date.now()).toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' })}` : ''}</b></div>
          </div>
          ${pk ? `<div class="ticket-qr">
            <div class="tq-code ${pk.expired ? 'is-used' : ''}">${SA.core.qr.svg(code, { size: 168, margin: 2, label: `Check-in QR code for package ${b.seriesId}` })}${pk.expired ? `<span class="tq-done">${icon('check', 30, 3)}</span>` : ''}</div>
            <div><b class="h4">${pk.expired ? 'Package used up' : 'One QR for the whole package'}</b><p class="small soft">${pk.expired ? `All ${pk.total} games are used. The code has expired.` : `Show it at reception every week. It works for ${pk.total} games, then it expires. If the camera can’t read it, staff can type <b class="num">${b.seriesId}</b>.`}</p></div>
          </div>` : !cancelled && !past ? `<div class="ticket-qr">
            <div class="tq-code ${checkedIn ? 'is-used' : ''}">${SA.core.qr.svg(code, { size: 168, margin: 2, label: `Check-in QR code for booking ${b.id}` })}${checkedIn ? `<span class="tq-done">${icon('check', 30, 3)}</span>` : ''}</div>
            <div><b class="h4">${checkedIn ? 'You’re checked in' : 'Your check-in code'}</b><p class="small soft">${checkedIn ? 'Have a great game!' : `Show this at reception. If the camera can’t read it, staff can type your booking ID <b class="num">${b.id}</b>.`}</p></div>
          </div>` : `<div class="ticket-code" aria-hidden="true">${Array.from({ length: 46 }, (_, i) => `<i style="width:${1 + ((b.id.charCodeAt(i % b.id.length) + i) % 4)}px"></i>`).join('')}</div>`}
        </article>

        ${pk ? `<section class="panel panel-pad pkg-panel">
          <div class="row-between wrap" style="--gap:8px"><h2 class="h4">${icon('repeat', 18)} Your weekly package</h2><span class="xs muted num">${esc(b.seriesId)}</span></div>
          <div class="pkg-stats"><span><b class="num">${pk.played}</b><span class="xs">Played</span></span><span class="is-bad"><b class="num">${pk.missed}</b><span class="xs">Missed</span></span><span><b class="num">${pk.left}</b><span class="xs">Left</span></span><span><b class="num">${pk.total}</b><span class="xs">In the package</span></span></div>
          <p class="xs muted">${icon('info', 12)} A game you don’t come to counts as used. Can’t make it? Cancel it before kick-off from its page.${pk.exception ? ' One missed game was excused by Super App — a make-up game was added.' : ''}</p>
          <ul class="pkg-list">${pk.sessions.map((x) => { const bk = series.find((y) => y.id === x.id); return `<li class="${x.id === b.id ? 'is-me' : ''}"><a href="#/booking/${x.id}" class="row-between" style="--gap:8px"><span class="small"><b class="num">${x.no ? `#${x.no}` : '—'}</b> · ${fmtDate(x.date)} · ${fmtRange(x.hour, x.duration)}${x.makeup ? ' <span class="badge badge-gold">Make-up</span>' : ''}</span><span class="badge ${ST[x.state][0]}">${ST[x.state][1]}</span></a></li>`; }).join('')}</ul>
          ${live.length > 1 ? `<button class="btn btn-ghost btn-sm" data-cancel-series style="color:var(--danger);margin-top:10px">Cancel all ${live.length} remaining games</button>` : ''}
        </section>` : ''}

        ${!cancelled && !past ? `<section class="panel panel-pad need-players">
          ${b.publishedMatchId ? `<div class="row wrap"><span class="service-ico">${icon('users', 20)}</span><div class="grow"><h2 class="h4">Your match is listed on Find Players</h2><p class="small soft">Players nearby can see it and join. You’ll get a notification for each new player.</p></div><a class="btn btn-primary" href="#/players/${b.publishedMatchId}">View listing</a></div>`
            : `<div class="row wrap"><span class="service-ico">${icon('users', 20)}</span><div class="grow"><h2 class="h4">Missing players?</h2><p class="small soft">Post this booking as “Looking for players” and fill the empty spots with players near ${esc(b.area)}.</p></div>${c.cta('Find players', { href: `#/players/new?booking=${b.id}` })}</div>`}
        </section>` : ''}

        ${past && !cancelled ? `<section class="panel panel-pad">
          ${myReview ? `<h2 class="h4">Your review</h2><div class="review" style="margin-top:10px"><div class="row-between">${c.stars(myReview.rating, 15)}<span class="xs muted">${timeAgo(myReview.createdAt)}</span></div><p class="small soft" style="margin-top:8px">${esc(myReview.text)}</p>
              ${myReview.reply ? `<div class="owner-reply"><b class="xs">${icon('message', 12)} Response from the venue</b><p class="small">${esc(myReview.reply.text)}</p></div>` : '<p class="xs muted" style="margin-top:8px">The venue can reply to your review — you’ll get a notification.</p>'}</div>`
            : `<h2 class="h4">How was ${esc(b.pitchName)}?</h2><p class="small soft">Your review helps other players choose — and the venue can reply.</p>${reviewForm()}`}
        </section>` : ''}

        <div class="row wrap confirm-actions">
          ${!cancelled && !past ? `<button class="btn btn-outline" data-ics>${icon('calendar', 16)} Add to calendar</button>
          <a class="btn btn-outline" target="_blank" rel="noopener" href="https://www.google.com/maps/dir/?api=1&destination=${p.lat},${p.lng}">${icon('map', 16)} Directions</a>` : `<a class="btn btn-primary" href="#/pitches/${p.id}">Book again</a>`}
          <button class="btn btn-outline" data-invoice>${icon('file', 16)} ${pk ? 'Package invoice (PDF)' : 'Invoice (PDF)'}</button>
          ${!cancelled && !past && !checkedIn ? `<button class="btn btn-ghost" data-cancel style="color:var(--danger)">Cancel booking</button>` : ''}
          <a class="btn btn-ghost" href="#/bookings">All bookings</a>
          <a class="btn btn-ghost" href="#/support?ref=booking:${encodeURIComponent(b.id)}">${icon('help', 16)} Get help</a>
        </div>
        ${!cancelled && !past ? `<p class="xs muted" style="text-align:center;margin-top:12px">${icon('shield', 12)} ${esc(pol.name)} policy — ${esc(pol.summary)}</p>` : ''}
      </div>`;
    },
    mount(el, ctx) {
      const b = S.bookings.getSync(ctx.params.id);
      if (ctx.query.new) setTimeout(() => { const m = el.querySelector('.success-mark')?.getBoundingClientRect(); SA.core.motion.confetti(m ? { x: m.left + m.width / 2, y: m.top + m.height / 2 } : {}); }, 350);
      el.querySelector('[data-copy]')?.addEventListener('click', (e) => { navigator.clipboard?.writeText(e.currentTarget.dataset.copy); SA.ui.overlay.toast('Booking ID copied'); });
      el.querySelector('[data-ics]')?.addEventListener('click', () => {
        const d = parseISO(b.date); d.setHours(0, 0, 0, 0);
        const start = new Date(d.getTime() + b.hour * 36e5), end = new Date(start.getTime() + b.duration * 36e5);
        const f = (x) => x.toISOString().replace(/[-:]/g, '').replace(/\.\d{3}/, '');
        const ics = `BEGIN:VCALENDAR\nVERSION:2.0\nPRODID:-//Super App//EN\nBEGIN:VEVENT\nUID:${b.id}@superapp\nDTSTAMP:${f(new Date())}\nDTSTART:${f(start)}\nDTEND:${f(end)}\nSUMMARY:Football at ${b.pitchName}\nLOCATION:${b.area}\\, ${b.city}\nDESCRIPTION:Booking ${b.id}\nEND:VEVENT\nEND:VCALENDAR`;
        const a = document.createElement('a'); a.href = URL.createObjectURL(new Blob([ics], { type: 'text/calendar' })); a.download = `super-app-${b.id}.ics`; a.click();
        SA.ui.overlay.toast('Calendar event downloaded');
      });
      el.querySelector('[data-invoice]')?.addEventListener('click', async (e) => {
        const btn = e.currentTarget; btn.disabled = true;
        const u = SA.core.store.get().user || {};
        try {
          if (b.seriesId) await SA.ui.docs.packageInvoice({ seriesId: b.seriesId, games: S.bookings.seriesOf(b.seriesId), customer: { name: u.name, phone: u.phone } });
          else await SA.ui.docs.bookingInvoice({ ...b, amount: b.pitchTotal, fee: b.serviceFee, customer: { name: u.name, phone: u.phone }, payment: b.payment.label, source: 'app' });
        }
        catch (err) { console.error(err); SA.ui.overlay.toast('Couldn’t create the invoice. Try again.', { type: 'error' }); }
        btn.disabled = false;
      });
      el.querySelector('[data-cancel]')?.addEventListener('click', async () => {
        const c0 = S.bookings.cancellationPolicy(b);
        const text = c0.kind === 'full' ? `You’ll get the full ${money(b.total)} back in your wallet.`
          : c0.kind === 'half' ? `Under the ${c0.policy.name.toLowerCase()} policy you get 50% back (${money(c0.refund)}) because kick-off is less than ${c0.policy.full} hours away.`
          : `Under the ${c0.policy.name.toLowerCase()} policy there’s no refund less than ${c0.policy.half} hours before kick-off. You can still cancel to free the pitch for others.`;
        const ok = await SA.ui.overlay.confirm({ title: 'Cancel this booking?', text, confirmLabel: 'Cancel booking', cancelLabel: 'Keep booking', danger: true });
        if (!ok) return;
        if (b.publishedMatchId) await S.matches.unpublish(b.publishedMatchId);
        const { refund } = await S.bookings.cancel(b.id);
        SA.ui.overlay.toast(refund ? `Booking cancelled — ${money(refund)} refunded to your wallet` : 'Booking cancelled');
        SA.app.rerender();
      });
      el.querySelector('[data-cancel-series]')?.addEventListener('click', async () => {
        const list = S.bookings.seriesOf(b.seriesId).filter((x) => x.status === 'confirmed' && parseISO(x.date).getTime() + x.hour * 36e5 > Date.now());
        const refund = list.reduce((t, x) => t + S.bookings.cancellationPolicy(x).refund, 0);
        const ok = await SA.ui.overlay.confirm({ title: `Cancel ${list.length} remaining games?`, text: `Each game follows the cancellation policy — you get ${money(refund)} back in your wallet in total.`, confirmLabel: 'Cancel all', cancelLabel: 'Keep them', danger: true });
        if (!ok) return;
        const r = await S.bookings.cancelSeries(b.seriesId);
        SA.ui.overlay.toast(`${r.count} games cancelled — ${money(r.refund)} refunded`);
        SA.app.rerender();
      });
      const rf = el.querySelector('[data-rate]');
      rf?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const d = SA.core.utils.formData(rf);
        rf.querySelectorAll('[data-err]').forEach((x) => (x.textContent = ''));
        try { await S.bookings.review(b.id, d); SA.core.motion.confetti({ count: 40, spread: 0.5 }); SA.ui.overlay.toast('Thanks — your review is live on the pitch page'); SA.app.rerender(); }
        catch (err) { Object.entries(err.details || {}).forEach(([k, v]) => { const n = rf.querySelector(`[data-err="${k}"]`); if (n) n.textContent = v; }); if (!err.details) SA.ui.overlay.toast(err.message, { type: 'error' }); }
      });
    },
  };
})(window.SA);
