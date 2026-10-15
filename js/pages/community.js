/**
 * Communities on the site: #/community (your feed + all communities), #/community/:slug (one community) and
 * #/community/:slug/:postId (one post). The post card and its actions are shared with the admin console, where the
 * Super App team also replies and posts as "Super App Admin" and moderates.
 */
(function (SA) {
  const { esc, timeAgo } = SA.core.utils;
  const { icon } = SA.ui;
  const media = SA.data.media;
  const CS = () => SA.services.community;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });
  const open = new Set(); // posts with comments expanded (kept across re-renders)
  const replyTarget = new Map(); // postId → commentId being replied to

  const REASONS = [['spam', 'Spam or advertising'], ['abuse', 'Insults or hate'], ['scam', 'Scam or fake'], ['offtopic', 'Off-topic'], ['other', 'Something else']];
  const badge = (b) => (b ? `<span class="cm-badge t-${esc(b.tone || '')}">${b.tone === 'team' ? icon('shield', 11) : ''}${esc(b.label)}</span>` : '');
  const who = (a, size = 'sm') => `${SA.ui.c.avatar({ name: a.name, avatar: a.avatar }, size)}`;
  const text = (t) => SA.ui.linkCards.linkify(esc(t)).replace(/\n/g, '<br>');
  const LC = () => SA.ui.linkCards;

  /** One post. opts: { admin, showCommunity, full } */
  const postCard = (p, opts = {}) => {
    const c = opts.showCommunity ? p.community || SA.services.db.find('communities', p.communityId) : null;
    const isOpen = opts.full || open.has(p.id);
    const comments = opts.admin ? (p.allComments || p.comments) : p.comments;
    const pollRows = p.poll ? p.poll.options.map((o, i) => { const n = (o.votes || []).length + (o.seed || 0); const pct = p.pollTotal ? Math.round((n / p.pollTotal) * 100) : 0; return { o, i, n, pct }; }) : [];
    const voted = p.myVote >= 0;
    const byId = Object.fromEntries((comments || []).map((x) => [x.id, x]));
    return `<article class="cm-post ${p.announcement ? 'is-announce' : ''} ${p.hidden ? 'is-hidden' : ''}" data-post="${p.id}">
      <header class="cm-post-head">${who(p.author)}
        <span class="grow"><span class="row wrap" style="--gap:6px"><b class="small">${esc(p.author.name)}</b>${badge(p.author.badge)}</span>
          <span class="xs muted">${c ? `<a class="link" href="#/community/${esc(c.slug)}">${esc(c.name)}</a> · ` : ''}${timeAgo(p.at)}${p.pinned ? ` · ${icon('pin', 11)} Pinned` : ''}${p.locked ? ` · ${icon('lock', 11)} Comments closed` : ''}${p.hidden ? ' · <b style="color:var(--danger)">Hidden</b>' : ''}</span></span>
        ${p.announcement ? `<span class="badge badge-blaze">${icon('megaphone', 11)} Announcement</span>` : ''}
        ${opts.admin ? '' : `<button class="icon-btn cm-more" data-menu="${p.id}" aria-label="More">${icon('list', 16)}</button>`}
      </header>
      <div class="cm-text">${text(p.text)}</div>
      ${p.card ? `<div class="lk-list">${p.card.type === 'slot' ? LC().slot(p.card) : LC().game(p.card)}</div>` : ''}
      ${(() => { const h = LC().fromText(p.text); return h ? `<div class="lk-list">${h}</div>` : ''; })()}
      ${p.image ? `<img class="cm-img" src="${media.u(p.image, 900)}" alt="" loading="lazy" />` : ''}
      ${p.poll ? `<div class="cm-poll">${pollRows.map(({ o, i, n, pct }) => (voted || opts.admin ? `<button class="cm-opt is-result ${p.myVote === i ? 'is-mine' : ''}" data-vote="${i}"><span class="cm-opt-bar" style="width:${pct}%"></span><span class="grow">${esc(o.text)}</span><b class="num">${pct}%</b></button>` : `<button class="cm-opt" data-vote="${i}"><span class="grow">${esc(o.text)}</span></button>`)).join('')}<span class="xs muted">${p.pollTotal.toLocaleString()} vote${p.pollTotal === 1 ? '' : 's'}${voted ? ' · tap another option to change your vote' : ''}</span></div>` : ''}
      <footer class="cm-actions">
        <span class="cm-reacts">${CS().REACTIONS.map((e) => { const n = (p.reactions?.[e] || []).length + (e === '👍' ? p.seedReactions || 0 : 0); return `<button class="cm-react ${p.myReaction === e ? 'is-on' : ''}" data-react="${e}" aria-pressed="${p.myReaction === e}">${e}${n ? ` <span class="num">${n}</span>` : ''}</button>`; }).join('')}</span>
        <button class="btn btn-ghost btn-sm" data-toggle-comments="${p.id}">${icon('message', 14)} ${(comments || []).length} comment${(comments || []).length === 1 ? '' : 's'}</button>
      </footer>
      ${opts.admin ? `<div class="cm-admin-bar">
          <button class="btn btn-primary btn-sm" data-admin-reply>${icon('shield', 13)} Reply as Super App Admin</button>
          <button class="btn btn-ghost btn-sm" data-admin="${p.pinned ? 'unpin' : 'pin'}">${icon('pin', 13)} ${p.pinned ? 'Unpin' : 'Pin'}</button>
          <button class="btn btn-ghost btn-sm" data-admin="${p.locked ? 'unlock' : 'lock'}">${icon('lock', 13)} ${p.locked ? 'Open comments' : 'Close comments'}</button>
          <button class="btn btn-ghost btn-sm" data-admin="${p.hidden ? 'show' : 'hide'}">${icon(p.hidden ? 'eye' : 'ban', 13)} ${p.hidden ? 'Show' : 'Hide'}</button>
          ${p.author.kind === 'user' ? '<button class="btn btn-ghost btn-sm" data-admin="mute">Mute author</button>' : ''}
          <button class="btn btn-ghost btn-sm" data-admin="delete" style="color:var(--danger)">${icon('trash', 13)} Delete</button></div>` : ''}
      ${isOpen ? `<div class="cm-comments">
          ${(comments || []).map((x) => `<div class="cm-comment ${x.author.kind === 'admin' ? 'is-admin' : ''} ${x.hidden ? 'is-hidden' : ''}" data-comment="${x.id}">${who(x.author)}
            <div class="grow"><div class="cm-bubble"><span class="row wrap" style="--gap:6px"><b class="xs">${esc(x.author.name)}</b>${badge(x.author.badge)}${x.hidden ? '<span class="xs" style="color:var(--danger)">Hidden</span>' : ''}</span>
              ${x.replyTo && byId[x.replyTo] ? `<span class="cm-quote xs">↪ ${esc(byId[x.replyTo].author.name)}: “${esc(byId[x.replyTo].text.slice(0, 70))}${byId[x.replyTo].text.length > 70 ? '…' : ''}”</span>` : ''}
              <p class="small">${text(x.text)}</p>${(() => { const h = LC().fromText(x.text); return h ? `<div class="lk-list lk-sm">${h}</div>` : ''; })()}</div>
              <span class="xs muted cm-cmeta">${timeAgo(x.at)} · ${opts.admin ? `<button class="link xs" data-admin-reply="${x.id}">Reply as admin</button> · <button class="link xs" data-admin-c="${x.hidden ? 'show' : 'hide'}">${x.hidden ? 'Show' : 'Hide'}</button>${x.author.kind === 'user' ? ' · <button class="link xs" data-admin-c="mute">Mute</button>' : ''}`
                : `${p.locked ? '' : `<button class="link xs" data-reply="${x.id}">Reply</button> · `}${x.author.id === SA.core.store.get().user?.id ? `<button class="link xs" data-del-c="${x.id}">Delete</button>` : `<button class="link xs" data-report-c="${x.id}">Report</button>`}`}</span></div></div>`).join('') || '<p class="xs muted">No comments yet.</p>'}
          ${opts.admin ? `<form class="cm-reply-form" data-admin-form>${replyTarget.get(p.id) && byId[replyTarget.get(p.id)] ? `<span class="xs cm-replying">↪ Replying to ${esc(byId[replyTarget.get(p.id)].author.name)} <button type="button" class="link xs" data-cancel-reply>cancel</button></span>` : ''}<input class="input" name="text" placeholder="Reply as Super App Admin…" maxlength="1500" /><button class="btn btn-primary btn-sm" type="submit">Send</button></form>`
            : p.locked ? '' : `<form class="cm-reply-form" data-comment-form>${replyTarget.get(p.id) && byId[replyTarget.get(p.id)] ? `<span class="xs cm-replying">↪ Replying to ${esc(byId[replyTarget.get(p.id)].author.name)} <button type="button" class="link xs" data-cancel-reply>cancel</button></span>` : ''}<input class="input" name="text" placeholder="Write a comment…" maxlength="800" /><button class="btn btn-primary btn-sm" type="submit">Send</button></form>`}
        </div>` : ''}
    </article>`;
  };

  /** Shared click/submit handling for post cards. onAdmin(action, postId, commentId) handles admin actions. */
  const bindPosts = (el, { onAdmin } = {}) => {
    const rerender = () => SA.app.rerender();
    el.addEventListener('click', async (e) => {
      const card = e.target.closest('[data-post]'); if (!card) return;
      const id = card.dataset.post;
      const t = (s) => e.target.closest(s);
      try {
        if (t('[data-react]')) { CS().react(id, t('[data-react]').dataset.react); rerender(); return; }
        if (t('[data-vote]')) { if (onAdmin) return; CS().vote(id, +t('[data-vote]').dataset.vote); rerender(); return; }
        if (t('[data-toggle-comments]')) { open.has(id) ? open.delete(id) : open.add(id); rerender(); return; }
        if (t('[data-reply]')) { replyTarget.set(id, t('[data-reply]').dataset.reply); open.add(id); rerender(); setTimeout(() => document.querySelector(`[data-post="${id}"] [data-comment-form] input`)?.focus(), 80); return; }
        if (t('[data-admin-reply]')) { const c = t('[data-admin-reply]').dataset.adminReply; if (c) replyTarget.set(id, c); else replyTarget.delete(id); open.add(id); rerender(); setTimeout(() => document.querySelector(`[data-post="${id}"] [data-admin-form] input`)?.focus(), 80); return; }
        if (t('[data-cancel-reply]')) { replyTarget.delete(id); rerender(); return; }
        if (t('[data-del-c]')) { if (!(await SA.ui.overlay.confirm({ title: 'Delete your comment?', text: 'It disappears for everyone.', confirmLabel: 'Delete', danger: true }))) return; CS().deleteOwn(id, t('[data-del-c]').dataset.delC); toast('Comment deleted', 'info'); rerender(); return; }
        if (t('[data-report-c]')) { report(id, t('[data-report-c]').dataset.reportC); return; }
        if (t('[data-menu]')) { menu(id); return; }
        if (onAdmin && t('[data-admin]')) { await onAdmin(t('[data-admin]').dataset.admin, id); return; }
        if (onAdmin && t('[data-admin-c]')) { await onAdmin(`c-${t('[data-admin-c]').dataset.adminC}`, id, t('[data-comment]').dataset.comment); return; }
      } catch (err) { toast(err.message, 'error'); }
    });
    el.addEventListener('submit', (e) => {
      const f = e.target; const card = f.closest('[data-post]'); if (!card) return;
      e.preventDefault();
      const id = card.dataset.post;
      try {
        if (f.matches('[data-comment-form]')) { CS().comment(id, f.text.value, replyTarget.get(id)); replyTarget.delete(id); open.add(id); rerender(); }
        if (f.matches('[data-admin-form]')) { SA.services.admin.communityReply(id, f.text.value, replyTarget.get(id)); replyTarget.delete(id); open.add(id); toast('Reply posted as Super App Admin'); rerender(); }
      } catch (err) { toast(err.message, 'error'); }
    });
  };
  const report = (postId, commentId = null) => SA.ui.overlay.open({
    title: commentId ? 'Report this comment' : 'Report this post', subtitle: 'The Super App team checks every report. The person won’t know who reported it.',
    body: `<form class="stack" style="--gap:8px" data-rep>${REASONS.map(([k, l], i) => `<label class="choice"><input type="radio" name="reason" value="${k}" ${i ? '' : 'checked'} /><span class="radio-dot"></span><span class="grow">${l}</span></label>`).join('')}
      <div class="row" style="--gap:8px;justify-content:flex-end;margin-top:6px"><button type="button" class="btn btn-ghost" data-close>Cancel</button><button class="btn btn-primary" type="submit">Send report</button></div></form>`,
    onMount(m, close) { m.querySelector('[data-rep]').addEventListener('submit', (e) => { e.preventDefault(); try { CS().report(postId, { commentId, reason: e.target.reason.value }); close(); toast('Thanks — the Super App team will check it'); } catch (err) { toast(err.message, 'error'); } }); },
  });
  const menu = (postId) => {
    const p = CS().post(postId);
    SA.ui.overlay.open({ title: 'Post', body: `<div class="stack" style="--gap:8px">${p.mine ? '<button class="btn btn-outline btn-block" data-m="delete" style="color:var(--danger)">Delete post</button>' : '<button class="btn btn-outline btn-block" data-m="report">Report post</button>'}<button class="btn btn-ghost btn-block" data-m="copy">Copy link</button></div>`,
      onMount(m, close) {
        m.addEventListener('click', async (e) => {
          const a = e.target.closest('[data-m]')?.dataset.m; if (!a) return; close();
          if (a === 'report') report(postId);
          if (a === 'copy') { navigator.clipboard?.writeText(`${location.origin}${location.pathname}#/community/${SA.services.db.find('communities', p.communityId)?.slug}/${postId}`); toast('Link copied'); }
          if (a === 'delete' && (await SA.ui.overlay.confirm({ title: 'Delete your post?', text: 'It disappears for everyone, with its comments.', confirmLabel: 'Delete', danger: true }))) { CS().deleteOwn(postId); toast('Post deleted', 'info'); SA.app.rerender(); }
        });
      } });
  };

  /** Write a post: text, optional photo, optional poll */
  let draftImage = null;
  const composer = (c) => `<form class="cm-composer" data-compose novalidate>
      <div class="row" style="--gap:10px;align-items:flex-start">${who({ name: SA.core.store.get().user?.name, avatar: SA.core.store.get().user?.avatar })}<textarea class="input" name="text" rows="3" maxlength="2000" placeholder="Share something with ${esc(c.name)}…"></textarea></div>
      <div data-poll hidden class="cm-poll-edit"><span class="xs muted">Poll options (2–6)</span>${[0, 1, 2].map((i) => `<input class="input" name="opt" placeholder="Option ${i + 1}" maxlength="80" />`).join('')}</div>
      <div class="cm-img-preview" data-img-preview hidden></div>
      <div class="row-between wrap" style="--gap:8px"><span class="row" style="--gap:4px"><button type="button" class="btn btn-ghost btn-sm" data-add-photo>${icon('image', 15)} Photo</button><button type="button" class="btn btn-ghost btn-sm" data-add-poll>${icon('chart', 15)} Poll</button></span><button class="btn btn-primary btn-sm" type="submit">Post</button></div>
    </form>`;

  const COVER = (c) => `style="--cm-color:${esc(c.color)}"`;
  const card = (c) => `<a class="cm-card" href="#/community/${esc(c.slug)}" ${COVER(c)}><span class="cm-card-img"><img src="${media.photo(c.cover, 600, 300)}" alt="" loading="lazy" /><span class="cm-card-ico">${icon(c.icon, 20)}</span></span>
    <span class="cm-card-body"><b>${esc(c.name)}</b><span class="xs muted">${c.members.toLocaleString()} members · ${c.posts} posts${c.audience !== 'everyone' ? ` · ${icon('lock', 11)} ${esc(c.audienceLabel)}` : ''}</span><span class="small soft cm-card-about">${esc(c.about)}</span>
    ${c.joined ? '<span class="badge badge-turf">Joined</span>' : c.access.ok ? '<span class="badge">Open</span>' : `<span class="badge">${icon('lock', 11)} ${esc(c.access.reason)}</span>`}</span></a>`;

  SA.pages.communities = {
    title: 'Communities — Super App',
    render() {
      const list = CS().list();
      const feed = CS().myFeed();
      return `<div class="container section-tight cm-page">
        <div class="page-head"><h1 class="h1">Communities</h1><p class="lead">Talk to players, sellers and fans — tips, polls and news from the Super App team.</p></div>
        <div class="cm-grid">${list.map(card).join('')}</div>
        <section class="section-tight"><div class="sec-head"><h2 class="h3">Latest from your communities</h2></div>
          ${feed.length ? `<div class="cm-feed">${feed.map((p) => postCard(p, { showCommunity: true })).join('')}</div>` : `<div class="panel">${SA.ui.c.empty({ icon: 'users', title: 'Join a community', text: 'Posts from the communities you join show up here.' })}</div>`}</section>
        <p class="xs muted" style="margin-top:12px">${icon('shield', 12)} Communities are run by the Super App team. No phone numbers or outside links — keep deals safe on Super App.</p>
      </div>`;
    },
    mount(el) { bindPosts(el); },
  };

  SA.pages.community = {
    title: 'Community — Super App',
    render(ctx) {
      let c; try { c = CS().get(ctx.params.slug); } catch (e) { return `<div class="container section">${SA.ui.c.empty({ icon: 'users', title: 'Community not found', action: '<a class="btn btn-primary" href="#/community">All communities</a>' })}</div>`; }
      const sort = ctx.query.sort === 'top' ? 'top' : 'new';
      const isPitches = c.id === CS().PITCH_COMMUNITY;
      let posts = c.access.ok ? CS().feed(c.id, { sort }) : [];
      if (isPitches) posts = CS().slotFilter(posts, { area: ctx.query.area || '', when: ctx.query.when || '' });
      const areas = isPitches ? [...new Set(SA.services.db.all('pitches').filter((p) => !p.deleted && p.status === 'active').map((p) => p.area))].sort() : [];
      const qh = (patch) => SA.core.router.href(`/community/${c.slug}`, { sort: sort === 'top' ? 'top' : undefined, area: ctx.query.area || undefined, when: ctx.query.when || undefined, ...patch });
      const m = CS().muted(c.id);
      return `<div class="cm-page">
        <section class="cm-hero" ${COVER(c)}><img src="${media.photo(c.cover, 1600, 500)}" alt="" /><div class="container cm-hero-in">
          <a class="back-link" href="#/community" style="color:#fff">${icon('chevronLeft', 16)} Communities</a>
          <div class="row wrap" style="--gap:14px;align-items:flex-end"><span class="cm-hero-ico">${icon(c.icon, 28)}</span><div class="grow"><h1 class="h1">${esc(c.name)}</h1><p class="small">${c.members.toLocaleString()} members · ${c.posts} posts · ${esc(c.audienceLabel)}</p></div>
          ${c.access.ok ? `<button class="btn ${c.joined ? 'btn-glass' : 'btn-blaze'}" data-join="${c.id}" data-on="${c.joined ? '' : '1'}">${c.joined ? `${icon('check', 15, 2.6)} Joined` : `${icon('plus', 15, 2.6)} Join`}</button>` : ''}</div></div></section>
        <div class="container cm-layout">
          <div class="cm-main">
            ${!c.access.ok ? `<div class="panel">${SA.ui.c.empty({ icon: 'lock', title: c.access.reason, text: c.about, action: c.access.action ? `<a class="btn btn-primary" href="${c.access.action[0]}">${c.access.action[1]}</a>` : '' })}</div>`
              : `${m ? `<div class="notice blaze">${icon('ban', 18)}<span>You can’t post here until ${new Date(m.until).toLocaleDateString('en-GB', { day: 'numeric', month: 'short' })}. ${esc(m.reason)}</span></div>` : composer(c)}
              ${isPitches ? `<div class="cm-filters"><label class="field"><span class="field-label">Area</span><select class="select" data-area><option value="">All areas</option>${areas.map((a) => `<option ${ctx.query.area === a ? 'selected' : ''}>${esc(a)}</option>`).join('')}</select></label>
                <div class="chips" role="group" aria-label="Time">${[['', 'Any time'], ['day', 'Before 5 PM'], ['evening', '5–9 PM'], ['night', 'After 9 PM']].map(([k, l]) => `<a class="chip" href="${qh({ when: k || undefined })}" aria-pressed="${(ctx.query.when || '') === k}">${l}</a>`).join('')}</div>
                <a class="btn btn-outline btn-sm" href="#/alerts">${icon('bell', 14)} Free-slot alerts</a></div>` : ''}
              <div class="tabs" role="tablist">${[['new', 'Newest'], ['top', 'Top']].map(([k, l]) => `<a class="tab" role="tab" href="${qh({ sort: k === 'top' ? 'top' : undefined })}" aria-selected="${sort === k}">${l}</a>`).join('')}</div>
              <div class="cm-feed">${posts.map((p) => postCard(p)).join('') || `<div class="panel">${SA.ui.c.empty({ icon: 'message', title: 'No posts yet', text: 'Be the first to post.' })}</div>`}</div>`}
          </div>
          <aside class="cm-side"><section class="panel panel-pad"><h2 class="h4">About</h2><p class="small soft" style="margin-top:6px">${esc(c.about)}</p>
            ${c.slowMode ? `<p class="xs muted" style="margin-top:8px">${icon('clock', 12)} Slow mode: one post every ${c.slowMode} min</p>` : ''}
            <h3 class="small strong" style="margin-top:14px">Rules</h3><ol class="cm-rules small">${(c.rules || []).map((r) => `<li>${esc(r)}</li>`).join('')}</ol></section></aside>
        </div></div>`;
    },
    mount(el, ctx) {
      bindPosts(el);
      const c = (() => { try { return CS().get(ctx.params.slug); } catch { return null; } })(); if (!c) return;
      el.querySelector('[data-area]')?.addEventListener('change', (e) => SA.core.router.go(SA.core.router.href(`/community/${ctx.params.slug}`, { ...ctx.query, area: e.target.value || undefined }).slice(1)));
      el.querySelector('[data-join]')?.addEventListener('click', (e) => { try { if (e.currentTarget.dataset.on) { CS().join(c.id); toast(`You joined ${c.name}`); } else { CS().leave(c.id); toast(`You left ${c.name}`, 'info'); } SA.app.rerender(); } catch (err) { toast(err.message, 'error'); } });
      const f = el.querySelector('[data-compose]'); if (!f) return;
      draftImage = null;
      f.querySelector('[data-add-poll]').addEventListener('click', () => { const p = f.querySelector('[data-poll]'); p.hidden = !p.hidden; });
      f.querySelector('[data-add-photo]').addEventListener('click', async () => {
        try { const img = await SA.ui.pickImage({ max: 1200 }); if (!img) return; draftImage = img; const pv = f.querySelector('[data-img-preview]'); pv.hidden = false; pv.innerHTML = `<img src="${img}" alt="" /><button type="button" class="icon-btn" data-rm-photo aria-label="Remove photo">${icon('x', 14)}</button>`; pv.querySelector('[data-rm-photo]').addEventListener('click', () => { draftImage = null; pv.hidden = true; pv.innerHTML = ''; }); }
        catch (err) { toast(err.message, 'error'); }
      });
      f.addEventListener('submit', (e) => {
        e.preventDefault();
        const poll = f.querySelector('[data-poll]').hidden ? [] : [...f.querySelectorAll('[name=opt]')].map((i) => i.value);
        try { CS().createPost(c.id, { text: f.text.value, image: draftImage, poll }); draftImage = null; toast('Posted'); SA.app.rerender(); } catch (err) { toast(err.message, 'error'); }
      });
    },
  };

  SA.pages.communityPost = {
    title: 'Post — Super App Communities',
    render(ctx) {
      let p, c;
      try { c = CS().get(ctx.params.slug); p = CS().post(ctx.params.postId); } catch (e) { return `<div class="container section">${SA.ui.c.empty({ icon: 'message', title: 'Post not found', text: esc(e.message), action: '<a class="btn btn-primary" href="#/community">Communities</a>' })}</div>`; }
      if (!c.access.ok) return `<div class="container section">${SA.ui.c.empty({ icon: 'lock', title: c.access.reason })}</div>`;
      return `<div class="container section-tight cm-page cm-single"><a class="back-link" href="#/community/${esc(c.slug)}">${icon('chevronLeft', 16)} ${esc(c.name)}</a><div class="cm-feed" style="margin-top:14px">${postCard(p, { full: true })}</div></div>`;
    },
    mount(el) { bindPosts(el); },
  };

  SA.ui.community = { postCard, bindPosts, open, replyTarget };
})(window.SA);
