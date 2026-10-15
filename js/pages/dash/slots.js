/**
 * Partner dashboard: post free slots (#/dash/slots). The owner picks a pitch, a day and free hours, optionally a
 * last-minute discount (an offer for that hour), and each hour becomes a live card in "Pitches & free slots".
 * Players with a matching alert are notified. Limited per day (set by the Super App team); posted vs booked stats.
 */
(function (SA) {
  const { esc, money, fmtHour, fmtDate, isoDate, addDays, today, relDay, parseISO } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const D = () => SA.services.dash;
  const db = () => SA.services.db;
  const CS = () => SA.services.community;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });
  const limit = () => db().settings().slotPosting?.perDay ?? 5;
  const myPosts = (ownerId) => db().all('communityPosts').filter((p) => !p.deleted && p.card?.type === 'slot' && p.author?.id === ownerId).sort((a, b) => b.at - a.at);
  const postedToday = (ownerId) => myPosts(ownerId).filter((p) => isoDate(new Date(p.at)) === isoDate(today())).length;

  SA.pages.dashSlots = C.guard({
    roles: ['owner', 'admin'],
    title: 'Free slots — Super App Partner',
    render(ctx) {
      const a = D().me();
      const owner = D().isAdmin() ? (C.scope().ownerId || a.id) : a.id;
      const pitches = D().myPitches(C.scope()).filter((p) => p.status === 'active' && (!p.review || p.review === 'approved'));
      const blocked = db().find('accounts', owner)?.slotBlocked;
      const pitch = pitches.find((p) => p.id === ctx.query.pitch) || pitches[0];
      const days = Array.from({ length: 7 }, (_, i) => isoDate(addDays(today(), i)));
      const date = days.includes(ctx.query.date) ? ctx.query.date : days[0];
      const pre = ctx.query.hour != null ? +ctx.query.hour : null;
      const now = new Date();
      const free = pitch ? SA.services.pitches.slotsSync(pitch.id, date).filter((s) => s.status === 'available' && (date !== isoDate(today()) || s.hour > now.getHours())) : [];
      const posted = myPosts(owner);
      const st = posted.map((p) => SA.ui.linkCards.slotState(p.card).state);
      const booked = st.filter((s) => s === 'booked').length;
      const left = Math.max(0, limit() - postedToday(owner));
      const href = (patch) => SA.core.router.href('/dash/slots', { pitch: pitch?.id, date, ...patch });
      const body = `<div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'Slots posted', value: posted.length, ic: 'megaphone' })}
          ${C.kpi({ label: 'Booked from the community', value: booked, ic: 'check', tone: 'k-turf', sub: posted.length ? `${Math.round((booked / posted.length) * 100)}% of posted slots` : '' })}
          ${C.kpi({ label: 'Still free', value: st.filter((s) => s === 'free').length, ic: 'clock' })}
          ${C.kpi({ label: 'Left to post today', value: `${left} / ${limit()}`, ic: 'calendar', tone: left ? '' : 'k-blaze' })}
        </div>
        ${blocked ? `<div class="notice blaze">${icon('ban', 18)}<span><b>Posting is paused by Super App.</b> ${esc(blocked)} Contact Help & support.</span></div>` : ''}
        ${!pitches.length ? `<div class="dash-card">${SA.ui.c.empty({ icon: 'pitch', title: 'No live pitches yet', text: 'Once a pitch is approved and open, you can post its free hours here.' })}</div>` : `
        <section class="dash-card"><h2 class="h4">Post a free hour</h2><p class="small soft" style="margin-top:4px">It shows as a live card in “Pitches & free slots” with a Book now button, and players with alerts for this pitch or area are notified.</p>
          <div class="form-grid" style="margin-top:14px"><label class="field"><span class="field-label">Pitch</span><select class="select" data-pitch>${pitches.map((p) => `<option value="${p.id}" ${p.id === pitch.id ? 'selected' : ''}>${esc(p.name)}</option>`).join('')}</select></label></div>
          <div class="chips" style="margin-top:12px">${days.map((d) => `<a class="chip" href="${href({ date: d, hour: undefined })}" aria-pressed="${d === date}">${d === days[0] ? 'Today' : d === days[1] ? 'Tomorrow' : fmtDate(d)}</a>`).join('')}</div>
          <form data-slots novalidate style="margin-top:14px">
            ${free.length ? `<div class="slot-pick">${free.map((s) => `<label class="slot-opt"><input type="checkbox" name="hour" value="${s.hour}" ${pre === s.hour ? 'checked' : ''} /><span><b class="num">${fmtHour(s.hour)}</b><span class="xs muted">${money(s.price)}</span></span></label>`).join('')}</div>`
              : `<p class="small muted">${icon('check', 14)} No free hours left on ${relDay(date).toLowerCase()} — nice!</p>`}
            <div class="form-grid" style="margin-top:14px">
              <label class="field"><span class="field-label">Last-minute discount</span><select class="select" name="pct"><option value="0">No discount</option>${[10, 15, 20, 25, 30].map((v) => `<option value="${v}">${v}% off</option>`).join('')}</select></label>
              <label class="field"><span class="field-label">Message (optional)</span><input class="input" name="text" maxlength="200" placeholder="e.g. A team cancelled — grab it!" /></label></div>
            <p class="field-error" data-err="slots"></p>
            <button class="btn btn-primary" type="submit" style="margin-top:8px" ${blocked || !left || !free.length ? 'disabled' : ''}>${icon('megaphone', 15)} Post to the community</button>
          </form></section>`}
        <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Your posted slots</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Pitch</th><th>When</th><th>Discount</th><th>Posted</th><th>Status</th><th></th></tr></thead><tbody>
          ${posted.slice(0, 30).map((p, i) => { const pt = db().find('pitches', p.card.pitchId); const s = st[i]; return `<tr><td class="small"><b>${esc(pt?.name || '')}</b></td><td class="small nowrap">${fmtDate(p.card.date)} · ${fmtHour(p.card.hour)}</td><td class="small">${p.card.pct ? `${Math.round(p.card.pct * 100)}%` : '—'}</td><td class="xs muted">${SA.core.utils.timeAgo(p.at)}</td>
            <td><span class="badge ${s === 'free' ? 'badge-info' : s === 'booked' ? 'badge-turf' : ''}">${s === 'free' ? 'Still free' : s === 'booked' ? 'Booked' : 'Expired'}</span>${p.hidden ? ' <span class="badge badge-danger">Hidden by Super App</span>' : ''}</td><td class="r"><a class="btn btn-ghost btn-sm" href="#/community/pitches/${p.id}" target="_blank" rel="noopener">View</a></td></tr>`; }).join('') || '<tr><td colspan="6" class="small muted" style="padding:18px">Nothing posted yet.</td></tr>'}
        </tbody></table></div></section>`;
      return C.layout('slots', { title: 'Free slots', subtitle: 'Fill empty hours: post them to the players in “Pitches & free slots”', body });
    },
    mount(el, ctx) {
      el.querySelector('[data-pitch]')?.addEventListener('change', (e) => SA.core.router.go(SA.core.router.href('/dash/slots', { pitch: e.target.value, date: ctx.query.date }).slice(1)));
      const f = el.querySelector('[data-slots]');
      f?.addEventListener('submit', (e) => {
        e.preventDefault();
        const a = D().me();
        const pitchId = el.querySelector('[data-pitch]').value;
        const p = db().find('pitches', pitchId);
        const date = el.querySelector('.chip[aria-pressed="true"]') ? new URLSearchParams(el.querySelector('.chip[aria-pressed="true"]').getAttribute('href').split('?')[1]).get('date') : isoDate(today());
        const hours = [...f.querySelectorAll('[name=hour]:checked')].map((i) => +i.value);
        const pct = +f.pct.value;
        const err = (m) => { f.querySelector('[data-err="slots"]').textContent = m; };
        if (!hours.length) return err('Choose at least one free hour.');
        const left = Math.max(0, limit() - postedToday(p.ownerId));
        if (hours.length > left) return err(`You can post ${left} more slot${left === 1 ? '' : 's'} today (the limit is ${limit()} a day).`);
        try {
          hours.forEach((h) => {
            let promoId = null;
            if (pct) promoId = D().createPromo({ pitchId, days: [parseISO(date).getDay()], hourFrom: h, hourTo: h + 1, pct, from: date, to: date, label: 'Last-minute slot' }).id;
            CS().postSlot({ ownerId: p.ownerId, pitchId, date, hour: h, pct: pct / 100, promoId, text: f.text.value });
          });
          D().auth && db().log(a.id, 'slot.posted', `Posted ${hours.length} free slot${hours.length > 1 ? 's' : ''} at ${p.name} on ${date}${pct ? ` with ${pct}% off` : ''}`, pitchId);
          toast(`${hours.length} slot${hours.length > 1 ? 's' : ''} posted to the community`); SA.app.rerender();
        } catch (e2) { err(e2.fields ? Object.values(e2.fields)[0] : e2.message); }
      });
    },
  });
})(window.SA);
