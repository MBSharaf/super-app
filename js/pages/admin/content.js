/** Admin console: site content — home banners, the announcement bar, pop-up ads, news, and pickup-game moderation. */
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
      m.querySelector('[data-banner]').addEventListener('submit', (e) => { e.preventDefault(); try { AD().saveBanner({ ...SA.core.utils.formData(e.target), active: b ? b.active : true }, b?.id); close(); toast('Banner saved'); SA.app.rerender(); } catch (err) { C.showErrors(e.target, err.details || {}); } });
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
      m.querySelector('[data-news]').addEventListener('submit', (e) => { e.preventDefault(); try { const out = AD().saveNews(SA.core.utils.formData(e.target), n?.id); close(); toast(n ? 'Story saved' : 'Story published', 'success'); SA.app.rerender(); void out; } catch (err) { C.showErrors(e.target, err.details || {}); } });
    },
  });

  /** Pop-up ad editor: what it says, where it goes, its colour and its 3D object */
  const popupDialog = (p = null) => SA.ui.overlay.open({
    wide: true, title: p ? 'Edit pop-up' : 'New pop-up', subtitle: 'A small card with a 3D object that slides in on the public site. Visitors can close it or turn offers off.',
    body: `<form class="form-grid" data-popup novalidate>
      <div class="field span-2"><label for="pp-t">Title</label><input class="input" id="pp-t" name="title" maxlength="60" value="${esc(p?.title || '')}" placeholder="e.g. Join as a seller and earn" /><p class="field-error" data-err="title"></p></div>
      <div class="field span-2"><label for="pp-x">Text</label><input class="input" id="pp-x" name="text" maxlength="140" value="${esc(p?.text || '')}" placeholder="One line about the offer" /><p class="field-error" data-err="text"></p></div>
      <div class="field"><label for="pp-c">Button text</label><input class="input" id="pp-c" name="cta" maxlength="24" value="${esc(p?.cta || '')}" placeholder="See more" /></div>
      <div class="field"><label for="pp-l">Opens</label><input class="input" id="pp-l" name="link" value="${esc(p?.link || '#/')}" placeholder="#/fantasy" /><p class="field-error" data-err="link"></p></div>
      <div class="field"><label for="pp-b">Badge (on the coin keep it short, e.g. 10%)</label><input class="input" id="pp-b" name="badge" maxlength="24" value="${esc(p?.badge || '')}" /></div>
      <div class="field"><label for="pp-k">Colour</label><input class="input" id="pp-k" name="color" type="color" value="${esc(p?.color || '#E8590C')}" /><p class="field-error" data-err="color"></p></div>
      <div class="field"><label for="pp-s">3D object</label><select class="select" id="pp-s" name="shape">${[['cube', 'Spinning cube with icons'], ['coin', 'Coin with the badge'], ['tee', '3D T-shirt']].map(([v, l]) => `<option value="${v}" ${(p?.shape || 'cube') === v ? 'selected' : ''}>${l}</option>`).join('')}</select></div>
      <div class="field"><label for="pp-i">Cube icons (comma separated)</label><input class="input" id="pp-i" name="icons" value="${esc((p?.icons || []).join(', '))}" placeholder="trophy, ball, star" /></div>
      <label class="choice span-2"><input type="checkbox" name="forNonSellers" ${p?.forNonSellers ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span><span class="grow"><b>Don’t show to people who already sell</b></span></label>
      <div class="span-2 row" style="--gap:8px;justify-content:flex-end"><button type="button" class="btn btn-ghost" data-close>Cancel</button><button class="btn btn-primary" type="submit">${p ? 'Save pop-up' : 'Add pop-up'}</button></div></form>`,
    onMount(m, close) {
      m.querySelector('[data-popup]').addEventListener('submit', (e) => { e.preventDefault(); try { AD().savePopup(SA.core.utils.formData(e.target), p?.id); close(); toast('Pop-up saved'); SA.app.rerender(); } catch (err) { C.showErrors(e.target, err.details || {}); toast(err.message, 'error'); } });
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
      } else if (tab === 'popups') {
        const cfg = CT().promoPops(), stats = CT().promoStats();
        const SH = { cube: 'Cube', coin: 'Coin', tee: '3D T-shirt' };
        section = `<section class="dash-card"><h2 class="h4">Pop-up ads</h2><p class="small soft" style="margin-top:4px">Small cards with a 3D object for Super App’s own services and offers. They take turns on the public site — never on checkout, dashboards or the studio, never over a dialog, and never for the page the visitor is already on. Visitors can close one or turn offers off for 7 days.</p>
          <form class="form-grid" data-popup-rhythm novalidate style="margin-top:14px">
            <label class="choice span-2"><input type="checkbox" name="on" ${cfg.on ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span><span class="grow"><b>Show pop-ups</b></span></label>
            <div class="field"><label for="pr-f">First one after (seconds)</label><input class="input num" id="pr-f" name="first" type="number" min="2" max="120" value="${cfg.first}" /></div>
            <div class="field"><label for="pr-i">Then one every (seconds)</label><input class="input num" id="pr-i" name="interval" type="number" min="5" max="600" value="${cfg.interval}" /></div>
            <div class="field"><label for="pr-s">Each stays (seconds)</label><input class="input num" id="pr-s" name="stay" type="number" min="4" max="30" value="${cfg.stay}" /></div>
            <div class="field"><label for="pr-m">At most per visit</label><input class="input num" id="pr-m" name="max" type="number" min="1" max="50" value="${cfg.max}" /></div>
            <div class="span-2"><button class="btn btn-primary" type="submit">Save</button></div></form></section>
          <section class="dash-card"><div class="row-between"><div><h2 class="h4">Pop-ups</h2><p class="xs muted" style="margin-top:4px">They take turns in this order, one at a time.</p></div><button class="btn btn-primary btn-sm" data-new-popup>${icon('plus', 15)} New pop-up</button></div>
            <ul class="adm-banners" style="margin-top:12px">${cfg.items.map((p) => { const s2 = stats[p.id] || { shown: 0, clicked: 0 }; return `<li class="${p.active ? '' : 'is-off'}"><span class="adm-pp-dot" style="background:${esc(p.color)}">${icon(p.shape === 'coin' ? 'percent' : p.shape === 'tee' ? 'shirt' : (p.icons || [])[0] || 'sparkle', 18)}</span>
              <span class="grow"><b class="small">${esc(p.title)}</b><span class="xs muted" style="display:block">${esc(p.text)}</span><span class="xs muted" style="display:block"><span>${SH[p.shape] || 'Cube'}</span> · <span translate="no">${esc(p.link)}</span> · <span>shown ${s2.shown} · clicked ${s2.clicked}${s2.shown ? ` (${Math.round((s2.clicked / s2.shown) * 100)}%)` : ''}</span></span></span>
              <span class="adm-row-tools"><button class="btn btn-ghost btn-sm" data-preview-popup="${p.id}">Preview</button><button class="btn btn-ghost btn-sm" data-toggle-popup="${p.id}">${p.active ? 'Pause' : 'Turn on'}</button><button class="icon-btn" data-edit-popup="${p.id}" aria-label="Edit">${icon('edit', 15)}</button><button class="icon-btn" data-del-popup="${p.id}" aria-label="Delete">${icon('trash', 15)}</button></span></li>`; }).join('')}</ul></section>`;
      } else if (tab === 'news') {
        const all = CT().allStories();
        section = `<section class="dash-card table-card"><div class="row-between" style="padding:18px 18px 0"><div><h2 class="h4">News</h2><p class="xs muted" style="margin-top:4px">Your stories plus the news feed. Hide anything that shouldn’t be on Super App.</p></div><button class="btn btn-primary btn-sm" data-new-news>${icon('edit', 14)} Write a story</button></div>
          <div class="table-wrap"><table class="table dash-table"><thead><tr><th>Story</th><th>Source</th><th>Published</th><th>Status</th><th></th></tr></thead><tbody>
          ${all.slice(0, 60).map((s) => `<tr class="${s.hidden ? 'is-dim' : ''}"><td class="small"><b>${esc(s.title)}</b><span class="xs muted" style="display:block">${esc(SA.data.news.categories.find((c) => c.id === s.cat)?.name || '')}</span></td><td>${s.own ? '<span class="badge badge-blaze">Super App</span>' : `<span class="badge">${esc(s.type)}</span>`}</td><td class="xs">${timeAgo(s.at)}</td>
            <td>${s.hidden ? '<span class="badge badge-danger">Hidden</span>' : s.breaking ? '<span class="badge badge-blaze">Breaking</span>' : '<span class="badge badge-turf">Live</span>'}</td>
            <td class="r"><div class="row" style="--gap:4px;justify-content:flex-end"><a class="btn btn-ghost btn-sm" href="#/news/${s.id}" target="_blank" rel="noopener">View</a>${s.own ? `<button class="btn btn-ghost btn-sm" data-edit-news="${s.id}">Edit</button>` : ''}<button class="btn btn-ghost btn-sm" data-break="${s.id}" data-on="${s.breaking ? '' : '1'}">${s.breaking ? 'Not breaking' : 'Breaking'}</button><button class="btn btn-ghost btn-sm" data-hide-news="${s.id}" data-on="${s.hidden ? '' : '1'}">${s.hidden ? 'Show' : 'Hide'}</button>${s.own ? `<button class="icon-btn" data-del-news="${s.id}" aria-label="Delete">${icon('trash', 15)}</button>` : ''}</div></td></tr>`).join('')}
          </tbody></table></div></section>`;
      } else if (tab === 'categories') {
        const cats = SA.data.products.categories;
        const count = (id) => db().all('products').filter((p) => p.cat === id && !p.deleted).length;
        const ICONS = ['boot', 'ball', 'shirt', 'glove', 'shoe', 'cone', 'racket', 'bag', 'whistle', 'trophy', 'tag', 'sparkle', 'heart', 'water', 'firstaid', 'camera'];
        section = `<section class="dash-card"><div class="row-between wrap" style="--gap:8px"><div><h2 class="h4">Shop categories</h2><p class="xs muted" style="margin-top:4px">Rename, reorder, hide or add the categories of the shop. Hiding one hides its products too. “Designs” is managed by print on demand.</p></div><button class="btn btn-outline btn-sm" data-cat-add>${icon('plus', 14, 2.6)} Add category</button></div>
          <form data-cats novalidate style="margin-top:14px"><ul class="adm-cats">${cats.map((c, i) => `<li data-cat="${esc(c.id)}" class="${c.hidden ? 'is-dim' : ''}"><span class="adm-cat-ico">${icon(c.icon, 18)}</span>
            <input class="input" name="name" value="${esc(c.name)}" aria-label="Name" maxlength="40" />
            <select class="select" name="icon" aria-label="Icon">${ICONS.map((k) => `<option ${k === c.icon ? 'selected' : ''}>${k}</option>`).join('')}</select>
            <span class="xs muted nowrap">${count(c.id)} products</span>
            <label class="choice adm-cat-hide"><input type="checkbox" name="hidden" ${c.hidden ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span><span class="xs">Hidden</span></label>
            <span class="adm-row-tools"><button type="button" class="icon-btn" data-cat-move="-1" ${i ? '' : 'disabled'} aria-label="Move up">${icon('chevronDown', 15)}</button><button type="button" class="icon-btn" data-cat-move="1" ${i < cats.length - 1 ? '' : 'disabled'} aria-label="Move down">${icon('chevronDown', 15)}</button>${count(c.id) ? '' : `<button type="button" class="icon-btn" data-cat-del aria-label="Remove">${icon('trash', 15)}</button>`}</span></li>`).join('')}</ul>
            <p class="field-error" data-err="cats"></p>
            <div class="row" style="--gap:8px;margin-top:12px"><button class="btn btn-primary" type="submit">Save categories</button>${SA.services.db.settings().shopCategories ? '<button type="button" class="btn btn-ghost" data-cat-reset>Back to the default list</button>' : ''}</div></form></section>`;
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
      const body = `${A.tabs('/admin/content', [['', 'Home banners'], ['announce', 'Announcement bar'], ['popups', 'Pop-ups'], ['news', 'News'], ['games', 'Pickup games'], ['categories', 'Shop categories']], tab)}${section}`;
      return C.layout('content', { title: 'Site content', subtitle: 'What visitors see: home banners, the announcement bar, pop-ups, news and pickup games', actions: '<a class="btn btn-ghost btn-sm" href="#/" target="_blank" rel="noopener">Open the site ' + icon('arrowUpRight', 14) + '</a>', body });
    },
    mount(el) {
      el.querySelector('[data-popup-rhythm]')?.addEventListener('submit', (e) => { e.preventDefault(); try { AD().setPopupRhythm(SA.core.utils.formData(e.target)); toast('Pop-up settings saved'); SA.app.rerender(); } catch (err) { toast(err.message, 'error'); } });
      el.querySelector('[data-announce]')?.addEventListener('submit', (e) => { e.preventDefault(); try { AD().setAnnouncement(SA.core.utils.formData(e.target)); toast('Announcement saved'); SA.app.rerender(); } catch (err) { C.showErrors(e.target, err.details || {}); } });
      const cf = el.querySelector('[data-cats]');
      const readCats = () => [...cf.querySelectorAll('[data-cat]')].map((li) => ({ id: li.dataset.cat, name: li.querySelector('[name=name]').value, icon: li.querySelector('[name=icon]').value, hidden: li.querySelector('[name=hidden]').checked }));
      cf?.addEventListener('submit', (e) => { e.preventDefault(); try { AD().saveCategories(readCats()); toast('Categories saved'); SA.app.rerender(); } catch (err) { cf.querySelector('[data-err="cats"]').textContent = err.message; } });
      el.addEventListener('click', async (e) => {
        const t = (s) => e.target.closest(s);
        if (t('[data-cat-move]')) { const li = t('[data-cat]'), dir = +t('[data-cat-move]').dataset.catMove; const sib = dir < 0 ? li.previousElementSibling : li.nextElementSibling; if (sib) (dir < 0 ? sib.before(li) : sib.after(li)); return; }
        if (t('[data-cat-del]')) { t('[data-cat]').remove(); return; }
        if (t('[data-cat-add]')) { const ul = cf.querySelector('.adm-cats'); const li = ul.lastElementChild.cloneNode(true); li.dataset.cat = ''; li.classList.remove('is-dim'); li.querySelector('[name=name]').value = ''; li.querySelector('[name=hidden]').checked = false; li.querySelector('.nowrap').textContent = 'new'; ul.appendChild(li); li.querySelector('[name=name]').focus(); return; }
        if (t('[data-cat-reset]')) { SA.services.db.setSettings({ shopCategories: null }); AD().log('shop.categories', 'Restored the default shop categories'); toast('Default categories restored', 'info'); SA.app.rerender(); return; }
        if (t('[data-new-popup]')) return popupDialog();
        if (t('[data-edit-popup]')) return popupDialog(CT().promoPops().items.find((x) => x.id === t('[data-edit-popup]').dataset.editPopup));
        if (t('[data-toggle-popup]')) { AD().togglePopup(t('[data-toggle-popup]').dataset.togglePopup); SA.app.rerender(); return; }
        if (t('[data-preview-popup]')) { SA.ui.promoPop?.show(t('[data-preview-popup]').dataset.previewPopup); return; }
        if (t('[data-del-popup]')) { if (!(await SA.ui.overlay.confirm({ title: 'Delete this pop-up?', text: 'It stops showing on the site.', confirmLabel: 'Delete', danger: true }))) return; AD().deletePopup(t('[data-del-popup]').dataset.delPopup); toast('Pop-up deleted', 'info'); SA.app.rerender(); return; }
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
