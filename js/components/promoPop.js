/**
 * Pop-up ads for Super App's own services and offers: become a seller, fantasy, discount codes, the shop, pitches,
 * tournaments… One small card at a time slides in with a 3D flip, tilts toward the pointer (layers at different depths)
 * and carries a 3D object: a spinning cube with icons, a discount coin with thickness, or the real 3D T-shirt (three.js).
 *
 * Rhythm (set in the admin console, Site content → Pop-ups): first one after a few seconds, then one every `interval`
 * seconds, each stays `stay` seconds, at most `max` per visit. Never on checkout, dashboards or the studio, never over a
 * dialog, never while the tab is hidden, never an ad for the page you're on. "Don't show offers" mutes them for 7 days.
 * Respects reduced motion (no spin, a plain fade).
 */
(function (SA) {
  const { esc } = SA.core.utils;
  const { icon } = SA.ui;
  const C = () => SA.services.content;
  const MUTE = 'superapp.promo.mute', SEEN = 'superapp.promo.session';
  const QUIET = /^\/(dash|seller|admin|factory|ceo|checkout|cart|book|sell\/studio|sell\/bulk|order|auth)(\/|$)/;
  const reduced = () => !SA.core.motion?.enabled?.() || matchMedia('(prefers-reduced-motion: reduce)').matches;
  const ls = { get: (k) => { try { return localStorage.getItem(k); } catch { return null; } }, set: (k, v) => { try { localStorage.setItem(k, v); } catch { /* private mode */ } } };
  const ss = { get: (k) => { try { return JSON.parse(sessionStorage.getItem(k)) || null; } catch { return null; } }, set: (k, v) => { try { sessionStorage.setItem(k, JSON.stringify(v)); } catch { /* private mode */ } } };

  /* ---------- 3D objects (CSS 3D; the T-shirt is a real model) ---------- */
  const cube = (p) => {
    const ic = p.icons?.length ? p.icons : [p.icon || 'sparkle'];
    const faces = ['front', 'back', 'right', 'left', 'top', 'bottom'];
    return `<div class="pp-cube" aria-hidden="true">${faces.map((f, i) => `<span class="pp-face pp-${f}">${icon(ic[i % ic.length], 30)}</span>`).join('')}</div>`;
  };
  const coin = (p) => `<div class="pp-coin" aria-hidden="true">${Array.from({ length: 10 }, (_, i) => `<i style="--z:${i - 5}px"></i>`).join('')}
    <span class="pp-coin-face pp-coin-f"><b>${esc(p.badge || '%')}</b></span><span class="pp-coin-face pp-coin-b">${icon(p.icon || 'tag', 26)}</span></div>`;
  const object = (p) => (p.shape === 'coin' ? coin(p) : p.shape === 'tee' ? '<div class="pp-tee" data-pp-tee aria-hidden="true"></div>' : cube(p));
  const sparks = () => `<div class="pp-sparks" aria-hidden="true">${Array.from({ length: 7 }, (_, i) => `<i style="--i:${i}"></i>`).join('')}</div>`;

  let root = null, timer = 0, hideT = 0, current = null, viewer = null, shown = 0, last = '';

  const eligible = () => {
    const cfg = C().promoPops();
    if (!cfg.on || ls.get(MUTE) > Date.now()) return [];
    const path = SA.core.router.location().split('?')[0];
    if (QUIET.test(path)) return [];
    const seller = !!SA.services.sellers?.mine?.();
    const closed = ss.get(SEEN)?.closed || [];
    return cfg.items.filter((p) => p.active && !closed.includes(p.id) && !(p.forNonSellers && seller)
      && !(p.link && path !== '/' && p.link.replace(/^#/, '').split('?')[0] === path));
  };
  const busy = () => document.hidden || !!document.querySelector('#overlay-root .modal, #overlay-root .drawer, .wa.is-open, .cookie-bar') || document.querySelector('input:focus, textarea:focus, select:focus');

  const schedule = (sec) => { clearTimeout(timer); timer = setTimeout(next, sec * 1000); };
  const next = () => {
    const cfg = C().promoPops();
    const sess = ss.get(SEEN) || { n: 0, closed: [] };
    if (sess.n >= cfg.max) return; // enough for this visit
    if (busy()) return schedule(4);
    const list = eligible();
    if (!list.length) return schedule(cfg.interval);
    // Round-robin, never the same one twice in a row
    const i = (list.findIndex((p) => p.id === last) + 1) % list.length;
    show(list[i]);
    ss.set(SEEN, { ...sess, n: sess.n + 1 });
  };

  const show = (p) => {
    const cfg = C().promoPops();
    current = p; last = p.id; shown += 1;
    C().promoStat(p.id, 'shown');
    root.querySelectorAll('.pp:not(.is-out)').forEach((n) => n.remove());
    root.insertAdjacentHTML('beforeend', `<div class="pp ${reduced() ? 'is-still' : ''}" role="dialog" aria-live="polite" aria-label="${esc(p.title)}" style="--pp-c:${esc(p.color || '#E8590C')}">
      <div class="pp-card">
        <button class="pp-x icon-btn" data-pp-x aria-label="Close">${icon('x', 14)}</button>
        <div class="pp-stage">${sparks()}<div class="pp-obj">${object(p)}</div></div>
        <div class="pp-body">
          ${p.badge && p.shape !== 'coin' ? `<span class="pp-badge">${esc(p.badge)}</span>` : ''}
          <b class="pp-title">${esc(p.title)}</b>
          <p class="pp-text">${esc(p.text)}</p>
          <div class="pp-actions"><a class="btn btn-sm pp-cta" href="${esc(p.link || '#/')}" data-pp-go>${esc(p.cta || 'See more')} ${icon('arrowRight', 14)}</a>
            <button class="pp-mute xs" data-pp-mute>Don’t show offers</button></div>
        </div>
        <i class="pp-timer" style="animation-duration:${cfg.stay}s"></i>
      </div></div>`);
    const card = root.querySelector('.pp:not(.is-out)');
    requestAnimationFrame(() => card.classList.add('is-in'));
    if (p.shape === 'tee' && SA.ui.model3d?.has('tee')) {
      viewer = SA.ui.model3d.mount(card.querySelector('[data-pp-tee]'), { kind: 'tee', hex: p.teeColor || '#141414', art: SA.data.pod.art[p.teeArt || 'nights'], place: { front: { x: 0.5, y: 0.38, w: 0.5 }, back: null }, artRatio: 1, spin: true, hint: false, label: p.title });
    } else if (p.shape === 'tee') card.querySelector('[data-pp-tee]').outerHTML = cube({ ...p, icons: ['shirt', 'sparkle', 'package', 'wallet'] });
    // Tilt toward the pointer: the card turns in 3D and its layers sit at different depths
    const c = card.querySelector('.pp-card');
    if (!reduced()) {
      c.addEventListener('pointermove', (e) => { const r = c.getBoundingClientRect(); const x = (e.clientX - r.left) / r.width - 0.5, y = (e.clientY - r.top) / r.height - 0.5; c.style.setProperty('--rx', `${(-y * 14).toFixed(2)}deg`); c.style.setProperty('--ry', `${(x * 18).toFixed(2)}deg`); });
      c.addEventListener('pointerleave', () => { c.style.setProperty('--rx', '0deg'); c.style.setProperty('--ry', '0deg'); });
    }
    // Hovering keeps it open
    let left = cfg.stay * 1000, t0 = Date.now();
    const arm = () => { clearTimeout(hideT); t0 = Date.now(); hideT = setTimeout(() => hide(), left); };
    c.addEventListener('pointerenter', () => { clearTimeout(hideT); left = Math.max(1500, left - (Date.now() - t0)); card.classList.add('is-held'); });
    c.addEventListener('pointerleave', () => { card.classList.remove('is-held'); arm(); });
    arm();
  };

  const hide = (then = true) => {
    clearTimeout(hideT);
    const card = root?.querySelector('.pp:not(.is-out)'), v = viewer;
    viewer = null; current = null;
    // Remove only this card: a new one may already be showing (admin preview, next ad)
    const done = () => { v?.destroy?.(); card?.remove(); if (then) schedule(C().promoPops().interval); };
    if (!card) return done();
    card.classList.remove('is-in'); card.classList.add('is-out');
    setTimeout(done, reduced() ? 150 : 520);
  };

  const init = () => {
    root = document.createElement('div');
    root.className = 'pp-root';
    document.body.appendChild(root);
    root.addEventListener('click', (e) => {
      if (e.target.closest('[data-pp-x]')) { const s = ss.get(SEEN) || { n: 0, closed: [] }; if (current) ss.set(SEEN, { ...s, closed: [...(s.closed || []), current.id] }); return hide(); }
      if (e.target.closest('[data-pp-mute]')) { ls.set(MUTE, String(Date.now() + 7 * 864e5)); hide(false); SA.ui.overlay.toast('OK — no offers for 7 days.'); return; }
      if (e.target.closest('[data-pp-go]') && current) { C().promoStat(current.id, 'clicked'); hide(false); schedule(C().promoPops().interval * 3); }
    });
    // A page change hides an ad that's now out of place (checkout, dashboards…) and keeps the rhythm going
    SA.core.router.onChange(() => { if (current && QUIET.test(SA.core.router.location())) hide(); });
    document.addEventListener('visibilitychange', () => { if (!document.hidden && !current) schedule(3); });
    schedule(C().promoPops().first);
  };

  SA.ui.promoPop = { init, show: (id) => { const p = C().promoPops().items.find((x) => x.id === id); if (p) { hide(false); show(p); } }, hide };
})(window.SA);
