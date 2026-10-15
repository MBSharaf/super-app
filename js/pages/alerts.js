/** Free-slot alerts (#/alerts): follow a pitch or an area, optionally a time window, and get notified when a venue posts a free slot. */
(function (SA) {
  const { esc, fmtHour } = SA.core.utils;
  const { icon } = SA.ui;
  const S = SA.services;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });
  const HOURS = Array.from({ length: 25 }, (_, h) => h);

  SA.pages.alerts = {
    title: 'Free-slot alerts — Super App',
    render(ctx) {
      const alerts = S.games.alerts();
      const pitches = S.db.all('pitches').filter((p) => !p.deleted && p.status === 'active');
      const areas = [...new Set(pitches.map((p) => p.area))].sort();
      const slots = S.db.all('communityPosts').filter((p) => !p.deleted && !p.hidden && p.card?.type === 'slot' && SA.ui.linkCards.slotState(p.card).state === 'free').sort((a, b) => b.at - a.at).slice(0, 6);
      const pre = ctx.query.pitch || '';
      return `<div class="container section-tight">
        <div class="page-head"><a class="back-link" href="#/community/pitches">${icon('chevronLeft', 16)} Pitches & free slots</a><h1 class="h1" style="margin-top:10px">Free-slot alerts</h1><p class="lead">Be the first to know when a venue frees up an hour you’d play.</p></div>
        <div class="sup-grid">
          <section class="panel panel-pad"><h2 class="h3">New alert</h2>
            <form class="form-grid" data-alert novalidate style="margin-top:14px">
              <div class="field span-2"><label for="al-p">Pitch</label><select class="select" id="al-p" name="pitchId"><option value="">Any pitch in an area…</option>${pitches.map((p) => `<option value="${p.id}" ${pre === p.id ? 'selected' : ''}>${esc(p.name)} · ${esc(p.area)}</option>`).join('')}</select></div>
              <div class="field span-2" data-area-wrap ${pre ? 'hidden' : ''}><label for="al-a">Area</label><select class="select" id="al-a" name="area"><option value="">Choose an area</option>${areas.map((a) => `<option>${esc(a)}</option>`).join('')}</select></div>
              <div class="field"><label for="al-f">From</label><select class="select" id="al-f" name="from">${HOURS.slice(0, 24).map((h) => `<option value="${h}" ${h === 17 ? 'selected' : ''}>${fmtHour(h)}</option>`).join('')}</select></div>
              <div class="field"><label for="al-t">Until</label><select class="select" id="al-t" name="to">${HOURS.slice(1).map((h) => `<option value="${h}" ${h === 24 ? 'selected' : ''}>${h === 24 ? 'Midnight' : fmtHour(h)}</option>`).join('')}</select></div>
              <p class="field-error span-2" data-err="form"></p>
              <div class="span-2"><button class="btn btn-primary" type="submit">${icon('bell', 15)} Turn on alert</button></div></form></section>
          <section><h2 class="h3" style="margin-bottom:12px">Your alerts</h2>
            ${alerts.length ? `<ul class="adm-list">${alerts.map((a) => { const p = a.pitchId ? S.pitches.getSync(a.pitchId) : null; return `<li><span class="grow"><b class="small">${p ? esc(p.name) : `Any pitch in ${esc(a.area)}`}</b><span class="xs muted" style="display:block">${fmtHour(a.from)} – ${a.to === 24 ? 'midnight' : fmtHour(a.to)}</span></span><button class="btn btn-ghost btn-sm" data-rm="${a.id}">Remove</button></li>`; }).join('')}</ul>`
              : `<div class="panel">${SA.ui.c.empty({ icon: 'bell', title: 'No alerts yet', text: 'Add a pitch or an area — we’ll notify you when a free slot is posted.' })}</div>`}
            <h2 class="h3" style="margin:22px 0 12px">Free right now</h2>
            <div class="lk-list">${slots.map((p) => SA.ui.linkCards.slot(p.card)).join('') || '<p class="small muted">No free slots posted at the moment.</p>'}</div></section>
        </div></div>`;
    },
    mount(el) {
      const f = el.querySelector('[data-alert]');
      f.pitchId.addEventListener('change', () => { el.querySelector('[data-area-wrap]').hidden = !!f.pitchId.value; });
      f.addEventListener('submit', (e) => {
        e.preventDefault();
        try { S.games.addAlert(SA.core.utils.formData(f)); toast('Alert on — we’ll notify you'); SA.app.rerender(); } catch (err) { f.querySelector('[data-err="form"]').textContent = err.message; }
      });
      el.addEventListener('click', (e) => { const r = e.target.closest('[data-rm]'); if (r) { S.games.removeAlert(r.dataset.rm); toast('Alert removed', 'info'); SA.app.rerender(); } });
    },
  };
})(window.SA);
