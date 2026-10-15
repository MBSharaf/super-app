/**
 * Admin console: communities — reports queue, every post (moderate, pin, close comments, reply as "Super App Admin"),
 * new posts as "Super App Admin", community settings (create / edit / archive, audience, slow mode, rules) and safety
 * (banned words, phone numbers and links, muted members).
 */
(function (SA) {
  const { esc, timeAgo, fmtDate } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const A = SA.pages.adminCommon;
  const AD = () => SA.services.admin;
  const CS = () => SA.services.community;
  const db = () => SA.services.db;
  const media = SA.data.media;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });
  const REASON = { spam: 'Spam or advertising', abuse: 'Insults or hate', scam: 'Scam or fake', offtopic: 'Off-topic', other: 'Something else' };
  let draftImage = null;

  /** Admin view of a post: hidden comments included */
  const adminView = (p) => ({ ...CS().postView(p), allComments: (p.comments || []).filter((x) => !x.deleted), community: db().find('communities', p.communityId) });

  const muteDialog = (userId, name, communityId) => A.reason({
    title: `Mute ${esc(name)}?`, text: 'They can still read, but can’t post or comment until the mute ends.', label: 'Reason they’ll see', confirmLabel: 'Mute', danger: true,
    extra: `<div class="form-grid"><div class="field"><label for="mu-d">For</label><select class="select" id="mu-d" name="days"><option value="1">1 day</option><option value="7" selected>7 days</option><option value="30">30 days</option><option value="365">1 year</option></select></div>
      <div class="field"><label for="mu-w">Where</label><select class="select" id="mu-w" name="where"><option value="here">This community</option><option value="all">All communities</option></select></div></div>`,
  }).then((r) => { if (!r) return false; AD().muteMember({ userId, name, communityId: r.where === 'all' ? null : communityId, days: +r.days, reason: r.reason }); toast(`${name} muted`, 'info'); return true; });

  /** Admin actions on post cards (shared post card from js/pages/community.js) */
  const onAdmin = async (action, postId, commentId) => {
    const p = db().find('communityPosts', postId);
    const cm = commentId ? p.comments.find((x) => x.id === commentId) : null;
    if (action === 'pin' || action === 'unpin') { AD().setPost(postId, { pinned: action === 'pin' }, action === 'pin' ? 'Pinned post' : 'Unpinned post'); toast(action === 'pin' ? 'Pinned' : 'Unpinned'); }
    else if (action === 'lock' || action === 'unlock') { AD().setPost(postId, { locked: action === 'lock' }, action === 'lock' ? 'Closed comments' : 'Opened comments'); toast(action === 'lock' ? 'Comments closed' : 'Comments open'); }
    else if (action === 'hide' || action === 'show') { AD().setPost(postId, { hidden: action === 'hide' }, action === 'hide' ? 'Hid post' : 'Restored post'); toast(action === 'hide' ? 'Post hidden' : 'Post restored', 'info'); }
    else if (action === 'delete') { if (!(await SA.ui.overlay.confirm({ title: 'Delete this post?', text: 'It’s removed for everyone, with its comments. It stays in the audit log.', confirmLabel: 'Delete', danger: true }))) return; AD().setPost(postId, { deleted: true }, 'Deleted post'); toast('Post deleted', 'info'); }
    else if (action === 'mute') { if (!(await muteDialog(p.author.id, p.author.name, p.communityId))) return; }
    else if (action === 'c-hide' || action === 'c-show') { AD().setComment(postId, commentId, { hidden: action === 'c-hide' }, action === 'c-hide' ? 'Hid' : 'Restored'); toast(action === 'c-hide' ? 'Comment hidden' : 'Comment restored', 'info'); }
    else if (action === 'c-mute') { if (!(await muteDialog(cm.author.id, cm.author.name, p.communityId))) return; }
    SA.app.rerender();
  };

  const communityDialog = (c = null) => SA.ui.overlay.open({
    wide: true, title: c ? `Edit ${esc(c.name)}` : 'New community', subtitle: 'Only Super App creates communities. Choose who can join and the rules.',
    body: `<form class="form-grid" data-cmf novalidate>
      <div class="field"><label for="cf-n">Name</label><input class="input" id="cf-n" name="name" maxlength="40" value="${esc(c?.name || '')}" /><p class="field-error" data-err="name"></p></div>
      <div class="field"><label for="cf-s">Link (English)</label><input class="input mono" id="cf-s" name="slug" maxlength="30" value="${esc(c?.slug || '')}" placeholder="e.g. padel-players" /><p class="field-error" data-err="slug"></p></div>
      <div class="field span-2"><label for="cf-a">About</label><textarea class="input" id="cf-a" name="about" rows="2" maxlength="240">${esc(c?.about || '')}</textarea><p class="field-error" data-err="about"></p></div>
      <div class="field"><label for="cf-w">Who can join</label><select class="select" id="cf-w" name="audience">${Object.entries(CS().AUDIENCE).map(([k, l]) => `<option value="${k}" ${c?.audience === k ? 'selected' : ''}>${l}</option>`).join('')}</select><p class="field-error" data-err="audience"></p></div>
      <div class="field"><label for="cf-m">Slow mode (minutes between posts, 0 = off)</label><input class="input num" id="cf-m" name="slowMode" type="number" min="0" max="120" value="${c?.slowMode || 0}" /></div>
      <div class="field"><label for="cf-i">Icon</label><select class="select" id="cf-i" name="icon">${['users', 'shirt', 'crown', 'trophy', 'ball', 'pitch', 'image', 'flag', 'megaphone', 'racket', 'whistle'].map((k) => `<option ${c?.icon === k ? 'selected' : ''}>${k}</option>`).join('')}</select></div>
      <div class="field"><label for="cf-c">Colour</label><input class="input" id="cf-c" name="color" type="color" value="${esc(c?.color || '#0B6E4F')}" /></div>
      <div class="field span-2"><label for="cf-p">Cover photo</label><select class="select" id="cf-p" name="cover">${['stadiumNight', 'nightMatch', 'litField', 'stadium', 'aerialPitch', 'greenTeam', 'kidsTraining', 'trophy', 'cairoNile'].filter((k) => media.photos[k]).map((k) => `<option ${c?.cover === k ? 'selected' : ''}>${k}</option>`).join('')}</select></div>
      <div class="field span-2"><label for="cf-r">Rules (one per line)</label><textarea class="input" id="cf-r" name="rules" rows="4">${esc((c?.rules || ['Be respectful — no insults, no fan fights.', 'No phone numbers or outside links: keep deals on Super App.']).join('\n'))}</textarea></div>
      <div class="span-2 row" style="--gap:8px;justify-content:flex-end"><button type="button" class="btn btn-ghost" data-close>Cancel</button><button class="btn btn-primary" type="submit">${c ? 'Save' : 'Create community'}</button></div></form>`,
    onMount(m, close) { m.querySelector('[data-cmf]').addEventListener('submit', (e) => { e.preventDefault(); try { AD().saveCommunity(SA.core.utils.formData(e.target), c?.id); close(); toast(c ? 'Community saved' : 'Community created'); SA.app.rerender(); } catch (err) { C.showErrors(e.target, err.fields || {}); } }); },
  });

  SA.pages.adminCommunity = A.guard({
    title: 'Communities — Super App Admin',
    render(ctx) {
      const q = ctx.query;
      const tab = q.tab || '';
      const reports = AD().communityReports();
      const comms = db().all('communities');
      const settings = db().settings().community || {};
      const activeMutes = (settings.muted || []).filter((x) => x.until > Date.now());
      let section = '';
      if (!tab) {
        section = reports.length ? `<div class="cm-feed">${reports.map((r) => `<section class="dash-card adm-report"><div class="row-between wrap" style="--gap:8px"><span class="row wrap" style="--gap:6px"><span class="badge badge-danger">${esc(REASON[r.reason] || r.reason)}</span><span class="badge">${r.comment ? 'Comment' : 'Post'}</span><span class="xs muted">${esc(r.community?.name || '')} · reported by ${esc(r.byName || 'a member')} · ${timeAgo(r.at)}</span></span></div>
            <div class="adm-report-body">${SA.ui.c.avatar({ name: r.target.author.name, avatar: r.target.author.avatar }, 'sm')}<div><b class="small">${esc(r.target.author.name)}</b><p class="small" style="margin-top:2px">${esc(r.target.text)}</p></div></div>
            <div class="row wrap" style="--gap:8px;justify-content:flex-end"><a class="btn btn-ghost btn-sm" href="#/admin/community?tab=posts&post=${r.post.id}">Open post</a><button class="btn btn-ghost btn-sm" data-rep="${r.post.id}|${r.id}|keep">Keep it</button>
              ${r.target.author.kind === 'user' ? `<button class="btn btn-outline btn-sm" data-rep-mute="${r.post.id}|${r.id}">Remove & mute author</button>` : ''}<button class="btn btn-primary btn-sm" data-rep="${r.post.id}|${r.id}|hide">${icon('ban', 13)} Remove</button></div></section>`).join('')}</div>`
          : `<div class="dash-card">${SA.ui.c.empty({ icon: 'flag', title: 'No reports waiting', text: 'Posts and comments members report show up here.' })}</div>`;
      } else if (tab === 'posts') {
        const cid = q.c || '';
        const s = (q.q || '').toLowerCase();
        const posts = db().all('communityPosts').filter((p) => !p.deleted && (!cid || p.communityId === cid) && (!q.post || p.id === q.post) && (!s || `${p.text} ${p.author.name}`.toLowerCase().includes(s))).sort((a, b) => (b.pinned - a.pinned) || b.at - a.at).slice(0, 40);
        if (q.post) SA.ui.community.open.add(q.post);
        section = `<section class="dash-card"><form class="adm-filters" data-pf><label class="field"><span class="field-label">Community</span><select class="select" name="c"><option value="">All</option>${comms.map((c) => `<option value="${c.id}" ${cid === c.id ? 'selected' : ''}>${esc(c.name)}</option>`).join('')}</select></label>
            <label class="field grow"><span class="field-label">Search</span><input class="input" name="q" value="${esc(q.q || '')}" placeholder="Text or member name…" /></label><button class="btn btn-primary" type="submit" style="align-self:end">Filter</button>${q.post ? '<a class="btn btn-ghost" href="#/admin/community?tab=posts" style="align-self:end">Show all</a>' : ''}</form></section>
          <div class="cm-feed adm-cm-feed">${posts.map((p) => SA.ui.community.postCard(adminView(p), { admin: true, showCommunity: true })).join('') || `<div class="dash-card">${SA.ui.c.empty({ icon: 'message', title: 'No posts found' })}</div>`}</div>`;
      } else if (tab === 'new') {
        section = `<section class="dash-card"><h2 class="h4">${icon('shield', 18)} Post as Super App Admin</h2><p class="small soft" style="margin-top:4px">Tell members something right away. It shows with the official badge; members of the community get a notification.</p>
          <form class="form-grid" data-np novalidate style="margin-top:14px">
            <fieldset class="field span-2"><legend class="field-label">Post in</legend><div class="chips" style="margin-top:6px">${comms.filter((c) => c.status !== 'archived').map((c, i) => `<label class="chip"><input type="checkbox" name="to" value="${c.id}" ${i === 0 ? 'checked' : ''} hidden /><span>${esc(c.name)}</span></label>`).join('')}</div><p class="field-error" data-err="to"></p></fieldset>
            <div class="field span-2"><label for="np-t">Post</label><textarea class="input" id="np-t" name="text" rows="5" maxlength="3000" placeholder="e.g. New this week: …"></textarea><p class="field-error" data-err="text"></p></div>
            <div class="field span-2" data-np-poll hidden><span class="field-label">Poll options (2–6)</span>${[0, 1, 2, 3].map((i) => `<input class="input" name="opt" placeholder="Option ${i + 1}" maxlength="80" style="margin-top:6px" />`).join('')}<p class="field-error" data-err="poll"></p></div>
            <div class="span-2 cm-img-preview" data-np-img hidden></div>
            <label class="choice"><input type="checkbox" name="announcement" checked /><span class="radio-dot" style="border-radius:6px"></span><span class="grow"><b>Mark as announcement</b></span></label>
            <label class="choice"><input type="checkbox" name="pinned" /><span class="radio-dot" style="border-radius:6px"></span><span class="grow"><b>Pin to the top</b></span></label>
            <div class="span-2 row-between wrap" style="--gap:8px"><span class="row" style="--gap:4px"><button type="button" class="btn btn-ghost btn-sm" data-np-photo>${icon('image', 15)} Photo</button><button type="button" class="btn btn-ghost btn-sm" data-np-pollbtn>${icon('chart', 15)} Poll</button></span><button class="btn btn-primary" type="submit">${icon('megaphone', 15)} Publish</button></div>
          </form></section>`;
      } else if (tab === 'communities') {
        section = `<section class="dash-card table-card"><div class="row-between" style="padding:18px 18px 0"><h2 class="h4">Communities</h2><button class="btn btn-primary btn-sm" data-new-c>${icon('plus', 14, 2.6)} New community</button></div><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Community</th><th>Who can join</th><th class="r">Members</th><th class="r">Posts</th><th>Slow mode</th><th>Status</th><th></th></tr></thead><tbody>
          ${comms.map((c) => { const v = CS().view(c); return `<tr class="${c.status === 'archived' ? 'is-dim' : ''}"><td class="small"><span class="row" style="--gap:10px"><span class="adm-cat-ico" style="background:${esc(c.color)};color:#fff">${icon(c.icon, 16)}</span><span><b>${esc(c.name)}</b><span class="xs muted" style="display:block">/community/${esc(c.slug)}</span></span></span></td><td class="small">${esc(CS().AUDIENCE[c.audience])}</td><td class="r num">${v.members.toLocaleString()}</td><td class="r num">${v.posts}</td><td class="small">${c.slowMode ? `${c.slowMode} min` : 'Off'}</td>
            <td><span class="badge ${c.status === 'archived' ? '' : 'badge-turf'}">${c.status === 'archived' ? 'Archived' : 'Live'}</span></td><td class="r"><div class="row" style="--gap:4px;justify-content:flex-end"><a class="btn btn-ghost btn-sm" href="#/community/${esc(c.slug)}" target="_blank" rel="noopener">View</a><button class="btn btn-ghost btn-sm" data-edit-c="${c.id}">Edit</button><button class="btn btn-ghost btn-sm" data-arch-c="${c.id}" data-to="${c.status === 'archived' ? 'active' : 'archived'}">${c.status === 'archived' ? 'Restore' : 'Archive'}</button></div></td></tr>`; }).join('')}
          </tbody></table></div></section>`;
      } else {
        section = `<div class="dash-grid g-1-1"><section class="dash-card"><h2 class="h4">Filter</h2><p class="small soft" style="margin-top:4px">Posts and comments with these words are blocked before they’re published.</p>
            <form data-filter novalidate class="stack" style="--gap:10px;margin-top:12px"><div class="field"><label for="fl-w">Banned words (comma or new line)</label><textarea class="input" id="fl-w" name="words" rows="6">${esc((settings.bannedWords || []).join('\n'))}</textarea></div>
              <label class="choice"><input type="checkbox" name="blockContacts" ${settings.blockContacts !== false ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span><span class="grow"><b>Block phone numbers and outside links</b><span class="xs muted" style="display:block">Keeps deals and payments on Super App and stops spam.</span></span></label>
              <div><button class="btn btn-primary" type="submit">Save filter</button></div></form></section>
          <section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Muted members</h2><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Member</th><th>Where</th><th>Until</th><th></th></tr></thead><tbody>
            ${activeMutes.map((x) => `<tr><td class="small"><b>${esc(x.name)}</b><span class="xs muted" style="display:block">${esc(x.reason)}</span></td><td class="small">${x.communityId ? esc(db().find('communities', x.communityId)?.name || '') : 'All communities'}</td><td class="small">${fmtDate(new Date(x.until))}</td><td class="r"><button class="btn btn-ghost btn-sm" data-unmute="${esc(x.userId)}|${x.communityId || ''}">Unmute</button></td></tr>`).join('') || '<tr><td colspan="4" class="small muted" style="padding:18px">Nobody is muted.</td></tr>'}
          </tbody></table></div></section></div>`;
      }
      const posts = db().all('communityPosts').filter((p) => !p.deleted);
      const body = `<div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'Reports waiting', value: reports.length, ic: 'flag', tone: reports.length ? 'k-blaze' : '' })}
          ${C.kpi({ label: 'Communities', value: comms.filter((c) => c.status !== 'archived').length, ic: 'users' })}
          ${C.kpi({ label: 'Posts this week', value: posts.filter((p) => Date.now() - p.at < 7 * 864e5).length, ic: 'message', tone: 'k-turf' })}
          ${C.kpi({ label: 'Muted members', value: activeMutes.length, ic: 'ban' })}
        </div>
        ${A.tabs('/admin/community', [['', 'Reports', reports.length], ['posts', 'All posts'], ['new', 'Post as Super App Admin'], ['communities', 'Communities', comms.length], ['safety', 'Safety & muted']], tab)}${section}`;
      return C.layout('community', { title: 'Communities', subtitle: 'Moderate, post and reply as Super App Admin, and manage the communities', actions: '<a class="btn btn-ghost btn-sm" href="#/community" target="_blank" rel="noopener">Open on the site ' + icon('arrowUpRight', 14) + '</a>', body });
    },
    mount(el, ctx) {
      SA.ui.community.bindPosts(el, { onAdmin });
      el.querySelector('[data-pf]')?.addEventListener('submit', (e) => { e.preventDefault(); const f = SA.core.utils.formData(e.target); SA.core.router.go(SA.core.router.href('/admin/community', { tab: 'posts', c: f.c || undefined, q: f.q || undefined }).slice(1)); });
      el.querySelector('[data-filter]')?.addEventListener('submit', (e) => { e.preventDefault(); const f = SA.core.utils.formData(e.target); AD().setCommunityFilter({ words: f.words, blockContacts: !!f.blockContacts }); toast('Filter saved'); SA.app.rerender(); });
      // New post as Super App Admin
      const np = el.querySelector('[data-np]');
      if (np) {
        draftImage = null;
        np.querySelectorAll('.chip input').forEach((i) => { const sync = () => i.closest('.chip').setAttribute('aria-pressed', i.checked); sync(); i.addEventListener('change', sync); });
        np.querySelector('[data-np-pollbtn]').addEventListener('click', () => { const p = np.querySelector('[data-np-poll]'); p.hidden = !p.hidden; });
        np.querySelector('[data-np-photo]').addEventListener('click', async () => { try { const img = await SA.ui.pickImage({ max: 1400 }); if (!img) return; draftImage = img; const pv = np.querySelector('[data-np-img]'); pv.hidden = false; pv.innerHTML = `<img src="${img}" alt="" />`; } catch (err) { toast(err.message, 'error'); } });
        np.addEventListener('submit', (e) => {
          e.preventDefault();
          const to = [...np.querySelectorAll('input[name=to]:checked')].map((i) => i.value);
          if (!to.length) { C.showErrors(np, { to: 'Choose at least one community.' }); return; }
          const poll = np.querySelector('[data-np-poll]').hidden ? [] : [...np.querySelectorAll('[name=opt]')].map((i) => i.value);
          try { to.forEach((cid) => AD().communityPost(cid, { text: np.text.value, poll, image: draftImage, pinned: np.pinned.checked, announcement: np.announcement.checked })); draftImage = null; toast(`Published in ${to.length} communit${to.length > 1 ? 'ies' : 'y'}`); SA.core.router.go('/admin/community?tab=posts'); }
          catch (err) { C.showErrors(np, err.fields || { text: err.message }); }
        });
      }
      el.addEventListener('click', async (e) => {
        const t = (s) => e.target.closest(s);
        try {
          if (t('[data-rep]')) { const [pid, rid, act] = t('[data-rep]').dataset.rep.split('|'); AD().resolveCommunityReport(pid, rid, act); toast(act === 'hide' ? 'Removed' : 'Kept', 'info'); SA.app.rerender(); return; }
          if (t('[data-rep-mute]')) { const [pid, rid] = t('[data-rep-mute]').dataset.repMute.split('|'); const r = AD().communityReports().find((x) => x.id === rid); if (!(await muteDialog(r.target.author.id, r.target.author.name, r.post.communityId))) return; AD().resolveCommunityReport(pid, rid, 'hide'); SA.app.rerender(); return; }
          if (t('[data-new-c]')) return communityDialog();
          if (t('[data-edit-c]')) return communityDialog(db().find('communities', t('[data-edit-c]').dataset.editC));
          if (t('[data-arch-c]')) { const b = t('[data-arch-c]'); AD().setCommunityStatus(b.dataset.archC, b.dataset.to); toast(b.dataset.to === 'archived' ? 'Community archived' : 'Community restored', 'info'); SA.app.rerender(); return; }
          if (t('[data-unmute]')) { const [uid, cid] = t('[data-unmute]').dataset.unmute.split('|'); AD().unmuteMember(uid, cid || null); toast('Unmuted'); SA.app.rerender(); }
        } catch (err) { toast(err.message, 'error'); }
      });
      void ctx;
    },
  });
})(window.SA);
