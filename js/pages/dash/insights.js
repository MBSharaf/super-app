/** Partner Dashboard — revenue forecast, quiet hours and off-peak offers that change prices on the site. */
(function (SA) {
  const { esc, money, fmtHour, fmtDate, isoDate, today, addDays, parseISO, MON } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const D = () => SA.services.dash;
  const DN = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
  const daysLabel = (days) => {
    const k = [...days].sort().join(',');
    if (k === '0,1,2,3,4') return 'Sun–Thu';
    if (k === '5,6') return 'Fri–Sat';
    if (k === '0,1,2,3,4,5,6') return 'Every day';
    return days.map((d) => DN[d]).join(', ');
  };

  /** Past 30 days (actual) + next 30 days (confirmed + expected, with a range) */
  const chart = (past, fut) => {
    const max = Math.max(1, ...past.map((r) => r.bookingsNet), ...fut.map((f) => f.high));
    const rows = [...past.map((r) => ({ ...r, past: true })), ...fut];
    const w = 100 / rows.length;
    const totPast = past.reduce((t, r) => t + r.bookingsNet, 0), totFut = fut.reduce((t, f) => t + f.confirmed + f.expected, 0);
    const alt = `Booking earnings: ${money(Math.round(totPast))} in the last 30 days; forecast ${money(Math.round(totFut))} for the next 30 days, of which ${money(fut.reduce((t, f) => t + f.confirmed, 0))} is already booked.`;
    return `<div class="bar-chart fc-chart grow-in" style="--h:240px" role="img" aria-label="${esc(alt)}">
      <div class="bc-grid">${[1, 0.75, 0.5, 0.25].map((f) => `<span style="bottom:${f * 100}%"><i>${money(Math.round(max * f)).replace(' EGP', '')}</i></span>`).join('')}</div>
      <div class="bc-bars">${rows.map((r, i) => {
        const d = parseISO(r.date);
        const lbl = i % 7 === 0 ? `<i class="bc-x">${d.getDate()} ${MON[d.getMonth()]}</i>` : '';
        if (r.past) return `<div class="bc-col" style="--i:${i};width:${w}%" data-tip="${fmtDate(r.date)} — earned ${money(Math.round(r.bookingsNet))} from ${r.bookings} booking${r.bookings === 1 ? '' : 's'}"><span class="bc-b" style="height:${(r.bookingsNet / max) * 100}%"></span>${lbl}</div>`;
        return `<div class="bc-col is-future ${i === past.length ? 'is-today' : ''}" style="--i:${i};width:${w}%" data-tip="${fmtDate(r.date)} — booked ${money(r.confirmed)} + expected ${money(r.expected)} (range ${money(r.low)}–${money(r.high)})">
          <span class="bc-w" style="bottom:${(r.low / max) * 100}%;height:${((r.high - r.low) / max) * 100}%"></span>
          <span class="bc-e" style="height:${(r.expected / max) * 100}%"></span><span class="bc-c" style="height:${(r.confirmed / max) * 100}%"></span>${lbl}</div>`;
      }).join('')}</div>
      <div class="bc-tip" hidden></div>
    </div>
    <div class="legend xs" aria-hidden="true"><span><i style="background:var(--turf)"></i>Earned (last 30 days)</span><span><i style="background:var(--ink)"></i>Already booked</span><span><i class="lg-hatch-ink"></i>Expected — striped</span><span><i class="lg-range"></i>Likely range</span></div>`;
  };

  const offerForm = (pitches, d) => {
    const p = pitches.find((x) => x.id === d.pitchId) || pitches[0];
    const hrs = []; for (let h = p.open[0]; h <= p.open[1]; h++) hrs.push(h);
    return `<form id="offer-form" class="stack" style="--gap:14px" novalidate>
      <div class="form-grid">
        <div class="field"><label for="of-p">Pitch</label><select class="select" id="of-p" name="pitchId">${pitches.map((x) => `<option value="${x.id}" ${x.id === p.id ? 'selected' : ''}>${esc(x.name)}</option>`).join('')}</select></div>
        <div class="field"><label for="of-l">Name shown to players</label><input class="input" id="of-l" name="label" maxlength="30" value="${esc(d.label || 'Off-peak deal')}" /></div>
      </div>
      <fieldset class="field"><legend class="field-label">Days</legend><div class="chips">${DN.map((n, i) => `<label class="chip-check"><input type="checkbox" name="days" value="${i}" ${d.days.includes(i) ? 'checked' : ''} /><span>${n}</span></label>`).join('')}</div><span class="field-error" data-err="days"></span></fieldset>
      <div class="form-grid">
        <div class="field"><label for="of-f">From</label><select class="select" id="of-f" name="hourFrom">${hrs.slice(0, -1).map((h) => `<option value="${h}" ${h === d.hourFrom ? 'selected' : ''}>${fmtHour(h)}</option>`).join('')}</select><span class="field-error" data-err="hourFrom"></span></div>
        <div class="field"><label for="of-t">Until</label><select class="select" id="of-t" name="hourTo">${hrs.slice(1).map((h) => `<option value="${h}" ${h === d.hourTo ? 'selected' : ''}>${fmtHour(h)}</option>`).join('')}</select></div>
        <div class="field"><label for="of-pct">Discount</label><div class="row" style="--gap:10px"><input type="range" min="5" max="50" step="5" value="${d.pct}" name="pctRange" aria-label="Discount slider" style="flex:1" /><input class="input num" id="of-pct" name="pct" type="number" min="5" max="50" step="1" value="${d.pct}" style="width:84px" /></div><span class="field-error" data-err="pct"></span></div>
        <div class="field"><span class="field-label">Players pay</span><b class="h4 num" data-price-prev>—</b></div>
        <div class="field"><label for="of-from">Starts</label><input class="input" id="of-from" type="date" name="from" min="${isoDate(today())}" value="${d.from}" /><span class="field-error" data-err="from"></span></div>
        <div class="field"><label for="of-to">Ends</label><input class="input" id="of-to" type="date" name="to" min="${isoDate(today())}" value="${d.to}" /><span class="field-error" data-err="to"></span></div>
      </div>
      <p class="xs muted">${icon('info', 12)} The lower price shows on your pitch page straight away for these hours. Bookings already made keep their price.</p>
    </form>`;
  };
  const openOffer = (pitches, preset = {}) => {
    const d = { pitchId: preset.pitchId || pitches[0]?.id, days: preset.days || [0, 1, 2, 3, 4], hourFrom: preset.hourFrom ?? 14, hourTo: preset.hourTo ?? 17, pct: Math.round((preset.discount || 0.2) * 100), from: isoDate(today()), to: isoDate(addDays(today(), 27)), label: 'Off-peak deal' };
    const p0 = pitches.find((x) => x.id === d.pitchId);
    if (p0) { d.hourFrom = Math.max(p0.open[0], d.hourFrom); d.hourTo = Math.min(p0.open[1], Math.max(d.hourFrom + 1, d.hourTo)); }
    SA.ui.overlay.open({
      wide: true, title: preset.hourFrom != null ? 'Create this offer' : 'New off-peak offer', subtitle: 'Fill quiet hours with a lower price for a limited time.',
      body: offerForm(pitches, d),
      footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-blaze" form="offer-form" type="submit">${icon('tag', 16)} Start offer</button>`,
      onMount(m, close) {
        const f = m.querySelector('#offer-form');
        const prev = () => {
          const p = pitches.find((x) => x.id === f.pitchId.value);
          const pct = Math.min(50, Math.max(0, +f.pct.value || 0));
          const days = [...f.querySelectorAll('[name=days]:checked')].map((x) => +x.value);
          const date = isoDate(addDays(today(), ((days[0] ?? 0) - today().getDay() + 7) % 7));
          const base = SA.services.pitches.basePrice(date, +f.hourFrom.value, p);
          m.querySelector('[data-price-prev]').innerHTML = `<s class="muted small">${money(base)}</s> ${money(Math.round(base * (1 - pct / 100)))}<span class="xs muted"> / hour</span>`;
        };
        f.pctRange.addEventListener('input', () => { f.pct.value = f.pctRange.value; prev(); });
        f.pct.addEventListener('input', () => { f.pctRange.value = f.pct.value; prev(); });
        f.pitchId.addEventListener('change', () => { const keep = { ...d, pitchId: f.pitchId.value, days: [...f.querySelectorAll('[name=days]:checked')].map((x) => +x.value), pct: +f.pct.value }; const p = pitches.find((x) => x.id === keep.pitchId); keep.hourFrom = Math.max(p.open[0], Math.min(+f.hourFrom.value, p.open[1] - 1)); keep.hourTo = Math.min(p.open[1], Math.max(keep.hourFrom + 1, +f.hourTo.value)); close(); openOffer(pitches, { ...keep, discount: keep.pct / 100 }); });
        f.addEventListener('change', prev);
        prev();
        f.addEventListener('submit', (e) => {
          e.preventDefault();
          const data = { ...SA.core.utils.formData(f), days: [...f.querySelectorAll('[name=days]:checked')].map((x) => +x.value) };
          try { D().createPromo(data); close(); SA.core.motion.confetti({ count: 50, spread: 0.6 }); SA.ui.overlay.toast(`${data.pct}% off is live on the site`, { type: 'success', action: 'View', onAction: () => window.open(`#/pitches/${data.pitchId}`, '_blank') }); SA.app.rerender(); }
          catch (err) { if (err.details) C.showErrors(f, err.details); SA.ui.overlay.toast(err.message, { type: 'error' }); }
        });
      },
    });
  };

  SA.pages.dashInsights = C.guard({
    title: 'Forecast & offers — Super App Partner',
    render(ctx) {
      const sc = C.scope();
      const pitches = D().myPitches(sc);
      if (!pitches.length) return C.layout('insights', { title: 'Forecast & offers', body: `<div class="dash-card">${SA.ui.c.empty({ icon: 'trend', title: 'Add a pitch first', text: 'Forecasts and quiet-hour suggestions start once you have a pitch on Super App.', action: '<a class="btn btn-primary" href="#/dash/pitches/new">Add a pitch</a>' })}</div>` });
      const sel = ctx.query.pitch && pitches.some((p) => p.id === ctx.query.pitch) ? [ctx.query.pitch] : null;
      const ids = sel || pitches.map((p) => p.id);
      const fc = D().forecast({ pitchIds: ids, days: 30 });
      const pastFrom = isoDate(addDays(today(), -30)), pastTo = isoDate(addDays(today(), -1));
      const ps = D().summary({ from: pastFrom, to: pastTo, ...sc, pitchIds: ids });
      const past = D().daily(ps, pastFrom, pastTo);
      const conf = fc.days.reduce((t, d) => t + d.confirmed, 0), exp = fc.days.reduce((t, d) => t + d.expected, 0);
      const low = fc.days.reduce((t, d) => t + d.low, 0), high = fc.days.reduce((t, d) => t + d.high, 0);
      const lastNet = ps.net;
      const change = lastNet ? ((conf + exp) / lastNet - 1) * 100 : 0;
      const capH = fc.days.reduce((t, d) => t + d.capH, 0), bookedH = fc.days.reduce((t, d) => t + d.bookedH, 0);
      // Month projection: earned so far this month + forecast for the rest of it
      const t0 = today(), mFrom = isoDate(new Date(t0.getFullYear(), t0.getMonth(), 1)), mEnd = isoDate(new Date(t0.getFullYear(), t0.getMonth() + 1, 0));
      const soFar = mFrom < isoDate(t0) ? D().summary({ from: mFrom, to: isoDate(addDays(t0, -1)), ...sc, pitchIds: ids }).net : 0;
      const restOfMonth = fc.days.filter((d) => d.date <= mEnd).reduce((t, d) => t + d.confirmed + d.expected, 0);
      const quiet = D().quietHours({ pitchIds: ids });
      const promos = D().promos(sc).filter((x) => !sel || sel.includes(x.pitchId));
      const live = promos.filter((x) => !x.ended);
      const body = `
        <div class="chips pitch-chips" role="group" aria-label="Pitch">${[['', 'All pitches'], ...pitches.map((p) => [p.id, p.name])].map(([v, l]) => `<a class="chip" href="${SA.core.router.href('/dash/insights', { pitch: v || undefined })}" aria-pressed="${(ctx.query.pitch || '') === v}">${esc(l)}</a>`).join('')}</div>
        <div class="kpi-grid">
          ${C.kpi({ label: 'Forecast — next 30 days', value: money(conf + exp), count: conf + exp, suffix: ' EGP', sub: `Likely ${money(low)} – ${money(high)}`, ic: 'trend', tone: 'k-turf' })}
          ${C.kpi({ label: 'Already booked', value: money(conf), count: conf, suffix: ' EGP', sub: `${Math.round(conf / Math.max(1, conf + exp) * 100)}% of the forecast`, ic: 'ticket', tone: 'k-ink' })}
          ${C.kpi({ label: 'Booked hours so far', value: `${capH ? ((bookedH / capH) * 100).toFixed(0) : 0}%`, sub: `${bookedH} of ${capH} open hours in the next 30 days`, ic: 'pitch' })}
          ${C.kpi({ label: 'vs the last 30 days', value: `${change >= 0 ? '+' : ''}${change.toFixed(0)}%`, sub: `Last 30 days: ${money(Math.round(lastNet))}`, ic: 'percent', tone: change >= 0 ? 'k-blaze' : '' })}
        </div>
        <div class="dash-grid g-2-1">
          <section class="dash-card"><div class="row-between"><h2 class="h4">Booking earnings: last 30 days and forecast</h2><span class="xs muted">Net, after commission</span></div>${chart(past, fc.days)}
            <p class="xs muted" style="margin-top:10px">${icon('info', 12)} The forecast adds games already booked to what usually gets booked on the same weekday and hour over the last 8 weeks.${fc.lowConfidence ? ' Some pitches are new, so their forecast uses typical Cairo demand until they build history.' : ''}</p></section>
          <section class="dash-card fc-month">
            <span class="xs muted">${['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'][t0.getMonth()]} is on track for</span>
            <b class="fc-big num" data-countup="${Math.round(soFar + restOfMonth)}" data-suffix=" EGP">${money(Math.round(soFar + restOfMonth))}</b>
            <div class="ms-bar"><span class="ms-b" style="--w:${(soFar / Math.max(1, soFar + restOfMonth)) * 100}%"></span><span class="ms-s fc-rest" style="--w:${(restOfMonth / Math.max(1, soFar + restOfMonth)) * 100}%"></span></div>
            <dl class="kv" style="margin-top:12px"><div><dt>Earned so far</dt><dd class="num">${money(Math.round(soFar))}</dd></div><div><dt>Forecast for the rest of the month</dt><dd class="num">${money(Math.round(restOfMonth))}</dd></div></dl>
            <a class="btn btn-outline btn-sm" href="#/dash/finance" style="margin-top:14px">${icon('percent', 14)} Finance details</a>
          </section>
        </div>
        <section class="dash-card">
          <div class="row-between"><div><h2 class="h4">Quiet hours you could fill</h2><p class="small muted">Times that are usually less than 30% booked. A limited-time discount brings players in.</p></div><button class="btn btn-outline btn-sm" data-new-offer>${icon('plus', 14)} Custom offer</button></div>
          <div class="qh-grid">${quiet.slice(0, 6).map((x, i) => `<article class="qh-card" style="--i:${i}">
              <div class="row-between"><span class="badge badge-ink">${esc(x.pitchName)}</span><span class="qh-pct">−${Math.round(x.discount * 100)}%</span></div>
              <b class="h4" style="margin-top:10px;display:block">${x.label} · ${fmtHour(x.hourFrom)}–${fmtHour(x.hourTo)}</b>
              <div class="qh-meter" role="img" aria-label="Usually ${Math.round(x.rate * 100)}% booked"><span style="--w:${Math.round(x.rate * 100)}%"></span></div>
              <span class="xs muted">Usually ${Math.round(x.rate * 100)}% booked · ${money(x.price)}/hour now</span>
              <p class="small qh-gain">${icon('trend', 14)} About <b>+${money(x.uplift)}</b> a month</p>
              <p class="xs muted">If about ${x.extraHours} more hour${x.extraHours === 1 ? '' : 's'} a week get booked at the lower price.${x.lowConfidence ? ' New pitch — estimate based on typical demand.' : ''}</p>
              <button class="btn btn-blaze btn-sm btn-block" data-suggest="${i}" style="margin-top:12px">${icon('tag', 14)} Create offer</button>
            </article>`).join('') || `<p class="small muted">${icon('check', 14)} No quiet hours right now — your pitches are well booked${live.length ? ' (or already covered by an offer)' : ''}.</p>`}</div>
        </section>
        <section class="dash-card table-card">
          <div class="row-between" style="padding:18px 18px 0"><h2 class="h4">Your offers</h2><span class="xs muted">${live.length} live</span></div>
          ${promos.length ? `<div class="table-wrap"><table class="table dash-table"><thead><tr><th>Offer</th><th>Pitch</th><th>When</th><th>Dates</th><th class="r">Bookings</th><th class="r">Revenue</th><th></th></tr></thead><tbody>
            ${promos.map((x) => `<tr class="${x.ended ? 'is-muted' : ''}"><td><b>−${Math.round(x.pct * 100)}%</b> <span class="small">${esc(x.label)}</span></td><td>${esc(x.pitchName)}</td><td class="small">${daysLabel(x.days)} · ${fmtHour(x.hourFrom)}–${fmtHour(x.hourTo)}</td><td class="small num">${fmtDate(x.from)} – ${fmtDate(x.to)}</td><td class="r num">${x.bookings}</td><td class="r num">${money(x.revenue)}</td>
              <td class="r">${x.ended ? '<span class="badge">Ended</span>' : `<button class="btn btn-ghost btn-sm" data-end-offer="${x.id}" style="color:var(--danger)">End</button>`}</td></tr>`).join('')}
          </tbody></table></div>` : `<div style="padding:0 18px 18px">${SA.ui.c.empty({ icon: 'tag', title: 'No offers yet', text: 'Pick a quiet-hour suggestion above to start one.' })}</div>`}
        </section>`;
      this._quiet = quiet;
      return C.layout('insights', { title: 'Forecast & offers', subtitle: 'What the next 30 days look like, and how to fill the quiet hours', body });
    },
    mount(el) {
      C.bindChart(el);
      const pitches = () => D().myPitches(C.scope()).filter((p) => p.status === 'active');
      el.addEventListener('click', async (e) => {
        const s = e.target.closest('[data-suggest]');
        if (s) { const x = this._quiet[+s.dataset.suggest]; openOffer(pitches(), { pitchId: x.pitchId, days: x.days, hourFrom: x.hourFrom, hourTo: x.hourTo, discount: x.discount }); return; }
        if (e.target.closest('[data-new-offer]')) { if (!pitches().length) return SA.ui.overlay.toast('Open a pitch for booking first.', { type: 'error' }); openOffer(pitches()); return; }
        const end = e.target.closest('[data-end-offer]');
        if (end) {
          if (!(await SA.ui.overlay.confirm({ title: 'End this offer?', text: 'Prices go back to normal on the site straight away. Bookings already made keep their price.', confirmLabel: 'End offer', danger: true }))) return;
          D().endPromo(end.dataset.endOffer); SA.ui.overlay.toast('Offer ended'); SA.app.rerender();
        }
      });
    },
  });
})(window.SA);
