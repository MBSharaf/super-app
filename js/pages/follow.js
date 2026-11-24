/**
 * News and notification settings (live site):
 *   #/news                 stories by the Super Malaab team (all, or only my clubs)
 *   #/news/:id             one story, with its source link
 *   #/notification-settings which kinds reach the phone, and my favourite clubs (up to 3)
 * On the demo (no server) both pages explain they work on the live site.
 */
(function (SA) {
  const { esc, timeAgo } = SA.core.utils;
  const { icon } = SA.ui;
  const F = () => SA.services.follow;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });
  const at = (iso) => (iso ? new Date(iso.endsWith('Z') ? iso : `${iso}Z`).getTime() : Date.now());

  /** Waits for the mode and the account; → 'demo' | 'out' | 'in' */
  const state = async () => {
    const S = SA.services;
    await S.api.ready;
    if (!S.api.isLive()) return 'demo';
    if (!S.session.loaded()) await S.session.load();
    return S.session.me() ? 'in' : 'out';
  };
  const liveOnly = (title) => `<div class="panel">${SA.ui.c.empty({ icon: 'globe', title, text: 'This part works on the live site, supermalaab.com.' })}</div>`;
  const signIn = (next, text) => `<div class="panel">${SA.ui.c.empty({ icon: 'lock', title: 'Sign in first', text,
    action: `<a class="btn btn-primary" href="#/auth/sign-in?next=${encodeURIComponent(next)}">${icon('lock', 16)} Sign in</a>` })}</div>`;
  const clubChip = (c) => `<span class="fp-chip">${SA.ui.clubPicker.crest(c, 20)}<span data-i18n-skip>${esc(F().clubName(c))}</span></span>`;
  /** Story text: plain paragraphs (no HTML from the server) */
  const paragraphs = (text) => String(text || '').split(/\n{2,}|\r\n\r\n/).map((p) => `<p>${esc(p.trim()).replace(/\n/g, '<br>')}</p>`).join('');

  /* ---------- #/news ---------- */
  const card = (n) => `<a class="card card-link news-card" href="#/news/${n.id}">
      <span class="row" style="--gap:8px;flex-wrap:wrap">${n.important ? '<span class="badge badge-blaze">Important</span>' : ''}${n.club ? clubChip(n.club) : ''}<span class="xs muted">${timeAgo(at(n.publishedAt))}</span></span>
      <b class="news-title" ${n.language === 'ar' ? 'dir="rtl" data-i18n-skip' : 'data-i18n-skip'}>${esc(n.title)}</b>
      <span class="small soft" ${n.language === 'ar' ? 'dir="rtl"' : ''} data-i18n-skip>${esc(n.excerpt)}</span>
      ${n.sourceName ? `<span class="xs muted">Source: <span data-i18n-skip>${esc(n.sourceName)}</span></span>` : ''}</a>`;

  SA.pages.news = {
    title: 'News — Super Malaab',
    async render(ctx) {
      const st = await state();
      const head = `<div class="page-head"><h1 class="h1">News</h1><p class="lead">Football news picked by the Super Malaab team. Follow your clubs to get their stories first.</p></div>`;
      if (st === 'demo') return `<div class="container section-tight">${head}${liveOnly('News is on the live site')}</div>`;
      const mineOnly = ctx.query.my === '1' && st === 'in';
      const mine = st === 'in' ? await F().mine().catch(() => []) : [];
      const page = Math.max(1, +ctx.query.page || 1);
      let list = [];
      try {
        if (mineOnly) list = (await Promise.all(mine.map((c) => F().news(c.id, page)))).flat().filter((n, i, a) => a.findIndex((x) => x.id === n.id) === i).sort((a, b) => at(b.publishedAt) - at(a.publishedAt));
        else list = await F().news(null, page);
      } catch (err) { return `<div class="container section-tight">${head}<div class="panel panel-pad"><p class="text-danger">${esc(err.message)}</p></div></div>`; }
      const tabs = `<div class="chips" style="margin-bottom:16px"><a class="chip ${mineOnly ? '' : 'is-active'}" href="#/news">All news</a>${st === 'in' ? `<a class="chip ${mineOnly ? 'is-active' : ''}" href="#/news?my=1">My clubs</a>` : ''}
        ${st === 'in' ? `<button class="chip" data-clubs>${icon('star', 14)} ${mine.length ? 'Change my clubs' : 'Pick my clubs'}</button>` : ''}</div>`;
      const empty = mineOnly && !mine.length ? SA.ui.c.empty({ icon: 'star', title: 'No clubs yet', text: 'Pick up to 3 clubs to see their news here and get their match alerts.', action: '<button class="btn btn-primary" data-clubs>Pick my clubs</button>' })
        : SA.ui.c.empty({ icon: 'news', title: 'No news yet', text: 'Stories from the Super Malaab team will show up here.' });
      const more = list.length >= 20 && !mineOnly ? `<div class="row" style="justify-content:center;margin-top:16px"><a class="btn btn-outline" href="#/news?page=${page + 1}">Older news</a></div>` : '';
      return `<div class="container section-tight">${head}${tabs}${list.length ? `<div class="news-list">${list.map(card).join('')}</div>${more}` : `<div class="panel">${empty}</div>`}</div>`;
    },
    mount(el) {
      el.addEventListener('click', (e) => { if (e.target.closest('[data-clubs]')) SA.ui.clubPicker.open({ onSaved: () => SA.app.rerender() }); });
    },
  };

  /* ---------- #/news/:id ---------- */
  SA.pages.newsPost = {
    title: 'News — Super Malaab',
    async render(ctx) {
      const st = await state();
      const back = `<a class="back-link" href="#/news">${icon('chevronLeft', 16)} News</a>`;
      if (st === 'demo') return `<div class="container section-tight">${back}${liveOnly('News is on the live site')}</div>`;
      let p;
      try { p = await F().story(ctx.params.id); } catch (err) {
        return `<div class="container section-tight">${back}<div class="panel">${SA.ui.c.empty({ icon: 'news', title: 'Story not found', text: err.message })}</div></div>`;
      }
      const n = p.item;
      const ar = n.language === 'ar';
      return `<div class="container section-tight news-post">${back}
        <article class="panel panel-pad" style="margin-top:12px" ${ar ? 'dir="rtl"' : ''}>
          <div class="row" style="--gap:8px;flex-wrap:wrap">${n.important ? '<span class="badge badge-blaze">Important</span>' : ''}${n.club ? clubChip(n.club) : ''}<span class="xs muted">${timeAgo(at(n.publishedAt))}</span></div>
          <h1 class="h2" style="margin:12px 0" data-i18n-skip>${esc(n.title)}</h1>
          <div class="news-body" data-i18n-skip>${paragraphs(p.body)}</div>
          ${n.sourceUrl ? `<p class="small" style="margin-top:16px"><a href="${esc(n.sourceUrl)}" target="_blank" rel="noopener noreferrer nofollow">${icon('arrowUpRight', 14)} <span>Read at the source</span>${n.sourceName ? ` · <span data-i18n-skip>${esc(n.sourceName)}</span>` : ''}</a></p>` : ''}
        </article></div>`;
    },
  };

  /* ---------- #/notification-settings ---------- */
  const KINDS = {
    matches: ['users', 'My clubs’ matches', 'Line-ups, kick-off, goals and full time'],
    news: ['news', 'News', 'Stories about your clubs and important news'],
    bookings: ['pitch', 'Bookings', 'Confirmations, reminders and check-in'],
    orders: ['package', 'Orders', 'Payments, shipping and delivery'],
    fantasy: ['trophy', 'Fantasy', 'Deadlines, points and leagues'],
    community: ['message', 'Communities and games', 'Replies, join requests and game updates'],
    money: ['wallet', 'Wallet and points', 'Refunds, top-ups and Super Points'],
    support: ['help', 'Support', 'Replies to your requests'],
    store: ['bag', 'My store', 'Sales and products (sellers)'],
    designs: ['image', 'My designs', 'Design reviews (sellers)'],
    level: ['trend', 'Player level', 'Level reviews'],
  };

  SA.pages.notifSettings = {
    title: 'Notifications — Super Malaab',
    async render() {
      const st = await state();
      const head = `<div class="page-head"><a class="back-link" href="#/profile">${icon('chevronLeft', 16)} Profile</a><h1 class="h1" style="margin-top:10px">Notifications</h1>
        <p class="lead">Everything stays in the bell. Choose what also reaches your phone.</p></div>`;
      if (st === 'demo') return `<div class="container section-tight">${head}${liveOnly('Notification settings are on the live site')}</div>`;
      if (st === 'out') return `<div class="container section-tight">${head}${signIn('/notification-settings', 'Sign in to choose your clubs and notifications.')}</div>`;
      const [settings, mine] = await Promise.all([F().settings().catch(() => []), F().mine().catch(() => [])]);
      const rows = settings.filter((s) => KINDS[s.category]).sort((a, b) => Object.keys(KINDS).indexOf(a.category) - Object.keys(KINDS).indexOf(b.category));
      const sw = (s) => `<button class="switch ${s.push ? 'is-on' : ''}" role="switch" aria-checked="${s.push}" data-kind="${esc(s.category)}" aria-label="${KINDS[s.category][1]}"><span class="switch-knob"></span></button>`;
      return `<div class="container section-tight">${head}
        <div class="sup-grid">
          <section class="panel panel-pad"><div class="row-between"><h2 class="h3">My clubs</h2><button class="btn btn-outline btn-sm" data-clubs>${icon('edit', 14)} ${mine.length ? 'Change' : 'Pick clubs'}</button></div>
            <p class="small soft" style="margin:6px 0 12px">Up to 3. We tell you about their line-ups, kick-off, goals and results, and their news.</p>
            ${mine.length ? `<div class="row" style="--gap:8px;flex-wrap:wrap">${mine.map((c, i) => `<span class="fp-chip"><b class="num">${i + 1}</b>${SA.ui.clubPicker.crest(c, 22)}<span data-i18n-skip>${esc(F().clubName(c))}</span></span>`).join('')}</div>`
              : '<p class="small muted">No clubs yet.</p>'}</section>
          <section class="panel panel-pad"><h2 class="h3">On my phone</h2><p class="small soft" style="margin:6px 0 8px">Switch off a kind to keep it in the bell only. Account and security messages always arrive.</p>
            <ul class="adm-list notif-kinds">${rows.map((s) => { const [ic, title, sub] = KINDS[s.category]; return `<li><span class="notif-ico">${icon(ic, 18)}</span><span class="grow"><b class="small">${title}</b><span class="xs muted" style="display:block">${sub}</span></span>${sw(s)}</li>`; }).join('')}</ul></section>
        </div></div>`;
    },
    mount(el) {
      el.addEventListener('click', async (e) => {
        if (e.target.closest('[data-clubs]')) { SA.ui.clubPicker.open({ onSaved: () => SA.app.rerender() }); return; }
        const b = e.target.closest('[data-kind]');
        if (!b) return;
        const on = !b.classList.contains('is-on');
        b.classList.toggle('is-on', on); b.setAttribute('aria-checked', on); b.disabled = true;
        try { await F().setSettings([{ category: b.dataset.kind, push: on }]); toast(on ? 'This kind reaches your phone again.' : 'This kind stays in the bell only.', 'info'); }
        catch (err) { b.classList.toggle('is-on', !on); b.setAttribute('aria-checked', !on); toast(err.message, 'error'); }
        finally { b.disabled = false; }
      });
    },
  };
})(window.SA);
