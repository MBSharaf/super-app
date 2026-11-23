/** Shared renderers for Matches: crests and match rows. */
(function (SA) {
  const { esc } = SA.core.utils;
  const { icon } = SA.ui;

  /** Club badge when the data has one (t.logo), drawn over the monogram crest so a badge that fails to load leaves the crest. */
  const logo = (t, size, fallback) => (t && t.logo && SA.config.clubLogos
    ? `<span class="club-logo" style="width:${size}px;height:${size}px"><img src="${esc(t.logo)}" alt="" width="${size}" height="${size}" loading="lazy" decoding="async" referrerpolicy="no-referrer" />${fallback}</span>`
    : fallback);

  /** Club crest: the real badge when there is one, else a split shield in club colours with a monogram. */
  const crest = (t, size = 36) => logo(t, size, monogram(t, size));

  /** Club in fantasy lists (fixtures, table): its badge, else its shirt. */
  const badgeOrKit = (t, size = 30, gk = false) => logo(t, size, SA.ui.c.kit(t, size, gk));

  /**
   * A player's picture in fantasy: his official headshot when the data has one (the light 40×40 copy up to 60px), over a
   * disc in club colours, with his club badge in the corner. No photo (the demo) or photos switched off → the club
   * badge, else the shirt. A photo that cannot load is removed, which shows the badge under it.
   */
  const face = (p, t, size = 34, gk = false) => {
    const fallback = badgeOrKit(t, size, gk);
    if (!p || !p.photo || !SA.config.clubLogos) return fallback;
    const src = size <= 60 ? p.photo.replace('/110x140/', '/40x40/') : p.photo;
    const club = t && t.logo && size >= 30 ? `<span class="pl-face-club">${logo(t, Math.round(size * 0.4), '')}</span>` : '';
    return `<span class="pl-face" style="--s:${size}px;--kc:${t ? esc(t.kit.a) : '#1F2A24'}">${fallback}<img class="pl-face-img" src="${esc(src)}" alt="" width="${size}" height="${size}" loading="lazy" decoding="async" referrerpolicy="no-referrer" />${club}</span>`;
  };

  /** League logos by competition id (server keys and demo ids), from the API-Football image CDN. */
  const LEAGUE_IDS = { epl: 39, pl: 39, egy: 233, 'epl-eg': 233, 'caf-cl': 12, ucl: 2, laliga: 140, seriea: 135, bundesliga: 78, ligue1: 61,
    superlig: 203, uel: 3, uecl: 848, cafcl: 12, cafcc: 20, afcon: 6, 'afcon-q': 6, euro: 4, wc: 1, unl: 5 };
  const leagueLogo = (comp) => (comp && LEAGUE_IDS[comp.id] && SA.config.clubLogos ? `https://media.api-sports.io/football/leagues/${LEAGUE_IDS[comp.id]}.png` : null);
  /** The league's mark: its logo on a white tile, else its colour with a three-letter code (also when the logo fails). */
  const compBadge = (comp) => {
    const src = leagueLogo(comp);
    return `<span class="comp-badge${src ? ' has-logo' : ''}">${src ? `<img class="comp-logo" src="${esc(src)}" alt="" loading="lazy" decoding="async" referrerpolicy="no-referrer" />` : ''}<span class="comp-abbr">${esc(String(comp.short || comp.name).split(' ')[0].slice(0, 3).toUpperCase())}</span></span>`;
  };
  /** Small league mark in a filter chip: the logo, else the coloured dot. */
  const compDot = (comp) => {
    const src = leagueLogo(comp);
    return src ? `<img class="chip-logo" src="${esc(src)}" alt="" width="20" height="20" loading="lazy" decoding="async" referrerpolicy="no-referrer" data-dot="${esc(comp.color)}" />` : `<i class="cat-dot" style="background:${esc(comp.color)}"></i>`;
  };

  /** A small badge before a club name (nothing when the club has no badge). */
  const mini = (t, size = 14) => (t && t.logo && SA.config.clubLogos ? `${logo(t, size, '')} ` : '');

  const monogram = (t, size) => {
    const light = (hex) => { const n = parseInt(hex.slice(1), 16); return ((n >> 16) * 299 + ((n >> 8) & 255) * 587 + (n & 255) * 114) / 1000 > 165; };
    const ink = light(t.a) ? '#0D1411' : '#FFFFFF';
    const id = 'c' + Math.random().toString(36).slice(2, 7);
    return `<svg class="crest" width="${size}" height="${size}" viewBox="0 0 40 44" aria-hidden="true">
      <defs><clipPath id="${id}"><path d="M20 2 L37 7 V21 C37 32 29 39 20 42 C11 39 3 32 3 21 V7 Z"/></clipPath></defs>
      <g clip-path="url(#${id})"><rect width="40" height="44" fill="${t.a}"/><path d="M40 0 V44 H22 Z" fill="${t.b}" opacity=".9"/><rect width="40" height="10" y="34" fill="rgba(0,0,0,.12)"/></g>
      <path d="M20 2 L37 7 V21 C37 32 29 39 20 42 C11 39 3 32 3 21 V7 Z" fill="none" stroke="rgba(13,20,17,.25)" stroke-width="1.2"/>
      <text x="20" y="25" text-anchor="middle" font-family="Archivo, sans-serif" font-weight="900" font-size="${t.short.length > 3 ? 8.5 : 10.5}" fill="${ink}" style="font-variation-settings:'wdth' 110">${esc(t.short)}</text></svg>`;
  };


  const matchRow = (m) => {
    const live = m.status === 'live', fin = m.status === 'finished';
    return `<article class="mrow ${live ? 'is-live' : ''} ${m.isDerby ? 'is-derby' : ''}" data-match="${m.id}" tabindex="0" role="button" aria-label="${esc(m.homeT.name)} vs ${esc(m.awayT.name)}, ${live ? 'live ' + m.label : fin ? 'full time' : 'kick-off ' + m.label}">
      <div class="mrow-team home"><span class="truncate">${esc(m.homeT.name)}</span>${crest(m.homeT, 34)}</div>
      <div class="mrow-mid">
        ${m.status === 'upcoming' ? `<span class="mrow-time num">${m.label}</span>` : `<span class="mrow-score num" data-score><b>${m.hs}</b><i>–</i><b>${m.as}</b></span>`}
        <span class="mrow-state ${live ? 'live' : ''}" data-minute>${live ? `<span class="live-dot"></span>${m.label}` : fin ? 'Full time' : m.round.split('·').pop().trim()}</span>
      </div>
      <div class="mrow-team away">${crest(m.awayT, 34)}<span class="truncate">${esc(m.awayT.name)}</span></div>
      <div class="mrow-extra">
        ${m.egyptians.length ? `<span class="egy-chip" title="Egyptian player in this match">${esc(m.egyptians.join(', '))}</span>` : ''}
        ${m.status === 'upcoming' ? `<button class="bell ${m.reminder ? 'is-on' : ''}" data-remind="${m.id}" aria-pressed="${m.reminder}" aria-label="${m.reminder ? 'Remove reminder' : 'Remind me'}">${icon('bell', 16)}</button>` : ''}
      </div>
    </article>`;
  };

  // A badge that cannot load is removed, which shows the crest or shirt drawn under it
  document.addEventListener('error', (e) => {
    const img = e.target;
    if (img instanceof HTMLImageElement && img.classList.contains('comp-logo')) { img.parentElement?.classList.remove('has-logo'); img.remove(); return; }
    if (img instanceof HTMLImageElement && img.classList.contains('chip-logo')) { const i = document.createElement('i'); i.className = 'cat-dot'; i.style.background = img.dataset.dot || ''; img.replaceWith(i); return; }
  }, true);
  document.addEventListener('error', (e) => { if (e.target instanceof HTMLImageElement && (e.target.parentElement?.classList.contains('club-logo') || e.target.classList.contains('pl-face-img'))) e.target.remove(); }, true);

  SA.ui.sports = { crest, badgeOrKit, mini, face, matchRow, leagueLogo, compBadge, compDot };
})(window.SA);
