/**
 * Legal pages (#/legal, #/legal/:key), the consent checkbox used on booking / game / tournament forms, and the cookie
 * banner. Document text is shown in the reader's language straight from js/data/legal.js (not the runtime translator).
 */
(function (SA) {
  const { esc, fmtDate } = SA.core.utils;
  const { icon } = SA.ui;
  const L = () => SA.services.legal;
  const tx = (o) => (SA.i18n.isAr ? o.ar : o.en);

  /* ---------- Consent checkbox for forms ---------- */
  SA.ui.legal = {
    /** A required "I accept" checkbox; the error shows under it */
    field: ({ name = 'waiver', text = 'I accept the <a class="link" href="#/legal/waiver" target="_blank" rel="noopener">injury & conduct waiver</a> and the <a class="link" href="#/legal/terms" target="_blank" rel="noopener">terms of use</a>.' } = {}) =>
      `<div class="legal-consent"><label class="choice"><input type="checkbox" name="${name}" /><span class="radio-dot" style="border-radius:6px"></span><span class="grow small">${text}</span></label><p class="field-error" data-err="${name}"></p></div>`,
    /** true when ticked; otherwise shows the error and returns false */
    check(form, name = 'waiver') {
      const box = form.querySelector(`[name="${name}"]`);
      const err = form.querySelector(`[data-err="${name}"]`);
      if (box?.checked) { if (err) err.textContent = ''; return true; }
      if (err) err.textContent = SA.i18n.t('Please accept the injury & conduct waiver.');
      box?.closest('.legal-consent')?.scrollIntoView({ behavior: 'smooth', block: 'center' });
      return false;
    },
  };

  /* ---------- Pages ---------- */
  const side = (cur) => `<nav class="legal-nav" aria-label="Legal documents">${L().docs().map((d) => `<a href="#/legal/${d.key}" ${d.key === cur ? 'aria-current="page"' : ''}>${icon(d.icon, 16)}<span data-i18n-skip>${esc(tx(d.title))}</span></a>`).join('')}<a href="#/help">${icon('help', 16)}<span>Help center</span></a></nav>`;
  const draft = (d) => (d.review ? '' : `<p class="notice gold legal-draft">${icon('alert', 16)}<span>Draft — to be reviewed by a lawyer before launch.</span></p>`);

  SA.pages.legalIndex = {
    title: 'Legal — Super App',
    render() {
      return `<div class="container section legal-page">
        <header class="legal-head"><span class="eyebrow">Legal</span><h1 class="h1">Terms, privacy and policies</h1><p class="lead">Everything you agree to when you use Super App, in plain words.</p></header>
        <div class="legal-grid">${L().docs().map((d) => `<a class="card card-link legal-card" href="#/legal/${d.key}">${icon(d.icon, 22)}<b data-i18n-skip>${esc(tx(d.title))}</b><span class="small soft" data-i18n-skip>${esc(tx(d.summary))}</span><span class="xs muted">v${d.version} · ${fmtDate(d.updated)}</span></a>`).join('')}</div>
      </div>`;
    },
  };
  SA.pages.legalDoc = {
    title: 'Legal — Super App',
    render(ctx) {
      const d = L().doc(ctx.params.key);
      return `<div class="container section legal-page">
        <div class="legal-layout">${side(d.key)}
          <article class="legal-doc">
            <span class="eyebrow">Legal</span>
            <h1 class="h1" data-i18n-skip>${esc(tx(d.title))}</h1>
            <p class="small muted">Version ${d.version} · Last updated ${fmtDate(d.updated)}</p>
            ${draft(d)}
            <p class="lead" data-i18n-skip>${esc(tx(d.summary))}</p>
            <div data-i18n-skip>${d.sections.map((s, i) => `<section class="legal-sec"><h2 class="h4">${i + 1}. ${esc(tx(s.h))}</h2>${s.p.map((p) => `<p>${esc(tx(p))}</p>`).join('')}</section>`).join('')}</div>
            <div class="row" style="--gap:8px;margin-top:20px;flex-wrap:wrap">
              <button class="btn btn-outline btn-sm" data-print>${icon('file', 14)} Print / PDF</button>
              ${d.key === 'cookies' ? '<button class="btn btn-primary btn-sm" data-cookie-settings>Cookie settings</button>' : ''}
              <a class="btn btn-ghost btn-sm" href="#/support">Questions? Contact support</a>
            </div>
          </article>
        </div>
      </div>`;
    },
    mount(el, ctx) {
      try { document.title = `${tx(L().doc(ctx.params.key).title)} — Super App`; } catch { /* not found */ }
      el.querySelector('[data-print]')?.addEventListener('click', () => window.print());
    },
  };

  /* ---------- Cookie banner ---------- */
  const banner = () => {
    if (L().consent() || document.querySelector('.cookie-bar')) return;
    if (/^\/(dash|seller|admin|factory|ceo)(\/|$|\?)/.test(SA.core.router.location())) return; // the public site only
    const el = document.createElement('div');
    el.className = 'cookie-bar';
    el.setAttribute('role', 'region'); el.setAttribute('aria-label', 'Cookies');
    el.innerHTML = `<p class="small"><b>Cookies</b> — we use essential cookies to run Super App. With your OK we also measure visits and ads. <a class="link" href="#/legal/cookies">Read more</a></p>
      <div class="row" style="--gap:8px"><button class="btn btn-ghost btn-sm" data-c="0">Essential only</button><button class="btn btn-primary btn-sm" data-c="1">Accept all</button></div>`;
    el.addEventListener('click', (e) => { const b = e.target.closest('[data-c]'); if (!b) return; L().setConsent({ analytics: b.dataset.c === '1' }); el.remove(); });
    document.body.appendChild(el);
  };
  const settings = () => {
    const c = L().consent() || { analytics: false };
    SA.ui.overlay.open({ label: 'Cookie settings', title: 'Cookie settings', body: `<form class="stack" style="--gap:12px" data-cookie-form>
        <label class="choice"><input type="checkbox" checked disabled /><span class="radio-dot" style="border-radius:6px"></span><span class="grow"><b class="small">Essential</b><span class="xs muted" style="display:block">Sign-in, cart, language and theme. Always on.</span></span></label>
        <label class="choice"><input type="checkbox" name="analytics" ${c.analytics ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span><span class="grow"><b class="small">Analytics & marketing</b><span class="xs muted" style="display:block">Measuring visits and ads.</span></span></label>
        <button class="btn btn-primary" type="submit">Save</button></form>`,
      onMount(m, close) { m.querySelector('[data-cookie-form]').addEventListener('submit', (e) => { e.preventDefault(); L().setConsent({ analytics: e.currentTarget.analytics.checked }); document.querySelector('.cookie-bar')?.remove(); close(); SA.ui.overlay.toast('Saved'); }); } });
  };
  document.addEventListener('click', (e) => { if (e.target.closest('[data-cookie-settings]')) { e.preventDefault(); settings(); } });
  SA.core.router.onChange(() => setTimeout(banner, 1200));
})(window.SA);
