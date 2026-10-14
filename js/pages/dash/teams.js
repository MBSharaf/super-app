/** Partner Dashboard — fixed teams: weekly bookings for N weeks and monthly memberships that renew. */
(function (SA) {
  const { esc, money, fmtHour, fmtRange, fmtDate, isoDate, today, addDays, DAY } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const D = () => SA.services.dash;
  const DAYS_LONG = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

  const kindLabel = (sr) => (sr.source === 'app' ? `Weekly via Super App · ${sr.weeks} weeks` : sr.kind === 'membership' ? 'Monthly membership · renews every 4 weeks' : `${sr.weeks}-week booking`);

  const form = (pitches, d) => `<form id="team-form" class="stack" style="--gap:16px" novalidate>
    <fieldset class="field"><legend class="field-label">Type</legend><div class="seg">
      <label class="seg-opt"><input type="radio" name="kind" value="membership" ${d.kind === 'membership' ? 'checked' : ''} /><span>${icon('repeat', 14)} Monthly membership</span></label>
      <label class="seg-opt"><input type="radio" name="kind" value="fixed" ${d.kind === 'fixed' ? 'checked' : ''} /><span>${icon('calendar', 14)} Fixed number of weeks</span></label></div>
      <span class="field-hint" data-kind-hint></span></fieldset>
    <div class="form-grid">
      <div class="field"><label for="tm-team">Team name</label><input class="input" id="tm-team" name="team" maxlength="40" placeholder="e.g. Thursday Legends" /><span class="field-error" data-err="team"></span></div>
      <div class="field"><label for="tm-cap">Captain</label><input class="input" id="tm-cap" name="captain" maxlength="40" placeholder="Name of the person in charge" /><span class="field-error" data-err="captain"></span></div>
      <div class="field"><label for="tm-phone">Captain’s phone</label><input class="input num" id="tm-phone" name="phone" inputmode="tel" placeholder="+20 1XX XXX XXXX" /><span class="field-error" data-err="phone"></span></div>
      <div class="field"><label for="tm-pitch">Pitch</label><select class="select" id="tm-pitch" name="pitchId">${pitches.map((p) => `<option value="${p.id}" ${p.id === d.pitchId ? 'selected' : ''}>${esc(p.name)}</option>`).join('')}</select></div>
      <div class="field"><label for="tm-start">First game</label><input class="input" id="tm-start" name="startDate" type="date" min="${isoDate(today())}" value="${d.startDate}" /><span class="field-hint" data-weekday></span><span class="field-error" data-err="startDate"></span></div>
      <div class="field"><label for="tm-hour">Kick-off</label><select class="select" id="tm-hour" name="hour"></select><span class="field-error" data-err="hour"></span></div>
      <div class="field"><label for="tm-dur">Length</label><select class="select" id="tm-dur" name="duration"><option value="1">60 min</option><option value="2">2 hours</option></select></div>
      <div class="field" data-weeks-field><label for="tm-weeks">Number of weeks</label><select class="select" id="tm-weeks" name="weeks">${[4, 6, 8, 10, 12, 16, 20, 24].map((w) => `<option ${w === 8 ? 'selected' : ''}>${w}</option>`).join('')}</select><span class="field-error" data-err="weeks"></span></div>
      <div class="field"><label for="tm-disc">Team discount (%)</label><input class="input num" id="tm-disc" name="discount" type="number" min="0" max="50" step="1" value="${d.discount}" /><span class="field-error" data-err="discount"></span></div>
      <div class="field"><label for="tm-pay">Payment</label><select class="select" id="tm-pay" name="payment">${['Cash at venue', 'Card at venue', 'Bank transfer'].map((x) => `<option>${x}</option>`).join('')}</select></div>
    </div>
    <div class="field"><label for="tm-notes">Notes <span class="muted">(optional)</span></label><input class="input" id="tm-notes" name="notes" maxlength="120" placeholder="e.g. Needs 10 bibs, pays on the first game of the month" /></div>
    <section class="tm-preview" data-preview aria-live="polite"></section>
  </form>`;

  const openNew = (pitches, preset = {}) => {
    const d = { kind: 'membership', pitchId: preset.pitchId || pitches[0]?.id, startDate: isoDate(addDays(today(), 1)), discount: 10 };
    SA.ui.overlay.open({
      wide: true, title: 'New team booking', subtitle: 'The same pitch and time every week — the times are taken off the site straight away.',
      body: form(pitches, d),
      footer: `<span class="small soft grow" data-sum></span><button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-primary" form="team-form" type="submit">${icon('check', 16)} Create</button>`,
      onMount(m, close) {
        const f = m.querySelector('#team-form');
        const hourSel = f.hour;
        const fillHours = () => {
          const p = pitches.find((x) => x.id === f.pitchId.value);
          const cur = +hourSel.value || 20;
          const hs = []; for (let h = p.open[0]; h < p.open[1]; h++) hs.push(h);
          hourSel.innerHTML = hs.map((h) => `<option value="${h}" ${h === (hs.includes(cur) ? cur : hs[Math.min(hs.length - 1, Math.floor(hs.length * 0.7))]) ? 'selected' : ''}>${fmtHour(h)}</option>`).join('');
        };
        const preview = () => {
          const kind = f.kind.value;
          m.querySelector('[data-weeks-field]').hidden = kind === 'membership';
          m.querySelector('[data-kind-hint]').textContent = kind === 'membership' ? 'Four games are booked at a time and renew automatically every 4 weeks until you end it.' : 'Books every week for the number of weeks you choose.';
          m.querySelector('[data-weekday]').textContent = f.startDate.value ? `Every ${DAYS_LONG[SA.core.utils.parseISO(f.startDate.value).getDay()]}` : '';
          if (!f.startDate.value) return;
          const weeks = kind === 'membership' ? 4 : +f.weeks.value;
          const disc = Math.min(50, Math.max(0, +f.discount.value || 0)) / 100;
          let rows;
          try { rows = D().previewSeries({ pitchId: f.pitchId.value, startDate: f.startDate.value, hour: +hourSel.value, duration: +f.duration.value, weeks, discount: disc }); } catch { return; }
          const ok = rows.filter((x) => x.ok);
          const total = ok.reduce((t, x) => t + x.price, 0);
          m.querySelector('[data-preview]').innerHTML = `<div class="row-between"><b class="small">${ok.length} of ${rows.length} weeks free</b><span class="small">${money(total)} ${kind === 'membership' ? 'per 4 weeks' : 'in total'}</span></div>
            <ul class="tm-dates">${rows.map((x, i) => `<li class="${x.ok ? '' : 'is-off'}" style="--i:${i}"><b class="num">${fmtDate(x.date)}</b><span class="xs">${x.ok ? money(x.price) : esc(x.reason)}</span></li>`).join('')}</ul>
            ${rows.length - ok.length ? `<p class="xs muted" style="margin-top:8px">${icon('info', 12)} Taken weeks are skipped — the team plays the free ones.</p>` : ''}`;
          m.querySelector('[data-sum]').textContent = ok.length ? `${ok.length} game${ok.length === 1 ? '' : 's'} · ${money(total)}` : 'No free weeks at this time';
        };
        f.pitchId.addEventListener('change', () => { fillHours(); preview(); });
        f.addEventListener('input', preview); f.addEventListener('change', preview);
        fillHours(); preview();
        f.addEventListener('submit', (e) => {
          e.preventDefault();
          const data = SA.core.utils.formData(f);
          try {
            const r = D().createSeries(data);
            close();
            SA.core.motion.confetti({ count: 60, spread: 0.7 });
            SA.ui.overlay.toast(`${r.series.team}: ${r.created} game${r.created === 1 ? '' : 's'} booked${r.skipped ? `, ${r.skipped} taken week${r.skipped === 1 ? '' : 's'} skipped` : ''}`, { type: 'success' });
            SA.app.rerender();
          } catch (err) { if (err.details) C.showErrors(f, err.details); SA.ui.overlay.toast(err.message, { type: 'error' }); }
        });
      },
    });
  };

  SA.pages.dashTeams = C.guard({
    title: 'Teams & memberships — Super App Partner',
    render(ctx) {
      const sc = C.scope();
      const pitches = D().myPitches(sc).filter((p) => p.status === 'active');
      const list = D().series(sc);
      const tab = ctx.query.tab || 'active';
      const active = list.filter((x) => x.status === 'active');
      const shown = tab === 'active' ? active : list.filter((x) => x.status !== 'active');
      const weekEnd = addDays(today(), 7).getTime();
      const thisWeek = active.reduce((t, x) => t + x.sessions.filter((b) => b.status === 'upcoming' && b.startsAt < weekEnd).length, 0);
      const body = `
        <div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'Active teams', value: active.length, count: active.length, sub: `${active.filter((x) => x.kind === 'membership').length} membership${active.filter((x) => x.kind === 'membership').length === 1 ? '' : 's'}`, ic: 'users' })}
          ${C.kpi({ label: 'Games in the next 7 days', value: thisWeek, count: thisWeek, ic: 'calendar', tone: 'k-blaze' })}
          ${C.kpi({ label: 'Games still to play', value: active.reduce((t, x) => t + x.upcoming, 0), ic: 'clock' })}
          ${C.kpi({ label: 'Earned from teams', value: money(Math.round(list.reduce((t, x) => t + x.revenue, 0))), ic: 'wallet', tone: 'k-turf' })}
        </div>
        <div class="notice turf">${icon('repeat', 18)}<span><b>Regular teams fill your pitch every week.</b> Monthly memberships book four games at a time and renew automatically; players can also book the same slot weekly from the site with your weekly discount (set it per pitch in <a class="link" href="#/dash/pitches">My pitches</a>).</span></div>
        <div class="row-between"><div class="tabs">${[['active', `Active (${active.length})`], ['ended', `Ended (${list.length - active.length})`]].map(([v, l]) => `<a class="tab" href="${SA.core.router.href('/dash/teams', { tab: v })}" aria-selected="${tab === v}">${l}</a>`).join('')}</div></div>
        <div class="team-grid">${shown.map((sr, i) => {
          const total = sr.sessions.length || 1;
          return `<article class="team-card ${sr.status !== 'active' ? 'is-ended' : ''}" style="--i:${i}">
            <div class="row-between"><span class="team-ava">${esc(SA.core.utils.initials(sr.team))}</span><span class="badge ${sr.source === 'app' ? 'badge-info' : sr.kind === 'membership' ? 'badge-turf' : 'badge-gold'}">${sr.source === 'app' ? 'Booked on the app' : sr.kind === 'membership' ? 'Membership' : 'Fixed weeks'}</span></div>
            <h3 class="h4" style="margin-top:12px">${esc(sr.team)}</h3>
            <p class="xs muted">${esc(sr.captain || sr.customer?.name || '')}${sr.phone || sr.customer?.phone ? ` · ${esc(sr.phone || sr.customer?.phone)}` : ''}</p>
            <div class="team-when"><b>${DAYS_LONG[sr.weekday]}s · ${fmtRange(sr.hour, sr.duration)}</b><span class="xs">${esc(sr.pitchName)} · ${esc(kindLabel(sr))}${sr.renewals ? ` · renewed ${sr.renewals}×` : ''}</span></div>
            <div class="team-prog" role="img" aria-label="${sr.played} of ${total} games played"><span style="--w:${(sr.played / total) * 100}%"></span></div>
            <div class="row-between xs muted"><span>${sr.played} played · ${sr.upcoming} to go${sr.cancelled ? ` · ${sr.cancelled} cancelled` : ''}</span><b class="num" style="color:var(--ink)">${money(Math.round(sr.revenue))}</b></div>
            ${sr.next ? `<p class="small team-next">${icon('calendar', 14)} Next: <b>${fmtDate(sr.next.date)}</b>${sr.discount ? ` · ${Math.round(sr.discount * 100)}% team price` : ''}</p>` : ''}
            <div class="row wrap" style="--gap:6px;margin-top:12px">
              <button class="btn btn-outline btn-sm" data-sessions="${sr.id}">${icon('list', 14)} Games</button>
              ${sr.status === 'active' ? `<button class="btn btn-ghost btn-sm" data-end="${sr.id}" style="color:var(--danger)">End</button>` : `<span class="badge">${sr.status === 'cancelled' ? 'Ended' : 'Finished'}</span>`}
            </div></article>`;
        }).join('') || `<div class="dash-card" style="grid-column:1/-1">${SA.ui.c.empty({ icon: 'repeat', title: tab === 'active' ? 'No regular teams yet' : 'Nothing here yet', text: 'Add a team that plays at the same time every week — their games are blocked on the site automatically.', action: pitches.length ? `<button class="btn btn-primary" data-new>${icon('plus', 16)} New team booking</button>` : '' })}</div>`}</div>`;
      return C.layout('teams', { title: 'Teams & memberships', subtitle: 'Weekly bookings for regular teams', actions: pitches.length ? `<button class="btn btn-primary btn-sm" data-new>${icon('plus', 15)} New team booking</button>` : '', body });
    },
    mount(el) {
      const sc = C.scope();
      el.addEventListener('click', async (e) => {
        if (e.target.closest('[data-new]')) { const pitches = D().myPitches(sc).filter((p) => p.status === 'active'); openNew(pitches); return; }
        const ses = e.target.closest('[data-sessions]');
        if (ses) {
          const sr = D().series(sc).find((x) => x.id === ses.dataset.sessions);
          SA.ui.overlay.open({ drawer: true, title: esc(sr.team), subtitle: `${DAYS_LONG[sr.weekday]}s · ${fmtRange(sr.hour, sr.duration)} · ${esc(sr.pitchName)}`,
            body: `<ul class="next-list">${sr.sessions.map((b) => `<li><span class="nl-time"><b class="num">${SA.core.utils.parseISO(b.date).getDate()}</b><span class="xs">${SA.core.utils.MON[SA.core.utils.parseISO(b.date).getMonth()]}</span></span><span class="grow"><b class="small">Game ${b.sessionNo || ''}</b><span class="xs muted" style="display:block">${DAY[SA.core.utils.parseISO(b.date).getDay()]} · ${fmtRange(b.hour, b.duration)} · ${money(b.amount)}</span></span>${C.statusBadge(b.status)}</li>`).join('')}</ul>`,
            onMount(m) { m.querySelectorAll('.next-list li').forEach((li, i) => li.addEventListener('click', () => SA.pages.dashBookings.openBooking(sr.sessions[i].id))); } });
          return;
        }
        const end = e.target.closest('[data-end]');
        if (end) {
          const sr = D().series(sc).find((x) => x.id === end.dataset.end);
          const ok = await SA.ui.overlay.confirm({ title: `End ${esc(sr.team)}?`, text: `${sr.upcoming} upcoming game${sr.upcoming === 1 ? '' : 's'} will be cancelled and the times open up on the site.${sr.source === 'app' ? ' The player paid online, so they get a full refund.' : ''} Played games stay in your reports.`, confirmLabel: 'End and cancel games', danger: true });
          if (!ok) return;
          const r = await D().cancelSeries(sr.id);
          SA.ui.overlay.toast(`${sr.team} ended — ${r.cancelled} game${r.cancelled === 1 ? '' : 's'} cancelled`);
          SA.app.rerender();
        }
      });
      if (SA.core.router.current().query.new && D().myPitches(sc).some((p) => p.status === 'active')) openNew(D().myPitches(sc).filter((p) => p.status === 'active'), { pitchId: SA.core.router.current().query.pitch });
    },
  });
})(window.SA);
