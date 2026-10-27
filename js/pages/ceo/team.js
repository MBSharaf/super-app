/**
 * CEO system: the team on the server (backend R3) — #/ceo/team (admins, invite one), #/ceo/team/:id (one admin: roles,
 * extra permissions, money limits, suspend / reactivate, reset the authenticator, send the invitation again) and
 * #/ceo/roles (the permission matrix: admin roles and their permissions; add, edit and delete roles).
 * Server: /ceo/admins, /ceo/roles, /ceo/permissions. Demo mode explains that these need the server.
 */
(function (SA) {
  const { esc, timeAgo, fmtDate } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.ceoCommon;
  const A = () => SA.pages.adminCommon;
  const api = () => SA.services.api;
  const at = (iso) => (iso ? new Date(iso.endsWith('Z') ? iso : `${iso}Z`).getTime() : 0);
  const serverOnly = (what) => `<section class="dash-card"><p class="small muted">${icon('info', 14)} ${what} come from the server. Open the Command Center on the API site to see them.</p></section>`;
  const ar = () => SA.i18n?.lang === 'ar';
  const permName = (p) => `<span data-i18n-skip>${esc(ar() ? p.nameAr : p.nameEn)}</span>`;
  const adminRoles = (roles) => roles.filter((r) => r.kind === 'AdminRole');
  const checks = (form, name) => [...form.querySelectorAll(`[name="${name}"]:checked`)].map((x) => x.value);
  const box = (name, value, on, label) => `<label class="choice"><input type="checkbox" name="${name}" value="${esc(value)}" ${on ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span><span class="grow small">${label}</span></label>`;

  /** Permission checkboxes grouped by area; `fixed` ones (from roles) show ticked and locked */
  const permGrid = (catalogue, name, chosen, fixed = []) => {
    const areas = [...new Set(catalogue.map((p) => p.area))];
    return `<div class="ceo-perms">${areas.map((a) => `<fieldset><legend class="xs muted">${esc(a)}</legend>${catalogue.filter((p) => p.area === a).map((p) => fixed.includes(p.code)
      ? `<label class="choice is-dim"><input type="checkbox" checked disabled /><span class="radio-dot" style="border-radius:6px"></span><span class="grow small">${permName(p)} <span class="xs muted">· from a role</span></span></label>`
      : box(name, p.code, chosen.includes(p.code), `${permName(p)}${p.isSensitive ? ` ${icon('lock', 11)}` : ''}`)).join('')}</fieldset>`).join('')}</div>`;
  };

  const status = (a) => (a.status === 'Suspended' ? '<span class="badge badge-danger">Suspended</span>'
    : a.invitationPending ? '<span class="badge badge-gold">Invitation sent</span>' : '<span class="badge badge-turf">Active</span>');
  const twoFa = (a) => (a.twoFactorEnabled ? `<span class="ceo-chip">${icon('lock', 12)} Authenticator on</span>` : `<span class="ceo-chip">${icon('alert', 12)} No authenticator yet</span>`);
  const roleChips = (a) => a.roles.map((r) => `<span class="ceo-tag">${esc(r)}</span>`).join(' ') || '<span class="xs muted">No role</span>';

  /* ---------- The team ---------- */
  const inviteForm = (roles) => `<details class="dash-card"><summary class="h4">${icon('plus', 16)} Invite an admin</summary>
    <form data-invite style="margin-top:10px">
      <div class="dash-grid g-1-1">
        <div class="field"><label for="tm-name">Full name</label><input class="input" id="tm-name" name="fullName" required maxlength="80" /></div>
        <div class="field"><label for="tm-mail">Email</label><input class="input" id="tm-mail" name="email" type="email" required maxlength="120" /></div>
      </div>
      <h3 class="h5" style="margin-top:10px">Roles</h3><div class="ceo-secs">${adminRoles(roles).map((r) => box('role', r.name, false, esc(r.name))).join('')}</div>
      <div class="field" style="max-width:220px;margin-top:10px"><label for="tm-lang">Invitation language</label><select class="input" id="tm-lang" name="language"><option value="ar">Arabic</option><option value="en">English</option></select></div>
      <p class="xs muted">${icon('info', 12)} They get an email to choose a password, then set up an authenticator app at the first sign-in. Money limits start at 0 — set them on their page.</p>
      <button class="btn btn-primary btn-sm" type="submit" style="margin-top:10px">Send the invitation</button>
    </form></details>`;

  SA.pages.ceoTeam = C.guard({
    title: 'Team — Super App',
    async render() {
      const sub = 'Admins, their roles, permissions and money limits';
      if (!api().isLive()) return C.layout('team', { title: 'Team', subtitle: sub, body: serverOnly('The team accounts') });
      const [list, roles] = await Promise.all([api().get('/ceo/admins'), api().get('/ceo/roles')]);
      const body = `${inviteForm(roles)}
        <section class="dash-card table-card"><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Admin</th><th>Roles</th><th>Status</th><th>Last seen</th></tr></thead><tbody>
        ${list.map((a) => `<tr><td class="small"><a class="link" href="#/ceo/team/${a.id}"><b>${esc(a.fullName)}</b></a><span class="xs muted" style="display:block">${esc(a.email)}</span></td>
          <td class="small">${roleChips(a)}${a.directPermissions.length ? ` <span class="xs muted">+${a.directPermissions.length} extra</span>` : ''}</td>
          <td>${status(a)}</td><td class="xs muted">${a.lastSeenAt ? timeAgo(at(a.lastSeenAt)) : '—'}</td></tr>`).join('') || '<tr><td colspan="4" class="small muted">No admins yet. Invite the first one above.</td></tr>'}
        </tbody></table></div></section>`;
      return C.layout('team', { title: 'Team', subtitle: sub, actions: '<a class="btn btn-ghost btn-sm" href="#/ceo/roles">Roles & permissions</a>', body });
    },
    mount(el) {
      el.querySelector('[data-invite]')?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const f = e.currentTarget;
        const a = await C.run(() => api().post('/ceo/admins', { fullName: f.fullName.value.trim(), email: f.email.value.trim(), roles: checks(f, 'role'), language: f.language.value }), 'Invitation sent');
        if (a?.id) SA.core.router.go(`/ceo/team/${a.id}`);
      });
    },
  });

  /* ---------- One admin ---------- */
  const actionsCard = (a) => `<section class="dash-card"><h2 class="h4">Account</h2>
    <ul class="ceo-list">
      <li><span class="grow small">Status</span>${status(a)}</li>
      ${a.suspendedReason ? `<li><span class="grow small t-danger">${esc(a.suspendedReason)}</span></li>` : ''}
      <li><span class="grow small">Two-step sign-in</span>${twoFa(a)}</li>
      <li><span class="grow small">Added</span><b class="small">${fmtDate(new Date(at(a.createdAt)), 'long')}</b></li>
      <li><span class="grow small">Last seen</span><b class="small">${a.lastSeenAt ? timeAgo(at(a.lastSeenAt)) : '—'}</b></li>
      <li><span class="grow small">Permissions in all</span><b class="num">${a.allPermissions.length}</b></li>
    </ul>
    <div class="row" style="--gap:8px;margin-top:14px;flex-wrap:wrap">
      ${a.status === 'Suspended' ? `<button class="btn btn-primary btn-sm" data-reactivate>${icon('play', 14)} Reactivate</button>` : `<button class="btn btn-blaze btn-sm" data-suspend>${icon('ban', 14)} Suspend the account</button>`}
      ${a.twoFactorEnabled ? `<button class="btn btn-ghost btn-sm" data-reset2fa>${icon('refresh', 14)} Reset the authenticator</button>` : ''}
      ${a.invitationPending ? `<button class="btn btn-ghost btn-sm" data-reinvite>${icon('message', 14)} Send the invitation again</button>` : ''}
    </div></section>`;

  const limitsCard = (l) => `<form class="dash-card" data-limits>
    <div class="row-between"><h2 class="h4">Money limits</h2><button class="btn btn-primary btn-sm" type="submit">Save</button></div>
    <p class="xs muted">${icon('info', 12)} The most they may move alone (EGP). Above it, the action becomes a request for approval.</p>
    <div class="dash-grid g-1-1" style="margin-top:10px">
      <div class="field"><label for="lm-r">Refunds</label><input class="input num" id="lm-r" name="refund" type="number" min="0" max="1000000" step="50" value="${l.refund}" /></div>
      <div class="field"><label for="lm-c">Credits</label><input class="input num" id="lm-c" name="credit" type="number" min="0" max="1000000" step="50" value="${l.credit}" /></div>
      <div class="field"><label for="lm-p">Withdrawals</label><input class="input num" id="lm-p" name="payout" type="number" min="0" max="10000000" step="100" value="${l.payout}" /></div>
    </div>
    ${box('second', 'yes', l.canSecondApprove, 'May give the second approval on big money actions')}</form>`;

  SA.pages.ceoTeamMember = C.guard({
    title: 'Admin — Super App',
    async render(ctx) {
      if (!api().isLive()) return C.layout('team', { title: 'Admin', body: serverOnly('The team accounts') });
      const [a, roles, catalogue] = await Promise.all([api().get(`/ceo/admins/${ctx.params.id}`), api().get('/ceo/roles'), api().get('/ceo/permissions')]);
      const fromRoles = roles.filter((r) => a.roles.includes(r.name)).flatMap((r) => r.permissions);
      const direct = a.directPermissions.map((d) => d.code);
      const until = a.directPermissions.find((d) => d.expiresAt)?.expiresAt;
      const body = `
        <div class="dash-grid g-2-1">
          <form class="dash-card" data-roles>
            <div class="row-between"><h2 class="h4">Name and roles</h2><button class="btn btn-primary btn-sm" type="submit">Save</button></div>
            <div class="field" style="margin-top:10px"><label for="am-name">Full name</label><input class="input" id="am-name" name="fullName" required maxlength="80" value="${esc(a.fullName)}" /></div>
            <div class="ceo-secs">${adminRoles(roles).map((r) => box('role', r.name, a.roles.includes(r.name), `${esc(r.name)}<span class="xs muted" style="display:block">${esc(r.description || '')}</span>`)).join('')}</div>
          </form>
          ${actionsCard(a)}
        </div>
        ${limitsCard(a.limits)}
        <form class="dash-card" data-perms>
          <div class="row-between"><h2 class="h4">Extra permissions</h2><button class="btn btn-primary btn-sm" type="submit">Save</button></div>
          <p class="xs muted">${icon('info', 12)} On top of their roles — for example the approvals for one person. ${icon('lock', 11)} marks sensitive ones.</p>
          <div class="field" style="max-width:240px;margin-top:10px"><label for="am-until">Until (empty = no end)</label><input class="input" id="am-until" name="until" type="date" value="${until ? until.slice(0, 10) : ''}" /></div>
          ${permGrid(catalogue, 'perm', direct, fromRoles)}
        </form>`;
      return C.layout('team', { title: esc(a.fullName), subtitle: esc(a.email), actions: '<a class="btn btn-ghost btn-sm" href="#/ceo/team">The team</a>', body });
    },
    mount(el, ctx) {
      const base = `/ceo/admins/${ctx.params.id}`;
      const on = (sel, ev, fn) => el.querySelector(sel)?.addEventListener(ev, fn);
      on('[data-roles]', 'submit', (e) => { e.preventDefault(); const f = e.currentTarget; C.run(() => api().put(base, { fullName: f.fullName.value.trim(), roles: checks(f, 'role') }), 'Saved'); });
      on('[data-limits]', 'submit', (e) => {
        e.preventDefault(); const f = e.currentTarget;
        C.run(() => api().put(`${base}/limits`, { refund: +f.refund.value || 0, credit: +f.credit.value || 0, payout: +f.payout.value || 0, canSecondApprove: f.second.checked }), 'Money limits saved');
      });
      on('[data-perms]', 'submit', (e) => {
        e.preventDefault(); const f = e.currentTarget;
        C.run(() => api().put(`${base}/permissions`, { permissions: checks(f, 'perm'), expiresAt: f.until.value ? `${f.until.value}T23:59:59Z` : null }), 'Permissions saved');
      });
      on('[data-suspend]', 'click', async () => {
        const r = await A().reason({ title: 'Suspend this admin?', text: 'They are signed out everywhere at once and can’t sign in until you reactivate them.', confirmLabel: 'Suspend', danger: true });
        if (r) C.run(() => api().post(`${base}/suspend`, { reason: r.reason }), 'Suspended and signed out');
      });
      on('[data-reactivate]', 'click', () => C.run(() => api().post(`${base}/reactivate`), 'Active again'));
      on('[data-reset2fa]', 'click', async () => {
        if (await SA.ui.overlay.confirm({ title: 'Reset the authenticator?', text: 'For a lost phone. They set up a new authenticator at the next sign-in.', confirmLabel: 'Reset' })) C.run(() => api().post(`${base}/reset-2fa`), 'Authenticator cleared');
      });
      on('[data-reinvite]', 'click', () => C.run(() => api().post(`${base}/resend-invite`), 'Invitation sent again'));
    },
  });

  /* ---------- Roles & permissions ---------- */
  const roleCard = (r, catalogue) => `<form class="dash-card" data-role="${r.id}">
    <div class="row-between"><h2 class="h4">${esc(r.name)} ${r.isLocked ? `<span class="xs muted">${icon('lock', 11)} comes with the platform</span>` : ''}</h2>
      <span class="row" style="--gap:8px">${r.isLocked ? '' : `<button class="btn btn-ghost btn-sm" type="button" data-del-role>${icon('trash', 14)} Delete</button>`}<button class="btn btn-primary btn-sm" type="submit">Save</button></span></div>
    <p class="xs muted">${r.holders} ${r.holders === 1 ? 'person has' : 'people have'} this role · ${r.permissions.length} permissions</p>
    <div class="dash-grid g-1-1" style="margin-top:10px">
      <div class="field"><label>Name</label><input class="input" name="name" maxlength="40" value="${esc(r.name)}" ${r.isLocked ? 'readonly' : ''} /></div>
      <div class="field"><label>What it is for</label><input class="input" name="description" maxlength="300" value="${esc(r.description || '')}" /></div>
    </div>
    <details><summary class="small">Permissions</summary>${permGrid(catalogue, 'perm', r.permissions)}</details></form>`;

  SA.pages.ceoRoles = C.guard({
    title: 'Roles & permissions — Super App',
    async render() {
      const sub = 'What each admin role may do — changes reach everyone with the role within a minute';
      if (!api().isLive()) return C.layout('roles', { title: 'Roles & permissions', subtitle: sub, body: serverOnly('Roles and permissions') });
      const [roles, catalogue] = await Promise.all([api().get('/ceo/roles'), api().get('/ceo/permissions')]);
      const body = `<p class="small muted">${icon('info', 12)} You have every permission. Partner, seller, factory and customer roles are fixed; AI agents get theirs from the AI panel.</p>
        ${adminRoles(roles).map((r) => roleCard(r, catalogue)).join('')}
        <details class="dash-card"><summary class="h4">${icon('plus', 16)} Add a role</summary><form data-new-role style="margin-top:10px">
          <div class="dash-grid g-1-1">
            <div class="field"><label for="nr-name">Name (English letters)</label><input class="input" id="nr-name" name="name" required maxlength="40" placeholder="e.g. Night support" /></div>
            <div class="field"><label for="nr-desc">What it is for</label><input class="input" id="nr-desc" name="description" maxlength="300" /></div>
          </div>${permGrid(catalogue, 'perm', [])}
          <button class="btn btn-primary btn-sm" type="submit" style="margin-top:10px">Add the role</button></form></details>`;
      return C.layout('roles', { title: 'Roles & permissions', subtitle: sub, actions: '<a class="btn btn-ghost btn-sm" href="#/ceo/team">The team</a>', body });
    },
    mount(el) {
      const values = (f) => ({ name: f.name.value.trim(), description: f.description.value.trim() || null, permissions: checks(f, 'perm') });
      el.querySelectorAll('[data-role]').forEach((f) => {
        f.addEventListener('submit', (e) => { e.preventDefault(); C.run(() => api().put(`/ceo/roles/${f.dataset.role}`, values(f)), 'Role saved'); });
        f.querySelector('[data-del-role]')?.addEventListener('click', async () => {
          if (await SA.ui.overlay.confirm({ title: 'Delete this role?', text: 'Only a role nobody has can be deleted.', confirmLabel: 'Delete', danger: true })) C.run(() => api().del(`/ceo/roles/${f.dataset.role}`), 'Role deleted');
        });
      });
      el.querySelector('[data-new-role]')?.addEventListener('submit', (e) => { e.preventDefault(); C.run(() => api().post('/ceo/roles', values(e.currentTarget)), 'Role added'); });
    },
  });
})(window.SA);
