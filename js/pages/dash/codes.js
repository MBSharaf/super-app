/** Partner Dashboard — discount codes for bookings (per pitch) and for the partner's store. */
(function (SA) {
  const { esc, money, fmtDate, isoDate, today, addDays } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const D = () => SA.services.dash;
  const K = () => SA.services.codes;

  const form = (c, { pitches, store }) => `<form id="code-form" class="stack" style="--gap:14px" novalidate>
    <fieldset class="field"><legend class="field-label">Where it works</legend><div class="seg">
      <label class="seg-opt"><input type="radio" name="kind" value="pitch" ${c.kind !== 'store' && pitches.length ? 'checked' : ''} ${pitches.length ? '' : 'disabled'} /><span>${icon('pitch', 14)} Pitch bookings</span></label>
      <label class="seg-opt"><input type="radio" name="kind" value="store" ${c.kind === 'store' || !pitches.length ? 'checked' : ''} ${store ? '' : 'disabled'} /><span>${icon('bag', 14)} My store${store ? '' : ' (open it first)'}</span></label></div><span class="field-error" data-err="kind"></span></fieldset>
    <div class="form-grid">
      <div class="field"><label for="cd-code">Code</label><div class="row" style="--gap:6px"><input class="input num code-input" id="cd-code" name="code" maxlength="16" value="${esc(c.code || '')}" placeholder="e.g. SUMMER20" autocomplete="off" /><button type="button" class="btn btn-outline btn-sm" data-gen>${icon('sparkle', 14)} Generate</button></div><span class="field-error" data-err="code"></span></div>
      <div class="field"><label for="cd-note">Internal note <span class="muted">(optional)</span></label><input class="input" id="cd-note" name="note" maxlength="60" value="${esc(c.note || '')}" placeholder="e.g. Instagram campaign" /></div>
      <fieldset class="field"><legend class="field-label">Discount</legend><div class="seg"><label class="seg-opt"><input type="radio" name="type" value="percent" ${c.type !== 'fixed' ? 'checked' : ''} /><span>Percent</span></label><label class="seg-opt"><input type="radio" name="type" value="fixed" ${c.type === 'fixed' ? 'checked' : ''} /><span>Fixed EGP</span></label></div></fieldset>
      <div class="field"><label for="cd-val" data-val-label>${c.type === 'fixed' ? 'Amount off (EGP)' : 'Percent off'}</label><input class="input num" id="cd-val" name="value" type="number" min="1" value="${c.value ?? 15}" /><span class="field-error" data-err="value"></span></div>
      <div class="field"><label for="cd-min">Minimum spend (EGP)</label><input class="input num" id="cd-min" name="min" type="number" min="0" value="${c.min ?? 0}" /><span class="field-error" data-err="min"></span></div>
      <div class="field"><label for="cd-max">Total uses <span class="muted">(0 = unlimited)</span></label><input class="input num" id="cd-max" name="maxUses" type="number" min="0" value="${c.maxUses ?? 100}" /><span class="field-error" data-err="maxUses"></span></div>
      <div class="field"><label for="cd-from">Starts</label><input class="input" id="cd-from" name="from" type="date" value="${c.from || isoDate(today())}" /></div>
      <div class="field"><label for="cd-to">Ends <span class="muted">(optional)</span></label><input class="input" id="cd-to" name="to" type="date" value="${c.to || isoDate(addDays(today(), 30))}" /><span class="field-error" data-err="to"></span></div>
    </div>
    <div class="field" data-pitch-pick ${c.kind === 'store' || !pitches.length ? 'hidden' : ''}><span class="field-label">Pitches</span><div class="chips" style="margin-top:6px">${pitches.map((p) => `<label class="chip-check"><input type="checkbox" name="pitchIds" value="${p.id}" ${(c.pitchIds || []).includes(p.id) ? 'checked' : ''} /><span>${esc(p.name)}</span></label>`).join('')}</div><span class="field-hint">Leave all unticked to use it on every pitch you own.</span></div>
    <label class="choice"><input type="checkbox" name="perCustomer" ${c.perCustomer !== 0 ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span><span class="grow"><b>Once per player</b><span class="xs soft" style="display:block">Each player can use it one time.</span></span></label>
    <div class="code-preview" data-preview></div>
  </form>`;

  const openForm = (c = {}) => {
    const sc = C.scope();
    const pitches = D().myPitches(sc), store = D().myStore(sc.ownerId);
    SA.ui.overlay.open({
      wide: true, title: c.id ? `Edit ${esc(c.code)}` : 'New discount code', subtitle: 'You fund the discount: your earnings are calculated on the discounted price.',
      body: form(c, { pitches, store }),
      footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-primary" form="code-form" type="submit">${icon('check', 16)} ${c.id ? 'Save code' : 'Create code'}</button>`,
      onMount(m, close) {
        const f = m.querySelector('#code-form');
        const prev = () => {
          const d = SA.core.utils.formData(f);
          m.querySelector('[data-pitch-pick]').hidden = d.kind === 'store';
          m.querySelector('[data-val-label]').textContent = d.type === 'fixed' ? 'Amount off (EGP)' : 'Percent off';
          const code = K().norm(d.code) || 'YOURCODE';
          const ex = d.kind === 'store' ? 1000 : 800, off = d.type === 'fixed' ? Math.min(ex, +d.value || 0) : Math.round(ex * (+d.value || 0) / 100);
          m.querySelector('[data-preview]').innerHTML = `<div class="ticket-code-card"><span class="tcc-cut l"></span><span class="tcc-cut r"></span><span class="xs">${d.kind === 'store' ? 'Store code' : 'Booking code'}</span><b class="num">${esc(code)}</b><span class="small">${d.type === 'fixed' ? `${money(+d.value || 0)} off` : `${+d.value || 0}% off`}${+d.min ? ` · min ${money(+d.min)}` : ''}</span><span class="xs">On a ${money(ex)} ${d.kind === 'store' ? 'order' : 'booking'}: player pays ${money(ex - off)}</span></div>`;
        };
        m.querySelector('[data-gen]').addEventListener('click', () => { f.code.value = `${(D().me().company || 'SA').replace(/[^A-Za-z]/g, '').slice(0, 5).toUpperCase()}${Math.floor(10 + Math.random() * 89)}`; prev(); });
        f.addEventListener('input', prev); f.addEventListener('change', prev); prev();
        f.addEventListener('submit', (e) => {
          e.preventDefault();
          const d = { ...SA.core.utils.formData(f), pitchIds: [...f.querySelectorAll('[name=pitchIds]:checked')].map((x) => x.value), perCustomer: f.perCustomer.checked, status: c.status };
          try { const out = K().save(d, { id: c.id, ownerId: sc.ownerId }); close(); SA.core.motion.confetti({ count: 40, spread: 0.5 }); SA.ui.overlay.toast(`${out.code} is ${out.status === 'active' ? 'live' : 'saved'}`, { type: 'success' }); SA.app.rerender(); }
          catch (err) { if (err.details) C.showErrors(f, err.details); SA.ui.overlay.toast(err.message, { type: 'error' }); }
        });
      },
    });
  };

  SA.pages.dashCodes = C.guard({
    title: 'Discount codes — Super App Partner',
    render() {
      const sc = C.scope();
      const list = K().list(sc);
      const t = isoDate(today());
      const state = (c) => (c.status !== 'active' ? 'paused' : c.to && c.to < t ? 'expired' : c.from > t ? 'scheduled' : c.maxUses && c.uses >= c.maxUses ? 'used up' : 'live');
      const live = list.filter((c) => state(c) === 'live');
      const sum = (k) => list.reduce((a, c) => a + (c[k] || 0), 0);
      const body = `
        <div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'Live codes', value: live.length, count: live.length, ic: 'tag', tone: 'k-turf' })}
          ${C.kpi({ label: 'Times used', value: sum('uses'), count: sum('uses'), ic: 'ticket' })}
          ${C.kpi({ label: 'Revenue with codes', value: money(sum('revenue')), ic: 'wallet', tone: 'k-ink' })}
          ${C.kpi({ label: 'Discount given', value: money(sum('discountGiven')), ic: 'percent', tone: 'k-blaze' })}
        </div>
        <div class="notice">${icon('info', 18)}<span>Share a code on Instagram or WhatsApp. Booking codes work at checkout for your pitches; store codes work in the cart on your products only. You pay for the discount — commission is charged on the discounted price.</span></div>
        <div class="code-grid">${list.map((c, i) => { const st = state(c); return `<article class="code-card is-${st.replace(' ', '-')}" style="--i:${i}">
          <div class="code-top"><span class="badge ${c.kind === 'store' ? 'badge-blaze' : 'badge-turf'}">${c.kind === 'store' ? 'Store' : 'Bookings'}</span><span class="badge ${st === 'live' ? 'badge-turf' : st === 'paused' ? '' : 'badge-gold'}">${st[0].toUpperCase() + st.slice(1)}</span></div>
          <button class="code-big num" data-copy="${esc(c.code)}" title="Copy">${esc(c.code)} ${icon('copy', 15)}</button>
          <p class="small strong">${esc(K().label(c))}${c.min ? ` · min ${money(c.min)}` : ''}</p>
          <p class="xs muted">${c.kind === 'pitch' ? (c.pitchIds.length ? c.pitchIds.map((id) => esc(SA.services.db.find('pitches', id)?.name || '')).join(', ') : 'All your pitches') : esc(SA.services.db.find('stores', c.storeId)?.name || '')} · ${fmtDate(c.from)}${c.to ? ` – ${fmtDate(c.to)}` : ''}${c.note ? ` · ${esc(c.note)}` : ''}</p>
          <div class="code-use"><span class="meter"><span style="width:${c.maxUses ? Math.min(100, (c.uses / c.maxUses) * 100) : Math.min(100, c.uses * 2)}%"></span></span><span class="xs num">${c.uses}${c.maxUses ? ` / ${c.maxUses}` : ''} uses</span></div>
          <dl class="kv"><div><dt>Revenue</dt><dd class="num">${money(c.revenue || 0)}</dd></div><div><dt>Discount given</dt><dd class="num">${money(c.discountGiven || 0)}</dd></div></dl>
          <div class="row" style="--gap:6px;margin-top:10px"><button class="btn btn-outline btn-sm" data-edit="${c.id}">${icon('edit', 14)} Edit</button><button class="btn btn-ghost btn-sm" data-toggle="${c.id}">${c.status === 'active' ? 'Pause' : 'Resume'}</button><button class="icon-btn" data-del="${c.id}" aria-label="Delete ${esc(c.code)}" style="color:var(--danger);margin-left:auto">${icon('trash', 16)}</button></div>
        </article>`; }).join('') || `<div class="dash-card" style="grid-column:1/-1">${SA.ui.c.empty({ icon: 'tag', title: 'No codes yet', text: 'Create a code for a campaign, a loyal team or a quiet week.', action: `<button class="btn btn-primary" data-new>${icon('plus', 16)} New code</button>` })}</div>`}</div>`;
      return C.layout('codes', { title: 'Discount codes', subtitle: 'Codes for your pitch bookings and your store', actions: `<button class="btn btn-primary btn-sm" data-new>${icon('plus', 15)} New code</button>`, body });
    },
    mount(el) {
      el.addEventListener('click', async (e) => {
        if (e.target.closest('[data-new]')) return openForm();
        const cp = e.target.closest('[data-copy]'); if (cp) { navigator.clipboard?.writeText(cp.dataset.copy); SA.ui.overlay.toast(`${cp.dataset.copy} copied`); return; }
        const ed = e.target.closest('[data-edit]'); if (ed) return openForm(SA.services.db.find('codes', ed.dataset.edit));
        const tg = e.target.closest('[data-toggle]'); if (tg) { const c = SA.services.db.find('codes', tg.dataset.toggle); K().setStatus(c.id, c.status === 'active' ? 'paused' : 'active'); SA.ui.overlay.toast(c.status === 'active' ? `${c.code} paused` : `${c.code} is live again`); SA.app.rerender(); return; }
        const del = e.target.closest('[data-del]');
        if (del) { const c = SA.services.db.find('codes', del.dataset.del); if (!(await SA.ui.overlay.confirm({ title: `Delete ${esc(c.code)}?`, text: 'Players can’t use it any more. Past bookings and orders keep their discount.', confirmLabel: 'Delete code', danger: true }))) return; K().remove(c.id); SA.ui.overlay.toast('Code deleted'); SA.app.rerender(); }
      });
    },
  });
})(window.SA);
