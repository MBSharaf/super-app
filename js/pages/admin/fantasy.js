/** Admin console: fantasy — prize leagues (prizes, sponsor, cap), sponsors, winners and Fantasy Pass subscribers. */
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

  SA.pages.adminFantasy = A.guard({
    title: 'Fantasy — Super App Admin',
    render(ctx) {
      const tab = ctx.query.tab || '';
      const LG = F().LEAGUES;
      const sponsors = db().all('sponsors') || [];
      const winners = (db().all('fantasyWinners') || []).slice().sort((a, b) => b.at - a.at);
      const from = isoDate(addDays(today(), -29)), to = isoDate(today());
      const passes = AD().passSales(from, to);
      let section = '';
      if (!tab) {
        section = Object.keys(LG).map((lg) => `<section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">${esc(LG[lg].label)}</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>League</th><th>Type</th><th>Sponsor</th><th>1st prize</th><th class="r">Managers</th><th>Status</th><th></th></tr></thead><tbody>
          ${F().allOfficial(lg).map((l) => `<tr class="${l.hidden ? 'is-dim' : ''}"><td class="small"><b>${esc(l.name)}</b><span class="xs muted" style="display:block">${esc(l.scope)}</span></td><td>${l.kind === 'official' ? '<span class="badge">Free</span>' : `<span class="badge badge-gold">${icon('crown', 11, 2)} Pass</span>`}</td>
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
        const plans = { egy: 0, epl: 0, both: 0 }; passes.forEach((x) => { plans[x.plan]++; });
        const prices = db().settings().fantasySubs;
        section = `<div class="dash-grid g-1-1"><section class="dash-card"><h2 class="h4">Fantasy Pass — last 30 days</h2>
            <div class="adm-counts" style="grid-template-columns:repeat(2,minmax(0,1fr))">${[['trophy', passes.length, 'Passes sold'], ['wallet', money(passes.reduce((t, x) => t + x.amount, 0)), 'Revenue'], ['flag', plans.egy, 'Egyptian league'], ['globe', plans.epl, 'Premier League'], ['sparkle', plans.both, 'Both leagues'], ['percent', `${passes.length ? Math.round((plans.both / passes.length) * 100) : 0}%`, 'Chose both']].map(([ic, n, l]) => `<div class="adm-count"><span class="kpi-ico">${icon(ic, 18)}</span><b class="h4 num">${n}</b><span class="xs muted">${l}</span></div>`).join('')}</div></section>
          <section class="dash-card"><h2 class="h4">Prices</h2><dl class="kv" style="margin-top:12px"><div><dt>One league / month</dt><dd class="num">${money(prices.one)}</dd></div><div><dt>Both leagues / month</dt><dd class="num">${money(prices.both)}</dd></div><div><dt>Pro sellers</dt><dd>50% off</dd></div><div><dt>Elite sellers</dt><dd>Free</dd></div></dl>
            <a class="btn btn-outline btn-sm" href="#/admin/settings" style="margin-top:12px">Change prices</a>
            <p class="xs muted" style="margin-top:10px">${icon('shield', 12)} Prize leagues give sponsor prizes and vouchers only — no cash prizes and no entry fees.</p></section></div>
          <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Latest passes</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Date</th><th>Customer</th><th>Plan</th><th class="r">Paid</th></tr></thead><tbody>
            ${passes.slice(0, 25).map((x) => `<tr><td class="small">${fmtDate(new Date(x.at))}</td><td class="small">${esc(x.customer)}</td><td class="small">${{ egy: 'Egyptian league', epl: 'Premier League', both: 'Both leagues' }[x.plan]}</td><td class="r num">${money(x.amount)}</td></tr>`).join('')}</tbody></table></div></section>`;
      }
      const active = sponsors.filter((s) => !s.to || s.to >= isoDate(today()));
      const body = `<div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'Prize leagues', value: Object.keys(LG).reduce((t, lg) => t + F().allOfficial(lg).filter((l) => !l.hidden).length, 0), ic: 'trophy' })}
          ${C.kpi({ label: 'Active sponsors', value: active.length, ic: 'sparkle', sub: money(active.reduce((t, s) => t + (s.value || 0), 0)) })}
          ${C.kpi({ label: 'Passes (30 days)', value: passes.length.toLocaleString(), ic: 'crown', tone: 'k-turf', sub: money(passes.reduce((t, x) => t + x.amount, 0)) })}
          ${C.kpi({ label: 'Prizes to deliver', value: winners.reduce((t, w) => t + w.rows.filter((r) => !r.delivered).length, 0), ic: 'gift', tone: winners.some((w) => w.rows.some((r) => !r.delivered)) ? 'k-blaze' : '' })}
        </div>
        ${A.tabs('/admin/fantasy', [['', 'Prize leagues'], ['sponsors', 'Sponsors', sponsors.length], ['winners', 'Winners', winners.length], ['pass', 'Fantasy Pass']], tab)}${section}`;
      return C.layout('fantasy', { title: 'Fantasy', subtitle: 'Prize leagues, sponsors, winners and Fantasy Pass subscribers — Egyptian league and Premier League', body });
    },
    mount(el) {
      el.addEventListener('click', async (e) => {
        const t = (s) => e.target.closest(s);
        if (t('[data-edit-league]')) { const b = t('[data-edit-league]'); return leagueDialog(b.dataset.lg, F().allOfficial(b.dataset.lg).find((l) => l.id === b.dataset.editLeague)); }
        if (t('[data-winners]')) {
          const b = t('[data-winners]'); const l = F().allOfficial(b.dataset.lg).find((x) => x.id === b.dataset.winners);
          if (!(await SA.ui.overlay.confirm({ title: `Announce winners of ${esc(l.name)}?`, text: 'The top of the table gets the prizes. Vouchers become single-use codes; other prizes are listed for delivery. Winners on this device are notified.', confirmLabel: 'Announce winners' }))) return;
          try { AD().announceWinners(l.id, b.dataset.lg); toast('Winners announced'); SA.core.router.go('/admin/fantasy?tab=winners'); } catch (err) { toast(err.message, 'error'); }
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
