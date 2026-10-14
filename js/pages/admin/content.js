/** Admin console: site content — home banners, the announcement bar, news, and pickup-game moderation. */
(function (SA) {
  const { esc, fmtDate, fmtHour, timeAgo, isoDate, addDays, today } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const A = SA.pages.adminCommon;
  const AD = () => SA.services.admin;
  const CT = () => SA.services.content;
  const db = () => SA.services.db;
  const media = SA.data.media;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });

  /** Photo picker: the Super App library or an upload */
  const LIB = ['stadiumNight', 'nightMatch', 'litField', 'aerialPitch', 'birdsEyeLights', 'goalTrees', 'turfLine', 'ballsInGoal', 'stadium', 'kick16', 'kickDay', 'duel', 'menPlaying', 'greenTeam', 'womenPlaying', 'kidsTraining', 'teamLineup', 'trophy', 'footBall', 'cairoNile', 'nightGrass', 'photo-1529900748604-07564a03e7a6'].filter((k) => media.photos[k] || k.startsWith('photo-'));
  const picker = (name, cur) => `<div class="adm-pick" data-pick="${name}"><input type="hidden" name="${name}" value="${esc(cur || '')}" />
    <div class="adm-pick-grid">${LIB.map((k) => `<button type="button" class="adm-pick-item ${cur === k ? 'is-on' : ''}" data-img="${k}"><img src="${media.photo(k, 220, 160)}" alt="" loading="lazy" /></button>`).join('')}
      <label class="adm-pick-item adm-pick-up">${icon('plus', 18)}<span class="xs">Upload</span><input type="file" accept="image/*" hidden data-up /></label></div>
    ${cur && !LIB.includes(cur) ? `<p class="xs muted" style="margin-top:6px">${icon('check', 12)} Your uploaded photo is selected.</p>` : ''}<p class="field-error" data-err="${name}"></p></div>`;
  const bindPicker = (root) => root.querySelectorAll('[data-pick]').forEach((p) => {
    const input = p.querySelector('input[type=hidden]');
    p.addEventListener('click', (e) => { const b = e.target.closest('[data-img]'); if (!b) return; input.value = b.dataset.img; p.querySelectorAll('.adm-pick-item').forEach((x) => x.classList.toggle('is-on', x === b)); });
    p.querySelector('[data-up]')?.addEventListener('change', async (e) => {
      try { input.value = await C.readImage(e.target.files[0], 1400); p.querySelectorAll('.adm-pick-item').forEach((x) => x.classList.remove('is-on')); e.target.closest('.adm-pick-item').classList.add('is-on'); toast('Photo uploaded'); }
      catch (err) { toast(err.message, 'error'); }
    });
  });

  const bannerDialog = (b = null) => SA.ui.overlay.open({
    wide: true, title: b ? 'Edit banner' : 'New home banner', subtitle: 'Up to three banners show on the home page, in order. The first one is the big one.',
    body: `<form class="form-grid" data-banner novalidate>
      <div class="field span-2"><label for="bn-t">Headline</label><input class="input" id="bn-t" name="title" maxlength="70" value="${esc(b?.title || '')}" placeholder="e.g. Ramadan nights: pitches open until 3 AM" /><p class="field-error" data-err="title"></p></div>
      <div class="field"><label for="bn-b">Badge (optional)</label><input class="input" id="bn-b" name="badge" maxlength="22" value="${esc(b?.badge || '')}" placeholder="New" /></div>
      <div class="field"><label for="bn-c">Button text (first banner)</label><input class="input" id="bn-c" name="cta" maxlength="22" value="${esc(b?.cta || '')}" placeholder="Book now" /></div>
      <div class="field span-2"><label for="bn-l">Opens</label><input class="input" id="bn-l" name="link" value="${esc(b?.link || '#/')}" placeholder="#/pitches" /><p class="field-error" data-err="link"></p></div>
      <div class="field"><label for="bn-f">Show from (optional)</label><input class="input" id="bn-f" name="from" type="date" value="${esc(b?.from || '')}" /></div>
      <div class="field"><label for="bn-e">Until (optional)</label><input class="input" id="bn-e" name="to" type="date" value="${esc(b?.to || '')}" /><p class="field-error" data-err="to"></p></div>
      <div class="field span-2"><span class="field-label">Photo</span>${picker('image', b?.image || '')}</div>
      <div class="span-2 row" style="--gap:8px;justify-content:flex-end"><button type="button" class="btn btn-ghost" data-close>Cancel</button><button class="btn btn-primary" type="submit">${b ? 'Save banner' : 'Add banner'}</button></div></form>`,
    onMount(m, close) {
      bindPicker(m);
      m.querySelector('[data-banner]').addEventListener('submit', (e) => { e.preventDefault(); try { AD().saveBanner({ ...SA.core.utils.formData(e.target), active: b ? b.active : true }, b?.id); close(); toast('Banner saved'); SA.app.rerender(); } catch (err) { C.showErrors(e.target, err.fields || {}); } });
    },
  });
  const newsDialog = (n = null) => SA.ui.overlay.open({
    wide: true, title: n ? 'Edit story' : 'Write a story', subtitle: 'Published as “Super App” in the news section. Leave an empty line between paragraphs.',
    body: `<form class="form-grid" data-news novalidate>
      <div class="field span-2"><label for="nw-t">Headline</label><input class="input" id="nw-t" name="title" maxlength="110" value="${esc(n?.title || '')}" /><p class="field-error" data-err="title"></p></div>
      <div class="field"><label for="nw-c">Category</label><select class="select" id="nw-c" name="cat">${SA.data.news.categories.map((c) => `<option value="${c.id}" ${n?.cat === c.id ? 'selected' : ''}>${esc(c.name)}</option>`).join('')}</select><p class="field-error" data-err="cat"></p></div>
      <label class="choice" style="align-self:end"><input type="checkbox" name="breaking" ${n?.breaking ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span><span class="grow"><b>Breaking</b></span></label>
      <div class="field span-2"><label for="nw-x">Summary</label><textarea class="input" id="nw-x" name="excerpt" rows="2" maxlength="260">${esc(n?.excerpt || '')}</textarea><p class="field-error" data-err="excerpt"></p></div>
      <div class="field span-2"><label for="nw-b">Story</label><textarea class="input" id="nw-b" name="body" rows="8">${esc((n?.body || []).join('\n\n'))}</textarea><p class="field-error" data-err="body"></p></div>
      <div class="field span-2"><span class="field-label">Photo</span>${picker('img', n?.img || '')}</div>
      <div class="span-2 row" style="--gap:8px;justify-content:flex-end"><button type="button" class="btn btn-ghost" data-close>Cancel</button><button class="btn btn-primary" type="submit">${n ? 'Save story' : 'Publish'}</button></div></form>`,
    onMount(m, close) {
      bindPicker(m);
      m.querySelector('[data-news]').addEventListener('submit', (e) => { e.preventDefault(); try { const out = AD().saveNews(SA.core.utils.formData(e.target), n?.id); close(); toast(n ? 'Story saved' : 'Story published', 'success'); SA.app.rerender(); void out; } catch (err) { C.showErrors(e.target, err.fields || {}); } });
    },
  });

  SA.pages.adminContent = A.guard({
    title: 'Site content — Super App Admin',
    render(ctx) {
      const tab = ctx.query.tab || '';
      let section = '';
      if (!tab) {
        const list = (db().all('banners') || []).slice().sort((a, b) => a.order - b.order);
        const live = CT().banners().map((b) => b.id);
        section = `<section class="dash-card"><div class="row-between"><div><h2 class="h4">Home banners</h2><p class="xs muted" style="margin-top:4px">The first three live banners show on the home page.</p></div><button class="btn btn-primary btn-sm" data-new-banner>${icon('plus', 14, 2.6)} New banner</button></div>
          <ul class="adm-banners">${list.map((b, i) => `<li class="${live.includes(b.id) ? '' : 'is-off'}"><img src="${media.photo(b.image, 240, 160)}" alt="" /><span class="grow"><span class="row wrap" style="--gap:6px">${b.badge ? `<span class="badge">${esc(b.badge)}</span>` : ''}${live.includes(b.id) ? `<span class="badge badge-turf">On the home page · #${live.indexOf(b.id) + 1}</span>` : `<span class="badge">${b.active ? 'Waiting (dates or more than 3)' : 'Hidden'}</span>`}</span><b class="small" style="display:block;margin-top:4px">${esc(b.title)}</b><span class="xs muted">${esc(b.link)}${b.from || b.to ? ` · ${b.from ? fmtDate(b.from) : '…'} – ${b.to ? fmtDate(b.to) : '…'}` : ''}</span></span>
            <span class="adm-row-tools"><button class="icon-btn" data-move="${b.id}" data-dir="-1" ${i ? '' : 'disabled'} aria-label="Move up">${icon('chevronDown', 15)}</button><button class="icon-btn" data-move="${b.id}" data-dir="1" ${i < list.length - 1 ? '' : 'disabled'} aria-label="Move down">${icon('chevronDown', 15)}</button><button class="btn btn-ghost btn-sm" data-toggle-banner="${b.id}">${b.active ? 'Hide' : 'Show'}</button><button class="btn btn-ghost btn-sm" data-edit-banner="${b.id}">Edit</button><button class="icon-btn" data-del-banner="${b.id}" aria-label="Delete">${icon('trash', 15)}</button></span></li>`).join('')}</ul></section>`;
      } else if (tab === 'announce') {
        const a = db().settings().announcement || {};
        section = `<section class="dash-card"><h2 class="h4">Announcement bar</h2><p class="small soft" style="margin-top:4px">A strip above the site header on every page — for a holiday, a big match, an outage or an offer. Visitors can close it.</p>
          <form class="form-grid" data-announce novalidate style="margin-top:14px">
            <label class="choice span-2"><input type="checkbox" name="on" ${a.on ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span><span class="grow"><b>Show the announcement</b></span></label>
            <div class="field span-2"><label for="an-t">Text</label><input class="input" id="an-t" name="text" maxlength="160" value="${esc(a.text || '')}" placeholder="e.g. Derby night: free pitch for the first 10 teams who book before 6 PM" /><p class="field-error" data-err="text"></p></div>
            <div class="field"><label for="an-l">Link (optional)</label><input class="input" id="an-l" name="link" value="${esc(a.link || '')}" placeholder="#/fantasy" /><p class="field-error" data-err="link"></p></div>
            <div class="field"><label for="an-c">Link text</label><input class="input" id="an-c" name="cta" maxlength="24" value="${esc(a.cta || '')}" placeholder="Learn more" /></div>
            <div class="field"><label for="an-s">Style</label><select class="select" id="an-s" name="tone">${[['info', 'Dark — information'], ['promo', 'Orange — offer'], ['alert', 'Red — warning']].map(([v, l]) => `<option value="${v}" ${a.tone === v ? 'selected' : ''}>${l}</option>`).join('')}</select></div>
            <div class="field"><label for="an-u">Show until (optional)</label><input class="input" id="an-u" name="until" type="date" value="${esc(a.until || '')}" /></div>
            <div class="span-2"><button class="btn btn-primary" type="submit">Save announcement</button></div></form></section>`;
      } else if (tab === 'news') {
        const all = CT().allStories();
        section = `<section class="dash-card table-card"><div class="row-between" style="padding:18px 18px 0"><div><h2 class="h4">News</h2><p class="xs muted" style="margin-top:4px">Your stories plus the news feed. Hide anything that shouldn’t be on Super App.</p></div><button class="btn btn-primary btn-sm" data-new-news>${icon('edit', 14)} Write a story</button></div>
          <div class="table-wrap"><table class="table dash-table"><thead><tr><th>Story</th><th>Source</th><th>Published</th><th>Status</th><th></th></tr></thead><tbody>
          ${all.slice(0, 60).map((s) => `<tr class="${s.hidden ? 'is-dim' : ''}"><td class="small"><b>${esc(s.title)}</b><span class="xs muted" style="display:block">${esc(SA.data.news.categories.find((c) => c.id === s.cat)?.name || '')}</span></td><td>${s.own ? '<span class="badge badge-blaze">Super App</span>' : `<span class="badge">${esc(s.type)}</span>`}</td><td class="xs">${timeAgo(s.at)}</td>
            <td>${s.hidden ? '<span class="badge badge-danger">Hidden</span>' : s.breaking ? '<span class="badge badge-blaze">Breaking</span>' : '<span class="badge badge-turf">Live</span>'}</td>
            <td class="r"><div class="row" style="--gap:4px;justify-content:flex-end"><a class="btn btn-ghost btn-sm" href="#/news/${s.id}" target="_blank" rel="noopener">View</a>${s.own ? `<button class="btn btn-ghost btn-sm" data-edit-news="${s.id}">Edit</button>` : ''}<button class="btn btn-ghost btn-sm" data-break="${s.id}" data-on="${s.breaking ? '' : '1'}">${s.breaking ? 'Not breaking' : 'Breaking'}</button><button class="btn btn-ghost btn-sm" data-hide-news="${s.id}" data-on="${s.hidden ? '' : '1'}">${s.hidden ? 'Show' : 'Hide'}</button>${s.own ? `<button class="icon-btn" data-del-news="${s.id}" aria-label="Delete">${icon('trash', 15)}</button>` : ''}</div></td></tr>`).join('')}
          </tbody></table></div></section>`;
      } else {
        const st = SA.core.store.get();
        const raw = [...(st.publishedMatches || []), ...SA.data.matches.list];
        const hidden = db().settings().hiddenMatches || {};
        const people = SA.data.matches.people;
        const rows = raw.map((m) => { const p = SA.services.pitches.getSync(m.pitchId); const date = m.date || isoDate(addDays(today(), m.dayOffset || 0)); return { m, p, date, host: m.hostName || people[m.host] || 'Player' }; }).filter((x) => x.date >= isoDate(today())).sort((a, b) => a.date.localeCompare(b.date) || a.m.hour - b.m.hour);
        section = `<section class="dash-card table-card"><h2 class="h4" style="padding:18px 18px 0">Pickup games</h2><p class="xs muted" style="padding:4px 18px 0">Games players post to find teammates. Hide one that breaks the rules — it disappears from Find players.</p>
          <div class="table-wrap"><table class="table dash-table"><thead><tr><th>Game</th><th>When</th><th>Host</th><th class="r">Spots</th><th>Status</th><th></th></tr></thead><tbody>
          ${rows.map(({ m, p, date, host }) => `<tr class="${hidden[m.id] ? 'is-dim' : ''}"><td class="small"><b>${esc(m.title || `${p?.format || 'Pickup'} game`)}</b><span class="xs muted" style="display:block">${esc(p?.name || 'Removed pitch')}${m.level ? ` · ${esc(m.level)}` : ''}</span></td><td class="small nowrap">${fmtDate(date)}<span class="xs muted" style="display:block">${fmtHour(m.hour)}</span></td><td class="small">${esc(host)}${m.isMine ? ' <span class="badge badge-info">This device</span>' : ''}</td><td class="r num">${m.filled ?? (m.participants || []).length}/${m.total}</td>
            <td>${hidden[m.id] ? `<span class="badge badge-danger" title="${esc(hidden[m.id].reason || '')}">Hidden</span>` : '<span class="badge badge-turf">Live</span>'}</td><td class="r"><button class="btn btn-ghost btn-sm" data-hide-match="${m.id}" data-on="${hidden[m.id] ? '' : '1'}">${hidden[m.id] ? 'Restore' : 'Hide'}</button></td></tr>`).join('') || '<tr><td colspan="6" class="small muted" style="padding:18px">No upcoming games.</td></tr>'}
          </tbody></table></div></section>`;
      }
      const body = `${A.tabs('/admin/content', [['', 'Home banners'], ['announce', 'Announcement bar'], ['news', 'News'], ['games', 'Pickup games']], tab)}${section}`;
      return C.layout('content', { title: 'Site content', subtitle: 'What visitors see: home banners, the announcement bar, news and pickup games', actions: '<a class="btn btn-ghost btn-sm" href="#/" target="_blank" rel="noopener">Open the site ' + icon('arrowUpRight', 14) + '</a>', body });
    },
    mount(el) {
      el.querySelector('[data-announce]')?.addEventListener('submit', (e) => { e.preventDefault(); try { AD().setAnnouncement(SA.core.utils.formData(e.target)); toast('Announcement saved'); SA.app.rerender(); } catch (err) { C.showErrors(e.target, err.fields || {}); } });
      el.addEventListener('click', async (e) => {
        const t = (s) => e.target.closest(s);
        if (t('[data-new-banner]')) return bannerDialog();
        if (t('[data-edit-banner]')) return bannerDialog(db().find('banners', t('[data-edit-banner]').dataset.editBanner));
        if (t('[data-toggle-banner]')) { AD().toggleBanner(t('[data-toggle-banner]').dataset.toggleBanner); SA.app.rerender(); return; }
        if (t('[data-move]')) { AD().moveBanner(t('[data-move]').dataset.move, +t('[data-move]').dataset.dir); SA.app.rerender(); return; }
        if (t('[data-del-banner]')) { if (!(await SA.ui.overlay.confirm({ title: 'Delete this banner?', text: 'It disappears from the home page.', confirmLabel: 'Delete', danger: true }))) return; AD().deleteBanner(t('[data-del-banner]').dataset.delBanner); toast('Banner deleted', 'info'); SA.app.rerender(); return; }
        if (t('[data-new-news]')) return newsDialog();
        if (t('[data-edit-news]')) return newsDialog(db().find('newsPosts', t('[data-edit-news]').dataset.editNews));
        if (t('[data-del-news]')) { if (!(await SA.ui.overlay.confirm({ title: 'Delete this story?', text: 'It disappears from the news section.', confirmLabel: 'Delete', danger: true }))) return; AD().deleteNews(t('[data-del-news]').dataset.delNews); toast('Story deleted', 'info'); SA.app.rerender(); return; }
        if (t('[data-break]')) { AD().flagNews(t('[data-break]').dataset.break, { breaking: !!t('[data-break]').dataset.on }); SA.app.rerender(); return; }
        if (t('[data-hide-news]')) { AD().flagNews(t('[data-hide-news]').dataset.hideNews, { hidden: !!t('[data-hide-news]').dataset.on }); toast(t('[data-hide-news]').dataset.on ? 'Story hidden' : 'Story is back', 'info'); SA.app.rerender(); return; }
        const hm = t('[data-hide-match]');
        if (hm) {
          let reason = '';
          if (hm.dataset.on) { const r = await A.reason({ title: 'Hide this game?', text: 'It disappears from Find players. Players who joined keep their spot.', label: 'Reason (internal)', confirmLabel: 'Hide game', danger: true }); if (!r) return; reason = r.reason; }
          AD().hideMatch(hm.dataset.hideMatch, reason); toast(hm.dataset.on ? 'Game hidden' : 'Game restored', 'info'); SA.app.rerender();
        }
      });
    },
  });
})(window.SA);
