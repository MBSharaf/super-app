(function (SA) {
  const { esc, fantasyPrice, debounce } = SA.core.utils;
  const { icon } = SA.ui;
  const c = SA.ui.c;
  const F = () => SA.services.fantasy;
  const POS = [['GK', 'Goalkeepers'], ['DEF', 'Defenders'], ['MID', 'Midfielders'], ['FWD', 'Forwards']];

  // Draft squad lives in memory while the user edits; saved through fantasyService.saveSquad
  let draft = null;
  let draftLeague = null;
  let justAdded = null;
  const filt = { pos: '', team: '', maxPrice: '', q: '', sort: 'total' };

  const when = (d) => `${SA.core.utils.fmtDate(d)}, ${d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })}`;
  const isLive = () => !!SA.services.api?.isLive?.();

  /** After the team is saved: says when it starts counting, so the player knows it's in */
  const confirmed = (team) => {
    const meta = F().meta();
    SA.ui.overlay.open({
      title: 'Your team is confirmed',
      body: `<div class="stack" style="--gap:12px;text-align:center">
        <span class="service-ico" style="margin:0 auto">${icon('check', 26)}</span>
        <p class="lead"><b>${esc(team?.name || '')}</b></p>
        <p>Your team plays from Gameweek ${meta.gw}.</p>
        <div class="panel panel-pad small"><span class="soft">Deadline</span><br><b class="num">${when(meta.deadline)}</b></div>
        <p class="small soft">Until then you can change players for free, pick your captain and set your lineup.</p>
        <p class="small soft">${isLive() ? 'After the deadline your team is locked for that gameweek. Your points come from the real Premier League matches and are added automatically after each game.' : 'This is the demo site: use Simulate to play a gameweek and see your points.'}</p></div>`,
      footer: '<button class="btn btn-primary btn-block" data-close>Pick my captain</button>',
    });
  };

  /** Creating a team: a summary to confirm (first gameweek, deadline, money left) and the team name */
  const promptCreate = (ids) => {
    const user = SA.services.user.me();
    const f = F(), meta = f.meta(), spent = f.cost(ids);
    SA.ui.overlay.open({
      title: 'Confirm your team', subtitle: 'Check your squad and give it a name.',
      body: `<form id="name-form" class="stack" style="--gap:12px">
        <dl class="small stack panel panel-pad" style="--gap:8px;margin:0">
          <div class="row-between"><dt class="soft">Players</dt><dd class="num"><b>15 / 15</b></dd></div>
          <div class="row-between"><dt class="soft">Squad cost</dt><dd class="num"><b>${fantasyPrice(spent)}</b></dd></div>
          <div class="row-between"><dt class="soft">Money left</dt><dd class="num"><b>${fantasyPrice(meta.budget - spent)}</b></dd></div>
          <div class="row-between"><dt class="soft">First gameweek</dt><dd class="num"><b>Gameweek ${meta.gw}</b></dd></div>
          <div class="row-between"><dt class="soft">Deadline</dt><dd class="num"><b>${when(meta.deadline)}</b></dd></div>
        </dl>
        <div class="field"><label for="tn">Team name</label><input class="input" id="tn" name="name" maxlength="24" value="${esc(user.firstName)} FC" autofocus /></div>
        <p class="xs muted">Your points start from Gameweek ${meta.gw}. Your team is entered into the Super Malaab League automatically.</p></form>`,
      footer: `<button class="btn btn-outline" data-close>Back</button><button class="btn btn-primary" form="name-form" type="submit">Confirm team</button>`,
      onMount(el, close) {
        el.querySelector('#tn').select();
        el.querySelector('#name-form').addEventListener('submit', async (e) => {
          e.preventDefault();
          const btn = el.closest('.modal, .drawer')?.querySelector('[form="name-form"]') || document.querySelector('[form="name-form"]');
          if (btn) btn.disabled = true;
          try {
            const r = await F().saveSquad(ids, { name: e.target.name.value });
            close(); draft = null;
            SA.core.router.go('/fantasy');
            SA.core.motion.confetti(); setTimeout(() => confirmed(r?.team || F().team()), 450);  // after the team page has opened
          } catch (err) { if (btn) btn.disabled = false; SA.ui.overlay.toast(err.message, { type: 'error' }); }
        });
      },
    });
  };

  const slotHTML = (id, pos) => {
    const f = F();
    if (!id) return `<button class="sq-slot is-empty" data-fill="${pos}"><span class="sq-plus">${icon('plus', 18)}</span><span class="xs">Add ${pos}</span></button>`;
    const p = f.byId[id], t = f.teamById[p.teamId];
    const isNew = f.team() && !f.team().squad.includes(id);
    return `<button class="sq-slot ${isNew ? 'is-new' : ''} ${id === justAdded ? 'just-added' : ''}" data-remove="${id}" aria-label="Remove ${esc(p.name)}">
      <span class="sq-x">${icon('x', 12, 2.6)}</span>${SA.ui.sports.face(p, t, 40, pos === 'GK')}<span class="sq-name">${esc(SA.pages.fantasy.short(p.name))}</span><span class="xs num">${fantasyPrice(p.price)}</span></button>`;
  };

  const summary = () => {
    const f = F(), team = f.team();
    const spent = f.cost(draft), bank = +(f.D.rules.budget - spent).toFixed(1);
    const cnt = draft.length;
    const errs = f.validateSquad(draft);
    let transferTxt = '';
    if (team) {
      const made = draft.filter((id) => !team.squad.includes(id)).length;
      const hit = Math.max(0, made - team.freeTransfers) * f.D.rules.transferCost;
      transferTxt = `<div><span class="xs muted">Transfers</span><b class="num">${made} <span class="xs muted">/ ${team.freeTransfers} free</span></b></div><div><span class="xs muted">Cost</span><b class="num" style="color:${hit ? 'var(--danger)' : 'inherit'}">${hit ? '−' + hit + ' pts' : '0 pts'}</b></div>`;
    }
    return `<div class="sq-summary">
      <div><span class="xs muted">Players</span><b class="num">${cnt}/15</b></div>
      <div><span class="xs muted">Money left</span><b class="num" style="color:${bank < 0 ? 'var(--danger)' : 'inherit'}">${fantasyPrice(bank)}</b></div>
      ${transferTxt}
      <div class="sq-budget"><div class="meter ${bank < 0 ? 'is-hot' : ''}"><span style="width:${Math.min(100, (spent / f.D.rules.budget) * 100)}%"></span></div></div>
    </div>
    ${cnt === 15 && errs.length ? `<p class="field-error">${esc(errs[0])}</p>` : ''}`;
  };

  const renderSquad = (el) => {
    const f = F();
    el.querySelector('[data-squad]').innerHTML = POS.map(([pos, label]) => {
      const ids = draft.filter((id) => f.byId[id].pos === pos);
      const need = f.D.rules.squad[pos];
      return `<div class="sq-line"><span class="xs sq-label">${label} <span class="muted">${ids.length}/${need}</span></span><div class="sq-row">${Array.from({ length: need }, (_, i) => slotHTML(ids[i], pos)).join('')}</div></div>`;
    }).join('');
    el.querySelector('[data-summary]').innerHTML = summary();
    const ok = draft.length === 15 && !f.validateSquad(draft).length;
    const team = f.team();
    const changed = team && draft.some((id) => !team.squad.includes(id));
    const save = el.querySelector('[data-save]');
    save.disabled = !(ok && (!team || changed));
    save.textContent = team ? (changed ? 'Confirm transfers' : 'No changes yet') : ok ? 'Continue — name your team' : `Pick ${15 - draft.length} more`;
  };

  const renderList = async (el) => {
    const f = F();
    const list = await f.players(filt);
    const perTeam = f.teamCounts(draft);
    const bank = f.D.rules.budget - f.cost(draft);
    el.querySelector('[data-plist]').innerHTML = list.slice(0, 80).map((p) => {
      const inSquad = draft.includes(p.id);
      const posFull = draft.filter((id) => f.byId[id].pos === p.pos).length >= f.D.rules.squad[p.pos];
      const teamFull = (perTeam[p.teamId] || 0) >= f.D.rules.maxPerTeam;
      const tooPricey = p.price > bank + 0.001;
      const reason = inSquad ? '' : posFull ? `${p.pos} full` : teamFull ? '3 from club' : tooPricey ? 'Over budget' : '';
      return `<li class="pl-row ${inSquad ? 'is-in' : ''}">
        <button class="pl-info" data-info="${p.id}" aria-label="View ${esc(p.name)}">${SA.ui.sports.face(p, f.teamById[p.teamId], 34, p.pos === 'GK')}<span class="grow"><b class="small">${esc(p.name)}</b> ${c.statusDot(p.status)}<span class="xs muted" style="display:block">${f.teamById[p.teamId].name} · ${p.pos} · ${p.total} pts · form ${p.form.toFixed(1)}</span></span></button>
        <span class="num small strong">${fantasyPrice(p.price)}</span>
        ${inSquad ? `<button class="pl-add is-in" data-remove="${p.id}" aria-label="Remove ${esc(p.name)}">${icon('check', 16, 2.6)}</button>`
          : `<button class="pl-add" data-add="${p.id}" ${reason ? `disabled title="${reason}"` : ''} aria-label="Add ${esc(p.name)}">${reason ? `<span class="xs">${reason}</span>` : icon('plus', 16, 2.4)}</button>`}
      </li>`;
    }).join('') || `<li>${c.empty({ icon: 'search', title: 'No players match', text: 'Clear a filter or search for another name.' })}</li>`;
    SA.core.motion.enhance(el.querySelector('[data-plist]'));
  };

  const filterBar = () => {
    const f = F();
    return `<div class="tabs" style="width:100%">${[['', 'All'], ['GK', 'GK'], ['DEF', 'DEF'], ['MID', 'MID'], ['FWD', 'FWD']].map(([v, l]) => `<button class="tab" data-pos="${v}" aria-selected="${filt.pos === v}" style="flex:1">${l}</button>`).join('')}</div>
      <div class="row wrap" style="margin-top:10px;--gap:8px">
        <div class="input-icon grow" style="min-width:160px">${icon('search', 18)}<input class="input" id="sq-q" placeholder="Search player or club" value="${esc(filt.q)}" /></div>
        <select class="select" id="sq-team" style="width:auto;flex:1"><option value="">All clubs</option>${f.D.teams.map((t) => `<option value="${t.id}" ${filt.team === t.id ? 'selected' : ''}>${SA.core.utils.esc(t.name)}</option>`).join('')}</select>
        <select class="select" id="sq-max" style="width:auto;flex:1"><option value="">Any price</option>${[5, 6, 7, 8, 9].map((v) => `<option value="${v}" ${+filt.maxPrice === v ? 'selected' : ''}>Up to ${v}.0M</option>`).join('')}</select>
        <select class="select" id="sq-sort" style="width:auto;flex:1">${[['total', 'Points'], ['form', 'Form'], ['price', 'Price'], ['selected', 'Popular'], ['value', 'Value']].map(([v, l]) => `<option value="${v}" ${filt.sort === v ? 'selected' : ''}>Sort: ${l}</option>`).join('')}</select>
      </div>`;
  };

  SA.pages.fantasySquad = {
    title: 'Pick your squad — Super Malaab Fantasy',
    promptCreate,
    render(ctx) {
      const f = F(), team = f.team();
      // Switching leagues starts a fresh draft and clears the club filter
      if (draftLeague !== f.league()) { draft = null; filt.team = ''; draftLeague = f.league(); }
      if (!draft || ctx.query.reset) draft = team ? team.squad.slice() : [];
      if (ctx.query.out && draft.includes(ctx.query.out)) { filt.pos = f.byId[ctx.query.out].pos; draft = draft.filter((x) => x !== ctx.query.out); }
      return `${SA.pages.fantasy.header('squad')}
      <div class="container fx-body">
        <div class="sq-layout">
          <section>
            <div class="row-between wrap" style="margin-bottom:12px">
              <div><h2 class="h3">${team ? 'Transfers' : 'Pick your 15'}</h2><p class="small soft">${team ? `Each transfer beyond your ${team.freeTransfers} free one${team.freeTransfers === 1 ? '' : 's'} costs ${f.D.rules.transferCost} points. Tap a player to sell, then add a replacement.` : '100M budget · 2 GK, 5 DEF, 5 MID, 3 FWD · max 3 per club'}</p></div>
              <div class="row"><button class="btn btn-outline btn-sm" data-auto>${icon('sparkle', 15)} ${team ? 'Auto-fill' : 'Auto-pick'}</button><button class="btn btn-ghost btn-sm" data-reset>${team ? 'Undo changes' : 'Clear'}</button></div>
            </div>
            <div class="sq-board fx-pitch-flat" data-squad></div>
            <div data-summary></div>
            <div class="sq-actions"><button class="btn btn-primary btn-lg btn-block" data-save>Continue</button></div>
          </section>
          <section class="panel sq-market">
            <div class="sq-market-head" data-filters>${filterBar()}</div>
            <ul class="pl-list" data-plist>${c.skeletonCards(6, 56)}</ul>
          </section>
        </div>
      </div>`;
    },
    mount(el) {
      const off = SA.pages.fantasy.bindHeader(el);
      const f = F();
      const refresh = () => { renderSquad(el); renderList(el); };
      el.addEventListener('click', async (e) => {
        const add = e.target.closest('[data-add]');
        if (add) { draft.push(add.dataset.add); justAdded = add.dataset.add; refresh(); justAdded = null; return; }
        const rem = e.target.closest('[data-remove]');
        if (rem) { const p = f.byId[rem.dataset.remove]; draft = draft.filter((x) => x !== rem.dataset.remove); filt.pos = p.pos; el.querySelector('[data-filters]').innerHTML = filterBar(); bindFilters(); refresh(); return; }
        const fill = e.target.closest('[data-fill]');
        if (fill) { filt.pos = fill.dataset.fill; el.querySelector('[data-filters]').innerHTML = filterBar(); bindFilters(); renderList(el); if (matchMedia('(max-width: 980px)').matches) el.querySelector('.sq-market').scrollIntoView({ behavior: 'smooth' }); return; }
        const pos = e.target.closest('[data-pos]');
        if (pos) { filt.pos = pos.dataset.pos; el.querySelectorAll('[data-pos]').forEach((b) => b.setAttribute('aria-selected', b === pos)); renderList(el); return; }
        const info = e.target.closest('[data-info]');
        if (info) { SA.pages.fantasy.openPlayer(info.dataset.info); return; }
        if (e.target.closest('[data-auto]')) {
          // Keep current picks, fill the remaining slots with an auto-pick that respects the rules
          const keep = draft.slice();
          if (keep.length === 15) { SA.ui.overlay.toast('Your squad is already full. Remove a player first.', { type: 'info' }); return; }
          if (!keep.length) draft = f.autoPick(String(Date.now()));
          else {
            const need = { ...f.D.rules.squad }; keep.forEach((id) => need[f.byId[id].pos]--);
            const perTeam = f.teamCounts(keep);
            let bank = f.D.rules.budget - f.cost(keep);
            const pool = f.playersSync().filter((p) => !keep.includes(p.id) && p.status === 'fit').sort((a, b) => (b.total + Math.random() * 25) - (a.total + Math.random() * 25));
            for (const p of pool) {
              const left = Object.values(need).reduce((a, b) => a + b, 0);
              if (!left) break;
              if (!need[p.pos] || (perTeam[p.teamId] || 0) >= 3 || bank - p.price < (left - 1) * 4) continue;
              keep.push(p.id); need[p.pos]--; bank -= p.price; perTeam[p.teamId] = (perTeam[p.teamId] || 0) + 1;
            }
            draft = keep;
          }
          refresh(); SA.ui.overlay.toast('Squad filled — swap anyone you don’t like', { type: 'info' });
          return;
        }
        if (e.target.closest('[data-reset]')) { draft = f.team() ? f.team().squad.slice() : []; refresh(); return; }
        if (e.target.closest('[data-save]')) {
          if (!f.team()) return promptCreate(draft.slice());
          const team = f.team();
          const made = draft.filter((id) => !team.squad.includes(id));
          const hit = Math.max(0, made.length - team.freeTransfers) * f.D.rules.transferCost;
          const outs = team.squad.filter((id) => !draft.includes(id));
          const ok = await SA.ui.overlay.confirm({
            title: `Confirm ${made.length} transfer${made.length > 1 ? 's' : ''}?`,
            text: `<ul class="transfer-list">${outs.map((o, i) => `<li><span class="t-out">${esc(f.byId[o].name)}</span>${icon('arrowRight', 14)}<span class="t-in">${esc(f.byId[made[i]]?.name || '')}</span></li>`).join('')}</ul>${hit ? `<p class="small" style="color:var(--danger);margin-top:10px">This costs ${hit} points next gameweek.</p>` : '<p class="small muted" style="margin-top:10px">Free transfer — no points deducted.</p>'}`,
            confirmLabel: 'Confirm transfers', cancelLabel: 'Keep editing',
          });
          if (!ok) return;
          try { await f.saveSquad(draft.slice()); SA.ui.overlay.toast(`Transfers confirmed for Gameweek ${f.meta().gw}`); draft = null; SA.core.router.go('/fantasy'); }
          catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
        }
      });
      const bindFilters = () => {
        el.querySelector('#sq-q').addEventListener('input', debounce((e) => { filt.q = e.target.value; renderList(el); }, 200));
        el.querySelector('#sq-team').addEventListener('change', (e) => { filt.team = e.target.value; renderList(el); });
        el.querySelector('#sq-max').addEventListener('change', (e) => { filt.maxPrice = e.target.value; renderList(el); });
        el.querySelector('#sq-sort').addEventListener('change', (e) => { filt.sort = e.target.value; renderList(el); });
      };
      bindFilters();
      refresh();
      return off;
    },
  };
})(window.SA);
