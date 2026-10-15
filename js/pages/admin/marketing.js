/** Admin console: broadcast messages and Super App promo codes (plus moderation of partner codes). */
(function (SA) {
  const { esc, money, timeAgo, fmtDate, isoDate, today } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const A = SA.pages.adminCommon;
  const AD = () => SA.services.admin;
  const db = () => SA.services.db;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });

  /* ======================= Broadcasts ======================= */
  const BST = { sent: ['badge-turf', 'Sent'], scheduled: ['badge-info', 'Scheduled'], cancelled: ['', 'Cancelled'], sending: ['badge-gold', 'Sending'] };
  SA.pages.adminBroadcast = A.guard({
    title: 'Broadcasts — Super App Admin',
    render() {
      const list = (db().all('broadcasts') || []).slice().sort((a, b) => b.sendAt - a.sendAt);
      const levels = SA.services.sellers.levels();
      const body = `<div class="dash-grid g-1-1">
        <section class="dash-card"><h2 class="h4">New message</h2><p class="small soft" style="margin-top:4px">Goes to the notification bell of everyone you choose. Customers see it in the site bell.</p>
          <form class="form-grid" data-bc novalidate style="margin-top:14px">
            <div class="field"><label for="bc-a">Send to</label><select class="select" id="bc-a" name="audience">${Object.entries(AD().AUDIENCES).map(([k, l]) => `<option value="${k}">${l}</option>`).join('')}</select><p class="field-error" data-err="audience"></p></div>
            <div class="field" data-level-wrap hidden><label for="bc-l">Seller level</label><select class="select" id="bc-l" name="level"><option value="">All levels</option>${levels.map((l) => `<option value="${l.id}">${esc(l.name)}</option>`).join('')}</select></div>
            <div class="field span-2"><label for="bc-t">Title</label><input class="input" id="bc-t" name="title" maxlength="80" placeholder="e.g. New: free delivery on printed hoodies this weekend" /><p class="field-error" data-err="title"></p></div>
            <div class="field span-2"><label for="bc-b">Message</label><textarea class="input" id="bc-b" name="body" rows="4" maxlength="400"></textarea><p class="field-error" data-err="body"></p></div>
            <div class="field"><label for="bc-k">Link (optional)</label><input class="input" id="bc-k" name="link" placeholder="#/shop?cat=designs" /><p class="field-error" data-err="link"></p></div>
            <div class="field"><label for="bc-w">Send later (optional)</label><input class="input" id="bc-w" name="when" type="datetime-local" /><p class="field-error" data-err="when"></p></div>
            <div class="span-2 row-between wrap" style="--gap:10px"><span class="xs muted" data-reach></span><button class="btn btn-primary" type="submit">${icon('megaphone', 15)} <span data-send-label>Send now</span></button></div>
          </form></section>
        <section class="dash-card"><h2 class="h4">Preview</h2>
          <div class="adm-preview"><span class="bp-ico t-info">${icon('megaphone', 16)}</span><span class="grow"><b class="small" data-pv-t>Your title</b><span class="xs muted" style="display:block" data-pv-b>Your message shows here.</span><span class="xs muted">now</span></span></div>
          <p class="xs muted" style="margin-top:12px">${icon('info', 12)} Demo: on this device, partners get it in their dashboard bell and the shopper in the site bell. With the backend it also goes out as push, email or SMS.</p></section>
      </div>
      <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">History</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>When</th><th>Message</th><th>Audience</th><th>Status</th><th class="r">Reached</th><th></th></tr></thead><tbody>
        ${list.map((b) => `<tr><td class="small nowrap">${fmtDate(new Date(b.sendAt))}<span class="xs muted" style="display:block">${new Date(b.sendAt).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })}</span></td><td class="small"><b>${esc(b.title)}</b><span class="xs muted" style="display:block">${esc(b.body.slice(0, 90))}${b.body.length > 90 ? '…' : ''}</span></td>
          <td class="small">${esc(AD().AUDIENCES[b.audience] || b.audience)}${b.level ? ` · ${esc(b.level)}` : ''}</td><td><span class="badge ${BST[b.status]?.[0] || ''}">${BST[b.status]?.[1] || b.status}</span></td><td class="r num">${b.status === 'sent' ? b.reach.toLocaleString() : '—'}</td>
          <td class="r">${b.status === 'scheduled' ? `<button class="btn btn-ghost btn-sm" data-cancel-bc="${b.id}">Cancel</button>` : ''}</td></tr>`).join('') || '<tr><td colspan="6" class="small muted" style="padding:18px">No messages sent yet.</td></tr>'}
      </tbody></table></div></section>`;
      return C.layout('broadcast', { title: 'Broadcasts', subtitle: 'Send a message to partners, sellers, the print partner or customers — now or later', body });
    },
    mount(el) {
      const f = el.querySelector('[data-bc]');
      const sync = () => {
        const d = SA.core.utils.formData(f);
        el.querySelector('[data-level-wrap]').hidden = d.audience !== 'sellers';
        const r = AD().audience(d.audience, d.level);
        el.querySelector('[data-reach]').textContent = `Reaches ${r.accounts.length} partner account${r.accounts.length === 1 ? '' : 's'}${r.customers ? ` and ${r.customers} customers` : ''}.`;
        el.querySelector('[data-pv-t]').textContent = d.title || 'Your title';
        el.querySelector('[data-pv-b]').textContent = d.body || 'Your message shows here.';
        el.querySelector('[data-send-label]').textContent = d.when ? 'Schedule' : 'Send now';
      };
      f.addEventListener('input', sync); f.addEventListener('change', sync); sync();
      f.addEventListener('submit', async (e) => {
        e.preventDefault();
        const d = SA.core.utils.formData(f);
        const r = AD().audience(d.audience, d.level);
        if (!d.when && !(await SA.ui.overlay.confirm({ title: 'Send this message now?', text: `It goes to ${r.accounts.length + r.customers} people right away.`, confirmLabel: 'Send now' }))) return;
        try { const b = AD().broadcast(d); toast(b.status === 'scheduled' ? 'Message scheduled' : 'Message sent'); SA.app.rerender(); } catch (err) { C.showErrors(f, err.details || {}); if (!err.details) toast(err.message, 'error'); }
      });
      el.addEventListener('click', (e) => { const c = e.target.closest('[data-cancel-bc]'); if (c) { AD().cancelBroadcast(c.dataset.cancelBc); toast('Scheduled message cancelled', 'info'); SA.app.rerender(); } });
    },
  });

  /* ======================= Promo codes ======================= */
  const SCOPE = { shop: 'Shop', bookings: 'Pitch bookings', both: 'Shop + bookings' };
  const active = (c) => c.status === 'active' && (!c.to || c.to >= isoDate(today())) && (!c.maxUses || (c.uses || 0) < c.maxUses);
  const codeDialog = (c = null) => SA.ui.overlay.open({
    wide: true, title: c ? `Edit ${esc(c.code)}` : 'New Super App code', subtitle: 'Super App pays the discount — venues and stores still earn on the full price.',
    body: `<form class="form-grid" data-code novalidate>
      <div class="field"><label for="pc-c">Code</label><input class="input mono" id="pc-c" name="code" value="${esc(c?.code || '')}" maxlength="16" style="text-transform:uppercase" placeholder="e.g. RAMADAN20" /><p class="field-error" data-err="code"></p></div>
      <div class="field"><label for="pc-s">Works on</label><select class="select" id="pc-s" name="scope">${Object.entries(SCOPE).map(([k, l]) => `<option value="${k}" ${c?.scope === k ? 'selected' : ''}>${l}</option>`).join('')}</select><p class="field-error" data-err="scope"></p></div>
      <div class="field"><label for="pc-t">Discount</label><select class="select" id="pc-t" name="type"><option value="percent" ${c?.type === 'percent' ? 'selected' : ''}>Percentage</option><option value="fixed" ${c?.type === 'fixed' ? 'selected' : ''}>Fixed amount (EGP)</option><option value="shipping" ${c?.type === 'shipping' ? 'selected' : ''}>Free delivery (shop)</option></select><p class="field-error" data-err="type"></p></div>
      <div class="field"><label for="pc-v">Value</label><input class="input num" id="pc-v" name="value" type="number" min="0" value="${c?.value ?? 10}" /><p class="field-error" data-err="value"></p></div>
      <div class="field"><label for="pc-m">Minimum spend (EGP)</label><input class="input num" id="pc-m" name="min" type="number" min="0" value="${c?.min || 0}" /></div>
      <div class="field"><label for="pc-x">Total uses (0 = unlimited)</label><input class="input num" id="pc-x" name="maxUses" type="number" min="0" value="${c?.maxUses || 0}" /></div>
      <div class="field"><label for="pc-f">Starts</label><input class="input" id="pc-f" name="from" type="date" value="${esc(c?.from || isoDate(today()))}" /></div>
      <div class="field"><label for="pc-e">Ends (optional)</label><input class="input" id="pc-e" name="to" type="date" value="${esc(c?.to || '')}" /><p class="field-error" data-err="to"></p></div>
      <div class="field span-2"><label for="pc-l">Label customers see (optional)</label><input class="input" id="pc-l" name="label" maxlength="60" value="${esc(c?.label || '')}" placeholder="e.g. 20% off for Ramadan nights" /></div>
      <div class="field span-2"><label for="pc-n">Internal note (optional)</label><input class="input" id="pc-n" name="note" maxlength="120" value="${esc(c?.note || '')}" /></div>
      <label class="choice span-2"><input type="checkbox" name="perCustomer" ${c?.perCustomer ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span><span class="grow"><b>Once per customer</b></span></label>
      <div class="span-2 row" style="--gap:8px;justify-content:flex-end"><button type="button" class="btn btn-ghost" data-close>Cancel</button><button class="btn btn-primary" type="submit">${c ? 'Save code' : 'Create code'}</button></div></form>`,
    onMount(m, close) {
      m.querySelector('[data-code]').addEventListener('submit', (e) => {
        e.preventDefault();
        try { AD().savePromo(SA.core.utils.formData(e.target), c?.id); close(); toast(c ? 'Code saved' : 'Code created'); SA.app.rerender(); }
        catch (err) { C.showErrors(e.target, err.details || { code: err.message }); }
      });
    },
  });

  SA.pages.adminPromos = A.guard({
    title: 'Promo codes — Super App Admin',
    render(ctx) {
      const tab = ctx.query.tab || '';
      const mine = AD().promos(), partner = AD().partnerCodes();
      const uses = (db().all('promoUses') || []);
      const month = isoDate(new Date(today().getFullYear(), today().getMonth(), 1));
      const costM = uses.filter((x) => isoDate(new Date(x.at)) >= month).reduce((t, x) => t + x.amount, 0);
      const table = !tab
        ? `<thead><tr><th>Code</th><th>Works on</th><th>Discount</th><th>Valid</th><th class="r">Uses</th><th class="r">Discount given</th><th class="r">Sales with it</th><th>Status</th><th></th></tr></thead><tbody>
          ${mine.map((c) => `<tr><td><b class="mono small">${esc(c.code)}</b>${c.note ? `<span class="xs muted" style="display:block">${esc(c.note)}</span>` : ''}</td><td class="small">${SCOPE[c.scope] || ''}</td><td class="small">${esc(c.label || SA.services.codes.label(c))}${c.min ? `<span class="xs muted" style="display:block">min ${money(c.min)}</span>` : ''}</td>
            <td class="xs">${fmtDate(c.from)}${c.to ? ` – ${fmtDate(c.to)}` : ' – no end'}</td><td class="r num">${(c.uses || 0).toLocaleString()}${c.maxUses ? ` / ${c.maxUses}` : ''}</td><td class="r num">${money(c.discountGiven || 0)}</td><td class="r num">${money(c.revenue || 0)}</td>
            <td><span class="badge ${active(c) ? 'badge-turf' : c.status === 'paused' ? 'badge-gold' : ''}">${active(c) ? 'Active' : c.status === 'paused' ? 'Paused' : 'Ended'}</span></td>
            <td class="r"><div class="row" style="--gap:4px;justify-content:flex-end"><button class="btn btn-ghost btn-sm" data-edit="${c.id}">Edit</button><button class="btn btn-ghost btn-sm" data-status="${c.id}" data-to="${c.status === 'active' ? 'paused' : 'active'}">${c.status === 'active' ? 'Pause' : 'Resume'}</button><button class="icon-btn" data-del="${c.id}" aria-label="Delete ${esc(c.code)}">${icon('trash', 15)}</button></div></td></tr>`).join('') || '<tr><td colspan="9" class="small muted" style="padding:18px">No Super App codes yet.</td></tr>'}`
        : `<thead><tr><th>Code</th><th>Partner</th><th>Works on</th><th>Discount</th><th class="r">Uses</th><th class="r">Discount given</th><th>Status</th><th></th></tr></thead><tbody>
          ${partner.map((c) => `<tr><td><b class="mono small">${esc(c.code)}</b></td><td class="small">${esc(c.owner?.company || '')}<span class="xs muted" style="display:block">${esc(c.owner?.name || '')}</span></td><td class="small">${c.kind === 'pitch' ? 'Their pitches' : 'Their store'}</td><td class="small">${esc(SA.services.codes.label(c))}</td>
            <td class="r num">${c.uses || 0}${c.maxUses ? ` / ${c.maxUses}` : ''}</td><td class="r num">${money(c.discountGiven || 0)}</td><td><span class="badge ${c.status === 'active' ? 'badge-turf' : 'badge-gold'}">${c.status === 'active' ? 'Active' : 'Paused'}</span></td>
            <td class="r"><button class="btn btn-ghost btn-sm" data-status="${c.id}" data-to="${c.status === 'active' ? 'paused' : 'active'}">${c.status === 'active' ? 'Pause' : 'Resume'}</button></td></tr>`).join('') || '<tr><td colspan="8" class="small muted" style="padding:18px">No partner codes.</td></tr>'}`;
      const body = `<div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'Active Super App codes', value: mine.filter(active).length, ic: 'gift' })}
          ${C.kpi({ label: 'Uses (all time)', value: mine.reduce((t, c) => t + (c.uses || 0), 0).toLocaleString(), ic: 'repeat' })}
          ${C.kpi({ label: 'Discount paid this month', value: money(Math.round(costM)), ic: 'wallet', tone: 'k-blaze', sub: 'Counted as a cost in Revenue' })}
          ${C.kpi({ label: 'Partner codes', value: partner.length, ic: 'tag', sub: `${partner.filter((c) => c.status === 'active').length} active` })}
        </div>
        ${A.tabs('/admin/promos', [['', 'Super App codes', mine.length], ['partners', 'Partner & seller codes', partner.length]], tab)}
        <section class="dash-card table-card"><div class="table-wrap"><table class="table dash-table">${table}</tbody></table></div></section>`;
      return C.layout('promos', { title: 'Promo codes', subtitle: 'Codes Super App pays for, in the shop and on pitch bookings — and the codes partners run', actions: `<button class="btn btn-primary btn-sm" data-new>${icon('plus', 14, 2.6)} New code</button>`, body });
    },
    mount(el) {
      el.addEventListener('click', async (e) => {
        if (e.target.closest('[data-new]')) return codeDialog();
        const ed = e.target.closest('[data-edit]'); if (ed) return codeDialog(db().find('codes', ed.dataset.edit));
        const st = e.target.closest('[data-status]'); if (st) { AD().setCodeStatus(st.dataset.status, st.dataset.to); toast(st.dataset.to === 'active' ? 'Code resumed' : 'Code paused', 'info'); SA.app.rerender(); return; }
        const del = e.target.closest('[data-del]');
        if (del) { const c = db().find('codes', del.dataset.del); if (!(await SA.ui.overlay.confirm({ title: `Delete ${esc(c.code)}?`, text: 'Customers can’t use it any more. Its history stays in the revenue report.', confirmLabel: 'Delete', danger: true }))) return; AD().deleteCode(c.id); toast('Code deleted', 'info'); SA.app.rerender(); }
      });
    },
  });
})(window.SA);
