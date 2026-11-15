/** Partner Dashboard — run cups and leagues: create, manage teams, draw fixtures, enter results. */
(function (SA) {
  const { esc, money, fmtDate, fmtHour, isoDate, today, addDays, parseISO } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const D = () => SA.services.dash;
  const T = () => SA.services.tournaments;
  const media = SA.data.media;

  const createForm = (pitches) => {
    const start = (() => { const d = addDays(today(), 14); while (d.getDay() !== 5) d.setDate(d.getDate() + 1); return isoDate(d); })();
    return `<form id="tour-form" class="stack" style="--gap:14px" novalidate>
      <div class="form-grid">
        <div class="field span-2"><label for="to-name">Name</label><input class="input" id="to-name" name="name" maxlength="40" placeholder="e.g. Winter Cup 2026" /><span class="field-error" data-err="name"></span></div>
        <div class="field"><label for="to-pitch">Pitch</label><select class="select" id="to-pitch" name="pitchId">${pitches.map((p) => `<option value="${p.id}">${esc(p.name)} · ${esc(p.format)}</option>`).join('')}</select></div>
        <fieldset class="field"><legend class="field-label">Format</legend><div class="seg"><label class="seg-opt"><input type="radio" name="format" value="knockout" checked /><span>${icon('trophy', 14)} Knockout cup</span></label><label class="seg-opt"><input type="radio" name="format" value="league" /><span>${icon('list', 14)} League</span></label></div><span class="field-error" data-err="format"></span></fieldset>
        <div class="field"><label for="to-teams">Teams</label><select class="select" id="to-teams" name="maxTeams"></select><span class="field-error" data-err="maxTeams"></span></div>
        <div class="field"><label for="to-fee">Entry fee per team (EGP)</label><input class="input num" id="to-fee" name="fee" type="number" min="0" step="50" value="1000" /><span class="field-error" data-err="fee"></span></div>
        <div class="field"><label for="to-start">First match day</label><input class="input" id="to-start" name="startDate" type="date" min="${isoDate(addDays(today(), 1))}" value="${start}" /><span class="field-hint">Then the same day every week</span><span class="field-error" data-err="startDate"></span></div>
        <div class="field"><label for="to-hour">First kick-off</label><select class="select" id="to-hour" name="hour"></select><span class="field-error" data-err="hour"></span></div>
        <div class="field"><label for="to-reg">Registration closes</label><input class="input" id="to-reg" name="regDeadline" type="date" min="${isoDate(today())}" value="${isoDate(addDays(parseISO(start), -2))}" /><span class="field-error" data-err="regDeadline"></span></div>
        <div class="field"><label for="to-prize">Prize</label><input class="input" id="to-prize" name="prize" maxlength="90" placeholder="e.g. 5,000 EGP + trophy" /></div>
        <div class="field span-2"><label for="to-rules">Rules</label><textarea class="textarea" id="to-rules" name="rules" rows="3" maxlength="500" placeholder="Match length, subs, what happens in a draw, arrival time…"></textarea><span class="field-error" data-err="rules"></span></div>
      </div>
      <section class="tm-preview" data-plan aria-live="polite"></section>
    </form>`;
  };

  const openCreate = (pitches) => SA.ui.overlay.open({
    wide: true, title: 'New tournament', subtitle: 'Match hours are reserved on your pitch straight away, so nobody can book them.',
    body: createForm(pitches),
    footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-blaze" form="tour-form" type="submit">${icon('trophy', 16)} Create & open registration</button>`,
    onMount(m, close) {
      const f = m.querySelector('#tour-form');
      const sync = () => {
        const p = pitches.find((x) => x.id === f.pitchId.value);
        const fmt = f.format.value;
        const cur = +f.maxTeams.value;
        const opts = fmt === 'knockout' ? [4, 8, 16] : [3, 4, 5, 6, 7, 8, 10];
        f.maxTeams.innerHTML = opts.map((n) => `<option value="${n}" ${n === (opts.includes(cur) ? cur : opts[1]) ? 'selected' : ''}>${n} teams</option>`).join('');
        const h0 = +f.hour.value || 18;
        const hs = []; for (let h = p.open[0]; h < p.open[1]; h++) hs.push(h);
        f.hour.innerHTML = hs.map((h) => `<option value="${h}" ${h === (hs.includes(h0) ? h0 : hs[Math.floor(hs.length / 2)]) ? 'selected' : ''}>${fmtHour(h)}</option>`).join('');
      };
      const prev = () => {
        const d = SA.core.utils.formData(f);
        const pv = T().preview({ ...d, maxTeams: +d.maxTeams, hour: +d.hour });
        if (!pv) return;
        const matches = pv.days.reduce((t, x) => t + x.matches, 0);
        m.querySelector('[data-plan]').innerHTML = `<div class="row-between"><b class="small">${pv.days.length} match day${pv.days.length === 1 ? '' : 's'} · ${matches} matches</b><span class="small">${+d.fee ? `Up to ${money(+d.fee * +d.maxTeams)} in entry fees` : 'Free entry'}</span></div>
          <ul class="tm-dates">${pv.reserved.map((r, i) => `<li style="--i:${i}" class="${pv.conflicts.some((c) => c.startsWith(fmtDate(r.date))) ? 'is-off' : ''}"><b class="num">${fmtDate(r.date)}</b><span class="xs">${fmtHour(r.hours[0])}–${fmtHour(r.hours[r.hours.length - 1] + 1)}</span></li>`).join('')}</ul>
          ${!pv.fits ? `<p class="field-error">The last match ends after closing time — start earlier.</p>` : pv.conflicts.length ? `<p class="field-error">${pv.conflicts.length} hour${pv.conflicts.length === 1 ? ' is' : 's are'} already booked: ${esc(pv.conflicts.slice(0, 3).join(', '))}${pv.conflicts.length > 3 ? '…' : ''}. Pick another day or time.</p>` : `<p class="xs muted">${icon('check', 12)} All hours are free — they’ll be reserved for the tournament.</p>`}`;
      };
      f.addEventListener('change', (e) => { if (['pitchId', 'format'].includes(e.target.name)) sync(); prev(); });
      f.addEventListener('input', prev);
      sync(); prev();
      f.addEventListener('submit', (e) => {
        e.preventDefault();
        const d = SA.core.utils.formData(f);
        try { const t = T().create(d); close(); SA.core.motion.confetti({ count: 80 }); SA.ui.overlay.toast(`${t.name} is open for registration`, { type: 'success', action: 'View on site', onAction: () => window.open(`#/tournaments/${t.id}`, '_blank') }); SA.core.router.go(`${SA.services.dash.adminMode() ? '/admin' : '/dash'}/tournaments/${t.id}`); }
        catch (err) { if (err.details) C.showErrors(f, err.details); SA.ui.overlay.toast(err.message, { type: 'error', duration: 6000 }); }
      });
    },
  });

  SA.pages.dashTournaments = C.guard({
    title: 'Tournaments — Super App Partner',
    render() {
      const sc = C.scope();
      const list = T().mine(sc);
      const active = list.filter((t) => ['open', 'full', 'running'].includes(t.status));
      const fees = list.filter((t) => t.status !== 'cancelled').reduce((a, t) => a + t.teams.filter((x) => x.paid).length * t.fee, 0);
      const body = `
        <div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'Active', value: active.length, count: active.length, ic: 'trophy', tone: 'k-blaze' })}
          ${C.kpi({ label: 'Teams registered', value: active.reduce((a, t) => a + t.teams.length, 0), ic: 'users' })}
          ${C.kpi({ label: 'Entry fees (all time)', value: money(fees), ic: 'wallet', tone: 'k-turf' })}
          ${C.kpi({ label: 'Matches played', value: list.reduce((a, t) => a + t.played, 0), ic: 'ball' })}
        </div>
        <div class="tour-grid dash-tour-grid">${list.map((t, i) => `<a class="card card-link tour-card ${t.status === 'cancelled' ? 'is-cancelled' : ''}" href="#/dash/tournaments/${t.id}" style="--i:${i}">
            <div class="media tour-media"><img src="${media.photo(t.cover, 700, 400)}" alt="" loading="lazy" /><div class="media-top">${t.status === 'cancelled' ? '<span class="badge badge-danger">Cancelled</span>' : SA.ui.tour.statusBadge(t)}<span class="badge badge-glass">${t.format === 'league' ? 'League' : 'Cup'}</span></div>${t.champion ? `<span class="tour-champ">${icon('trophy', 14)} ${esc(t.champion)}</span>` : ''}</div>
            <div class="tour-body"><h3 class="h4">${esc(t.name)}</h3><p class="small soft">${esc(t.pitchName)} · ${fmtDate(t.firstDay)}${t.matchDays > 1 ? ` – ${fmtDate(t.lastDay)}` : ''}</p>
              <div class="tour-spots"><span class="meter"><span style="width:${(t.teams.length / t.maxTeams) * 100}%"></span></span><span class="xs">${t.teams.length}/${t.maxTeams} teams · ${money(t.fee)} entry</span></div>
              ${t.status === 'open' && t.teams.length >= 2 && isoDate(today()) > t.regDeadline ? `<span class="badge badge-gold" style="margin-top:8px">Ready to draw fixtures</span>` : ''}</div></a>`).join('') || `<div class="dash-card" style="grid-column:1/-1">${SA.ui.c.empty({ icon: 'trophy', title: 'Run your first tournament', text: 'Cups and leagues bring teams back every week — and entry fees go straight to your wallet.', action: `<button class="btn btn-blaze" data-new>${icon('plus', 16)} New tournament</button>` })}</div>`}</div>`;
      return C.layout('tournaments', { title: 'Tournaments', subtitle: 'Cups and leagues at your pitches', actions: D().myPitches(sc).length ? `<button class="btn btn-blaze btn-sm" data-new>${icon('plus', 15)} New tournament</button>` : '', body });
    },
    mount(el) {
      el.addEventListener('click', (e) => { if (e.target.closest('[data-new]')) { e.preventDefault(); openCreate(D().myPitches(C.scope()).filter((p) => p.status === 'active')); } });
    },
  });

  SA.pages.dashTournament = C.guard({
    title: 'Tournament — Super App Partner',
    render(ctx) {
      const t = T().manage(ctx.params.id);
      const todayISO = isoDate(today());
      const canDraw = !t.fixtures.length && ['open', 'full'].includes(t.status);
      const editable = t.fixtures.filter((f) => f.date <= todayISO || f.status === 'played').sort((a, b) => a.date.localeCompare(b.date) || a.hour - b.hour);
      const upcoming = t.fixtures.filter((f) => f.date > todayISO && f.status !== 'played');
      const body = `
        <section class="store-hero" style="--cover:url('${media.photo(t.cover, 1600, 600)}')"><div class="sh-bg" data-parallax="0.08"></div>
          <div class="sh-inner"><span class="sh-logo">${icon('trophy', 26)}</span><div class="grow"><div class="row wrap" style="--gap:8px"><h2 class="h2" style="color:#fff">${esc(t.name)}</h2>${t.status === 'cancelled' ? '<span class="badge badge-danger">Cancelled</span>' : SA.ui.tour.statusBadge(t)}</div>
            <p style="color:rgba(255,255,255,.82)">${esc(t.pitchName)} · ${t.format === 'league' ? 'League' : 'Knockout cup'} · ${t.teams.length}/${t.maxTeams} teams · ${money(t.fee)} entry${t.champion ? ` · Champion: ${esc(t.champion)}` : ''}</p></div>
            <div class="row wrap" style="--gap:8px"><a class="btn btn-sm sh-btn" href="#/tournaments/${t.id}" target="_blank" rel="noopener">${icon('eye', 14)} View on site</a></div></div></section>
        ${t.status === 'cancelled' ? '' : `<div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'Teams', value: `${t.teams.length}/${t.maxTeams}`, ic: 'users' })}
          ${C.kpi({ label: 'Entry fees', value: money(t.teams.filter((x) => x.paid).length * t.fee), ic: 'wallet', tone: 'k-turf', sub: `${t.teams.filter((x) => ['Card', 'Wallet'].includes(x.payment)).length} paid online` })}
          ${C.kpi({ label: 'Matches played', value: `${t.played}/${t.fixtures.length || '—'}`, ic: 'ball' })}
          ${C.kpi({ label: t.next ? 'Next match' : 'Registration closes', value: t.next ? fmtDate(t.next.date) : fmtDate(t.regDeadline), ic: 'calendar', tone: 'k-blaze' })}
        </div>`}
        ${canDraw ? `<div class="notice ${t.teams.length >= 3 ? 'turf' : ''}">${icon('trophy', 18)}<span class="grow">${t.format === 'knockout' ? `Knockout cups need 4, 8 or 16 teams to draw (you have ${t.teams.length}).` : `Leagues need at least 3 teams (you have ${t.teams.length}).`} Drawing fixtures closes registration and tells every team when they play; unused reserved hours are released back to bookings.</span><button class="btn btn-primary btn-sm" data-draw ${(t.format === 'knockout' ? [4, 8, 16].includes(t.teams.length) : t.teams.length >= 3) ? '' : 'disabled'}>${icon('sparkle', 14)} Draw fixtures</button></div>` : ''}
        <div class="dash-grid g-1-1">
          <section class="dash-card"><div class="row-between"><h2 class="h4">Teams</h2>${canDraw && t.teams.length < t.maxTeams ? `<button class="btn btn-outline btn-sm" data-add-team>${icon('plus', 14)} Add a team</button>` : ''}</div>
            <ul class="next-list">${t.teams.map((x) => `<li><span class="team-ava sm">${esc(SA.core.utils.initials(x.name))}</span><span class="grow"><b class="small">${esc(x.name)}</b><span class="xs muted" style="display:block">${esc(x.captain || '—')}${x.phone ? ` · ${esc(x.phone)}` : ''} · ${esc(x.payment)}${x.userId ? ' · registered on the app' : ''}</span></span>${canDraw ? `<button class="icon-btn" data-rm-team="${x.id}" aria-label="Remove ${esc(x.name)}" style="color:var(--danger)">${icon('trash', 15)}</button>` : ''}</li>`).join('') || '<li class="small muted">No teams yet. Share the tournament link or add teams yourself.</li>'}</ul></section>
          <section class="dash-card"><h2 class="h4">${t.format === 'league' ? 'Table' : 'Bracket'}</h2><div style="margin-top:12px">${SA.ui.tour.tableHtml(t)}</div></section>
        </div>
        ${t.fixtures.length ? `<section class="dash-card"><div class="row-between"><h2 class="h4">Results</h2><span class="xs muted">Enter scores after each match — tables and the next round update automatically</span></div>
          <ul class="result-list">${editable.map((f) => `<li data-fx="${f.id}">
            <span class="fx-time num">${fmtDate(f.date)}<span class="xs muted" style="display:block">${fmtHour(f.hour)}</span></span>
            <span class="rs-team">${esc(T().teamName(t, f.home))}</span>
            <input class="input num rs-in" name="hs" type="number" min="0" max="40" value="${f.hs ?? ''}" aria-label="${esc(T().teamName(t, f.home))} goals" />
            <span class="muted">–</span>
            <input class="input num rs-in" name="as" type="number" min="0" max="40" value="${f.as ?? ''}" aria-label="${esc(T().teamName(t, f.away))} goals" />
            <span class="rs-team away">${esc(T().teamName(t, f.away))}</span>
            ${t.format === 'knockout' ? `<select class="select rs-pens" name="pens" aria-label="Penalty winner" ${f.hs != null && f.hs === f.as ? '' : 'hidden'}><option value="">Penalties: who won?</option><option value="${f.home}" ${f.pens === f.home ? 'selected' : ''}>${esc(T().teamName(t, f.home))}</option><option value="${f.away}" ${f.pens === f.away ? 'selected' : ''}>${esc(T().teamName(t, f.away))}</option></select>` : ''}
            <button class="btn ${f.status === 'played' ? 'btn-outline' : 'btn-primary'} btn-sm" data-save-result>${f.status === 'played' ? 'Update' : 'Save'}</button></li>`).join('') || '<li class="small muted">Results open on match day.</li>'}</ul>
          ${upcoming.length ? `<p class="xs muted" style="margin-top:10px">${upcoming.length} upcoming match${upcoming.length === 1 ? '' : 'es'} — next on ${fmtDate(upcoming[0].date)}.</p>` : ''}</section>` : ''}
        ${['open', 'full', 'running'].includes(t.status) ? `<div class="row" style="justify-content:flex-end"><button class="btn btn-ghost btn-sm" data-cancel-tour style="color:var(--danger)">Cancel tournament</button></div>` : ''}`;
      return C.layout('tournaments', { title: esc(t.name), subtitle: `${fmtDate(t.firstDay)}${t.matchDays > 1 ? ` – ${fmtDate(t.lastDay)}` : ''} · ${t.matchDays} match day${t.matchDays === 1 ? '' : 's'}`, actions: `<a class="btn btn-outline btn-sm" href="#/dash/tournaments">${icon('chevronLeft', 14)} All tournaments</a>`, body });
    },
    mount(el, ctx) {
      const id = ctx.params.id;
      el.addEventListener('input', (e) => {
        const li = e.target.closest('[data-fx]'); if (!li) return;
        const pens = li.querySelector('[name=pens]');
        if (pens) pens.hidden = !(li.querySelector('[name=hs]').value !== '' && li.querySelector('[name=hs]').value === li.querySelector('[name=as]').value);
      });
      el.addEventListener('click', async (e) => {
        if (e.target.closest('[data-draw]')) {
          if (!(await SA.ui.overlay.confirm({ title: 'Draw fixtures now?', text: 'Registration closes and every team is told when they play. You can’t add or remove teams after this.', confirmLabel: 'Draw fixtures' }))) return;
          try { const fx = T().generate(id); SA.core.motion.confetti({ count: 60 }); SA.ui.overlay.toast(`${fx.length} matches drawn`, { type: 'success' }); SA.app.rerender(); } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
          return;
        }
        const save = e.target.closest('[data-save-result]');
        if (save) {
          const li = save.closest('[data-fx]');
          try {
            const v = T().setResult(id, li.dataset.fx, { hs: li.querySelector('[name=hs]').value, as: li.querySelector('[name=as]').value, pens: li.querySelector('[name=pens]')?.value });
            SA.ui.overlay.toast(v.status === 'finished' ? `Finished — ${v.champion} are the champions!` : 'Result saved', { type: 'success' });
            if (v.status === 'finished') SA.core.motion.confetti({ count: 120 });
            SA.app.rerender();
          } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
          return;
        }
        if (e.target.closest('[data-add-team]')) {
          SA.ui.overlay.open({
            title: 'Add a team', subtitle: 'For teams that register in person. They pay the entry fee at the venue.',
            body: `<form id="at" class="stack" style="--gap:12px" novalidate><div class="field"><label for="at-n">Team name</label><input class="input" id="at-n" name="name" /><span class="field-error" data-err="name"></span></div><div class="field"><label for="at-c">Captain</label><input class="input" id="at-c" name="captain" /></div><div class="field"><label for="at-p">Phone</label><input class="input num" id="at-p" name="phone" inputmode="tel" /></div></form>`,
            footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-primary" form="at" type="submit">Add team</button>`,
            onMount(m, close) { m.querySelector('#at').addEventListener('submit', (ev) => { ev.preventDefault(); try { const tm = T().addTeam(id, SA.core.utils.formData(ev.target)); close(); SA.ui.overlay.toast(`${tm.name} added`); SA.app.rerender(); } catch (err) { if (err.details) C.showErrors(ev.target, err.details); SA.ui.overlay.toast(err.message, { type: 'error' }); } }); },
          });
          return;
        }
        const rm = e.target.closest('[data-rm-team]');
        if (rm) {
          const t = T().manage(id); const tm = t.teams.find((x) => x.id === rm.dataset.rmTeam);
          if (!(await SA.ui.overlay.confirm({ title: `Remove ${esc(tm.name)}?`, text: tm.userId ? `They registered on the app, so they get the ${money(t.fee)} entry fee back.` : 'They paid (or will pay) at the venue — settle any refund with them directly.', confirmLabel: 'Remove team', danger: true }))) return;
          await T().removeTeam(id, tm.id); SA.ui.overlay.toast(`${tm.name} removed`); SA.app.rerender(); return;
        }
        if (e.target.closest('[data-cancel-tour]')) {
          const t = T().manage(id);
          if (!(await SA.ui.overlay.confirm({ title: `Cancel ${esc(t.name)}?`, text: 'Every team is refunded, the reserved hours open up for bookings again, and the tournament disappears from the site.', confirmLabel: 'Cancel tournament', danger: true }))) return;
          await T().cancel(id); SA.ui.overlay.toast('Tournament cancelled — teams refunded'); SA.core.router.go((SA.services.dash.adminMode() ? '/admin' : '/dash') + '/tournaments');
        }
      });
    },
  });
})(window.SA);
