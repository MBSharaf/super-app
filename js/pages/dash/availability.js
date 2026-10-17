(function (SA) {
  const { esc, money, isoDate, today, addDays, fmtHour, fmtDate, DAY, relDay } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const D = () => SA.services.dash;
  const PS = () => SA.services.pitches;

  const blockDialog = (p, { date, hours = [] } = {}) => new Promise((resolve) => {
    SA.ui.overlay.open({
      title: hours.length === 1 ? `Block ${fmtHour(hours[0])} on ${fmtDate(date)}` : `Block dates at ${esc(p.name)}`,
      subtitle: 'Blocked time can’t be booked on Super App. Existing bookings are not affected.',
      body: `<form id="blk" novalidate class="form-grid">
        <div class="field"><label for="b-from">From</label><input class="input" id="b-from" type="date" name="from" value="${date}" min="${isoDate(today())}" /></div>
        <div class="field"><label for="b-to">To</label><input class="input" id="b-to" type="date" name="to" value="${date}" min="${isoDate(today())}" /></div>
        <fieldset class="field span-2"><legend class="field-label">Hours</legend><div class="seg"><label class="seg-opt"><input type="radio" name="scope" value="all" ${hours.length ? '' : 'checked'} /><span>Whole day</span></label><label class="seg-opt"><input type="radio" name="scope" value="some" ${hours.length ? 'checked' : ''} /><span>Specific hours</span></label></div>
          <div class="hour-picks" ${hours.length ? '' : 'hidden'}>${Array.from({ length: p.open[1] - p.open[0] }, (_, i) => p.open[0] + i).map((h) => `<label class="hp"><input type="checkbox" name="hours" value="${h}" ${hours.includes(h) ? 'checked' : ''} /><span>${fmtHour(h)}</span></label>`).join('')}</div></fieldset>
        <div class="field span-2"><label for="b-reason">Reason</label><input class="input" id="b-reason" name="reason" placeholder="Maintenance, private event, academy training…" /></div>
        <p class="field-error span-2" data-err="form"></p></form>`,
      footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-primary" form="blk" type="submit">${icon('lock', 15)} Block</button>`,
      onMount(m, close) {
        const f = m.querySelector('#blk');
        f.addEventListener('change', () => (m.querySelector('.hour-picks').hidden = f.scope.value !== 'some'));
        f.addEventListener('submit', async (e) => {
          e.preventDefault();
          const fd = new FormData(f);
          const hrs = fd.get('scope') === 'some' ? fd.getAll('hours').map(Number) : [];
          if (fd.get('scope') === 'some' && !hrs.length) { m.querySelector('[data-err="form"]').textContent = 'Pick at least one hour.'; return; }
          try { await D().addClosure({ pitchId: p.id, from: fd.get('from'), to: fd.get('to'), hours: hrs, reason: fd.get('reason') }); close(); resolve(true); }
          catch (err) { m.querySelector('[data-err="form"]').textContent = err.message; }
        });
      },
      onClose: () => resolve(false),
    });
  });

  const walkInDialog = (p, date, hour) => new Promise((resolve) => {
    const price = PS().priceAt(p, date, hour);
    SA.ui.overlay.open({
      title: 'Add a walk-in booking', subtitle: `${esc(p.name)} · ${fmtDate(date, 'long')} · from ${fmtHour(hour)}`,
      body: `<form id="wi" novalidate class="form-grid">
        <div class="field"><label for="w-name">Customer name</label><input class="input" id="w-name" name="name" autofocus /><span class="field-error" data-err="name"></span></div>
        <div class="field"><label for="w-phone">Phone (optional)</label><input class="input" id="w-phone" name="phone" inputmode="tel" placeholder="+20 1XX XXX XXXX" /></div>
        <div class="field"><label for="w-dur">Duration</label><select class="select" id="w-dur" name="duration">${[1, 2, 3].map((d) => `<option value="${d}">${d} hour${d > 1 ? 's' : ''}</option>`).join('')}</select></div>
        <div class="field"><label for="w-amt">Amount collected (EGP)</label><input class="input num" id="w-amt" name="amount" type="number" min="0" step="10" value="${price}" /></div>
        <fieldset class="field span-2"><legend class="field-label">Paid with</legend><div class="seg">${['Cash at venue', 'Card at venue'].map((x, i) => `<label class="seg-opt"><input type="radio" name="payment" value="${x}" ${i === 0 ? 'checked' : ''} /><span>${x}</span></label>`).join('')}</div></fieldset>
        <p class="xs muted span-2">Walk-ins are recorded for your reports and block the time on Super App. No commission is charged on walk-ins.</p>
        <p class="field-error span-2" data-err="form"></p></form>`,
      footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-primary" form="wi" type="submit">${icon('plus', 15)} Add booking</button>`,
      onMount(m, close) {
        const f = m.querySelector('#wi');
        f.duration.addEventListener('change', () => { let t = 0; for (let h = hour; h < hour + +f.duration.value; h++) t += PS().priceAt(p, date, h); f.amount.value = t; });
        f.addEventListener('submit', async (e) => {
          e.preventDefault();
          const fd = SA.core.utils.formData(f);
          try { const b = await D().walkIn({ pitchId: p.id, date, hour, duration: +fd.duration, name: fd.name, phone: fd.phone, amount: +fd.amount, payment: fd.payment }); close(); resolve(b); }
          catch (err) { if (err.details) C.showErrors(f, err.details); m.querySelector('[data-err="form"]').textContent = err.message; }
        });
      },
      onClose: () => resolve(null),
    });
  });

  SA.pages.dashAvailability = C.guard({
    title: 'Availability — Super App Partner',
    render(ctx) {
      const pitches = D().myPitches(C.scope());
      if (!pitches.length) return C.layout('availability', { title: 'Availability', body: `<div class="dash-card">${SA.ui.c.empty({ icon: 'calendar', title: 'Add a pitch first', text: 'Once you have a pitch you can manage its hours, block time and add walk-in bookings here.', action: `<a class="btn btn-primary" href="#/dash/pitches/new">Add a pitch</a>` })}</div>` });
      const p = pitches.find((x) => x.id === ctx.query.pitch) || pitches[0];
      const date = ctx.query.date || isoDate(today());
      const slots = D().slots(p.id, date);
      const L = D().ledger({ from: date, to: date, pitchIds: [p.id] });
      const byBooking = Object.fromEntries(L.map((b) => [b.id, b]));
      const booked = slots.filter((s) => ['booked', 'yours'].includes(s.status)).length;
      const free = slots.filter((s) => s.status === 'available').length;
      const blocked = slots.filter((s) => s.status === 'blocked').length;
      const dayNet = L.filter((b) => b.status !== 'cancelled').reduce((t, b) => t + b.money.net, 0);
      const closures = D().closures(p.id).filter((c) => c.to >= isoDate(today()));
      const href = (q) => SA.core.router.href('/dash/availability', { pitch: p.id, date, ...q });

      const cell = (s) => {
        const b = s.booking ? byBooking[s.booking] : null;
        const cl = s.status === 'blocked' ? PS().closureFor(p, date, s.hour) : null;
        const wl = ['booked', 'yours', 'blocked'].includes(s.status) ? SA.services.waitlist.countFor(p.id, date, s.hour) : 0;
        const wlTag = wl ? `<span class="badge badge-info wl-tag" title="Players waiting for this hour">${icon('bell', 11)} ${wl} waiting</span>` : '';
        const inner = {
          available: `<span class="av-price num">${money(s.price)}</span><span class="av-actions"><button class="btn btn-sm btn-outline" data-block="${s.hour}">${icon('lock', 13)} Block</button><button class="btn btn-sm btn-primary" data-walkin="${s.hour}">${icon('plus', 13)} Walk-in</button></span>`,
          booked: b ? `<span class="av-who"><b class="truncate">${esc(b.customer.name)}</b><span class="xs">${esc(b.sourceLabel)} · ${b.payment} · ${money(b.amount)}</span></span>${wlTag}${C.statusBadge(b.status)}` : `<span class="xs">Booked</span>${wlTag}`,
          yours: b ? `<span class="av-who"><b class="truncate">${esc(b.customer.name)}</b><span class="xs">App · ${b.payment} · ${money(b.amount)}</span></span>${C.statusBadge(b.status)}` : '<span class="xs">Booked</span>',
          blocked: cl?.tournamentId ? `<span class="av-who"><b>${icon('trophy', 13)} Tournament</b><span class="xs truncate">${esc((cl.reason || '').replace('Tournament: ', ''))}</span></span>${wlTag}<a class="btn btn-sm btn-outline" href="#/dash/tournaments/${cl.tournamentId}">Open</a>`
            : `<span class="av-who"><b>Blocked</b><span class="xs truncate">${esc(cl?.reason || '')}</span></span>${wlTag}<button class="btn btn-sm btn-outline" data-unblock="${cl?.id || ''}">Unblock</button>`,
          closed: '<span class="xs">Pitch closed</span>',
          past: '<span class="xs">Passed</span>',
        }[s.status];
        return `<div class="av-slot av-${s.status}" style="--i:${s.hour - p.open[0]}" ${b ? `data-booking="${b.id}" role="button" tabindex="0"` : ''}><span class="av-time num">${fmtHour(s.hour)}</span>${inner}</div>`;
      };

      const body = `
        <div class="av-top">
          <div class="chips av-pitches" role="tablist">${pitches.map((x) => `<a class="chip" href="${SA.core.router.href('/dash/availability', { pitch: x.id, date })}" aria-pressed="${x.id === p.id}">${x.status === 'closed' ? '<i class="cat-dot" style="background:var(--danger)"></i>' : ''}${esc(x.name)}</a>`).join('')}</div>
          <div class="date-strip compact">${Array.from({ length: 21 }, (_, i) => { const d = addDays(today(), i - 3); const iso = isoDate(d); return `<a class="date-chip ${iso === date ? 'is-active' : ''}" href="${href({ date: iso })}"><span class="xs">${i === 3 ? 'Today' : DAY[d.getDay()]}</span><b class="num">${d.getDate()}</b></a>`; }).join('')}</div>
        </div>
        ${p.status === 'closed' ? `<div class="notice blaze">${icon('alert', 18)}<span><b>${esc(p.name)} is closed for booking.</b> ${esc(p.closedReason || '')} <a class="link" href="#/dash/pitches">Reopen it from My pitches</a></span></div>` : ''}
        <div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'Booked hours', value: booked, count: booked, ic: 'ticket', tone: 'k-turf' })}
          ${C.kpi({ label: 'Free hours', value: free, count: free, ic: 'clock' })}
          ${C.kpi({ label: 'Blocked hours', value: blocked, count: blocked, ic: 'lock', tone: 'k-blaze' })}
          ${C.kpi({ label: `Net for ${relDay(date).toLowerCase()}`, value: money(Math.round(dayNet)), count: Math.round(dayNet), suffix: ' EGP', ic: 'wallet', tone: 'k-ink' })}
        </div>
        <div class="dash-grid g-2-1">
          <section class="dash-card"><div class="row-between wrap"><h2 class="h4">${fmtDate(date, 'long')}</h2><div class="legend xs"><span><i class="tb-available"></i>Free</span><span><i class="tb-booked"></i>Booked</span><span><i class="tb-blocked"></i>Blocked</span></div></div>
            <div class="av-grid">${slots.map(cell).join('')}</div></section>
          <section class="dash-card">
            <div class="row-between"><h2 class="h4">Blocked dates</h2><button class="btn btn-primary btn-sm" data-block-range>${icon('lock', 14)} Block dates</button></div>
            <ul class="cl-list">${closures.map((c) => `<li><span class="grow"><b class="small">${c.from === c.to ? fmtDate(c.from) : `${fmtDate(c.from)} → ${fmtDate(c.to)}`}</b><span class="xs muted" style="display:block">${c.hours ? c.hours.map(fmtHour).join(', ') : 'Whole day'} · ${esc(c.reason)}</span></span><button class="icon-btn" data-unblock="${c.id}" aria-label="Remove block">${icon('trash', 16)}</button></li>`).join('') || '<li class="small muted">Nothing blocked. Use this for maintenance, private events or holidays.</li>'}</ul>
          </section>
        </div>`;
      return C.layout('availability', { title: 'Availability', subtitle: `${esc(p.name)} · ${esc(p.area)} · open ${fmtHour(p.open[0])}–${fmtHour(p.open[1])}`, actions: `<a class="btn btn-outline btn-sm" href="#/dash/pitches/${p.id}">${icon('edit', 14)} Edit pitch</a>`, body });
    },
    mount(el, ctx) {
      const pitches = D().myPitches(C.scope());
      const p = pitches.find((x) => x.id === ctx.query.pitch) || pitches[0];
      if (!p) return;
      const date = ctx.query.date || isoDate(today());
      el.querySelector('.av-pitches [aria-pressed="true"]')?.scrollIntoView({ inline: 'center', block: 'nearest' });
      el.querySelector('.date-strip .is-active')?.scrollIntoView({ inline: 'center', block: 'nearest' });
      el.addEventListener('click', async (e) => {
        const bl = e.target.closest('[data-block]');
        if (bl) { if (await blockDialog(p, { date, hours: [+bl.dataset.block] })) { SA.ui.overlay.toast('Time blocked on Super App'); SA.app.rerender(); } return; }
        if (e.target.closest('[data-block-range]')) { if (await blockDialog(p, { date })) { SA.ui.overlay.toast('Dates blocked on Super App'); SA.app.rerender(); } return; }
        const ub = e.target.closest('[data-unblock]');
        if (ub && ub.dataset.unblock) {
          const cl = SA.services.db.find('closures', ub.dataset.unblock);
          const wide = cl && (cl.from !== cl.to || (cl.hours?.length || 0) !== 1);
          if (wide && !(await SA.ui.overlay.confirm({ title: 'Remove this block?', text: `This hour is part of a block from ${fmtDate(cl.from)} to ${fmtDate(cl.to)} (${cl.hours ? cl.hours.map(fmtHour).join(', ') : 'whole day'}). The whole block will be removed.`, confirmLabel: 'Remove block' }))) return;
          try { await D().removeClosure(ub.dataset.unblock); } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); return; }
          SA.ui.overlay.toast('Time is bookable again'); SA.app.rerender(); return;
        }
        const wi = e.target.closest('[data-walkin]');
        if (wi) { const b = await walkInDialog(p, date, +wi.dataset.walkin); if (b) { SA.core.motion.confetti({ count: 40, spread: 0.5 }); SA.ui.overlay.toast(`Walk-in ${b.id} added`); SA.app.rerender(); } return; }
        const bk = e.target.closest('[data-booking]');
        if (bk && !e.target.closest('button')) SA.pages.dashBookings.openBooking(bk.dataset.booking);
      });
      el.addEventListener('keydown', (e) => { const bk = e.target.closest('[data-booking]'); if (bk && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); SA.pages.dashBookings.openBooking(bk.dataset.booking); } });
    },
  });
})(window.SA);
