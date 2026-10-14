/**
 * Seller dashboard: marketing calendar — big matches, Egyptian occasions and shopping moments, typical weather and
 * what's selling on Super App. What a seller sees depends on their level (calendar: basic → weather → trends → early).
 * Live forecasts and real fixtures arrive with the backend (API-FOOTBALL, Open-Meteo); today the matches come from the
 * schedule service and the weather from Cairo's monthly averages.
 */
(function (SA) {
  const { esc, fmtDate, isoDate, addDays, today, parseISO, fmtNum, MON } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const D = () => SA.services.dash;
  const P = () => SA.services.pod;
  const SL = () => SA.services.sellers;
  const DAY = 864e5;

  const RANK = { basic: 0, weather: 1, trends: 2, early: 3 };
  const HORIZON = [30, 45, 60, 120]; // days ahead by calendar rank

  /** Occasions in Egypt. "expected" dates depend on the moon sighting. */
  const OCCASIONS = [
    ['2026-10-06', 'Armed Forces Day', 'holiday', 'National pride designs — flags, 1973, “Egypt” typography.'],
    ['2026-11-27', 'White Friday', 'shopping', 'Egypt’s biggest online sale week. Put your best sellers in front and share a store code.'],
    ['2026-12-20', 'New Year gift season', 'shopping', 'Gift-ready hoodies and couple/crew sets sell well in the last ten days of December.'],
    ['2027-01-07', 'Coptic Christmas', 'holiday', 'Warm, festive designs and family gifts.'],
    ['2027-01-20', 'Mid-year exams & winter break', 'season', 'Students shop after exams — gaming, anime and campus humour.'],
    ['2027-01-25', 'Police Day · 25 January', 'holiday', 'A long weekend: good timing for a new drop.'],
    ['2027-02-08', 'Ramadan begins (expected)', 'holiday', 'Ramadan nights football, “Fanoos” art, suhoor crews and Ramadan tournament kits.', true],
    ['2027-02-14', 'Valentine’s Day', 'shopping', 'Couple tees and “Ya Habibi” style designs.'],
    ['2027-03-10', 'Eid al-Fitr (expected)', 'holiday', 'New clothes for Eid — the biggest clothing week of the year.', true],
    ['2027-03-21', 'Mother’s Day', 'shopping', 'Gifts for mum: soft colours, Arabic calligraphy, family sets.'],
    ['2027-04-25', 'Sinai Liberation Day', 'holiday', 'Long weekend — campaign timing.'],
    ['2027-05-03', 'Sham El-Nessim', 'holiday', 'Spring outings: light tees, caps and bright colours.'],
    ['2027-05-16', 'Eid al-Adha (expected)', 'holiday', 'Second Eid shopping peak.', true],
  ];
  /** Cairo monthly averages (°C high / low) */
  const CLIMATE = [[19, 9], [21, 10], [24, 12], [29, 15], [33, 18], [35, 21], [35, 23], [35, 23], [33, 21], [30, 18], [25, 14], [21, 10]];
  const weatherTip = (hi) => (hi <= 22 ? ['Hoodie season', 'Lead with hoodies and dark colours — searches for hoodies peak now.']
    : hi <= 27 ? ['Layering weather', 'Tees by day, hoodies at night. Oversized tees do well.']
    : ['Tee & cap weather', 'Light colours, breathable tees and caps. Hoodies slow down.']);

  const KIND = { match: ['badge-blaze', 'Big match', 'ball'], holiday: ['badge-turf', 'Occasion', 'calendar'], shopping: ['badge-gold', 'Shopping', 'tag'], season: ['badge-info', 'Season', 'sun'] };

  const importance = (m) => (m.isDerby ? 100 : 0) + (['AHL', 'ZAM', 'PYR', 'EGY'].some((t) => t === m.home || t === m.away) ? 40 : 0) + (m.compId === 'ucl' ? 30 : 0) + (m.egyptians?.length ? 15 : 0);
  /** The biggest fixture per day over the horizon (derbies, Egyptian giants, national team, Champions League) */
  const bigMatches = (days) => {
    const out = [];
    for (let i = 0; i < days; i++) {
      const d = isoDate(addDays(today(), i));
      const best = SA.services.schedule.listSync(d).map((m) => ({ m, s: importance(m) })).filter((x) => x.s >= 40).sort((a, b) => b.s - a.s)[0];
      if (best) {
        const m = best.m;
        const title = `${m.homeT?.name || m.home} vs ${m.awayT?.name || m.away}`;
        const idea = m.isDerby ? 'The Cairo derby — the biggest football moment of the month. Publish derby designs at least a week before.' : m.compId === 'ucl' ? 'Champions League night — watch-party and “Egyptian abroad” designs.' : 'A big game for Egyptian fans — terrace-chant and team-colour designs.';
        out.push({ date: d, title, kind: 'match', idea, sub: m.comp?.name || '', derby: m.isDerby, score: best.s });
      }
    }
    // Keep the most important ones so the calendar stays readable
    return out.sort((a, b) => b.score - a.score).slice(0, 8);
  };

  /** What sold on Super App in the last 30 days (all sellers): tags, sections, products, colours */
  const trends = () => {
    const since = Date.now() - 30 * DAY;
    const orders = P().orders().filter((o) => o.placedAt >= since && o.status !== 'refused');
    const count = (fn) => { const m = new Map(); orders.forEach((o) => { [].concat(fn(o)).filter(Boolean).forEach((k) => m.set(k, (m.get(k) || 0) + o.qty)); }); return [...m.entries()].sort((a, b) => b[1] - a[1]).slice(0, 6); };
    const design = (o) => SA.services.db.find('designs', o.designId);
    return {
      total: orders.reduce((t, o) => t + o.qty, 0),
      tags: count((o) => design(o)?.tags || []),
      cats: count((o) => P().category(design(o)?.category)?.name),
      products: count((o) => P().catalogItem(o.catalogId)?.name),
      colors: count((o) => o.color),
    };
  };

  const bars = (rows) => {
    const max = Math.max(1, ...rows.map((r) => r[1]));
    return `<ul class="mk-bars">${rows.map(([k, v]) => `<li><span class="small truncate">${esc(k)}</span><span class="meter"><span style="width:${(v / max) * 100}%"></span></span><b class="xs num">${fmtNum(v)}</b></li>`).join('') || '<li class="xs muted">Not enough sales yet.</li>'}</ul>`;
  };
  const locked = (title, text, level) => `<section class="dash-card mk-locked">${icon('lock', 20)}<div><h2 class="h4">${title}</h2><p class="small soft">${text}</p><span class="lvl-chip lvl-${level.toLowerCase()}">Unlocks at ${level}</span></div></section>`;

  SA.pages.dashSellerCalendar = C.guard({
    roles: ['seller'],
    title: 'Marketing calendar — Super App',
    render() {
      const s = SL().byAccount(D().me().id);
      if (!s) return C.layout('seller-calendar', { title: 'Marketing calendar', body: `<div class="dash-card">${SA.ui.c.empty({ icon: 'lock', title: 'Sellers only', text: 'Sign in with a seller account.' })}</div>` });
      const l = SL().levelOf(s);
      const rank = RANK[l.calendar] ?? 0;
      const days = HORIZON[rank];
      const t0 = isoDate(today()), t1 = isoDate(addDays(today(), days));
      const events = [
        ...bigMatches(Math.min(days, 21)),
        ...OCCASIONS.filter(([d]) => d >= t0 && d <= t1).map(([date, title, kind, idea, expected]) => ({ date, title, kind, idea, expected })),
      ].sort((a, b) => a.date.localeCompare(b.date));
      const now = new Date();
      const months = [0, 1, 2].map((k) => { const m = (now.getMonth() + k) % 12; return { m, hi: CLIMATE[m][0], lo: CLIMATE[m][1], tip: weatherTip(CLIMATE[m][0]) }; });
      const tr = rank >= 2 ? trends() : null;
      const dayDiff = (d) => Math.round((parseISO(d) - today()) / DAY);
      const body = `
        <p class="small soft">Plan drops around the moments your buyers care about. Publish designs <b>7–10 days before</b> an event so they’re reviewed, live and shared in time. Showing the next <b>${days} days</b> for your level (${esc(l.name)}).</p>
        <section class="dash-card"><h2 class="h4">Coming up</h2>
          <ol class="mk-list">${events.map((e) => {
            const [cls, label, ic] = KIND[e.kind];
            const n = dayDiff(e.date);
            const early = rank >= 3 && n > 30;
            return `<li class="mk-item ${e.derby ? 'is-hot' : ''}"><span class="mk-date"><b class="num">${parseISO(e.date).getDate()}</b><span class="xs">${MON[parseISO(e.date).getMonth()]}</span></span>
              <div class="grow"><div class="row wrap" style="--gap:6px"><span class="badge ${cls}">${icon(ic, 11)} ${label}</span>${early ? `<span class="badge badge-ink">${icon('bolt', 11)} Early alert</span>` : ''}<span class="xs muted">${n === 0 ? 'Today' : n === 1 ? 'Tomorrow' : `in ${n} days`}${e.sub ? ` · ${esc(e.sub)}` : ''}</span></div>
                <b class="small" style="display:block;margin-top:4px">${esc(e.title)}</b><p class="xs soft">${esc(e.idea)}${e.expected ? ' Date depends on the moon sighting.' : ''}</p></div>
              ${n >= 3 ? `<a class="btn btn-ghost btn-sm" href="#/sell/studio" aria-label="Create a design for ${esc(e.title)}">${icon('plus', 13)} Design</a>` : ''}</li>`;
          }).join('') || '<li class="small soft">Nothing in this window.</li>'}</ol>
          ${rank < 3 ? `<p class="xs muted" style="margin-top:12px">${icon('lock', 12)} Elite sellers see 120 days ahead with early alerts.</p>` : ''}</section>
        ${rank >= 1 ? `<section class="dash-card"><div class="row-between"><h2 class="h4">${icon('sun', 18)} Weather in Cairo</h2><span class="xs muted">Monthly averages · live forecast coming soon</span></div>
          <div class="mk-weather">${months.map((w) => `<div class="mk-w"><span class="xs muted">${MON[w.m]}</span><b class="num">${w.hi}° <span class="muted">/ ${w.lo}°</span></b><b class="small">${w.tip[0]}</b><p class="xs soft">${w.tip[1]}</p></div>`).join('')}</div></section>`
          : locked('Weather planning', 'See which months are hoodie season and which are tee weather, so you push the right products.', 'Rising')}
        ${tr ? `<section class="dash-card"><div class="row-between"><h2 class="h4">${icon('trend', 18)} What’s selling on Super App</h2><span class="xs muted">${fmtNum(tr.total)} printed items · last 30 days · all sellers</span></div>
          <div class="mk-trends"><div><h3 class="xs muted">Top search tags</h3>${bars(tr.tags)}</div><div><h3 class="xs muted">Sections</h3>${bars(tr.cats)}</div><div><h3 class="xs muted">Products</h3>${bars(tr.products)}</div><div><h3 class="xs muted">Colours</h3>${bars(tr.colors)}</div></div></section>`
          : locked('What’s selling', 'Top tags, sections, products and colours across Super App — use them to pick your next design.', 'Pro')}`;
      return C.layout('seller-calendar', { title: 'Marketing calendar', subtitle: 'Matches, occasions, weather and trends to plan your next drop', body });
    },
  });
})(window.SA);
