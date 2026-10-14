/** Partner Dashboard — player reviews of your pitches, with public replies and reporting. */
(function (SA) {
  const { esc, timeAgo } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const D = () => SA.services.dash;
  const c = SA.ui.c;

  const TEMPLATES = [
    ['Thank you', (n) => `Thanks ${n}! Really glad you enjoyed your game — see you on the pitch again soon.`],
    ['Sorry + fixed', (n) => `Sorry about that, ${n}, and thanks for telling us. We’ve fixed it and our staff now check between games.`],
    ['Invite back', (n) => `Thanks for the feedback, ${n}. Come back for another game — our weekday afternoons are quieter and cheaper.`],
  ];
  const REASONS = ['Fake or not a real customer', 'Offensive language', 'About a different venue', 'Contains personal information'];

  const card = (r, admin, i) => {
    const first = r.customer.name.split(' ')[0];
    return `<article class="rv-card ${r.hidden ? 'is-hidden' : ''} ${r.rating <= 3 ? 'is-low' : ''}" style="--i:${i}" data-review="${r.id}">
      <div class="row">${c.avatar({ name: r.customer.name, avatar: r.customer.avatar })}
        <div class="grow"><b class="small">${esc(r.customer.name)}</b><span class="xs muted" style="display:block">${esc(r.pitchName)} · ${timeAgo(r.createdAt)}${r.bookingId ? ' · verified booking' : ''}</span></div>
        ${c.stars(r.rating, 14)}</div>
      <p class="rv-text">${esc(r.text)}</p>
      ${r.hidden ? `<p class="xs" style="color:var(--danger)">${icon('eye', 12)} Hidden from the site by Super App</p>` : ''}
      ${r.reported && !r.reported.resolved ? `<p class="xs rv-flag">${icon('flag', 12)} Reported to Super App — ${esc(r.reported.reason)}</p>` : ''}
      ${r.reply ? `<div class="rv-reply"><div class="row-between"><b class="xs">${icon('message', 13)} Your reply${r.reply.edited ? ' (edited)' : ''}</b><span class="xs muted">${timeAgo(r.reply.at)}</span></div>
          <p class="small" data-reply-text>${esc(r.reply.text)}</p>
          <div class="row" style="--gap:4px"><button class="link xs" data-edit>Edit</button><span class="muted xs">·</span><button class="link xs" data-del-reply style="color:var(--danger)">Delete</button></div></div>`
        : `<form class="rv-compose" data-compose novalidate>
          <div class="chips">${TEMPLATES.map(([l], k) => `<button type="button" class="chip" data-tpl="${k}">${l}</button>`).join('')}</div>
          <label class="sr-only" for="rp-${r.id}">Reply to ${esc(first)}</label>
          <textarea class="textarea" id="rp-${r.id}" name="reply" maxlength="600" rows="2" placeholder="Write a public reply to ${esc(first)}…"></textarea><span class="field-error" data-err="reply"></span>
          <div class="row-between"><span class="xs muted">Shown under the review on your pitch page.</span><button class="btn btn-primary btn-sm" type="submit">${icon('message', 14)} Post reply</button></div></form>`}
      <div class="rv-foot">
        ${admin ? (r.hidden ? `<button class="btn btn-outline btn-sm" data-mod="show">Restore on site</button>` : `<button class="btn btn-ghost btn-sm" data-mod="hide" style="color:var(--danger)">${icon('eye', 14)} Hide from site</button>${r.reported && !r.reported.resolved ? '<button class="btn btn-outline btn-sm" data-mod="dismiss">Keep review</button>' : ''}`)
          : !r.reported ? `<button class="link xs" data-report>${icon('flag', 12)} Report to Super App</button>` : ''}
      </div>
    </article>`;
  };

  SA.pages.dashReviews = C.guard({
    title: 'Reviews — Super App Partner',
    render(ctx) {
      const q = ctx.query;
      const sc = C.scope();
      const admin = D().isAdmin();
      const pitches = D().myPitches({ ...sc, includeDeleted: true });
      const all = D().reviews({ ...sc, pitchId: q.pitch });
      const filter = q.filter || 'all';
      const F = {
        all: (r) => true, unreplied: (r) => !r.reply && !r.hidden, replied: (r) => !!r.reply, low: (r) => r.rating <= 3,
        reported: (r) => r.reported && !r.reported.resolved, hidden: (r) => r.hidden,
      };
      const list = all.filter(F[filter] || F.all);
      const visible = all.filter((r) => !r.hidden);
      const avg = visible.length ? visible.reduce((t, r) => t + r.rating, 0) / visible.length : 0;
      const replied = visible.filter((r) => r.reply).length;
      const dist = [5, 4, 3, 2, 1].map((s) => visible.filter((r) => r.rating === s).length);
      const tabs = [['all', 'All'], ['unreplied', 'Waiting for reply'], ['replied', 'Replied'], ['low', '3★ or less'], ['reported', 'Reported'], ...(admin ? [['hidden', 'Hidden']] : [])];
      const body = `
        <div class="rv-top">
          <section class="dash-card rv-score">
            <b class="rv-avg num" data-countup="${avg.toFixed(1)}" data-decimals="1">${avg.toFixed(1)}</b>${c.stars(+avg.toFixed(1), 18)}
            <span class="small muted">${visible.length} recent review${visible.length === 1 ? '' : 's'}</span>
            <ul class="rv-dist">${dist.map((n, i) => `<li><span class="xs">${5 - i}★</span><span class="meter"><span style="width:${visible.length ? (n / visible.length) * 100 : 0}%"></span></span><span class="xs num">${n}</span></li>`).join('')}</ul>
          </section>
          <div class="kpi-grid kpi-sm rv-kpis">
            ${C.kpi({ label: 'Reply rate', value: `${visible.length ? Math.round((replied / visible.length) * 100) : 0}%`, count: visible.length ? Math.round((replied / visible.length) * 100) : 0, suffix: '%', ic: 'message', tone: 'k-turf', sub: 'Partners who reply get rebooked more' })}
            ${C.kpi({ label: 'Waiting for a reply', value: visible.filter((r) => !r.reply).length, ic: 'clock', tone: visible.some((r) => !r.reply) ? 'k-blaze' : '' })}
            ${C.kpi({ label: 'Low ratings (3★ or less)', value: visible.filter((r) => r.rating <= 3).length, ic: 'alert' })}
            ${C.kpi({ label: 'Reported', value: all.filter((r) => r.reported && !r.reported.resolved).length, ic: 'flag' })}
          </div>
        </div>
        <div class="filters-bar">
          <div class="tabs" role="tablist">${tabs.map(([v, l]) => `<a class="tab" role="tab" href="${SA.core.router.href('/dash/reviews', { ...q, filter: v })}" aria-selected="${filter === v}">${l}</a>`).join('')}</div>
          <select class="select" data-pitch aria-label="Pitch"><option value="">All pitches</option>${pitches.map((p) => `<option value="${p.id}" ${q.pitch === p.id ? 'selected' : ''}>${esc(p.name)}</option>`).join('')}</select>
        </div>
        <div class="rv-list">${list.map((r, i) => card(r, admin, i)).join('') || `<div class="dash-card">${c.empty({ icon: 'star', title: filter === 'unreplied' ? 'All caught up' : 'No reviews here', text: filter === 'unreplied' ? 'You’ve replied to every review.' : 'Players can review a pitch after a game they booked on Super App.' })}</div>`}</div>`;
      return C.layout('reviews', { title: 'Reviews', subtitle: 'What players say about your pitches — reply publicly from here', body });
    },
    mount(el, ctx) {
      el.querySelector('[data-pitch]')?.addEventListener('change', (e) => SA.core.router.go(SA.core.router.href(SA.services.dash.adminMode() ? '/admin/reviews' : '/dash/reviews', { ...ctx.query, pitch: e.target.value || undefined }).slice(1)));
      el.addEventListener('click', async (e) => {
        const art = e.target.closest('[data-review]'); if (!art) return;
        const id = art.dataset.review;
        const r = D().review(id);
        const first = r.customer.name.split(' ')[0];
        const tpl = e.target.closest('[data-tpl]');
        if (tpl) { const ta = art.querySelector('textarea'); ta.value = TEMPLATES[+tpl.dataset.tpl][1](first); ta.focus(); return; }
        if (e.target.closest('[data-edit]')) {
          const box = art.querySelector('.rv-reply');
          box.outerHTML = `<form class="rv-compose" data-compose novalidate><textarea class="textarea" name="reply" maxlength="600" rows="3" aria-label="Edit reply">${esc(r.reply.text)}</textarea><span class="field-error" data-err="reply"></span><div class="row" style="justify-content:flex-end;--gap:8px"><button type="button" class="btn btn-ghost btn-sm" data-cancel-edit>Cancel</button><button class="btn btn-primary btn-sm" type="submit">Save reply</button></div></form>`;
          art.querySelector('textarea').focus();
          return;
        }
        if (e.target.closest('[data-cancel-edit]')) { SA.app.rerender(); return; }
        if (e.target.closest('[data-del-reply]')) {
          if (!(await SA.ui.overlay.confirm({ title: 'Delete your reply?', text: 'It’s removed from your pitch page. You can write a new one any time.', confirmLabel: 'Delete reply', danger: true }))) return;
          D().deleteReply(id); SA.ui.overlay.toast('Reply deleted'); SA.app.rerender(); return;
        }
        const mod = e.target.closest('[data-mod]');
        if (mod) { D().moderateReview(id, mod.dataset.mod); SA.ui.overlay.toast({ hide: 'Review hidden from the site', show: 'Review restored', dismiss: 'Report closed — review kept' }[mod.dataset.mod]); SA.app.rerender(); return; }
        if (e.target.closest('[data-report]')) {
          SA.ui.overlay.open({
            title: 'Report this review', subtitle: 'Super App checks reported reviews and removes ones that break the rules. Bad ratings alone aren’t removed.',
            body: `<form id="rep" class="stack" style="--gap:10px" novalidate>${REASONS.map((x, k) => `<label class="choice"><input type="radio" name="reason" value="${esc(x)}" ${k === 0 ? 'checked' : ''} /><span>${esc(x)}</span></label>`).join('')}</form>`,
            footer: `<button class="btn btn-outline" data-close>Cancel</button><button class="btn btn-primary" form="rep" type="submit">${icon('flag', 15)} Send report</button>`,
            onMount(m, close) { m.querySelector('#rep').addEventListener('submit', (ev) => { ev.preventDefault(); D().reportReview(id, ev.target.reason.value); close(); SA.ui.overlay.toast('Reported — Super App will review it'); SA.app.rerender(); }); },
          });
        }
      });
      el.addEventListener('submit', (e) => {
        const f = e.target.closest('[data-compose]'); if (!f) return;
        e.preventDefault();
        const id = f.closest('[data-review]').dataset.review;
        try { D().replyReview(id, f.reply.value); SA.ui.overlay.toast('Reply posted on your pitch page', { type: 'success' }); SA.app.rerender(); }
        catch (err) { if (err.details) C.showErrors(f, err.details); SA.ui.overlay.toast(err.message, { type: 'error' }); }
      });
    },
  });
})(window.SA);
