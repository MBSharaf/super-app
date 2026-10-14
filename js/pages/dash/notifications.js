/** Dashboard (every role): all notifications, filters, mark as read, and notification preferences. */
(function (SA) {
  const { esc, timeAgo } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const D = () => SA.services.dash;
  const N = () => SA.services.notify;

  /** Categories that matter for each kind of account */
  const catsFor = (role) => (role === 'seller' ? ['designs', 'orders', 'money', 'level', 'store', 'account'] : role === 'printer' ? ['orders', 'account'] : role === 'admin' ? ['designs', 'orders', 'store', 'account'] : ['orders', 'store', 'money', 'account']);
  const dayLabel = (ts) => { const d = new Date(ts), t = new Date(); t.setHours(0, 0, 0, 0); const diff = Math.round((t - new Date(d.getFullYear(), d.getMonth(), d.getDate())) / 864e5); return diff <= 0 ? 'Today' : diff === 1 ? 'Yesterday' : diff < 7 ? 'This week' : 'Earlier'; };

  SA.pages.dashNotifications = C.guard({
    roles: () => true,
    title: 'Notifications — Super App',
    render(ctx) {
      const a = D().me();
      const f = ctx.query.f || '';
      const cats = catsFor(a.role);
      const all = N().list(a.id);
      const list = f === 'unread' ? all.filter((n) => !n.read) : f ? all.filter((n) => n.cat === f) : all;
      const unread = all.filter((n) => !n.read).length;
      const prefs = N().prefs(a.id);
      const groups = [];
      list.forEach((n) => { const g = dayLabel(n.at); const last = groups[groups.length - 1]; if (!last || last.label !== g) groups.push({ label: g, items: [n] }); else last.items.push(n); });
      const body = `<div class="nt-layout">
        <div class="stack" style="--gap:14px">
          <div class="row-between wrap" style="--gap:10px">
            <div class="chips" role="tablist" aria-label="Filter">${[['', 'All', all.length], ['unread', 'Unread', unread], ...cats.map((c) => [c, N().CATS[c].label, all.filter((n) => n.cat === c).length])].map(([k, l, n]) => `<a class="chip" role="tab" href="#/dash/notifications${k ? `?f=${k}` : ''}" aria-pressed="${f === k}" aria-selected="${f === k}">${l} <span class="chip-count">${n}</span></a>`).join('')}</div>
            ${unread ? `<button class="btn btn-outline btn-sm" data-all>${icon('check', 14, 2.6)} Mark all as read</button>` : ''}
          </div>
          ${groups.length ? groups.map((g) => `<section class="dash-card nt-group"><h2 class="xs muted nt-day">${g.label}</h2><ul class="nt-list">${g.items.map((n) => `
            <li class="nt-item ${n.read ? '' : 'is-unread'}"><span class="bp-ico t-${n.tone || 'info'}">${icon(n.icon || 'bell', 16)}</span>
              <a class="grow nt-main" href="${esc(n.link || '#/dash/notifications')}" data-open="${n.id}"><b class="small">${esc(n.title)}</b><span class="small soft" style="display:block">${esc(n.body)}</span><span class="xs muted">${esc(N().CATS[n.cat]?.label || '')} · ${timeAgo(n.at)}</span></a>
              <span class="nt-acts">${n.read ? '' : `<button class="icon-btn" data-read="${n.id}" aria-label="Mark as read" title="Mark as read">${icon('check', 15, 2.4)}</button>`}<button class="icon-btn" data-del="${n.id}" aria-label="Delete" title="Delete">${icon('x', 15)}</button></span></li>`).join('')}</ul></section>`).join('')
            : `<div class="dash-card">${SA.ui.c.empty({ icon: 'bell', title: f === 'unread' ? 'You’re all caught up' : 'No notifications here', text: 'Orders, reviews, money and level updates show up here as they happen.' })}</div>`}
        </div>
        <aside class="dash-card nt-prefs">
          <h2 class="h4">What to notify me about</h2>
          <form data-prefs class="stack" style="--gap:6px;margin-top:10px">
            ${cats.map((c) => `<label class="nt-toggle"><span class="grow small">${icon(N().CATS[c].icon, 15)} ${N().CATS[c].label}</span><input type="checkbox" class="nt-switch" name="${c}" ${prefs[c] === false ? '' : 'checked'} ${c === 'account' ? 'disabled title="Account and safety notices are always on"' : ''} /></label>`).join('')}
          </form>
          <h3 class="small strong" style="margin-top:18px">Where</h3>
          <div class="stack" style="--gap:6px;margin-top:8px">
            <label class="nt-toggle"><span class="grow small">${icon('bell', 15)} In the dashboard</span><input type="checkbox" class="nt-switch" checked disabled /></label>
            <label class="nt-toggle"><span class="grow small">${icon('globe', 15)} Browser pop-ups</span><input type="checkbox" class="nt-switch" data-browser ${prefs.browser ? 'checked' : ''} /></label>
            <label class="nt-toggle is-soon"><span class="grow small">${icon('message', 15)} Email · SMS · WhatsApp</span><span class="xs muted">Coming soon</span></label>
          </div>
          <p class="xs muted" style="margin-top:10px">Browser pop-ups show while the dashboard is open in another tab.</p>
        </aside></div>`;
      return C.layout('notifications', { title: 'Notifications', subtitle: unread ? `${unread} unread` : 'All caught up', body });
    },
    mount(el) {
      const a = D().me();
      el.addEventListener('click', (e) => {
        if (e.target.closest('[data-all]')) { N().markAll(a.id); SA.app.rerender(); return; }
        const r = e.target.closest('[data-read]'); if (r) { N().markRead(r.dataset.read); SA.app.rerender(); return; }
        const d = e.target.closest('[data-del]'); if (d) { N().remove(d.dataset.del); SA.app.rerender(); return; }
        const o = e.target.closest('[data-open]'); if (o) N().markRead(o.dataset.open);
      });
      el.querySelector('[data-prefs]')?.addEventListener('change', (e) => {
        N().setPrefs(a.id, { [e.target.name]: e.target.checked });
        SA.ui.overlay.toast(e.target.checked ? 'You’ll be notified about this' : 'Muted — you won’t get these');
      });
      el.querySelector('[data-browser]')?.addEventListener('change', async (e) => {
        if (e.target.checked) {
          if (!('Notification' in window)) { e.target.checked = false; return SA.ui.overlay.toast('This browser doesn’t support pop-up notifications.', { type: 'error' }); }
          const p = Notification.permission === 'granted' ? 'granted' : await Notification.requestPermission();
          if (p !== 'granted') { e.target.checked = false; return SA.ui.overlay.toast('Allow notifications for this site in your browser settings first.', { type: 'error' }); }
        }
        N().setPrefs(a.id, { browser: e.target.checked });
        SA.ui.overlay.toast(e.target.checked ? 'Browser pop-ups on' : 'Browser pop-ups off');
      });
    },
  });
})(window.SA);
