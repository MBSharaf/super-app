/** Shared renderers for News and Matches: crests, news cards, match rows. */
(function (SA) {
  const { esc, timeAgo, fmtNum } = SA.core.utils;
  const { icon } = SA.ui;
  const media = SA.data.media;

  /** Original crest: split shield in club colours with a monogram (no official badges). */
  const crest = (t, size = 36) => {
    const light = (hex) => { const n = parseInt(hex.slice(1), 16); return ((n >> 16) * 299 + ((n >> 8) & 255) * 587 + (n & 255) * 114) / 1000 > 165; };
    const ink = light(t.a) ? '#0D1411' : '#FFFFFF';
    const id = 'c' + Math.random().toString(36).slice(2, 7);
    return `<svg class="crest" width="${size}" height="${size}" viewBox="0 0 40 44" aria-hidden="true">
      <defs><clipPath id="${id}"><path d="M20 2 L37 7 V21 C37 32 29 39 20 42 C11 39 3 32 3 21 V7 Z"/></clipPath></defs>
      <g clip-path="url(#${id})"><rect width="40" height="44" fill="${t.a}"/><path d="M40 0 V44 H22 Z" fill="${t.b}" opacity=".9"/><rect width="40" height="10" y="34" fill="rgba(0,0,0,.12)"/></g>
      <path d="M20 2 L37 7 V21 C37 32 29 39 20 42 C11 39 3 32 3 21 V7 Z" fill="none" stroke="rgba(13,20,17,.25)" stroke-width="1.2"/>
      <text x="20" y="25" text-anchor="middle" font-family="Archivo, sans-serif" font-weight="900" font-size="${t.short.length > 3 ? 8.5 : 10.5}" fill="${ink}" style="font-variation-settings:'wdth' 110">${esc(t.short)}</text></svg>`;
  };

  const typeBadge = (s) => s.video ? `<span class="nbadge nb-video">${icon('play', 11, 2.4)} ${s.video}</span>` : `<span class="nbadge nb-${s.type.toLowerCase()}">${s.type === 'TV' ? 'TV' : s.type === 'Club' ? 'Club statement' : 'Press'}</span>`;

  const newsCard = (s, { size = '' } = {}) => `
    <article class="news-card ${size}" style="--cat:${s.category.color}">
      <a class="news-card-link" href="#/news/${s.id}" aria-label="${esc(s.title)}"></a>
      <div class="media news-media"><img src="${media.photo(s.img, size === 'lg' ? 1200 : 640, size === 'lg' ? 760 : 420)}" alt="" loading="lazy" />
        ${s.video ? `<span class="play-btn" aria-hidden="true">${icon('play', 20, 2.4)}</span>` : ''}
        <span class="news-cat">${esc(s.category.name)}</span></div>
      <div class="news-body">
        <div class="row" style="--gap:8px">${typeBadge(s)}${s.breaking ? '<span class="nbadge nb-breaking"><span class="live-dot"></span>Breaking</span>' : ''}</div>
        <h3 class="news-title">${esc(s.title)}</h3>
        ${size !== 'sm' ? `<p class="news-excerpt">${esc(s.excerpt)}</p>` : ''}
        <p class="news-meta xs">${timeAgo(s.at)} · ${s.read} min read · ${fmtNum(s.views)} views</p>
      </div>
    </article>`;

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

  SA.ui.sports = { crest, newsCard, matchRow, typeBadge };
})(window.SA);
