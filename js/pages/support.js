/**
 * Help & support: customers open requests from the site (#/support), partners, sellers and the print partner from their
 * dashboard (#/dash/support). The Super App team answers in the admin console.
 */
(function (SA) {
  const { esc, timeAgo, fmtDate } = SA.core.utils;
  const { icon } = SA.ui;
  const SUP = () => SA.services.support;
  const store = SA.core.store;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });

  const STATUS = { open: ['badge-info', 'Waiting for Super App'], pending: ['badge-blaze', 'Super App replied'], resolved: ['badge-turf', 'Resolved'] };
  const list = (tickets, base) => tickets.length ? `<ul class="adm-tickets sup-list">${tickets.map((t) => { const [c, l] = STATUS[t.status]; return `<li><a href="${base}/${t.id}"><span class="bp-ico t-info">${icon(SUP().CATS[t.category]?.icon || 'message', 16)}</span><span class="grow"><b class="small">${esc(t.subject)}</b><span class="xs muted" style="display:block">${esc(t.id)} · ${timeAgo(t.updatedAt)}${t.refund ? ` · refunded ${t.refund.amount.toLocaleString()} EGP` : ''}</span></span><span class="badge ${c}">${l}</span></a></li>`; }).join('')}</ul>`
    : `<div class="panel">${SA.ui.c.empty({ icon: 'message', title: 'No requests yet', text: 'Questions, problems with a booking or an order — we usually reply within a few hours.' })}</div>`;

  const thread = (t) => `<ol class="adm-thread">${t.messages.map((m) => `<li class="by-${m.by === 'admin' || m.by === 'system' ? 'admin' : 'me'}"><div class="adm-bubble"><span class="xs strong">${m.by === 'admin' || m.by === 'system' ? 'Super App support' : 'You'}</span><p class="small">${esc(m.text)}</p><span class="xs muted">${fmtDate(new Date(m.at))} · ${new Date(m.at).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })}</span></div></li>`).join('')}</ol>`;

  /** Choices the shopper can attach to a request */
  const refOptions = () => {
    const s = store.get(), u = s.user;
    const b = (s.bookings || []).slice(0, 8).map((x) => [`booking:${x.id}`, `Booking ${x.id} · ${x.pitchName} · ${x.date}`]);
    const o = (s.orders || []).slice(0, 8).map((x) => [`order:${x.id}`, `Order ${x.id} · ${x.lines.length} item${x.lines.length > 1 ? 's' : ''}`]);
    const p = (SA.services.db.all('podOrders') || []).filter((x) => x.customer?.id === u?.id).slice(0, 8).map((x) => [`print:${x.id}`, `Printed: ${x.title} · ${x.color} · ${x.size}`]);
    return [...b, ...o, ...p];
  };
  const form = ({ withRef, preset = '', category = '' }) => {
    const cats = Object.entries(SUP().CATS).filter(([k]) => withRef || !SUP().CATS[k].dispute);
    const opts = withRef ? refOptions() : [];
    if (preset && !opts.some(([v]) => v === preset)) opts.unshift([preset, preset.replace(':', ' ')]);
    return `<form class="form-grid sup-form" data-new novalidate>
      <div class="field"><label for="sp-c">What is it about?</label><select class="select" id="sp-c" name="category">${cats.map(([k, c]) => `<option value="${k}" ${k === category ? 'selected' : ''}>${c.label}</option>`).join('')}</select><p class="field-error" data-err="category"></p></div>
      ${withRef ? `<div class="field"><label for="sp-r">Which one? (optional)</label><select class="select" id="sp-r" name="ref"><option value="">—</option>${opts.map(([v, l]) => `<option value="${esc(v)}" ${v === preset ? 'selected' : ''}>${esc(l)}</option>`).join('')}</select></div>` : ''}
      <div class="field span-2"><label for="sp-s">Subject</label><input class="input" id="sp-s" name="subject" maxlength="120" placeholder="e.g. The pitch was closed when we arrived" /><p class="field-error" data-err="subject"></p></div>
      <div class="field span-2"><label for="sp-t">Message</label><textarea class="input" id="sp-t" name="text" rows="5" maxlength="2000" placeholder="Tell us what happened. Include the time and anything the team should know."></textarea><p class="field-error" data-err="text"></p></div>
      <div class="span-2 row-between wrap" style="--gap:10px"><span class="xs muted">${icon('shield', 12)} Only the Super App team sees this.</span><button class="btn btn-primary" type="submit">${icon('message', 15)} Send request</button></div>
    </form>`;
  };
  const bindForm = (el, { withRef, from, after }) => el.querySelector('[data-new]')?.addEventListener('submit', (e) => {
    e.preventDefault();
    const f = SA.core.utils.formData(e.target);
    const [type, ...rest] = (f.ref || '').split(':');
    try {
      const t = SUP().create({ category: f.category, subject: f.subject, text: f.text, ref: withRef && type ? { type, id: rest.join(':') } : null, from });
      toast(`Request ${t.id} sent — we’ll reply soon`);
      after(t);
    } catch (err) { SA.pages.dashCommon.showErrors(e.target, err.fields || { text: err.message }); }
  });

  /* ======================= Site ======================= */
  SA.pages.support = {
    title: 'Help & support — Super App',
    render(ctx) {
      const mine = SUP().mine();
      const preset = ctx.query.ref || '';
      const category = preset ? preset.split(':')[0] : '';
      return `<div class="container section-tight sup-page">
        <div class="page-head"><a class="back-link" href="#/profile">${icon('chevronLeft', 16)} Profile</a><h1 class="h1" style="margin-top:10px">Help & support</h1><p class="lead">Questions about a booking, an order, a printed item or your wallet — the Super App team answers here.</p></div>
        <div class="sup-grid">
          <section class="panel panel-pad"><h2 class="h3">${preset ? 'Report a problem' : 'New request'}</h2>${form({ withRef: true, preset, category })}</section>
          <section><h2 class="h3" style="margin-bottom:12px">Your requests</h2>${list(mine, '#/support')}</section>
        </div></div>`;
    },
    mount(el) { bindForm(el, { withRef: true, after: (t) => SA.core.router.go(`/support/${t.id}`) }); },
  };
  SA.pages.supportTicket = {
    title: 'Request — Super App',
    render(ctx) {
      const t = SUP().get(ctx.params.id);
      if (!t || t.from?.kind !== 'customer' || t.from.id !== store.get().user?.id) return `<div class="container section">${SA.ui.c.empty({ icon: 'message', title: 'Request not found', action: '<a class="btn btn-primary" href="#/support">Help & support</a>' })}</div>`;
      const [c, l] = STATUS[t.status];
      return `<div class="container section-tight sup-page">
        <div class="page-head"><a class="back-link" href="#/support">${icon('chevronLeft', 16)} Help & support</a><h1 class="h2" style="margin-top:10px">${esc(t.subject)}</h1><p class="small muted">${esc(t.id)} · ${esc(SUP().CATS[t.category]?.label || '')}${t.ref ? ` · ${esc(t.ref.label || t.ref.id)}` : ''} · <span class="badge ${c}">${l}</span></p></div>
        <section class="panel panel-pad">${thread(t)}
          ${t.status !== 'resolved' ? `<form class="stack" style="--gap:10px;margin-top:16px" data-reply novalidate><label class="field-label" for="sp-rp">Reply</label><textarea class="input" id="sp-rp" name="text" rows="3" maxlength="2000"></textarea><p class="field-error" data-err="text"></p><div class="row" style="justify-content:flex-end"><button class="btn btn-primary" type="submit">Send</button></div></form>`
            : `<p class="small muted" style="margin-top:14px">${icon('check', 14, 2.4)} This request is resolved. Still need help? <a class="link" href="#/support">Open a new one</a>.</p>`}</section></div>`;
    },
    mount(el, ctx) {
      el.querySelector('[data-reply]')?.addEventListener('submit', (e) => {
        e.preventDefault();
        try { SUP().reply(ctx.params.id, e.target.text.value); toast('Message sent'); SA.app.rerender(); } catch (err) { e.target.querySelector('[data-err="text"]').textContent = err.message; }
      });
    },
  };

  /* ======================= Partner dashboard ======================= */
  const C = () => SA.pages.dashCommon;
  const D = () => SA.services.dash;
  const KIND = { owner: 'partner', seller: 'seller', printer: 'printer' };
  const fromMe = () => { const a = D().me(); return { kind: KIND[a.role] || 'partner', id: a.id, name: a.name }; };
  const dashSupport = {
    roles: () => true,
    title: 'Help & support — Super App Partner',
    render() {
      const mine = SUP().forAccount(D().me().id);
      const body = `<div class="dash-grid g-1-1"><section class="dash-card"><h2 class="h4">New request</h2><p class="small soft" style="margin-top:4px">Payouts, your account, the shop, the print queue — anything you need from the Super App team.</p>${form({ withRef: false })}</section>
        <section class="dash-card"><h2 class="h4" style="margin-bottom:12px">Your requests</h2>${list(mine, '#/dash/support')}</section></div>`;
      return C().layout('support', { title: 'Help & support', subtitle: 'Talk to the Super App team', body });
    },
    mount(el) { bindForm(el, { withRef: false, from: fromMe(), after: (t) => SA.core.router.go(`/dash/support/${t.id}`) }); },
  };
  const dashTicket = {
    roles: () => true,
    title: 'Request — Super App Partner',
    render(ctx) {
      const t = SUP().get(ctx.params.id);
      if (!t || t.from?.id !== D().me().id) return C().layout('support', { title: 'Not found', body: `<div class="dash-card">${SA.ui.c.empty({ icon: 'message', title: 'Request not found', action: '<a class="btn btn-primary" href="#/dash/support">Help & support</a>' })}</div>` });
      const [c, l] = STATUS[t.status];
      const body = `<section class="dash-card"><p class="small muted">${esc(t.id)} · ${esc(SUP().CATS[t.category]?.label || '')} · <span class="badge ${c}">${l}</span></p>${thread(t)}
        ${t.status !== 'resolved' ? `<form class="stack" style="--gap:10px;margin-top:16px" data-reply novalidate><label class="field-label" for="ds-rp">Reply</label><textarea class="input" id="ds-rp" name="text" rows="3" maxlength="2000"></textarea><p class="field-error" data-err="text"></p><div class="row" style="justify-content:flex-end"><button class="btn btn-primary" type="submit">Send</button></div></form>` : `<p class="small muted" style="margin-top:14px">${icon('check', 14, 2.4)} Resolved.</p>`}</section>`;
      return C().layout('support', { title: esc(t.subject), actions: '<a class="btn btn-ghost btn-sm" href="#/dash/support">All requests</a>', body });
    },
    mount(el, ctx) {
      el.querySelector('[data-reply]')?.addEventListener('submit', (e) => {
        e.preventDefault();
        try { SUP().reply(ctx.params.id, e.target.text.value, fromMe()); toast('Message sent'); SA.app.rerender(); } catch (err) { e.target.querySelector('[data-err="text"]').textContent = err.message; }
      });
    },
  };
  SA.pages.dashSupport = SA.pages.dashCommon.guard(dashSupport);
  SA.pages.dashSupportTicket = SA.pages.dashCommon.guard(dashTicket);
})(window.SA);
