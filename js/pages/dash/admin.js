(function (SA) {
  const { esc, money, timeAgo, fmtDate } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const D = () => SA.services.dash;
  const db = () => SA.services.db;

  const adminOnly = (active, title) => C.layout(active, { title, body: `<div class="dash-card">${SA.ui.c.empty({ icon: 'lock', title: 'Super App admins only', text: 'This section is only available to the Super App team.' })}</div>` });

  SA.pages.dashPartners = C.guard({
    title: 'Partners — Super App Admin',
    render() {
      if (!D().isAdmin()) return adminOnly('partners', 'Partners');
      const list = D().partners();
      const stores = db().all('stores');
      const tot = list.reduce((t, p) => ({ gross: t.gross + p.gross30, comm: t.comm + p.commission30 }), { gross: 0, comm: 0 });
      const body = `
        <div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'Partners', value: list.length, count: list.length, ic: 'users' })}
          ${C.kpi({ label: 'Pitches on Super App', value: list.reduce((t, p) => t + p.pitches, 0), ic: 'pitch' })}
          ${C.kpi({ label: 'Partner revenue (30d)', value: money(Math.round(tot.gross)), count: Math.round(tot.gross), suffix: ' EGP', ic: 'percent', tone: 'k-turf' })}
          ${C.kpi({ label: 'Commission earned (30d)', value: money(Math.round(tot.comm)), count: Math.round(tot.comm), suffix: ' EGP', ic: 'wallet', tone: 'k-blaze' })}
        </div>
        <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Pitch owners</h2>
          <div class="table-wrap"><table class="table dash-table"><thead><tr><th>Partner</th><th class="r">Pitches</th><th class="r">Occupancy</th><th class="r">Revenue (30d)</th><th class="r">Commission</th><th>Store</th><th></th></tr></thead><tbody>
          ${list.map((p) => `<tr><td><span class="row">${SA.ui.c.avatar({ name: p.name, avatar: p.avatar }, 'sm')}<span><b class="small">${esc(p.company)}</b><span class="xs muted" style="display:block">${esc(p.name)} · ${esc(p.email)}</span></span></span></td>
            <td class="r num">${p.pitches}${p.closed ? `<span class="xs" style="display:block;color:var(--danger)">${p.closed} closed</span>` : ''}</td><td class="r num">${(p.occupancy * 100).toFixed(1)}%</td><td class="r num">${money(Math.round(p.gross30))}</td><td class="r num strong">${money(Math.round(p.commission30))}</td>
            <td>${p.store ? `<span class="small">${esc(p.store.name)}</span> <span class="badge ${p.store.status === 'active' ? 'badge-turf' : 'badge-danger'}">${p.store.status === 'active' ? 'Live' : 'Suspended'}</span>` : '<span class="xs muted">No store</span>'}</td>
            <td class="r"><button class="btn btn-outline btn-sm" data-view-as="${p.id}">Open their dashboard ${icon('arrowUpRight', 14)}</button></td></tr>`).join('')}
          </tbody></table></div></section>
        <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Stores</h2>
          <div class="table-wrap"><table class="table dash-table"><thead><tr><th>Store</th><th>Owner</th><th class="r">Products</th><th>Opened</th><th>Status</th><th></th></tr></thead><tbody>
          ${stores.map((s) => `<tr><td><b class="small">${esc(s.name)}</b>${s.official ? ' <span class="badge badge-ink">Official</span>' : ''}</td><td class="small">${esc(db().find('accounts', s.ownerId)?.company || '')}</td><td class="r num">${db().all('products').filter((p) => p.storeId === s.id && !p.deleted).length}</td><td class="small">${fmtDate(new Date(s.createdAt))}</td>
            <td><span class="badge ${s.status === 'active' ? 'badge-turf' : 'badge-danger'}">${s.status === 'active' ? 'Live' : 'Suspended'}</span></td>
            <td class="r"><div class="row" style="--gap:6px;justify-content:flex-end"><a class="btn btn-ghost btn-sm" href="#/shop?store=${s.id}" target="_blank" rel="noopener">View</a>${s.official ? '' : `<button class="btn btn-outline btn-sm" data-store-status="${s.id}" data-to="${s.status === 'active' ? 'suspended' : 'active'}">${s.status === 'active' ? 'Suspend' : 'Reactivate'}</button>`}</div></td></tr>`).join('')}
          </tbody></table></div></section>`;
      return C.layout('partners', { title: 'Partners', subtitle: 'Pitch owners and stores on Super App', body });
    },
    mount(el) {
      el.addEventListener('click', async (e) => {
        const v = e.target.closest('[data-view-as]');
        if (v) { SA.services.admin.openDashboard(v.dataset.viewAs); window.open(`${location.pathname}#/dash`, '_blank', 'noopener'); return; }
        const st = e.target.closest('[data-store-status]');
        if (st) {
          const s = db().find('stores', st.dataset.storeStatus);
          if (st.dataset.to === 'suspended' && !(await SA.ui.overlay.confirm({ title: `Suspend ${esc(s.name)}?`, text: 'All its products disappear from the Super App shop until you reactivate it.', confirmLabel: 'Suspend store', danger: true }))) return;
          D().setStoreStatus(s.id, st.dataset.to); SA.ui.overlay.toast(st.dataset.to === 'active' ? 'Store reactivated' : 'Store suspended'); SA.app.rerender();
        }
      });
    },
  });

  SA.pages.dashSettings = C.guard({
    title: 'Platform settings — Super App Admin',
    render() {
      if (!D().isAdmin()) return adminOnly('settings', 'Platform settings');
      const cm = D().commission();
      const body = `<div class="dash-grid g-1-1">
        <section class="dash-card"><h2 class="h4">Commission</h2><p class="small soft" style="margin-top:6px">Applied to every new calculation across partner dashboards. Walk-in bookings and the official store are never charged.</p>
          <form id="cm" novalidate class="form-grid" style="margin-top:16px">
            <div class="field"><label for="cm-b">On pitch bookings (%)</label><input class="input num" id="cm-b" name="bookings" type="number" min="0" max="50" step="0.5" value="${+(cm.bookings * 100).toFixed(1)}" /></div>
            <div class="field"><label for="cm-s">On store sales (%)</label><input class="input num" id="cm-s" name="store" type="number" min="0" max="50" step="0.5" value="${+(cm.store * 100).toFixed(1)}" /></div>
            <p class="field-error span-2" data-err="form"></p>
            <div class="span-2"><button class="btn btn-primary" type="submit">Save commission</button></div></form></section>
        <section class="dash-card"><h2 class="h4">VAT</h2><p class="small soft" style="margin-top:6px">Prices on Super App include VAT. Invoices, statements and finance reports show the VAT inside every amount at this rate.</p>
          <form id="vat" novalidate class="form-grid" style="margin-top:16px">
            <div class="field"><label for="vat-r">VAT rate (%)</label><input class="input num" id="vat-r" name="vat" type="number" min="0" max="30" step="0.5" value="${+(D().vat() * 100).toFixed(1)}" /><span class="field-hint">Egypt standard rate: 14%</span></div>
            <p class="field-error span-2" data-err="vat"></p>
            <div class="span-2"><button class="btn btn-primary" type="submit">Save VAT rate</button></div></form></section>
        <section class="dash-card"><h2 class="h4">Super Points (loyalty)</h2><p class="small soft" style="margin-top:6px">Players earn points on played games, delivered orders and tournament entries, and turn them into wallet cash.</p>
          <form id="lty" novalidate class="form-grid" style="margin-top:16px">
            <div class="field"><label for="lt-e">EGP spent per point</label><input class="input num" id="lt-e" name="earnPer" type="number" min="1" max="100" value="${SA.services.db.settings().loyalty.earnPer}" /></div>
            <div class="field"><label for="lt-v">Value of 1 point (EGP)</label><input class="input num" id="lt-v" name="pointValue" type="number" min="0.01" max="5" step="0.01" value="${SA.services.db.settings().loyalty.pointValue}" /></div>
            <div class="field"><label for="lt-m">Minimum to redeem (points)</label><input class="input num" id="lt-m" name="minRedeem" type="number" min="100" step="100" value="${SA.services.db.settings().loyalty.minRedeem}" /></div>
            <div class="field"><span class="field-label">Cashback rate</span><b class="h4 num" data-lt-rate></b><span class="field-hint">Bronze · Silver ×1.25 · Gold ×1.5</span></div>
            <p class="field-error span-2" data-err="lty"></p>
            <div class="span-2"><button class="btn btn-primary" type="submit">Save loyalty settings</button></div></form></section>
        <section class="dash-card"><h2 class="h4">Settlements</h2><dl class="kv" style="margin-top:14px"><div><dt>Automatic payouts</dt><dd>Every Monday</dd></div><div><dt>Booking money available</dt><dd>After the match ends</dd></div><div><dt>Store money available</dt><dd>When the order is delivered</dd></div><div><dt>Minimum withdrawal</dt><dd>100 EGP</dd></div><div><dt>Withdrawal requests</dt><dd><a class="link" href="#/admin/payouts">Approved by the Super App team</a></dd></div><div><dt>Customer service fee</dt><dd>${Math.round(SA.services.db.settings().serviceFee * 100)}% per booking</dd></div></dl></section>
        <section class="dash-card"><h2 class="h4">Sellers & print on demand</h2><p class="small soft" style="margin-top:6px">Rules for seller profit, withdrawals and customers who refuse printed items.</p>
          <form id="podset" novalidate class="form-grid" style="margin-top:16px">
            <div class="field"><label for="ps-h">Profit on hold after delivery (days)</label><input class="input num" id="ps-h" name="holdDays" type="number" min="0" max="30" value="${db().settings().pod.holdDays}" /></div>
            <div class="field"><label for="ps-m">Minimum withdrawal (EGP)</label><input class="input num" id="ps-m" name="minWithdraw" type="number" min="50" step="50" value="${db().settings().pod.minWithdraw}" /></div>
            <div class="field"><label for="ps-r">Refused deliveries before online payment is required</label><input class="input num" id="ps-r" name="refuseLimit" type="number" min="1" max="10" value="${db().settings().pod.refuseLimit}" /></div>
            <p class="field-error span-2" data-err="podset"></p>
            <div class="span-2"><button class="btn btn-primary" type="submit">Save seller rules</button></div></form></section>
        <section class="dash-card"><h2 class="h4">Fantasy Pass</h2><p class="small soft" style="margin-top:6px">Monthly price for the prize leagues. Prizes are sponsor prizes and vouchers — never cash.</p>
          <form id="fpass" novalidate class="form-grid" style="margin-top:16px">
            <div class="field"><label for="fp-1">One league (EGP / month)</label><input class="input num" id="fp-1" name="one" type="number" min="0" step="5" value="${db().settings().fantasySubs.one}" /></div>
            <div class="field"><label for="fp-2">Both leagues (EGP / month)</label><input class="input num" id="fp-2" name="both" type="number" min="0" step="5" value="${db().settings().fantasySubs.both}" /></div>
            <p class="field-error span-2" data-err="fpass"></p>
            <div class="span-2"><button class="btn btn-primary" type="submit">Save Fantasy Pass prices</button></div></form></section>
        <section class="dash-card span-all"><h2 class="h4">Seller levels</h2><p class="small soft" style="margin-top:6px">Levels are reached on delivered items with a minimum rating. Leave “Designs” or “Codes” empty for unlimited.</p>
          <form id="lvls" novalidate style="margin-top:14px"><div class="table-wrap"><table class="table lvl-edit"><thead><tr><th>Level</th><th>Items sold</th><th>Min rating</th><th>Designs</th><th>Codes</th><th>Base discount (EGP)</th><th>Store commission %</th><th>Fantasy discount %</th></tr></thead><tbody>
            ${db().settings().sellerLevels.map((l, i) => `<tr><th scope="row"><span class="lvl-chip lvl-${l.id}">${esc(l.name)}</span></th>
              <td><input class="input num" name="sales-${i}" type="number" min="0" value="${l.sales}" ${i === 0 ? 'readonly' : ''} aria-label="${esc(l.name)} items sold" /></td>
              <td><input class="input num" name="rating-${i}" type="number" min="0" max="5" step="0.1" value="${l.rating}" ${i === 0 ? 'readonly' : ''} aria-label="${esc(l.name)} minimum rating" /></td>
              <td><input class="input num" name="designs-${i}" type="number" min="1" value="${l.designs ?? ''}" placeholder="∞" aria-label="${esc(l.name)} designs" /></td>
              <td><input class="input num" name="codes-${i}" type="number" min="0" value="${l.codes ?? ''}" placeholder="∞" aria-label="${esc(l.name)} codes" /></td>
              <td><input class="input num" name="baseDiscount-${i}" type="number" min="0" value="${l.baseDiscount}" aria-label="${esc(l.name)} base discount" /></td>
              <td><input class="input num" name="commission-${i}" type="number" min="0" max="50" step="0.5" value="${+(l.commission * 100).toFixed(1)}" aria-label="${esc(l.name)} commission" /></td>
              <td><input class="input num" name="fantasyDiscount-${i}" type="number" min="0" max="100" step="5" value="${Math.round(l.fantasyDiscount * 100)}" aria-label="${esc(l.name)} fantasy discount" /></td></tr>`).join('')}
          </tbody></table></div><p class="field-error" data-err="lvls"></p><button class="btn btn-primary" type="submit" style="margin-top:12px">Save levels</button></form></section>
        <section class="dash-card"><h2 class="h4">Delivery & service fees</h2><p class="small soft" style="margin-top:6px">What customers pay on top: shop delivery and the booking service fee. Changes apply to new orders and bookings.</p>
          <form id="fees" novalidate class="form-grid" style="margin-top:16px">
            <div class="field"><label for="fe-s">Standard delivery (EGP)</label><input class="input num" id="fe-s" name="standard" type="number" min="0" step="5" value="${SA.data.products.shipping.standard}" /></div>
            <div class="field"><label for="fe-e">Express delivery (EGP)</label><input class="input num" id="fe-e" name="express" type="number" min="0" step="5" value="${SA.data.products.shipping.express}" /></div>
            <div class="field"><label for="fe-f">Free delivery from (EGP)</label><input class="input num" id="fe-f" name="freeOver" type="number" min="0" step="50" value="${SA.data.products.shipping.freeOver}" /><span class="field-hint">0 = never free</span></div>
            <div class="field"><label for="fe-b">Booking service fee (%)</label><input class="input num" id="fe-b" name="serviceFee" type="number" min="0" max="15" step="0.5" value="${+(db().settings().serviceFee * 100).toFixed(1)}" /></div>
            <p class="field-error span-2" data-err="fees"></p>
            <div class="span-2"><button class="btn btn-primary" type="submit">Save fees</button></div></form></section>
        <section class="dash-card"><h2 class="h4">Maintenance mode</h2><p class="small soft" style="margin-top:6px">Pause checkout while something is being fixed. People can still browse; a red bar on the site explains it.</p>
          <form id="maint" novalidate class="stack" style="--gap:10px;margin-top:14px">
            <label class="choice"><input type="checkbox" name="bookings" ${db().settings().maintenance?.bookings ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span><span class="grow"><b>Pause pitch bookings</b></span></label>
            <label class="choice"><input type="checkbox" name="shop" ${db().settings().maintenance?.shop ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span><span class="grow"><b>Pause shop checkout</b></span></label>
            <div class="field"><label for="mt-m">Message (optional)</label><input class="input" id="mt-m" name="message" maxlength="140" value="${esc(db().settings().maintenance?.message || '')}" placeholder="e.g. Back by 2 AM — sorry for the wait." /></div>
            <div><button class="btn btn-primary" type="submit">Save</button></div></form></section>
        <section class="dash-card"><h2 class="h4">Demo data</h2><p class="small soft" style="margin-top:6px">Restore all pitches, products, stores and bookings to the original demo state on this device.</p><button class="btn btn-outline" data-reset style="margin-top:14px;color:var(--danger)">${icon('refresh', 15)} Reset platform data</button></section>
      </div>`;
      return C.layout('settings', { title: 'Platform settings', subtitle: 'Commission, VAT, sellers, Fantasy Pass and demo data', body });
    },
    mount(el) {
      el.querySelector('#cm')?.addEventListener('submit', (e) => {
        e.preventDefault();
        try { D().setCommission(SA.core.utils.formData(e.target)); SA.ui.overlay.toast('Commission updated for all partners'); SA.app.rerender(); }
        catch (err) { e.target.querySelector('[data-err="form"]').textContent = err.message; }
      });
      const lty = el.querySelector('#lty');
      const rate = () => { const f = SA.core.utils.formData(lty); el.querySelector('[data-lt-rate]').textContent = `${((+f.pointValue / +f.earnPer) * 100).toFixed(1)}% back`; };
      lty?.addEventListener('input', rate); if (lty) rate();
      lty?.addEventListener('submit', (e) => {
        e.preventDefault();
        const f = SA.core.utils.formData(e.target);
        const v = { earnPer: Math.round(+f.earnPer), pointValue: +(+f.pointValue).toFixed(2), minRedeem: Math.round(+f.minRedeem / 100) * 100 };
        if (!(v.earnPer >= 1 && v.earnPer <= 100 && v.pointValue > 0 && v.pointValue <= 5 && v.minRedeem >= 100)) { e.target.querySelector('[data-err="lty"]').textContent = 'Check the values: 1–100 EGP per point, point value up to 5 EGP, minimum 100 points.'; return; }
        SA.services.db.setSettings({ loyalty: v }); SA.services.db.log(D().me().id, 'settings.loyalty', `Set Super Points to 1 point per ${v.earnPer} EGP, worth ${v.pointValue} EGP`);
        SA.ui.overlay.toast('Loyalty settings saved'); SA.app.rerender();
      });
      el.querySelector('#vat')?.addEventListener('submit', (e) => {
        e.preventDefault();
        try { D().setVat(e.target.vat.value); SA.ui.overlay.toast('VAT rate updated'); SA.app.rerender(); }
        catch (err) { e.target.querySelector('[data-err="vat"]').textContent = err.message; }
      });
      el.querySelector('#podset')?.addEventListener('submit', (e) => {
        e.preventDefault();
        const f = SA.core.utils.formData(e.target);
        const v = { holdDays: Math.round(+f.holdDays), minWithdraw: Math.round(+f.minWithdraw), refuseLimit: Math.round(+f.refuseLimit) };
        if (!(v.holdDays >= 0 && v.holdDays <= 30 && v.minWithdraw >= 50 && v.refuseLimit >= 1 && v.refuseLimit <= 10)) { e.target.querySelector('[data-err="podset"]').textContent = 'Check the values: hold 0–30 days, minimum withdrawal from 50 EGP, refusals 1–10.'; return; }
        db().setSettings({ pod: v }); db().log(D().me().id, 'settings.pod', `Seller rules: ${v.holdDays}-day hold, ${v.minWithdraw} EGP minimum withdrawal`);
        SA.ui.overlay.toast('Seller rules saved'); SA.app.rerender();
      });
      el.querySelector('#fpass')?.addEventListener('submit', (e) => {
        e.preventDefault();
        const f = SA.core.utils.formData(e.target);
        const v = { one: Math.round(+f.one), both: Math.round(+f.both) };
        if (!(v.one >= 0 && v.both >= 0 && v.both <= v.one * 2)) { e.target.querySelector('[data-err="fpass"]').textContent = 'Prices can’t be negative, and both leagues shouldn’t cost more than two single passes.'; return; }
        db().setSettings({ fantasySubs: v }); db().log(D().me().id, 'settings.fantasy', `Fantasy Pass: ${v.one} EGP one league, ${v.both} EGP both`);
        SA.ui.overlay.toast('Fantasy Pass prices saved'); SA.app.rerender();
      });
      el.querySelector('#lvls')?.addEventListener('submit', (e) => {
        e.preventDefault();
        const f = SA.core.utils.formData(e.target);
        const cur = db().settings().sellerLevels;
        const opt = (v) => (v === '' || v == null ? null : Math.max(0, Math.round(+v)));
        const next = cur.map((l, i) => ({ ...l, sales: i === 0 ? 0 : Math.round(+f[`sales-${i}`]), rating: i === 0 ? 0 : +(+f[`rating-${i}`]).toFixed(1), designs: opt(f[`designs-${i}`]), codes: opt(f[`codes-${i}`]),
          baseDiscount: Math.round(+f[`baseDiscount-${i}`]), commission: +(+f[`commission-${i}`] / 100).toFixed(3), fantasyDiscount: Math.min(1, +(+f[`fantasyDiscount-${i}`] / 100).toFixed(2)) }));
        const bad = next.some((l, i) => i > 0 && (l.sales <= next[i - 1].sales || l.rating < next[i - 1].rating)) || next.some((l) => !(l.rating >= 0 && l.rating <= 5 && l.commission >= 0 && l.commission <= 0.5 && l.baseDiscount >= 0))
          || next.some((l) => SA.services.pod.catalog({ all: true }).some((c) => c.base - l.baseDiscount <= c.partnerCost));
        if (bad) { e.target.querySelector('[data-err="lvls"]').textContent = 'Each level needs more items and at least the same rating as the one before; commission 0–50%; the base discount must keep every product above its partner cost.'; return; }
        db().setSettings({ sellerLevels: next }); db().log(D().me().id, 'settings.levels', 'Seller levels updated');
        SA.ui.overlay.toast('Seller levels saved'); SA.app.rerender();
      });
      el.querySelector('#fees')?.addEventListener('submit', (e) => {
        e.preventDefault();
        try { SA.services.admin.setFees(SA.core.utils.formData(e.target)); SA.ui.overlay.toast('Fees saved'); SA.app.rerender(); }
        catch (err) { e.target.querySelector('[data-err="fees"]').textContent = err.message; }
      });
      el.querySelector('#maint')?.addEventListener('submit', (e) => {
        e.preventDefault();
        const f = SA.core.utils.formData(e.target);
        SA.services.admin.setMaintenance({ bookings: !!f.bookings, shop: !!f.shop, message: f.message || '' });
        SA.ui.overlay.toast(f.bookings || f.shop ? 'Maintenance mode is on' : 'Maintenance mode is off', { type: 'info' }); SA.app.rerender();
      });
      el.querySelector('[data-reset]')?.addEventListener('click', async () => {
        if (!(await SA.ui.overlay.confirm({ title: 'Reset platform data?', text: 'Pitches, products, stores, bookings, closures and payouts go back to the demo state on this device.', confirmLabel: 'Reset', danger: true }))) return;
        SA.services.db.reset(); SA.ui.overlay.toast('Platform data reset'); SA.app.rerender();
      });
    },
  });

  SA.pages.dashActivity = C.guard({
    title: 'Activity — Super App Partner',
    render() {
      const list = D().activity(150);
      const actors = Object.fromEntries(db().all('accounts').map((a) => [a.id, a]));
      const kind = (a) => (a.startsWith('booking') ? ['ticket', 'k-turf'] : a.startsWith('pitch') || a.startsWith('closure') ? ['pitch', ''] : a.startsWith('product') || a.startsWith('store') || a.startsWith('order') ? ['bag', 'k-blaze'] : a.startsWith('payout') ? ['wallet', 'k-ink'] : ['clock', '']);
      const body = `<section class="dash-card"><ol class="activity">${list.map((x, i) => { const [ic, tone] = kind(x.action); return `<li class="${tone}" style="--i:${Math.min(i, 20)}"><span class="ac-ico">${icon(ic, 15)}</span><span class="grow"><b class="small">${esc(x.text)}</b><span class="xs muted" style="display:block">${x.actor === 'customer' ? 'Customer on Super App' : esc(actors[x.actor]?.name || 'System')} · ${timeAgo(x.at)}</span></span></li>`; }).join('') || '<li class="small muted">No activity yet.</li>'}</ol></section>`;
      return C.layout('activity', { title: 'Activity', subtitle: 'Everything that changed on your account', body });
    },
  });
})(window.SA);
