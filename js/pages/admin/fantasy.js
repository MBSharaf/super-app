/** Admin console: fantasy — free leagues (prizes, sponsor, cap), the Super App League's weekly points, sponsors and winners. */
(function (SA) {
  const { esc, money, fmtDate, timeAgo, isoDate, addDays, today } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const A = SA.pages.adminCommon;
  const AD = () => SA.services.admin;
  const F = () => SA.services.fantasy;
  const db = () => SA.services.db;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });
  const TIER = { gold: ['badge-gold', 'Gold'], silver: ['', 'Silver'], partner: ['badge-info', 'Partner'] };

  const leagueDialog = (lg, l) => {
    const sponsors = db().all('sponsors') || [];
    SA.ui.overlay.open({
      wide: true, title: `Edit ${esc(l.name)}`, subtitle: `${F().LEAGUES[lg].label} · ${esc(l.scope)}`,
      body: `<form class="form-grid" data-league novalidate>
        <div class="field span-2"><label for="lg-b">Description</label><input class="input" id="lg-b" name="blurb" maxlength="140" value="${esc(l.blurb)}" /></div>
        <div class="field"><label for="lg-s">Sponsor</label><select class="select" id="lg-s" name="sponsorId"><option value="">No sponsor</option>${sponsors.map((s) => `<option value="${s.id}" ${l.sponsorId === s.id ? 'selected' : ''}>${esc(s.name)}</option>`).join('')}</select></div>
        <div class="field"><label for="lg-c">Max managers (0 = no cap)</label><input class="input num" id="lg-c" name="cap" type="number" min="0" value="${l.cap || 0}" /><p class="field-error" data-err="cap"></p></div>
        <div class="field"><label for="lg-v">Prizes worth (EGP)</label><input class="input num" id="lg-v" name="prizeValue" type="number" min="0" value="${l.prizeValue || 0}" /></div>
        <label class="choice" style="align-self:end"><input type="checkbox" name="hidden" ${l.hidden ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span><span class="grow"><b>Hide from the site</b></span></label>
        <div class="field span-2"><label for="lg-p">Prizes — one per line: <span class="mono">place | prize</span></label><textarea class="input mono" id="lg-p" name="prizes" rows="6">${esc(l.prizes.map(([a, b]) => `${a} | ${b}`).join('\n'))}</textarea>
          <span class="field-hint">Places: 1st, 2nd, 4th – 10th, Top 100. Vouchers like “500 EGP shop voucher” or “25% off one booking” become single-use codes when you announce winners. No cash prizes.</span><p class="field-error" data-err="prizes"></p></div>
        <div class="span-2 row" style="--gap:8px;justify-content:flex-end"><button type="button" class="btn btn-ghost" data-close>Cancel</button><button class="btn btn-primary" type="submit">Save league</button></div></form>`,
      onMount(m, close) { m.querySelector('[data-league]').addEventListener('submit', (e) => { e.preventDefault(); try { AD().saveLeague(l.id, SA.core.utils.formData(e.target)); close(); toast('League saved'); SA.app.rerender(); } catch (err) { C.showErrors(e.target, err.details || {}); } }); },
    });
  };
  const sponsorDialog = (s = null) => SA.ui.overlay.open({
    title: s ? `Edit ${esc(s.name)}` : 'New sponsor', subtitle: 'Sponsors provide the prizes — Super App never pays cash prizes.',
    body: `<form class="form-grid" data-sponsor novalidate>
      <div class="field span-2"><label for="sp-n">Name</label><input class="input" id="sp-n" name="name" value="${esc(s?.name || '')}" /><p class="field-error" data-err="name"></p></div>
      <div class="field"><label for="sp-t">Tier</label><select class="select" id="sp-t" name="tier">${Object.entries(TIER).map(([k, [, l]]) => `<option value="${k}" ${s?.tier === k ? 'selected' : ''}>${l}</option>`).join('')}</select></div>
      <div class="field"><label for="sp-v">Deal value (EGP)</label><input class="input num" id="sp-v" name="value" type="number" min="0" value="${s?.value || 0}" /><p class="field-error" data-err="value"></p></div>
      <div class="field"><label for="sp-f">From</label><input class="input" id="sp-f" name="from" type="date" value="${esc(s?.from || isoDate(today()))}" /></div>
      <div class="field"><label for="sp-e">Until</label><input class="input" id="sp-e" name="to" type="date" value="${esc(s?.to || isoDate(addDays(today(), 90)))}" /><p class="field-error" data-err="to"></p></div>
      <div class="field"><label for="sp-c">Contact</label><input class="input" id="sp-c" name="contact" value="${esc(s?.contact || '')}" placeholder="email or phone" /></div>
      <div class="field"><label for="sp-k">Colour</label><input class="input" id="sp-k" name="color" type="color" value="${esc(s?.color || '#0B6E4F')}" /></div>
      <div class="field span-2"><label for="sp-o">Note</label><input class="input" id="sp-o" name="note" maxlength="140" value="${esc(s?.note || '')}" /></div>
      <div class="span-2 row" style="--gap:8px;justify-content:flex-end"><button type="button" class="btn btn-ghost" data-close>Cancel</button><button class="btn btn-primary" type="submit">Save sponsor</button></div></form>`,
    onMount(m, close) { m.querySelector('[data-sponsor]').addEventListener('submit', (e) => { e.preventDefault(); try { AD().saveSponsor(SA.core.utils.formData(e.target), s?.id); close(); toast('Sponsor saved'); SA.app.rerender(); } catch (err) { C.showErrors(e.target, err.details || {}); } }); },
  });

  /* ---------- Competitions on the matches page (API mode: /admin/football/competitions) ---------- */
  const REGIONS = ['Egypt', 'Africa', 'Europe', 'National teams'];
  let comps = null;
  let compsLoading = false;
  const loadComps = () => {
    if (compsLoading) return;
    compsLoading = true;
    SA.services.api.get('/admin/football/competitions').then((list) => { comps = list; }, (err) => toast(err.message, 'error'))
      .finally(() => { compsLoading = false; SA.app.rerender(); });
  };
  const compSource = (c) => (c.provider === 'fpl' ? 'Fantasy Premier League' : c.provider === 'thesportsdb' ? `TheSportsDB ${c.externalLeagueId}` : esc(c.provider));
  const compFields = (c) => `
      <div class="field"><label for="cp-e">Name (English)</label><input class="input" id="cp-e" name="nameEn" value="${esc(c?.nameEn || '')}" /><p class="field-error" data-err="nameEn"></p></div>
      <div class="field"><label for="cp-a">Name (Arabic)</label><input class="input" id="cp-a" name="nameAr" dir="rtl" value="${esc(c?.nameAr || '')}" /><p class="field-error" data-err="nameAr"></p></div>
      <div class="field"><label for="cp-s">Short name</label><input class="input" id="cp-s" name="shortName" maxlength="30" value="${esc(c?.shortName || '')}" /><p class="field-error" data-err="shortName"></p></div>
      <div class="field"><label for="cp-r">Region</label><select class="select" id="cp-r" name="region">${REGIONS.map((r) => `<option ${c?.region === r ? 'selected' : ''}>${r}</option>`).join('')}</select></div>
      <div class="field"><label for="cp-c">Colour</label><input class="input" id="cp-c" name="color" type="color" value="${esc(c?.color || '#0B6E4F')}" /></div>`;
  const compDialog = (c = null) => SA.ui.overlay.open({
    title: c ? `Edit ${esc(c.nameEn)}` : 'Add a competition',
    subtitle: c ? compSource(c) : 'Fixtures and results from TheSportsDB (free). The league number is in its page address on thesportsdb.com.',
    body: `<form class="form-grid" data-comp novalidate>
      ${c ? '' : `<div class="field"><label for="cp-k">Key</label><input class="input mono" id="cp-k" name="key" maxlength="10" placeholder="saudi" /><p class="field-error" data-err="key"></p></div>
      <div class="field"><label for="cp-i">TheSportsDB league number</label><input class="input num" id="cp-i" name="theSportsDbId" type="number" min="1" placeholder="4668" /><p class="field-error" data-err="theSportsDbId"></p></div>`}
      ${compFields(c)}
      ${c ? `<div class="field"><label for="cp-o">Order on the matches page</label><input class="input num" id="cp-o" name="sortOrder" type="number" min="0" value="${c.sortOrder}" /></div>
      <label class="choice span-2"><input type="checkbox" name="isActive" ${c.isActive ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span><span class="grow"><b>Show on the site</b></span></label>` : ''}
      <div class="span-2 row" style="--gap:8px;justify-content:flex-end"><button type="button" class="btn btn-ghost" data-close>Cancel</button><button class="btn btn-primary" type="submit">${c ? 'Save' : 'Add competition'}</button></div></form>`,
    onMount(m, close) {
      m.querySelector('[data-comp]').addEventListener('submit', async (e) => {
        e.preventDefault();
        const d = SA.core.utils.formData(e.target);
        const body = { nameEn: d.nameEn, nameAr: d.nameAr, shortName: d.shortName, region: d.region, color: d.color };
        try {
          if (c) await SA.services.api.put(`/admin/football/competitions/${c.key}`, { ...body, isActive: !!d.isActive, sortOrder: +d.sortOrder || 0 });
          else await SA.services.api.post('/admin/football/competitions', { ...body, key: d.key, theSportsDbId: +d.theSportsDbId || 0 });
          close(); toast(c ? 'Competition saved' : 'Added — its matches arrive with the next sync'); comps = null; loadComps();
        } catch (err) { toast(err.message, 'error'); }
      });
    },
  });
  const compRow = (c) => `<tr class="${c.isActive ? '' : 'is-dim'}"><td class="small"><i class="adm-dot" style="background:${esc(c.color)}"></i><b>${esc(c.nameEn)}</b><span class="xs muted" style="display:block">${esc(c.region)} · ${esc(c.shortName)}</span></td>
        <td class="small">${compSource(c)}</td>
        <td>${c.fantasyEnabled ? '<span class="badge badge-turf">On</span>' : '<span class="badge">Off</span>'}</td>
        <td class="r num small">${c.fixtures}</td><td class="small">${c.lastSyncedAt ? timeAgo(new Date(c.lastSyncedAt.endsWith('Z') ? c.lastSyncedAt : `${c.lastSyncedAt}Z`).getTime()) : '<span class="muted">Waiting</span>'}</td>
        <td>${c.isActive ? '<span class="badge badge-turf">Shown</span>' : '<span class="badge badge-danger">Hidden</span>'}</td>
        <td class="r"><div class="row" style="--gap:4px;justify-content:flex-end"><button class="btn btn-ghost btn-sm" data-edit-comp="${esc(c.key)}">Edit</button><button class="btn btn-outline btn-sm" data-sync-comp="${esc(c.key)}">Sync now</button></div></td></tr>`;
  const compsSection = () => {
    if (!SA.services.api.isLive()) {
      return `<section class="dash-card"><p class="small muted">${icon('info', 14)} Competitions are managed on the server (API mode). Demo mode shows the sample schedule.</p></section>`;
    }
    if (!comps) { loadComps(); return `<section class="dash-card"><p class="small muted">Loading competitions…</p></section>`; }
    return `<section class="dash-card table-card"><div class="row" style="padding:18px 18px 0;justify-content:space-between"><h2 class="h4">Competitions on the matches page</h2><button class="btn btn-primary btn-sm" data-new-comp>${icon('plus', 14)} Add competition</button></div>
      <div class="table-wrap"><table class="table dash-table"><thead><tr><th>Competition</th><th>Source</th><th>Fantasy</th><th class="r">Matches</th><th>Last sync</th><th>Status</th><th></th></tr></thead><tbody>
      ${comps.map(compRow).join('')}
      </tbody></table></div><p class="xs muted" style="padding:0 18px 16px">${icon('shield', 12)} Free sources: the English league from the Fantasy Premier League site, the others from TheSportsDB (results, no live minutes). Egyptian fantasy is paused.</p></section>`;
  };

  SA.pages.adminFantasy = A.guard({
    title: 'Fantasy — Super App Admin',
    render(ctx) {
      const tab = ctx.query.tab || '';
      const LG = F().LEAGUES;
      const sponsors = db().all('sponsors') || [];
      const winners = (db().all('fantasyWinners') || []).slice().sort((a, b) => b.at - a.at);
      const from = isoDate(addDays(today(), -29)), to = isoDate(today());
      const weekly = (db().all('fantasyWeekly') || []).slice().sort((a, b) => b.at - a.at);
      const weeklyGiven = weekly.filter((w) => w.at >= addDays(today(), -29).getTime()).reduce((t, w) => t + w.winners.reduce((x, y) => x + y.points, 0), 0);
      let section = '';
      if (!tab) {
        section = Object.keys(LG).map((lg) => `<section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">${esc(LG[lg].label)}</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>League</th><th>Type</th><th>Sponsor</th><th>1st prize</th><th class="r">Managers</th><th>Status</th><th></th></tr></thead><tbody>
          ${F().allOfficial(lg).map((l) => `<tr class="${l.hidden ? 'is-dim' : ''}"><td class="small"><b>${esc(l.name)}</b><span class="xs muted" style="display:block">${esc(l.scope)}</span></td><td>${l.kind === 'official' ? '<span class="badge badge-ink">Super App · Free</span>' : '<span class="badge">Free</span>'}</td>
            <td class="small">${l.sponsor ? `<i class="adm-dot" style="background:${esc(l.sponsor.color)}"></i>${esc(l.sponsor.name)}` : '<span class="muted">—</span>'}</td><td class="small">${esc(l.prizes[0]?.[1] || '')}</td><td class="r num">${l.participants.toLocaleString()}${l.cap ? `<span class="xs muted"> / ${l.cap.toLocaleString()}</span>` : ''}</td>
            <td>${l.hidden ? '<span class="badge badge-danger">Hidden</span>' : '<span class="badge badge-turf">Live</span>'}</td>
            <td class="r"><div class="row" style="--gap:4px;justify-content:flex-end"><button class="btn btn-ghost btn-sm" data-edit-league="${l.id}" data-lg="${lg}">Edit</button><button class="btn btn-outline btn-sm" data-winners="${l.id}" data-lg="${lg}">${icon('trophy', 13)} Announce winners</button></div></td></tr>`).join('')}
          </tbody></table></div></section>`).join('');
      } else if (tab === 'sponsors') {
        section = `<section class="dash-card table-card"><div class="row-between" style="padding:18px 18px 0"><h2 class="h4">Sponsors</h2><button class="btn btn-primary btn-sm" data-new-sponsor>${icon('plus', 14, 2.6)} New sponsor</button></div>
          <div class="table-wrap"><table class="table dash-table"><thead><tr><th>Sponsor</th><th>Tier</th><th>Leagues</th><th>Deal</th><th class="r">Value</th><th></th></tr></thead><tbody>
          ${sponsors.map((s) => { const ls = Object.keys(LG).flatMap((lg) => F().allOfficial(lg).filter((l) => l.sponsorId === s.id).map((l) => l.name)); return `<tr><td class="small"><i class="adm-dot" style="background:${esc(s.color)}"></i><b>${esc(s.name)}</b><span class="xs muted" style="display:block">${esc(s.contact)}${s.note ? ` · ${esc(s.note)}` : ''}</span></td><td><span class="badge ${TIER[s.tier]?.[0] || ''}">${TIER[s.tier]?.[1] || s.tier}</span></td>
            <td class="xs">${ls.map(esc).join(', ') || '—'}</td><td class="xs">${s.from ? fmtDate(s.from) : '…'} – ${s.to ? fmtDate(s.to) : '…'}${s.to && s.to < isoDate(today()) ? ' <span class="badge badge-danger">Ended</span>' : ''}</td><td class="r num">${money(s.value || 0)}</td>
            <td class="r"><div class="row" style="--gap:4px;justify-content:flex-end"><button class="btn btn-ghost btn-sm" data-edit-sponsor="${s.id}">Edit</button><button class="icon-btn" data-del-sponsor="${s.id}" aria-label="Remove">${icon('trash', 15)}</button></div></td></tr>`; }).join('') || '<tr><td colspan="6" class="small muted" style="padding:18px">No sponsors yet.</td></tr>'}
          </tbody></table></div></section>`;
      } else if (tab === 'winners') {
        section = winners.map((w) => `<section class="dash-card table-card"><div class="row-between wrap" style="padding:18px 18px 0;--gap:8px"><div><h2 class="h4">${esc(w.league)} · GW${w.gw}</h2><p class="xs muted" style="margin-top:2px">${fmtDate(new Date(w.at))} · by ${esc(w.by)}${w.sponsor ? ` · prizes from ${esc(w.sponsor)}` : ''}</p></div></div>
          <div class="table-wrap"><table class="table dash-table"><thead><tr><th class="r">Rank</th><th>Manager</th><th>Prize</th><th>Voucher code</th><th>Delivery</th></tr></thead><tbody>
          ${w.rows.map((r, i) => `<tr><td class="r num strong">${r.rank}</td><td class="small"><b>${esc(r.name)}</b>${r.isMe ? ' <span class="badge badge-info">This device</span>' : ''}<span class="xs muted" style="display:block">${esc(r.team)} · ${r.total} pts</span></td><td class="small">${esc(r.prize)}</td><td class="mono small">${esc(r.code || '—')}</td>
            <td>${r.delivered ? `<span class="badge badge-turf">${r.code ? 'Code sent' : 'Delivered'}</span>` : `<button class="btn btn-outline btn-sm" data-delivered="${w.id}" data-i="${i}">Mark delivered</button>`}</td></tr>`).join('')}
          </tbody></table></div></section>`).join('') || `<div class="dash-card">${SA.ui.c.empty({ icon: 'trophy', title: 'No winners announced yet', text: 'Announce winners from the Leagues tab when a round or a competition ends.' })}</div>`;
      } else {
        // Super App League: the top 3 of every gameweek win Super Points (owner's rule, 2026-11-03)
        const w = F().weeklyPoints();
        section = `<div class="dash-grid g-1-1"><section class="dash-card"><h2 class="h4">Super App League — weekly points</h2>
            <p class="small soft" style="margin-top:6px">Every league is free. In the Super App League, the 3 highest gameweek scores win Super Points after each gameweek. The points go to their accounts automatically.</p>
            <form class="form-grid" data-weekly style="margin-top:14px">${['1st', '2nd', '3rd'].map((p, i) => `<div class="field"><label for="fw-${i}">${p} place (points)</label><input class="input num" id="fw-${i}" name="p${i}" type="number" min="0" step="50" value="${w[i]}" /></div>`).join('')}
              <div class="span-2"><button class="btn btn-primary" type="submit">Save weekly points</button></div></form></section>
          <section class="dash-card"><h2 class="h4">Last 30 days</h2><dl class="kv" style="margin-top:12px"><div><dt>Gameweeks paid out</dt><dd class="num">${weekly.filter((x) => x.at >= addDays(today(), -29).getTime()).length}</dd></div><div><dt>Super Points given</dt><dd class="num">${weeklyGiven.toLocaleString()}</dd></div></dl>
            <p class="xs muted" style="margin-top:10px">${icon('shield', 12)} No entry fees and no cash prizes.</p></section></div>
          <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Weekly winners</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Gameweek</th><th>League</th><th>1st</th><th>2nd</th><th>3rd</th></tr></thead><tbody>
            ${weekly.slice(0, 30).map((x) => `<tr><td class="small">GW${x.gw}<span class="xs muted" style="display:block">${fmtDate(new Date(x.at))}</span></td><td class="small">${esc(LG[x.league]?.label || x.league)}</td>${[0, 1, 2].map((i) => { const r = x.winners[i]; return `<td class="small">${r ? `<b>${esc(r.name)}</b>${r.isMe ? ' <span class="badge badge-info">This device</span>' : ''}<span class="xs muted" style="display:block">${r.gw} pts · +${r.points.toLocaleString()} Super Points</span>` : '—'}</td>`; }).join('')}</tr>`).join('') || '<tr><td colspan="5" class="small muted" style="padding:18px">No gameweek finished yet.</td></tr>'}</tbody></table></div></section>`;
      }
      const active = sponsors.filter((s) => !s.to || s.to >= isoDate(today()));
      const body = `<div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'Leagues', value: Object.keys(LG).reduce((t, lg) => t + F().allOfficial(lg).filter((l) => !l.hidden).length, 0), ic: 'trophy' })}
          ${C.kpi({ label: 'Active sponsors', value: active.length, ic: 'sparkle', sub: money(active.reduce((t, s) => t + (s.value || 0), 0)) })}
          ${C.kpi({ label: 'Weekly Super Points (30 days)', value: weeklyGiven.toLocaleString(), ic: 'sparkle', tone: 'k-turf' })}
          ${C.kpi({ label: 'Prizes to deliver', value: winners.reduce((t, w) => t + w.rows.filter((r) => !r.delivered).length, 0), ic: 'gift', tone: winners.some((w) => w.rows.some((r) => !r.delivered)) ? 'k-blaze' : '' })}
        </div>
        ${A.tabs('/admin/fantasy', [['', 'Leagues'], ['sponsors', 'Sponsors', sponsors.length], ['winners', 'Winners', winners.length], ['weekly', 'Weekly points', weekly.length], ['competitions', 'Competitions']], tab)}${tab === 'competitions' ? compsSection() : section}`;
      return C.layout('fantasy', { title: 'Fantasy', subtitle: 'Free leagues, the Super App League’s weekly points, sponsors, winners and the competitions on the matches page', body });
    },
    mount(el) {
      el.querySelector('[data-weekly]')?.addEventListener('submit', (e) => {
        e.preventDefault();
        const v = [0, 1, 2].map((i) => Math.max(0, Math.round(+e.target[`p${i}`].value) || 0));
        db().setSettings({ fantasyWeekly: v });
        db().log(SA.services.dash?.me?.()?.id || 'admin', 'settings.fantasy.weekly', `Super App League weekly points: ${v.join(' / ')}`);
        toast('Weekly points saved'); SA.app.rerender();
      });
      el.addEventListener('click', async (e) => {
        const t = (s) => e.target.closest(s);
        if (t('[data-edit-league]')) { const b = t('[data-edit-league]'); return leagueDialog(b.dataset.lg, F().allOfficial(b.dataset.lg).find((l) => l.id === b.dataset.editLeague)); }
        if (t('[data-winners]')) {
          const b = t('[data-winners]'); const l = F().allOfficial(b.dataset.lg).find((x) => x.id === b.dataset.winners);
          if (!(await SA.ui.overlay.confirm({ title: `Announce winners of ${esc(l.name)}?`, text: 'The top of the table gets the prizes. Vouchers become single-use codes; other prizes are listed for delivery. Winners on this device are notified.', confirmLabel: 'Announce winners' }))) return;
          try { AD().announceWinners(l.id, b.dataset.lg); toast('Winners announced'); SA.core.router.go('/admin/fantasy?tab=winners'); } catch (err) { toast(err.message, 'error'); }
          return;
        }
        if (t('[data-new-comp]')) return compDialog();
        if (t('[data-edit-comp]')) return compDialog(comps.find((c) => c.key === t('[data-edit-comp]').dataset.editComp));
        if (t('[data-sync-comp]')) {
          const b = t('[data-sync-comp]'); b.disabled = true; toast('Syncing — this can take a minute');
          try { await SA.services.api.post(`/admin/fantasy/sync?competition=${b.dataset.syncComp}`); toast('Synced'); comps = null; loadComps(); } catch (err) { toast(err.message, 'error'); b.disabled = false; }
          return;
        }
        if (t('[data-new-sponsor]')) return sponsorDialog();
        if (t('[data-edit-sponsor]')) return sponsorDialog(db().find('sponsors', t('[data-edit-sponsor]').dataset.editSponsor));
        if (t('[data-del-sponsor]')) { const s = db().find('sponsors', t('[data-del-sponsor]').dataset.delSponsor); if (!(await SA.ui.overlay.confirm({ title: `Remove ${esc(s.name)}?`, text: 'Their leagues show no sponsor until you pick another one.', confirmLabel: 'Remove', danger: true }))) return; AD().deleteSponsor(s.id); toast('Sponsor removed', 'info'); SA.app.rerender(); return; }
        if (t('[data-delivered]')) { const b = t('[data-delivered]'); AD().markPrizeDelivered(b.dataset.delivered, +b.dataset.i); toast('Marked as delivered'); SA.app.rerender(); }
      });
    },
  });
})(window.SA);
