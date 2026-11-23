/**
 * League tables and match facts (statistics, events, line-ups) for the matches page, from the server's live feed
 * (`/matches/competitions/{key}/standings`, `/matches/{id}/facts`). Demo mode (GitHub Pages, no server) says these
 * need the live server. Also the competitions rail: one scrolling row of league tiles instead of wrapping chips.
 */
(function (SA) {
  const { esc } = SA.core.utils;
  const { icon } = SA.ui;
  const api = () => SA.services.api;
  const live = () => api()?.isLive?.();

  const compName = (k) => (SA.i18n?.isAr && k.nameAr ? k.nameAr : k.name);

  /** One row of league tiles (logo + name) that scrolls sideways; the chosen one is pressed */
  const rail = (comps, active, href) => `<div class="comp-rail" role="group" aria-label="League">
    ${comps.map((k) => {
      const src = SA.ui.sports.leagueLogo(k);
      return `<a class="comp-tile ${active === k.id ? 'is-on' : ''}" href="${href(k)}" aria-pressed="${active === k.id}" style="--comp:${esc(k.color)}" title="${esc(compName(k))}">
        <span class="comp-tile-logo">${src ? `<img src="${esc(src)}" alt="" width="28" height="28" loading="lazy" decoding="async" referrerpolicy="no-referrer" />` : `<b>${esc(String(k.short || k.name).slice(0, 3).toUpperCase())}</b>`}</span>
        <span class="comp-tile-name" translate="no">${esc(compName(k))}</span></a>`;
    }).join('')}</div>`;

  /* ---------- Tables ---------- */
  const HEAD = { en: ['P', 'W', 'D', 'L', 'GF', 'GA', 'GD', 'Pts'], ar: ['لعب', 'ف', 'ت', 'خ', 'له', 'عليه', '±', 'ن'] };
  const head = () => (SA.i18n?.isAr ? HEAD.ar : HEAD.en).map((h, i) => `<th class="r${i === 4 || i === 5 ? ' hide-s' : ''}" translate="no">${h}</th>`).join('');
  const logo = (src, name) => (src && SA.config.clubLogos ? `<img class="st-logo" src="${esc(src)}" alt="" width="20" height="20" loading="lazy" referrerpolicy="no-referrer" onerror="this.remove()" />` : `<span class="st-logo st-mono">${esc(name.slice(0, 1))}</span>`);
  const group = (g, multi) => `<div class="panel table-wrap st-group">${multi && g.name ? `<h3 class="small strong st-group-name">${esc(g.name)}</h3>` : ''}
    <table class="table league-table st-table"><thead><tr><th>#</th><th>Club</th>${head()}</tr></thead>
    <tbody>${g.rows.map((r) => `<tr><td class="st-pos" style="${r.color ? `box-shadow: inset 3px 0 0 ${esc(r.color)}` : ''}"><b class="num">${r.position}</b></td>
      <td><span class="st-club">${logo(r.logo, r.club)}<span translate="no">${esc(r.club)}</span></span></td>
      <td class="r num">${r.played}</td><td class="r num">${r.won}</td><td class="r num">${r.drawn}</td><td class="r num">${r.lost}</td>
      <td class="r num hide-s">${r.goalsFor}</td><td class="r num hide-s">${r.goalsAgainst}</td><td class="r num">${r.goalDifference > 0 ? '+' : ''}${r.goalDifference}</td><td class="r num"><b>${r.points}</b></td></tr>`).join('')}</tbody></table></div>`;

  const standings = async (key) => {
    if (!live()) return `<div class="panel panel-pad small muted">${icon('info', 14)} League tables come from the live server.</div>`;
    try {
      const t = await api().get(`/matches/competitions/${encodeURIComponent(key)}/standings`);
      if (!t.groups.length) return SA.ui.c.empty({ icon: 'trophy', title: 'No table for this competition yet', text: 'Knockout rounds have no table. Pick another competition.' });
      return `${t.season ? `<p class="xs muted" style="margin-bottom:10px">Season <span translate="no">${esc(t.season)}</span></p>` : ''}
        <div class="st-groups ${t.groups.length > 1 ? 'is-multi' : ''}">${t.groups.map((g) => group(g, t.groups.length > 1)).join('')}</div>
        ${t.legend.length ? `<ul class="st-legend xs">${t.legend.map((l) => `<li><i style="background:${esc(l.color)}"></i>${esc(l.title)}</li>`).join('')}</ul>` : ''}`;
    } catch (e) {
      return `<div class="panel panel-pad small muted">${icon('info', 14)} The table couldn’t load. Try again in a minute.</div>`;
    }
  };

  /* ---------- Match facts ---------- */
  const lead = (v) => parseFloat(String(v).replace(/[^\d.].*$/, '')) || 0;
  const statRow = (s) => {
    const [h, a] = [lead(s.home), lead(s.away)], tot = h + a || 1;
    const win = h === a ? '' : h > a ? 'home' : 'away';
    return `<div class="fx-stat"><b class="num ${win === 'home' ? 'is-lead' : ''}">${esc(s.home)}</b><span class="fx-stat-title xs">${esc(s.title)}</span><b class="num ${win === 'away' ? 'is-lead' : ''}">${esc(s.away)}</b>
      ${s.bar ? `<div class="stat-bars"><span class="sb-h" style="--w:${(h / tot) * 100}%"></span><span class="sb-a" style="--w:${(a / tot) * 100}%"></span></div>` : ''}</div>`;
  };
  const stats = (f) => (f.stats.length ? f.stats.map((g) => `<section class="fx-stat-group"><h4 class="xs strong fx-stat-head">${esc(g.title)}</h4>${g.items.map(statRow).join('')}</section>`).join('')
    : '<p class="small muted">Match stats appear here once the match kicks off.</p>');

  const evIcon = (e) => (e.type === 'goal' ? `<span class="tl-ico goal">${icon('ball', 14)}</span>`
    : e.type === 'card' ? `<span class="tl-ico ${e.detail === 'Red' ? 'red' : 'yellow'}"></span>` : `<span class="tl-ico sub">${icon('swap', 13)}</span>`);
  const evText = (e) => (e.type === 'goal' ? `<b translate="no">${esc(e.player || '')}</b>${e.detail ? ` <span class="xs muted">${esc(e.detail)}</span>` : ''}${e.homeScore != null ? ` <span class="badge">${e.homeScore} – ${e.awayScore}</span>` : ''}`
    : e.type === 'card' ? `<span translate="no">${esc(e.player || '')}</span>`
      : `<span class="fx-in" translate="no">${esc(e.playerIn || '')}</span> <span class="fx-out" translate="no">${esc(e.playerOut || '')}</span>`);
  const events = (f) => (f.events.length ? `<ol class="fx-events">${f.events.map((e, i) => `<li class="fx-ev is-${e.side}" style="--i:${i}"><span class="tl-min num">${e.minute}${e.added ? `+${e.added}` : ''}'</span>${evIcon(e)}<span class="fx-ev-text">${evText(e)}</span></li>`).join('')}</ol>`
    : '<p class="small muted">Goals, cards and substitutions appear here during the match.</p>');

  const side = (l, name) => (l ? `<div class="fx-lineup"><div class="row-between"><b class="small" translate="no">${esc(name)}</b>${l.formation ? `<span class="badge">${esc(l.formation)}</span>` : ''}</div>
      <ol class="fx-players">${l.starters.map((p) => `<li><span class="fx-num num">${esc(p.number || '')}</span><span class="grow" translate="no">${esc(p.name)}</span>${p.rating ? `<span class="fx-rating num">${esc(p.rating)}</span>` : ''}</li>`).join('')}</ol>
      ${l.subs.length ? `<p class="xs muted fx-subs-head">Substitutes</p><ol class="fx-players is-subs">${l.subs.map((p) => `<li><span class="fx-num num">${esc(p.number || '')}</span><span class="grow" translate="no">${esc(p.name)}</span>${p.rating ? `<span class="fx-rating num">${esc(p.rating)}</span>` : ''}</li>`).join('')}</ol>` : ''}
      ${l.coach ? `<p class="xs muted" style="margin-top:8px">Coach: <span translate="no">${esc(l.coach)}</span></p>` : ''}</div>` : '');
  const lineups = (f, m) => (f.home || f.away ? `<div class="fx-lineups">${side(f.home, m.homeT.name)}${side(f.away, m.awayT.name)}</div>`
    : '<p class="small muted">Line-ups are usually out about an hour before kick-off.</p>');

  /** The three tabs of the match centre, filled from the server */
  const facts = async (m) => {
    const f = await api().get(`/matches/${m.apiId}/facts`);
    return `<div class="tabs fx-tabs" role="tablist"><button class="tab" role="tab" aria-selected="true" data-fx="stats">Stats</button><button class="tab" role="tab" aria-selected="false" data-fx="events">Timeline</button><button class="tab" role="tab" aria-selected="false" data-fx="lineups">Line-ups</button></div>
      <div class="fx-pane" data-pane="stats">${stats(f)}</div><div class="fx-pane" data-pane="events" hidden>${events(f)}</div><div class="fx-pane" data-pane="lineups" hidden>${lineups(f, m)}</div>`;
  };
  const wireTabs = (el) => el.addEventListener('click', (e) => {
    const b = e.target.closest('[data-fx]');
    if (!b) return;
    el.querySelectorAll('[data-fx]').forEach((t) => t.setAttribute('aria-selected', t === b));
    el.querySelectorAll('[data-pane]').forEach((p) => { p.hidden = p.dataset.pane !== b.dataset.fx; });
    requestAnimationFrame(() => el.querySelectorAll('.stat-bars').forEach((s) => s.classList.add('is-in')));
  });

  SA.ui.matchInsights = { rail, standings, facts, wireTabs, compName, live };
})(window.SA);
