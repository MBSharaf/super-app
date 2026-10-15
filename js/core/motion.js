/**
 * Motion engine.
 *  - enhance(root): runs after every page render — assigns scroll reveals with stagger, splits headings
 *    into animated words, grows meters/bars, counts numbers up, registers parallax.
 *  - init(): global behaviours — scroll progress, hide-on-scroll header, ripple, magnetic buttons,
 *    3D card tilt with glare, sliding nav indicators.
 * Motion is on by default. People can switch to reduced motion in the footer or profile
 * (html.reduce-motion), and the choice is remembered on this device.
 */
(function (SA) {
  const KEY = 'sa.motion';
  const root = document.documentElement;
  const enabled = () => !root.classList.contains('reduce-motion');
  const fine = () => matchMedia('(pointer: fine)').matches;

  const getPref = () => { try { return localStorage.getItem(KEY) || 'full'; } catch { return 'full'; } };
  const setPref = (v) => { try { localStorage.setItem(KEY, v); } catch { /* ignore */ } root.classList.toggle('reduce-motion', v === 'reduced'); };
  setPref(getPref());

  /* ---------------- Scroll reveal ---------------- */
  // selector → animation variant (see css/motion.css)
  const AUTO = [
    ['.sec-head', 'up'], ['.page-head', 'up'],
    ['.pitch-card', 'rise'], ['.match-card', 'slide'], ['.product-card', 'rise'], ['.service-tile', 'rise'],
    ['.league-card', 'flip'], ['.booking-row', 'slide'], ['.review', 'up'], ['.account-tile', 'rise'],
    ['.panel', 'up'], ['.card:not(.pitch-card):not(.match-card):not(.booking-row):not(.account-tile)', 'up'],
    ['.fixture', 'slide'], ['.table tbody tr', 'fade-left'], ['.roster li', 'pop'], ['.facility-grid li', 'pop'],
    ['.spec-grid > div', 'up'], ['.cat-tile', 'pop'], ['.fb-players li', 'fade-left'], ['.mini-bookings li', 'fade-left'],
    ['.tx-list li', 'fade-left'], ['.prize-list li', 'fade-left'], ['.fx-steps li', 'fade-left'], ['.flow-step', 'pop'],
    ['.gallery', 'zoom'], ['.match-hero', 'zoom'], ['.pdp-gallery', 'zoom'], ['.shop-hero-art', 'zoom'],
    ['.promo', 'zoom'], ['.ticket', 'ticket'], ['.wallet-card', 'flip'], ['.profile-hero', 'up'],
    ['.fantasy-band', 'zoom'], ['.stats-strip > div', 'up'], ['.next-up', 'up'], ['.ptoken', 'drop'], ['.sq-slot', 'pop'],
    ['.loc-prompt', 'up'], ['.notice', 'up'], ['.empty', 'up'], ['.gw-bars', 'up'], ['.fx-stats > div', 'up'],
    ['.cart-line', 'slide'], ['.co-items li', 'fade-left'], ['.track li', 'pop'], ['.ticker', 'fade'],
    ['.kpi', 'rise'], ['.dash-card', 'up'], ['.dp-card', 'rise'], ['.av-slot', 'pop'], ['.dl-acc', 'fade-left'], ['.pocket', 'up'], ['.flip-card', 'flip'], ['.activity li', 'fade-left'], ['.next-list li', 'fade-left'], ['.tb-row', 'fade-left'], ['.store-hero', 'zoom'], ['.money-split', 'up'], ['.platform-card', 'flip'],
    ['.news-card', 'rise'], ['.most-read li', 'fade-left'], ['.derby-item', 'up'], ['.motd', 'flip'], ['.comp-block', 'up'], ['.mrow', 'slide'], ['.article-body p', 'up'], ['.home-match', 'rise'],
  ];

  let io;
  const observer = () => io ||= new IntersectionObserver((entries) => {
    entries.forEach((e) => {
      if (!e.isIntersecting) return;
      const el = e.target;
      el.classList.add('is-in');
      // Once revealed, drop the reveal classes so hover/tilt transitions aren't slowed by the stagger delay
      if (el.classList.contains('reveal')) {
        const delay = parseFloat(el.style.getPropertyValue('--d')) || 0;
        setTimeout(() => {
          el.classList.remove('reveal', ...[...el.classList].filter((c) => c.startsWith('rv-')));
          el.style.removeProperty('--d');
        }, delay + 1150);
      }
      if (el.dataset.countup !== undefined) runCount(el);
      io.unobserve(el);
    });
  }, { rootMargin: '0px 0px -6% 0px', threshold: 0.06 });

  const assignReveals = (scope) => {
    const seen = new Set();
    AUTO.forEach(([sel, variant]) => {
      scope.querySelectorAll(sel).forEach((el) => {
        if (seen.has(el) || el.classList.contains('reveal') || el.classList.contains('is-in') || el.closest('.modal, .drawer, .no-motion')) return;
        // Don't double-animate nested matches (e.g. a .card inside a revealed .panel)
        if (el.parentElement?.closest('.reveal:not(.is-in)')) return;
        seen.add(el);
        el.classList.add('reveal', `rv-${variant}`);
      });
    });
    // Stagger siblings: each revealed element gets a delay by its index among revealed siblings
    const groups = new Map();
    scope.querySelectorAll('.reveal:not(.is-in)').forEach((el) => {
      const p = el.parentElement; if (!groups.has(p)) groups.set(p, []);
      groups.get(p).push(el);
    });
    groups.forEach((list) => list.forEach((el, i) => { if (!el.style.getPropertyValue('--d')) el.style.setProperty('--d', `${Math.min(i, 10) * 70}ms`); }));
  };

  const reveal = (scope = document) => {
    if (!enabled()) { scope.querySelectorAll('.reveal, .split, .grow-in').forEach((el) => el.classList.add('is-in')); return; }
    const obs = observer();
    scope.querySelectorAll('.reveal:not(.is-in), .split:not(.is-in), .grow-in:not(.is-in), [data-countup]:not(.counted)').forEach((el) => obs.observe(el));
  };

  /* ---------------- Split headings into words ---------------- */
  const splitText = (scope) => {
    scope.querySelectorAll('.h1, .h2, .display:not(.hero-title), .fx-title, .gw-big').forEach((el) => {
      if (el.classList.contains('split') || el.closest('.modal, .drawer, .float-card, .no-motion')) return;
      let i = 0;
      const walk = (node) => {
        [...node.childNodes].forEach((n) => {
          if (n.nodeType === 3) {
            const frag = document.createDocumentFragment();
            // Translate the whole phrase before splitting it into words (the translator works on whole text nodes)
            let text = n.textContent;
            if (SA.i18n?.isAr && /[A-Za-z]/.test(text)) { const core = text.trim(); text = text.replace(core, () => SA.i18n.t(core)); }
            text.split(/(\s+)/).forEach((part) => {
              if (!part) return;
              if (/^\s+$/.test(part)) { frag.appendChild(document.createTextNode(part)); return; }
              const w = document.createElement('span'); w.className = 'w';
              const inner = document.createElement('span'); inner.className = 'wi'; inner.style.setProperty('--i', i++);
              inner.textContent = part; w.appendChild(inner); frag.appendChild(w);
              if (SA.i18n?.isAr) w.setAttribute('translate', 'no'); // already translated as a whole phrase
            });
            n.replaceWith(frag);
          } else if (n.nodeType === 1 && !n.matches('svg, img, .w')) walk(n);
        });
      };
      walk(el);
      el.classList.add('split');
    });
  };

  /* ---------------- Meters and bars grow from zero ---------------- */
  const growables = (scope) => {
    scope.querySelectorAll('.meter, .gw-bars, .gw-compare, .bars, .spots, .ticket-code').forEach((el) => {
      if (!el.closest('.modal, .drawer, .no-motion')) el.classList.add('grow-in');
    });
  };

  /* ---------------- Count-up numbers ---------------- */
  const runCount = (el) => {
    if (el.classList.contains('counted')) return;
    el.classList.add('counted');
    const target = parseFloat(el.dataset.countup), dec = +el.dataset.decimals || 0;
    const prefix = el.dataset.prefix || '', suffix = el.dataset.suffix || '';
    const fmt = (v) => prefix + v.toLocaleString('en-US', { minimumFractionDigits: dec, maximumFractionDigits: dec }) + suffix;
    if (!enabled()) { el.textContent = fmt(target); return; }
    const t0 = performance.now(), dur = +el.dataset.dur || 1400;
    const step = (t) => { const p = Math.min(1, (t - t0) / dur); el.textContent = fmt(target * (1 - Math.pow(1 - p, 4))); if (p < 1) requestAnimationFrame(step); };
    requestAnimationFrame(step);
  };
  /** Mark plain numbers for count-up: <b class="num" data-count>1,250</b> */
  const autoCount = (scope) => {
    scope.querySelectorAll('.fx-stats b, .profile-stats b, .wallet-bal, .stats-strip b, .gw-big, .pl-kpis b, .sq-summary b, .ticket-id').forEach((el) => {
      if (el.dataset.countup !== undefined || el.querySelector('*')) return;
      const m = el.textContent.trim().match(/^([^\d-]*)(-?[\d,]+(?:\.\d+)?)(.*)$/);
      if (!m || el.classList.contains('ticket-id')) return;
      const n = parseFloat(m[2].replace(/,/g, ''));
      if (!isFinite(n) || n === 0) return;
      el.dataset.countup = n; el.dataset.prefix = m[1]; el.dataset.suffix = m[3];
      el.dataset.decimals = (m[2].split('.')[1] || '').length;
    });
  };

  /* ---------------- Parallax ---------------- */
  let parallaxEls = [];
  let ticking = false;
  const parallaxFrame = () => {
    ticking = false;
    const vh = innerHeight;
    parallaxEls = parallaxEls.filter((el) => el.isConnected);
    parallaxEls.forEach((el) => {
      const r = el.getBoundingClientRect();
      if (r.bottom < -200 || r.top > vh + 200) return;
      const speed = parseFloat(el.dataset.parallax) || 0.15;
      const offset = (r.top + r.height / 2 - vh / 2) * -speed;
      el.style.setProperty('--py', `${offset.toFixed(1)}px`);
    });
  };
  const registerParallax = (scope) => {
    scope.querySelectorAll('[data-parallax]').forEach((el) => parallaxEls.push(el));
    // Real photography inside big image blocks drifts slightly while scrolling
    scope.querySelectorAll('.promo img, .match-hero img, .service-media img').forEach((img) => {
      if (!img.dataset.parallax) { img.dataset.parallax = '0.06'; img.classList.add('px-img'); parallaxEls.push(img); }
    });
    parallaxFrame();
  };

  /* ---------------- Public: enhance a freshly rendered page ---------------- */
  const enhance = (scope) => {
    assignReveals(scope);
    if (enabled()) splitText(scope);
    growables(scope);
    autoCount(scope);
    registerParallax(scope);
    reveal(scope);
  };

  /* ---------------- Count-up for legacy data-countup usage ---------------- */
  const countUp = (scope = document) => scope.querySelectorAll('[data-countup]').forEach((el) => observer().observe(el));

  /* ---------------- Pointer tilt for the hero stage ---------------- */
  const tilt = (el, { max = 8, base = { x: 0, y: 0 } } = {}) => {
    if (!el || !enabled() || !fine()) return () => {};
    let raf;
    const move = (e) => {
      const r = el.getBoundingClientRect();
      const px = (e.clientX - (r.left + r.width / 2)) / r.width, py = (e.clientY - (r.top + r.height / 2)) / r.height;
      cancelAnimationFrame(raf);
      raf = requestAnimationFrame(() => {
        el.style.setProperty('--rx', `${base.x - py * max}deg`); el.style.setProperty('--ry', `${base.y + px * max}deg`);
        el.style.setProperty('--mx', px.toFixed(3)); el.style.setProperty('--my', py.toFixed(3));
      });
    };
    window.addEventListener('pointermove', move, { passive: true });
    return () => window.removeEventListener('pointermove', move);
  };

  /* ---------------- Fly an element to the cart ---------------- */
  const flyTo = (fromEl, toSel = '[data-cart-target]') => {
    const to = [...document.querySelectorAll(toSel)].find((n) => n.offsetParent !== null);
    if (!fromEl || !to || !enabled()) return;
    const a = fromEl.getBoundingClientRect(), b = to.getBoundingClientRect();
    const ghost = fromEl.cloneNode(true);
    Object.assign(ghost.style, { position: 'fixed', left: a.left + 'px', top: a.top + 'px', width: a.width + 'px', height: a.height + 'px', zIndex: 400, borderRadius: '16px', pointerEvents: 'none', objectFit: 'cover', transition: 'all 760ms cubic-bezier(.5,-0.3,.3,1)', boxShadow: '0 20px 40px rgba(0,0,0,.3)' });
    document.body.appendChild(ghost);
    requestAnimationFrame(() => Object.assign(ghost.style, { left: b.left + b.width / 2 - 12 + 'px', top: b.top + b.height / 2 - 12 + 'px', width: '24px', height: '24px', opacity: '.4', borderRadius: '50%', transform: 'rotate(260deg)' }));
    setTimeout(() => { ghost.remove(); to.classList.remove('cart-hit'); void to.offsetWidth; to.classList.add('cart-hit'); }, 760);
  };

  /* ---------------- Confetti (DOM particles, no canvas) ---------------- */
  const COLORS = ['#FF5B14', '#0B6E4F', '#FFD23F', '#0D1411', '#2459D6', '#ffffff'];
  const confetti = ({ x = innerWidth / 2, y = innerHeight / 3, count = 90, spread = 1 } = {}) => {
    if (!enabled()) return;
    const layer = document.createElement('div');
    layer.className = 'confetti-layer';
    for (let i = 0; i < count; i++) {
      const p = document.createElement('i');
      const angle = Math.random() * Math.PI * 2;
      const dist = (120 + Math.random() * 320) * spread;
      p.style.cssText = `left:${x}px;top:${y}px;background:${COLORS[i % COLORS.length]};--tx:${Math.cos(angle) * dist}px;--ty:${Math.sin(angle) * dist * 0.7 - 120}px;--r:${Math.random() * 900 - 450}deg;--dur:${900 + Math.random() * 900}ms;--w:${6 + Math.random() * 6}px;--h:${8 + Math.random() * 10}px;border-radius:${Math.random() > .6 ? '50%' : '2px'}`;
      layer.appendChild(p);
    }
    document.body.appendChild(layer);
    setTimeout(() => layer.remove(), 2000);
  };
  /** Small heart / spark burst at an element (wishlist, favourites) */
  const burst = (el, color = '#FF5B14') => {
    if (!enabled() || !el) return;
    const r = el.getBoundingClientRect();
    const layer = document.createElement('div'); layer.className = 'burst-layer';
    for (let i = 0; i < 10; i++) {
      const p = document.createElement('i'); const a = (i / 10) * Math.PI * 2;
      p.style.cssText = `left:${r.left + r.width / 2}px;top:${r.top + r.height / 2}px;background:${color};--tx:${Math.cos(a) * 34}px;--ty:${Math.sin(a) * 34}px`;
      layer.appendChild(p);
    }
    document.body.appendChild(layer); setTimeout(() => layer.remove(), 700);
  };

  /* ---------------- Global behaviours ---------------- */
  const init = () => {
    // Scroll progress bar + parallax + hide-on-scroll header
    const bar = document.createElement('div'); bar.className = 'scroll-progress'; document.body.appendChild(bar);
    const header = document.getElementById('site-header');
    let lastY = scrollY;
    window.addEventListener('scroll', () => {
      const max = document.documentElement.scrollHeight - innerHeight;
      bar.style.transform = `scaleX(${max > 0 ? scrollY / max : 0})`;
      const y = scrollY;
      header.classList.toggle('is-hidden', y > 260 && y > lastY + 4 && !document.querySelector('.overlay'));
      if (y < lastY - 4 || y < 260) header.classList.remove('is-hidden');
      lastY = y;
      if (!ticking && enabled()) { ticking = true; requestAnimationFrame(parallaxFrame); }
    }, { passive: true });

    // Ripple on every pressable control
    document.addEventListener('pointerdown', (e) => {
      if (!enabled()) return;
      const t = e.target.closest('.btn, .chip, .tab, .slot, .size-btn, .date-chip, .pl-add, .sq-slot, .choice, .icon-btn, .tabbar a, .main-nav a, .facet button');
      if (!t || t.disabled) return;
      const r = t.getBoundingClientRect();
      const s = Math.max(r.width, r.height) * 2.2;
      const ink = document.createElement('span');
      ink.className = 'ripple';
      ink.style.cssText = `width:${s}px;height:${s}px;left:${e.clientX - r.left - s / 2}px;top:${e.clientY - r.top - s / 2}px`;
      if (getComputedStyle(t).position === 'static') t.style.position = 'relative';
      t.classList.add('has-ripple');
      t.appendChild(ink);
      setTimeout(() => ink.remove(), 650);
    });

    // Magnetic buttons + 3D tilt cards with moving glare (desktop pointers only)
    const TILT = '.pitch-card, .product-card .product-media, .service-tile, .league-card, .wallet-card, .ticket, .promo, .account-tile, .fb-league, .kpi, .dp-card, .pocket, .dl-acc';
    let tiltEl = null, magEl = null;
    document.addEventListener('pointermove', (e) => {
      if (!enabled() || !fine()) return;
      const m = e.target.closest('.btn-cta, .btn-lg, .btn-blaze, .fx-sim, .hero-ctas .btn');
      if (magEl && magEl !== m) { magEl.style.transform = ''; magEl = null; }
      if (m && !m.disabled) {
        const r = m.getBoundingClientRect();
        m.style.transform = `translate(${((e.clientX - r.left) / r.width - 0.5) * 10}px, ${((e.clientY - r.top) / r.height - 0.5) * 8}px)`;
        magEl = m;
      }
      const c = e.target.closest(TILT);
      if (tiltEl && tiltEl !== c) { tiltEl.classList.remove('is-tilting'); tiltEl.style.removeProperty('--tx'); tiltEl.style.removeProperty('--ty'); tiltEl = null; }
      if (c) {
        const r = c.getBoundingClientRect();
        const px = (e.clientX - r.left) / r.width, py = (e.clientY - r.top) / r.height;
        c.classList.add('is-tilting');
        c.style.setProperty('--tx', `${(0.5 - py) * 7}deg`); c.style.setProperty('--ty', `${(px - 0.5) * 9}deg`);
        c.style.setProperty('--gx', `${px * 100}%`); c.style.setProperty('--gy', `${py * 100}%`);
        tiltEl = c;
      }
    }, { passive: true });
    document.addEventListener('pointerleave', () => { if (tiltEl) tiltEl.classList.remove('is-tilting'); }, true);
  };

  /** Slide the active-indicator pill under the current nav item (desktop nav + mobile tab bar). */
  const lastPill = {};
  const syncIndicators = () => {
    [['.main-nav', ':scope > a[aria-current="page"], :scope > .nav-dd > [aria-current="page"]'], ['.tabbar', 'a[aria-current="page"]']].forEach(([wrapSel, activeSel]) => {
      const wrap = document.querySelector(wrapSel); if (!wrap || !wrap.getClientRects().length || getComputedStyle(wrap).display === 'none') return;
      let pill = wrap.querySelector('.nav-pill');
      const fresh = !pill;
      if (fresh) { pill = document.createElement('span'); pill.className = 'nav-pill'; pill.setAttribute('aria-hidden', 'true'); wrap.prepend(pill); }
      const a = wrap.querySelector(activeSel);
      if (!a) { pill.style.opacity = 0; delete lastPill[wrapSel]; return; }
      const wr = wrap.getBoundingClientRect(), ar = a.getBoundingClientRect();
      const next = { x: ar.left - wr.left, y: ar.top - wr.top, w: ar.width, h: ar.height };
      const place = (p) => { pill.style.width = `${p.w}px`; pill.style.height = `${p.h}px`; pill.style.transform = `translate(${p.x}px, ${p.y}px)`; };
      const prev = lastPill[wrapSel];
      if (fresh && prev && (prev.x !== next.x) && enabled()) {
        // Header was re-rendered: start where the old pill was, then glide to the new item
        pill.style.transition = 'none'; place(prev); pill.style.opacity = 1;
        void pill.offsetWidth; pill.style.transition = ''; place(next);
      } else { if (fresh) pill.style.transition = 'none'; place(next); pill.style.opacity = 1; if (fresh) { void pill.offsetWidth; pill.style.transition = ''; } }
      lastPill[wrapSel] = next;
    });
  };

  SA.core.motion = { init, enhance, reveal, countUp, tilt, flyTo, confetti, burst, syncIndicators, enabled, getPref, setPref, reduced: () => !enabled() };
})(window.SA);
