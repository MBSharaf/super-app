/** Help center (#/help): questions by topic with search, "was this helpful?", and a way to contact support. */
(function (SA) {
  const { esc } = SA.core.utils;
  const { icon } = SA.ui;
  const H = () => SA.data.help;
  const tx = (o) => (SA.i18n.isAr ? o.ar : o.en);
  const plain = (s) => s.replace(/<[^>]+>/g, '');

  /** "Was this helpful?" votes, so the CEO and support see which answers fail */
  const vote = (i, yes) => {
    const d = SA.services.db.raw();
    if (!d.helpVotes) d.helpVotes = {};
    const v = d.helpVotes[i] || { yes: 0, no: 0 };
    v[yes ? 'yes' : 'no'] += 1;
    d.helpVotes[i] = v;
    SA.services.db.touch('helpVotes');
  };

  SA.pages.help = {
    title: 'Help center — Super App',
    render(ctx) {
      const topic = ctx.query.topic || '';
      const s = (ctx.query.q || '').trim().toLowerCase();
      const list = H().QA.map((x, i) => ({ ...x, i })).filter((x) => (!topic || x.topic === topic) && (!s || `${x.q.en} ${x.q.ar} ${plain(x.a.en)} ${plain(x.a.ar)}`.toLowerCase().includes(s)));
      return `<div class="container help-page">
        <section class="help-hero"><span class="eyebrow">Help center</span><h1 class="h1">How can we help?</h1>
          <form class="help-search" role="search" data-help-search>${icon('search', 18)}<input name="q" type="search" value="${esc(ctx.query.q || '')}" placeholder="Search — e.g. cancel, refund, delivery" aria-label="Search the help center" /><button class="btn btn-primary btn-sm" type="submit">Search</button></form></section>
        <nav class="help-topics" aria-label="Topics"><a class="help-topic" href="#/help" aria-current="${!topic}">${icon('grid', 18)}<span>All topics</span></a>${H().TOPICS.map(([k, ic, en, ar]) => `<a class="help-topic" href="${SA.core.router.href('/help', { topic: k })}" aria-current="${topic === k}">${icon(ic, 18)}<span data-i18n-skip>${SA.i18n.isAr ? ar : en}</span></a>`).join('')}</nav>
        ${list.length ? `<div class="help-list">${list.map((x) => `<details class="help-q" ${s || list.length === 1 ? 'open' : ''}><summary>${icon('help', 18)}<span class="grow" data-i18n-skip>${esc(tx(x.q))}</span>${icon('chevronDown', 16)}</summary>
            <div class="help-a"><p data-i18n-skip>${tx(x.a)}</p><div class="row" style="--gap:8px;margin-top:12px;align-items:center"><span class="xs muted">Was this helpful?</span><button class="btn btn-ghost btn-sm" data-help-vote="${x.i}" data-yes="1">Yes</button><button class="btn btn-ghost btn-sm" data-help-vote="${x.i}" data-yes="0">No</button></div></div></details>`).join('')}</div>`
          : SA.ui.c.empty({ icon: 'search', title: 'No answers for that', text: 'Try other words, or ask our team.' })}
        <section class="help-more"><div><b class="h4">Still need help?</b><p class="small soft">Our team replies within 24 hours. For a booking or an order, open it and tap “Get help”.</p></div><a class="btn btn-blaze" href="#/support">${icon('message', 16)} Contact support</a></section>
        <p class="xs muted" style="margin:16px 0 40px">See also: <a class="link" href="#/legal">Terms, privacy and policies</a></p>
      </div>`;
    },
    mount(el) {
      el.querySelector('[data-help-search]')?.addEventListener('submit', (e) => { e.preventDefault(); const v = e.currentTarget.q.value.trim(); SA.core.router.go(SA.core.router.href('/help', { q: v || undefined }).slice(1)); });
      el.addEventListener('click', (e) => {
        const b = e.target.closest('[data-help-vote]'); if (!b) return;
        vote(b.dataset.helpVote, b.dataset.yes === '1');
        b.parentElement.innerHTML = `<span class="xs muted">${icon('check', 12)} ${SA.i18n.t(b.dataset.yes === '1' ? 'Thanks — glad it helped.' : 'Thanks — we’ll improve this answer.')}</span>${b.dataset.yes === '1' ? '' : ' <a class="btn btn-ghost btn-sm" href="#/support">Contact support</a>'}`;
      });
    },
  };
})(window.SA);
