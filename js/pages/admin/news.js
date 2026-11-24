/**
 * Admin console: News (#/admin/news). The team writes football stories (Arabic or English) with a link to the source.
 * Publishing a story tells the followers of its club; an important story tells everyone. Hide takes a story off the
 * site. Needs the server (permission content.news); on the demo the page says so.
 */
(function (SA) {
  const { esc, timeAgo } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const A = SA.pages.adminCommon;
  const F = () => SA.services.follow;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });
  const at = (iso) => (iso ? new Date(iso.endsWith('Z') ? iso : `${iso}Z`).getTime() : Date.now());

  const clubOptions = async (selected) => {
    const comps = await F().competitions().catch(() => []);
    return `<option value="">No club (general news)</option>${comps.map((c) => `<optgroup label="${esc(c.name)}">${c.clubs.map((x) => `<option value="${x.id}" ${selected === x.id ? 'selected' : ''}>${esc(x.name)}</option>`).join('')}</optgroup>`).join('')}`;
  };

  const editor = async (id) => {
    const p = id ? await F().admin.get(id) : null;
    const n = p?.item;
    const clubs = await clubOptions(n?.club?.id ?? null);
    SA.ui.overlay.open({
      wide: true,
      title: id ? 'Edit story' : 'New story',
      subtitle: id ? 'Changes show at once. Editing does not send the notification again.' : 'Publishing tells the club’s followers, or everyone when it is important.',
      body: `<form class="form-grid" id="nw" novalidate>
        <div class="field"><label for="nw-l">Language</label><select class="select" id="nw-l" name="language"><option value="ar" ${n?.language !== 'en' ? 'selected' : ''}>Arabic</option><option value="en" ${n?.language === 'en' ? 'selected' : ''}>English</option></select></div>
        <div class="field"><label for="nw-c">Club</label><select class="select" id="nw-c" name="clubId">${clubs}</select></div>
        <div class="field span-2"><label for="nw-t">Title</label><input class="input" id="nw-t" name="title" maxlength="160" value="${esc(n?.title || '')}" required /></div>
        <div class="field span-2"><label for="nw-b">Story</label><textarea class="input" id="nw-b" name="body" rows="9" maxlength="6000" required>${esc(p?.body || '')}</textarea><p class="xs muted">Plain text. Leave an empty line between paragraphs.</p></div>
        <div class="field"><label for="nw-sn">Source name</label><input class="input" id="nw-sn" name="sourceName" maxlength="80" value="${esc(n?.sourceName || '')}" placeholder="e.g. Club website" /></div>
        <div class="field"><label for="nw-su">Source link</label><input class="input" id="nw-su" name="sourceUrl" type="url" maxlength="400" value="${esc(n?.sourceUrl || '')}" placeholder="https://…" /></div>
        <label class="choice span-2"><input type="checkbox" name="important" ${n?.important ? 'checked' : ''} /> <span>Important: tell everyone, not only the club’s followers</span></label>
        <label class="choice span-2"><input type="checkbox" name="published" ${!p || p.published ? 'checked' : ''} /> <span>Published (shown on the site)</span></label>
        <p class="field-error span-2" data-err="form"></p></form>`,
      footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-primary" form="nw" type="submit">${id ? 'Save story' : 'Publish story'}</button>`,
      onMount(m, close) {
        const f = m.querySelector('#nw');
        f.addEventListener('submit', async (e) => {
          e.preventDefault();
          const err = f.querySelector('[data-err="form"]');
          const body = { title: f.title.value.trim(), body: f.body.value.trim(), language: f.language.value, sourceName: f.sourceName.value.trim() || null,
            sourceUrl: f.sourceUrl.value.trim() || null, clubId: f.clubId.value ? +f.clubId.value : null, important: f.important.checked, published: f.published.checked };
          if (body.title.length < 5 || body.body.length < 20) { err.textContent = 'Write a title (5+ letters) and a story (20+ letters).'; return; }
          try { await F().admin.save(id, body); close(); toast(id ? 'Story saved' : 'Story published'); SA.app.rerender(); }
          catch (ex) { err.textContent = Object.values(ex.details || {})[0] || ex.message; }
        });
      },
    });
  };

  SA.pages.adminNews = A.guard({
    title: 'News — Super Malaab Admin',
    async render(ctx) {
      await SA.services.api.ready;
      if (!SA.services.api.isLive()) {
        return C.layout('news', { title: 'News', body: `<div class="dash-card">${SA.ui.c.empty({ icon: 'news', title: 'News works on the live site', text: 'Stories and their notifications are saved on the server.' })}</div>` });
      }
      const page = Math.max(1, +ctx.query.page || 1);
      let list;
      try { list = await F().admin.list(page); } catch (err) {
        return C.layout('news', { title: 'News', body: `<div class="dash-card">${SA.ui.c.empty({ icon: 'lock', title: err.status === 403 ? 'No access to news' : 'Couldn’t load news', text: esc(err.message) })}</div>` });
      }
      const row = (n) => `<tr><td class="small"><b data-i18n-skip ${n.language === 'ar' ? 'dir="rtl"' : ''}>${esc(n.title)}</b><span class="xs muted" style="display:block" data-i18n-skip>${esc(n.excerpt).slice(0, 120)}</span></td>
          <td class="small" data-i18n-skip>${n.club ? esc(n.club.name) : '—'}</td>
          <td>${n.important ? '<span class="badge badge-blaze">Important</span>' : '<span class="badge">Followers</span>'}</td>
          <td class="small muted">${timeAgo(at(n.publishedAt))}</td>
          <td class="r"><div class="row" style="--gap:4px;justify-content:flex-end"><a class="btn btn-ghost btn-sm" href="#/news/${n.id}" target="_blank" rel="noopener">View</a><button class="btn btn-ghost btn-sm" data-edit="${n.id}">Edit</button><button class="btn btn-ghost btn-sm" data-hide="${n.id}">Hide</button></div></td></tr>`;
      const body = `<p class="adm-help small">${icon('info', 14)} <span>Write short stories with a link to the source. A story with a club tells that club’s followers; an important story tells everyone. Hidden stories leave the site.</span></p>
        ${list.length ? `<section class="dash-card table-card"><div class="table-wrap"><table class="table dash-table"><thead><tr><th>Story</th><th>Club</th><th>Who is told</th><th>Published</th><th></th></tr></thead><tbody>${list.map(row).join('')}</tbody></table></div></section>
          ${list.length >= 20 ? `<div class="row" style="justify-content:center;margin-top:12px"><a class="btn btn-outline btn-sm" href="#/admin/news?page=${page + 1}">Older stories</a></div>` : ''}`
          : `<div class="dash-card">${SA.ui.c.empty({ icon: 'news', title: 'No stories yet', text: 'Write the first story: it shows on the News page and reaches the club’s followers.' })}</div>`}`;
      return C.layout('news', { title: 'News', subtitle: 'Football stories by the team, with notifications to followers', actions: `<button class="btn btn-primary" data-new>${icon('plus', 16)} New story</button>`, body });
    },
    mount(el) {
      el.addEventListener('click', async (e) => {
        const t = (s) => e.target.closest(s);
        try {
          if (t('[data-new]')) { await editor(null); return; }
          if (t('[data-edit]')) { await editor(t('[data-edit]').dataset.edit); return; }
          if (t('[data-hide]')) {
            const id = t('[data-hide]').dataset.hide;
            const r = await A.reason({ title: 'Hide this story?', text: 'It leaves the News page. Notifications already sent stay in people’s bells.', label: 'Note (optional)', confirmLabel: 'Hide story', danger: true, required: false });
            if (!r) return;
            await F().admin.hide(id); toast('Story hidden', 'info'); SA.app.rerender();
          }
        } catch (err) { toast(err.message, 'error'); }
      });
    },
  });
})(window.SA);
