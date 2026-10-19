(function (SA) {
  const { icon, cards } = SA.ui;
  const c = SA.ui.c;
  const S = SA.services;

  SA.pages.bookings = {
    title: 'My bookings — Super App',
    async render(ctx) {
      const { upcoming, past } = await S.bookings.list();
      const tab = ['past', 'waitlist'].includes(ctx.query.tab) ? ctx.query.tab : 'upcoming';
      const list = tab === 'past' ? past : upcoming;
      await S.waitlist.load?.();
      const wl = S.waitlist.mine();
      return `${c.pageHead({ crumbs: [['Home', '#/'], ['My bookings']], title: 'My bookings', actions: c.cta('Book a pitch', { href: '#/pitches' }) })}
      <div class="container">
        <div class="tabs" role="tablist" style="margin-bottom:20px">
          <a class="tab" role="tab" href="#/bookings" aria-selected="${tab === 'upcoming'}">Upcoming <span class="muted">${upcoming.length}</span></a>
          <a class="tab" role="tab" href="#/bookings?tab=past" aria-selected="${tab === 'past'}">Previous <span class="muted">${past.length}</span></a>
          <a class="tab" role="tab" href="#/bookings?tab=waitlist" aria-selected="${tab === 'waitlist'}">${icon('bell', 14)} Waitlist <span class="muted">${wl.length}</span></a>
        </div>
        <div class="stack" style="--gap:12px">
          ${tab === 'waitlist' ? (wl.length ? wl.map((e) => { const p = S.pitches.getSync(e.pitchId); return `<article class="card booking-row wl-row ${e.status === 'notified' ? 'is-free' : ''}">
              <a href="#/pitches/${e.pitchId}?date=${e.date}" class="booking-thumb media"><img src="${SA.data.media.photo(p?.images[0], 300, 300)}" alt="" loading="lazy" /></a>
              <div class="grow"><span class="badge ${e.status === 'notified' ? 'badge-turf' : 'badge-info'}">${e.status === 'notified' ? 'Free now — book it!' : 'Waiting'}</span>
                <h3 class="h4" style="margin-top:6px">${SA.core.utils.esc(p?.name || '')}</h3><p class="small soft">${icon('calendar', 14)} ${SA.core.utils.fmtDate(e.date)} · ${SA.core.utils.fmtHour(e.hour)}</p></div>
              <div class="row" style="--gap:6px">${e.status === 'notified' ? `<a class="btn btn-primary btn-sm" href="#/pitches/${e.pitchId}?date=${e.date}&hour=${e.hour}">Book now</a>` : ''}<button class="btn btn-ghost btn-sm" data-leave-wl="${e.id}">Leave</button></div></article>`; }).join('')
            : c.empty({ icon: 'bell', title: 'You’re not waiting for any time', text: 'On a pitch page, tap a booked hour to join its waitlist — we’ll tell you if it frees up.', action: '<a class="btn btn-primary" href="#/pitches">Find a pitch</a>' }))
          : list.length ? list.map((b) => cards.bookingRow(b)).join('') : c.empty({ icon: 'calendar', title: tab === 'past' ? 'No previous bookings yet' : 'No upcoming games', text: tab === 'past' ? 'Games you’ve played or cancelled will appear here.' : 'Find a pitch near you and book a time — it takes less than a minute.', action: `<a class="btn btn-primary" href="#/pitches">Find a pitch</a>` })}
        </div>
      </div>`;
    },
    mount(el) {
      el.addEventListener('click', async (e) => { const b = e.target.closest('[data-leave-wl]'); if (b) { await S.waitlist.leave(b.dataset.leaveWl); SA.ui.overlay.toast('Removed from the waitlist'); SA.app.rerender(); } });
    },
  };
})(window.SA);
