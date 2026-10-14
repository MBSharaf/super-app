/** Presentational primitives. Pure functions → HTML strings (1:1 with future React components). */
(function (SA) {
  const { esc, money, initials, countdownParts } = SA.core.utils;
  const { icon } = SA.ui;
  const media = SA.data.media;

  const AV_COLORS = ['#0B6E4F', '#2459D6', '#B4421E', '#6D28D9', '#0E7490', '#9D174D', '#4D7C0F', '#334155'];

  const ui = {
    stars(rating, size = 14) {
      return `<span class="stars" aria-label="${rating} out of 5">${[1, 2, 3, 4, 5].map((i) => icon(rating >= i - 0.25 ? 'star' : 'starOutline', size, 1.6)).join('')}</span>`;
    },
    rating(rating, count, size = 14) {
      return `<span class="rating">${icon('star', size)}<span>${Number(rating).toFixed(1)}</span>${count != null ? `<span class="count">(${count.toLocaleString()})</span>` : ''}</span>`;
    },
    avatar({ name, avatar, photo = true }, size = '') {
      const cls = `avatar ${size ? 'avatar-' + size : ''}`;
      if (photo && avatar != null) return `<span class="${cls}"><img src="${media.avatar(avatar, 160)}" alt="" loading="lazy" /></span>`;
      const c = AV_COLORS[(name || '').length % AV_COLORS.length];
      return `<span class="${cls}" style="background:${c}" aria-hidden="true">${esc(initials(name || '?'))}</span>`;
    },
    avatarStack(people, max = 4) {
      const shown = people.slice(0, max).map((p) => ui.avatar(p, 'sm')).join('');
      const more = people.length > max ? `<span class="avatar avatar-sm more">+${people.length - max}</span>` : '';
      return `<span class="avatar-stack">${shown}${more}</span>`;
    },
    price(price, was) {
      const off = was && was > price ? Math.round((1 - price / was) * 100) : 0;
      return `<span class="price"><span>${money(price)}</span>${off ? `<s>${money(was)}</s><span class="off">−${off}%</span>` : ''}</span>`;
    },
    empty({ icon: ic = 'info', title, text, action = '' }) {
      return `<div class="empty"><div class="empty-icon">${icon(ic, 28)}</div><h3 class="h4">${esc(title)}</h3>${text ? `<p>${text}</p>` : ''}${action}</div>`;
    },
    /** Signature CTA: ink pill + blaze disc */
    cta(label, { href, attrs = '', variant = 'primary', size = '', ic = 'arrowRight' } = {}) {
      const cls = `btn btn-${variant} btn-cta ${size ? 'btn-' + size : ''}`;
      const inner = `<span>${label}</span><span class="btn-disc">${icon(ic, 16, 2.2)}</span>`;
      return href ? `<a class="${cls}" href="${href}" ${attrs}>${inner}</a>` : `<button class="${cls}" ${attrs}>${inner}</button>`;
    },
    spots(filled, total) {
      return `<span class="spots" aria-label="${filled} of ${total} players">${Array.from({ length: total }, (_, i) => `<i class="${i < filled ? '' : 'open'}"></i>`).join('')}</span>`;
    },
    steps(list, current) {
      return `<ol class="steps">${list.map((s, i) => `<li class="${i < current ? 'is-done' : i === current ? 'is-current' : ''}"><span class="step-n">${i < current ? icon('check', 13, 2.6) : i + 1}</span>${esc(s)}</li>`).join('')}</ol>`;
    },
    countdown(target, { compact = false } = {}) {
      const c = countdownParts(target);
      if (compact) return `<span class="num" data-countdown="${new Date(target).toISOString()}" data-compact="1">${c.d ? c.d + 'd ' : ''}${c.h}h ${String(c.m).padStart(2, '0')}m</span>`;
      return `<span class="countdown" data-countdown="${new Date(target).toISOString()}">${[['d', 'days'], ['h', 'hrs'], ['m', 'min'], ['s', 'sec']].map(([k, l]) => `<span><b data-k="${k}">${String(c[k]).padStart(2, '0')}</b><small>${l}</small></span>`).join('')}</span>`;
    },
    /** Live-updates every [data-countdown] inside root. Returns cleanup. */
    tickCountdowns(root) {
      const t = setInterval(() => {
        root.querySelectorAll('[data-countdown]').forEach((el) => {
          const c = countdownParts(el.dataset.countdown);
          if (el.dataset.compact) el.textContent = `${c.d ? c.d + 'd ' : ''}${c.h}h ${String(c.m).padStart(2, '0')}m`;
          else el.querySelectorAll('[data-k]').forEach((b) => (b.textContent = String(c[b.dataset.k]).padStart(2, '0')));
        });
      }, 1000);
      return () => clearInterval(t);
    },
    /** Club shirt (original vector — no club crests or real kit artwork). */
    kit(team, size = 44, gk = false) {
      const k = gk ? { a: '#1F2937', b: team.kit.a, p: 'solid' } : team.kit;
      const id = 'k' + Math.random().toString(36).slice(2, 7);
      const body = 'M14 6 L22 3 Q32 8 42 3 L50 6 L60 16 L52 25 L48 22 L48 60 L16 60 L16 22 L12 25 L4 16 Z';
      const pat = {
        solid: '',
        stripes: `<g clip-path="url(#${id})">${[18, 28, 38].map((x) => `<rect x="${x}" y="0" width="5" height="64" fill="${k.b}"/>`).join('')}</g>`,
        hoops: `<g clip-path="url(#${id})">${[24, 36, 48].map((y) => `<rect x="0" y="${y}" width="64" height="5" fill="${k.b}"/>`).join('')}</g>`,
        bands: `<g clip-path="url(#${id})"><rect x="0" y="26" width="64" height="4" fill="${k.b}"/><rect x="0" y="33" width="64" height="4" fill="${k.b}"/></g>`,
        sash: `<g clip-path="url(#${id})"><path d="M10 10 L22 6 L56 60 L44 64 Z" fill="${k.b}"/></g>`,
        half: `<g clip-path="url(#${id})"><rect x="32" y="0" width="32" height="64" fill="${k.b}"/></g>`,
      }[k.p];
      return `<svg class="kit" width="${size}" height="${size}" viewBox="0 0 64 64" aria-hidden="true"><defs><clipPath id="${id}"><path d="${body}"/></clipPath><linearGradient id="${id}g" x1="0" x2="0" y1="0" y2="1"><stop offset="0" stop-color="#fff" stop-opacity=".22"/><stop offset="1" stop-color="#000" stop-opacity=".12"/></linearGradient></defs>
        <path d="${body}" fill="${k.a}" stroke="rgba(0,0,0,.22)" stroke-width="1.2"/>${pat}<path d="${body}" fill="url(#${id}g)"/>
        <path d="M24 4.2 Q32 10 40 4.2" fill="none" stroke="${k.p === 'solid' ? k.b : 'rgba(0,0,0,.35)'}" stroke-width="2.4"/></svg>`;
    },
    statusDot(status) {
      if (status === 'injured') return `<span class="badge badge-danger" title="Injured">Out</span>`;
      if (status === 'doubt') return `<span class="badge badge-gold" title="Doubtful">75%</span>`;
      return '';
    },
    fdr(n) { return `<span class="fdr fdr-${n}">${n}</span>`; },
    skeletonCards(n = 3, h = 280) { return Array.from({ length: n }, () => `<div class="skeleton" style="height:${h}px;border-radius:16px"></div>`).join(''); },
    pageHead({ crumbs = [], title, text = '', actions = '', eyebrow = '' }) {
      return `<div class="page-head container">
        ${crumbs.length ? `<nav class="crumbs" aria-label="Breadcrumb">${crumbs.map(([l, h], i) => (h ? `<a href="${h}">${esc(l)}</a>` : `<span aria-current="page">${esc(l)}</span>`) + (i < crumbs.length - 1 ? icon('chevronRight', 14) : '')).join('')}</nav>` : ''}
        <div class="row-between wrap" style="align-items:flex-end;margin-top:${crumbs.length ? 10 : 0}px">
          <div>${eyebrow}<h1 class="h1">${title}</h1>${text ? `<p class="lead">${text}</p>` : ''}</div>
          ${actions ? `<div class="row wrap">${actions}</div>` : ''}
        </div></div>`;
    },
    facilityIcon(key) {
      return { lights: 'lights', parking: 'parking', showers: 'shower', lockers: 'locker', cafe: 'coffee', water: 'water', bibs: 'shirt', ac: 'ac', wc: 'wc', prayer: 'prayer', camera: 'camera', wifi: 'wifi', firstaid: 'firstaid', seats: 'seats' }[key] || 'check';
    },
  };

  SA.ui.c = ui;
})(window.SA);
