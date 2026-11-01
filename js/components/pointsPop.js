/**
 * "Congratulations, you earned N Super Points" pop-up. Owner's rule (2026-11-01): every 1 EGP a player pays earns
 * 1 point, and each payment shows this pop-up. It watches the points history (SA.services.loyalty.summary()) and
 * celebrates every new earning the player hasn't seen yet — payments in the app, the rest of a booking paid at
 * the pitch (when the venue scans the QR) and cash-on-delivery orders. Several new earnings show as one pop-up.
 * It waits until no dialog is open (e.g. the payment dialog) and never shows on business dashboards.
 */
(function (SA) {
  const { esc } = SA.core.utils;
  const { icon } = SA.ui;
  const KEY = 'superapp.points.seen';
  const BUSINESS = /^\/(dash|seller|admin|factory|ceo)(\/|$)/;
  const ls = {
    get: () => { try { const v = localStorage.getItem(KEY); return v ? new Set(JSON.parse(v)) : null; } catch { return null; } },
    set: (s) => { try { localStorage.setItem(KEY, JSON.stringify([...s].slice(-800))); } catch { /* private mode */ } },
  };
  let timer = 0, showing = false, routeAt = 0;

  const earnings = () => {
    try { return SA.services.loyalty.summary().history.filter((e) => e.kind === 'earn' && e.points > 0); } catch { return []; }
  };
  // Wait for open dialogs (payment) and for the page to settle after a navigation (a route change closes dialogs)
  const busy = () => showing || !!document.querySelector('.overlay') || Date.now() - routeAt < 900 || BUSINESS.test(SA.core.router?.location?.() || '') || document.hidden;

  const show = (fresh) => {
    showing = true;
    const opened = Date.now();
    const pts = fresh.reduce((t, e) => t + e.points, 0);
    const balance = SA.services.loyalty.summary().balance;
    const lines = fresh.slice(0, 4).map((e) => `<li><span class="grow">${esc(e.label)}</span><b class="num">+${e.points.toLocaleString()}</b></li>`).join('');
    const m = SA.ui.overlay.open({
      label: 'Super Points earned',
      body: `<div class="pts-pop" data-points-pop>
        <span class="pts-pop-ico">${icon('sparkle', 34, 1.8)}</span>
        <h2 class="h2">Congratulations!</h2>
        <p class="lead"><span>You earned</span> <b class="num">${pts.toLocaleString()}</b> <span>Super Points</span></p>
        <p class="small soft">1 point for every 1 EGP you pay. They’re in your wallet now.</p>
        <ul class="pts-pop-list">${lines}</ul>
        <p class="small"><span>Your balance:</span> <b class="num">${balance.toLocaleString()}</b> <span>points</span></p>
        <div class="row" style="--gap:8px;justify-content:center;margin-top:16px"><a class="btn btn-outline" href="#/wallet" data-close>See my points</a><button class="btn btn-primary" data-close autofocus>Great!</button></div>
      </div>`,
      // Seen only once the player had a chance to read it (closed by a sudden navigation → shown again)
      onClose: () => { showing = false; if (Date.now() - opened > 700) { const seen = ls.get() || new Set(); fresh.forEach((e) => seen.add(e.id)); ls.set(seen); } schedule(1000); },
    });
    setTimeout(() => { const r = m.el.querySelector('.pts-pop-ico')?.getBoundingClientRect(); SA.core.motion?.confetti?.(r ? { x: r.left + r.width / 2, y: r.top + r.height / 2 } : {}); }, 200);
  };

  const check = () => {
    timer = 0;
    const all = earnings();
    let seen = ls.get();
    // First run on this device: what's already there counts as seen
    if (!seen) { ls.set(new Set(all.map((e) => e.id))); return; }
    const fresh = all.filter((e) => !seen.has(e.id));
    if (!fresh.length) return;
    if (busy()) { schedule(900); return; }
    show(fresh.reverse());
  };
  const schedule = (ms = 450) => { if (!timer) timer = setTimeout(check, ms); };

  SA.ui.pointsPop = {
    init() {
      SA.core.store.subscribe(() => schedule());
      SA.services.db?.subscribe?.(() => schedule());
      SA.core.router.onChange(() => { routeAt = Date.now(); schedule(1000); });
      document.addEventListener('visibilitychange', () => { if (!document.hidden) schedule(); });
      schedule(1200);
    },
    check,
  };
})(window.SA);
