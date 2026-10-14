(function (SA) {
  const { esc, timeAgo, fmtNum, fmtDate } = SA.core.utils;
  const { icon } = SA.ui;
  const c = SA.ui.c;
  const { newsCard, typeBadge } = SA.ui.sports;
  const media = SA.data.media;
  const S = SA.services;

  const chips = (active) => `<div class="chips news-chips" role="group" aria-label="Filter news">
    <a class="chip" href="#/news" aria-pressed="${!active}">All stories</a>
    <a class="chip" href="#/news?cat=video" aria-pressed="${active === 'video'}">${icon('play', 13, 2.4)} Videos</a>
    ${S.news.categories().map((k) => `<a class="chip" href="#/news?cat=${k.id}" aria-pressed="${active === k.id}"><i class="cat-dot" style="background:${k.color}"></i>${esc(k.name)}</a>`).join('')}
  </div>`;

  SA.pages.news = {
    title: 'Football news — Super App',
    async render(ctx) {
      const cat = ctx.query.cat || '';
      const stories = await S.news.list(cat === 'video' ? { type: 'video' } : { cat });
      const breaking = S.news.breaking();
      const catName = cat === 'video' ? 'Videos' : S.news.categories().find((k) => k.id === cat)?.name;
      const head = `<section class="container news-head">
          <span class="watermark" data-parallax="-0.3" aria-hidden="true" style="top:-10px;right:-6%">NEWS NEWS</span>
          <nav class="crumbs" aria-label="Breadcrumb"><a href="#/">Home</a>${icon('chevronRight', 14)}<a href="#/news">News</a>${catName ? icon('chevronRight', 14) + `<span>${esc(catName)}</span>` : ''}</nav>
          <h1 class="display news-display">${catName ? esc(catName) : 'The Egyptian game, as it happens.'}</h1>
          <p class="lead">Stories from the press, TV studios and club statements — Al Ahly, Zamalek, Pyramids, the Pharaohs and every club in between.</p>
          <p class="xs muted demo-note">${icon('info', 12)} Sample stories for this demo. Connect a licensed news feed to publish live coverage.</p>
        </section>
        <section class="container"><div class="ticker ticker-breaking" aria-label="Breaking news">
          <span class="ticker-label"><span class="live-dot" style="width:8px;height:8px;border-radius:50%;background:#fff;display:inline-block"></span>BREAKING</span>
          <div class="ticker-track" aria-hidden="true">${[0, 1].map(() => breaking.map((b) => `<span><i></i><b>${esc(b.category.name)}</b> ${esc(b.title)}</span>`).join('')).join('')}</div>
        </div></section>
        <section class="container" style="margin-top:20px">${chips(cat)}</section>`;

      if (cat) {
        return `${head}<section class="container section-tight">
          ${stories.length ? `<div class="news-grid">${stories.map((s, i) => newsCard(s, { size: i === 0 ? 'lg' : '' })).join('')}</div>` : c.empty({ icon: 'info', title: 'No stories here yet', text: 'Check back after the next round of matches.' })}
        </section>`;
      }

      const [lead, ...rest] = stories;
      const mostRead = S.news.mostRead(5);
      const ahly = stories.filter((s) => s.clubs.includes('ahly') || s.cat === 'ahly').slice(0, 3);
      const zam = stories.filter((s) => s.clubs.includes('zamalek') || s.cat === 'zamalek').slice(0, 3);
      const videos = stories.filter((s) => s.video);
      const derbyItem = (s) => `<a class="derby-item" href="#/news/${s.id}"><span class="xs">${timeAgo(s.at)} · ${s.type}</span><b>${esc(s.title)}</b></a>`;

      return `${head}
      <section class="container section-tight">
        <div class="news-top">
          ${newsCard(lead, { size: 'lg' })}
          <aside class="most-read panel">
            <h2 class="h4">Most read</h2>
            <ol>${mostRead.map((s, i) => `<li><a href="#/news/${s.id}"><span class="mr-n">${i + 1}</span><span class="grow"><b>${esc(s.title)}</b><span class="xs muted">${esc(s.category.name)} · ${fmtNum(s.views)} views</span></span></a></li>`).join('')}</ol>
          </aside>
        </div>
      </section>

      <section class="container section-tight">
        <div class="sec-head"><div><h2 class="h2">Derby corner</h2><p>The latest from both sides of Cairo, side by side.</p></div></div>
        <div class="derby">
          <div class="derby-side ahly reveal rv-slide"><div class="derby-club">${SA.ui.sports.crest(SA.data.competitions.teams.AHL, 46)}<div><b>Al Ahly</b><span class="xs">The Red Giants</span></div></div>${ahly.map(derbyItem).join('')}<a class="link" href="#/news?cat=ahly">All Al Ahly news ${icon('arrowRight', 14)}</a></div>
          <div class="derby-vs" aria-hidden="true"><span>VS</span></div>
          <div class="derby-side zamalek reveal rv-fade-left"><div class="derby-club">${SA.ui.sports.crest(SA.data.competitions.teams.ZAM, 46)}<div><b>Zamalek</b><span class="xs">The White Knights</span></div></div>${zam.map(derbyItem).join('')}<a class="link" href="#/news?cat=zamalek">All Zamalek news ${icon('arrowRight', 14)}</a></div>
        </div>
      </section>

      <section class="section tv-band">
        <div class="container">
          <div class="sec-head"><div><h2 class="h2" style="color:#fff">From the studio</h2><p style="color:rgba(255,255,255,.65)">Analysis, press conferences and previews — watch in under ten minutes.</p></div><a class="btn btn-ghost btn-sm" style="color:#fff" href="#/news?cat=video">All videos ${icon('arrowRight', 16)}</a></div>
          <div class="scroller" style="--col:clamp(260px, 32vw, 380px)">${videos.map((s) => newsCard(s, { size: 'sm' })).join('')}</div>
        </div>
      </section>

      <section class="container section">
        <div class="sec-head"><div><h2 class="h2">Latest stories</h2><p>Everything from the last 48 hours.</p></div></div>
        <div class="news-grid">${rest.filter((s) => !s.video).map((s) => newsCard(s)).join('')}</div>
      </section>`;
    },
  };

  SA.pages.newsArticle = {
    title: 'News — Super App',
    async render(ctx) {
      const s = await S.news.get(ctx.params.id);
      const related = await S.news.related(s.id, 3);
      this.title = `${s.title} — Super App News`;
      return `<article class="article">
        <header class="article-hero">
          <div class="article-img"><img src="${media.photo(s.img, 1800, 900)}" alt="" data-parallax="0.12" class="px-hero" />${s.video ? `<button class="play-btn big" data-play aria-label="Play video, ${s.video}">${icon('play', 30, 2.4)}</button>` : ''}</div>
          <div class="container article-head-inner">
            <a class="back-link" href="#/news" style="color:#fff">${icon('chevronLeft', 16)} All news</a>
            <div class="row wrap" style="--gap:8px;margin-top:14px"><span class="news-cat static" style="--cat:${s.category.color}">${esc(s.category.name)}</span>${typeBadge(s)}</div>
            <h1 class="h1 article-title">${esc(s.title)}</h1>
            <p class="article-meta">${fmtDate(new Date(s.at), 'long')} · ${timeAgo(s.at)} · ${s.read} min read · Super App newsroom</p>
          </div>
        </header>
        <div class="container article-layout">
          <div class="article-body">
            <p class="article-lede">${esc(s.excerpt)}</p>
            ${s.body.map((p) => `<p>${esc(p)}</p>`).join('')}
            <p class="xs muted demo-note" style="margin-top:28px">${icon('info', 12)} Sample story for this demo.</p>
            <div class="article-actions">
              <button class="btn btn-outline btn-sm ${s.saved ? 'is-on' : ''}" data-save aria-pressed="${s.saved}">${icon(s.saved ? 'heartFill' : 'heart', 15)} ${s.saved ? 'Saved' : 'Save story'}</button>
              <button class="btn btn-outline btn-sm" data-copy>${icon('link', 15)} Copy link</button>
            </div>
          </div>
          <aside class="article-side">
            <div class="panel panel-pad"><h2 class="h4">Related</h2><div class="stack" style="--gap:14px;margin-top:14px">${related.map((r) => newsCard(r, { size: 'sm' })).join('')}</div></div>
            <a class="panel panel-pad article-cta" href="#/matches"><span class="service-ico">${icon('calendar', 20)}</span><span><b>Match schedule</b><span class="small soft" style="display:block">Kick-off times, live scores and results</span></span>${icon('arrowRight', 18)}</a>
          </aside>
        </div>
      </article>`;
    },
    mount(el, ctx) {
      el.querySelector('[data-save]')?.addEventListener('click', (e) => {
        const on = S.news.toggleSave(ctx.params.id);
        const b = e.currentTarget;
        b.classList.toggle('is-on', on); b.setAttribute('aria-pressed', on);
        b.innerHTML = `${icon(on ? 'heartFill' : 'heart', 15)} ${on ? 'Saved' : 'Save story'}`;
        if (on) SA.core.motion.burst(b);
      });
      el.querySelector('[data-copy]')?.addEventListener('click', () => { navigator.clipboard?.writeText(location.href); SA.ui.overlay.toast('Link copied'); });
      el.querySelector('[data-play]')?.addEventListener('click', () => SA.ui.overlay.toast('Video playback will connect to your media provider.', { type: 'info' }));
    },
  };
})(window.SA);
