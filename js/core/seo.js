/**
 * SEO: after every page, update the description, the canonical URL, Open Graph / Twitter tags (for link previews on
 * WhatsApp, Facebook and X) and structured data (JSON-LD) for pitches, products, tournaments and the organisation.
 * Private pages (dashboards, the admin console, the factory, the CEO system, cart and account) are marked noindex.
 * Link previews and search engines read the static tags in index.html; these runtime tags help Google, which runs JS.
 * A server-rendered or pre-rendered version comes with the backend for the best results.
 */
(function (SA) {
  const SITE = 'https://mbsharaf.github.io/super-app';
  const IMAGE = `${SITE}/og-image.png`;
  const DEFAULT = 'Book football pitches near you, find players for your match, play Egyptian Premier League fantasy and shop sports gear — in one app.';
  const PRIVATE = /^\/(dash|seller|admin|factory|ceo|checkout|cart|profile|wallet|orders|order|booking|bookings|wishlist|alerts|my-games|support|sell\/studio|sell\/bulk)(\/|$)/;
  const PAGES = {
    '/': DEFAULT,
    '/pitches': 'Find and book 5-a-side and 7-a-side football pitches in Cairo, Giza and Alexandria — live free hours, prices and reviews.',
    '/players': 'Need players for tonight? Join football games near you or post yours and fill the squad in minutes.',
    '/players/new': 'Post your football game and find the players you need.',
    '/matches': 'Today’s football: Egyptian Premier League, CAF, Premier League and more — fixtures, live scores and results.',
    '/news': 'Egyptian and world football news, transfers and match reports.',
    '/tournaments': 'Join 5-a-side tournaments and leagues near you — prizes, fixtures and tables.',
    '/fantasy': 'Play Egyptian Premier League fantasy: pick your squad, join leagues and win prizes.',
    '/shop': 'Football boots, jerseys, balls and original printed designs from Egyptian creators — delivered across Egypt.',
    '/sell': 'Sell your football designs on Super App: we print, ship and pay you — or list your own products.',
    '/partner/apply': 'List your football pitch on Super App: online bookings, payments and a free partner dashboard.',
    '/community': 'Football communities: players, fantasy managers, creators and venues talking football.',
    '/help': 'Answers about bookings, payments, delivery, returns, fantasy and selling on Super App.',
    '/legal': 'Super App terms, privacy policy, refunds and returns, and agreements.',
  };
  const meta = (attr, key, value) => {
    let el = document.head.querySelector(`meta[${attr}="${key}"]`);
    if (!el) { el = document.createElement('meta'); el.setAttribute(attr, key); document.head.appendChild(el); }
    el.setAttribute('content', value);
  };
  const link = (rel, href) => { let el = document.head.querySelector(`link[rel="${rel}"]`); if (!el) { el = document.createElement('link'); el.rel = rel; document.head.appendChild(el); } el.href = href; };
  const ld = (data) => {
    let el = document.getElementById('ld-json');
    if (!data) { el?.remove(); return; }
    if (!el) { el = document.createElement('script'); el.type = 'application/ld+json'; el.id = 'ld-json'; document.head.appendChild(el); }
    el.textContent = JSON.stringify(data);
  };
  const clip = (s, n = 158) => { s = String(s || '').replace(/\s+/g, ' ').trim(); return s.length > n ? `${s.slice(0, n - 1)}…` : s; };

  /** Description, image and structured data for one route */
  const describe = (path) => {
    const [, a, b] = path.split('?')[0].split('/');
    const S = SA.services;
    try {
      if (a === 'pitches' && b) {
        const p = S.pitches.getSync(b); if (!p) return {};
        return { desc: clip(`${p.name} in ${p.area}, ${p.city}: ${p.format || 'football pitch'}${p.price ? ` from ${p.price} EGP an hour` : ''}. See free hours and book online on Super App.`), image: p.images?.[0] ? SA.data.media.photo(p.images[0], 1200, 630) : IMAGE,
          ld: { '@context': 'https://schema.org', '@type': 'SportsActivityLocation', name: p.name, url: `${SITE}/pitches/${p.id}`, image: p.images?.[0] ? SA.data.media.photo(p.images[0], 1200, 800) : IMAGE,
            address: { '@type': 'PostalAddress', streetAddress: p.address || '', addressLocality: p.area || '', addressRegion: p.city || '', addressCountry: 'EG' }, ...(p.lat ? { geo: { '@type': 'GeoCoordinates', latitude: p.lat, longitude: p.lng } } : {}),
            ...(p.price ? { priceRange: `${p.price} EGP / hour` } : {}), ...(p.rating ? { aggregateRating: { '@type': 'AggregateRating', ratingValue: p.rating, reviewCount: p.reviews || 1 } } : {}) } };
      }
      if (a === 'shop' && b) {
        const p = S.shop.getSync(b); if (!p) return {};
        const img = p.pod ? IMAGE : (p.images?.[0] ? SA.data.media.u(p.images[0], 1200, 1200) : IMAGE);
        return { desc: clip(`${p.name}${p.brand ? ` by ${p.brand}` : ''} — ${p.price} EGP on Super App. ${p.desc || ''}`), image: img,
          ld: { '@context': 'https://schema.org', '@type': 'Product', name: p.name, image: img, description: clip(p.desc || p.name, 300), ...(p.brand ? { brand: { '@type': 'Brand', name: p.brand } } : {}),
            offers: { '@type': 'Offer', priceCurrency: 'EGP', price: p.price, availability: (p.stock ?? 1) > 0 ? 'https://schema.org/InStock' : 'https://schema.org/OutOfStock', url: `${SITE}/shop/${p.id}` },
            ...(p.rating ? { aggregateRating: { '@type': 'AggregateRating', ratingValue: p.rating, reviewCount: p.reviewCount || p.reviews || 1 } } : {}) } };
      }
      if (a === 'tournaments' && b) {
        const t = S.tournaments.getSync(b); if (!t) return {};
        return { desc: clip(`${t.name}: ${t.format || ''} tournament${t.fee ? `, entry ${t.fee} EGP` : ''}. Register your team on Super App.`),
          ld: { '@context': 'https://schema.org', '@type': 'SportsEvent', name: t.name, sport: 'Football', url: `${SITE}/tournaments/${t.id}`, ...(t.startDate ? { startDate: t.startDate } : {}) } };
      }
      if (a === 'news' && b) {
        const n = (SA.data.news?.stories || []).find?.((x) => x.id === b);
        return n ? { desc: clip(n.summary || n.title), ld: { '@context': 'https://schema.org', '@type': 'NewsArticle', headline: n.title, url: `${SITE}/news/${n.id}` } } : {};
      }
      if (a === 's' && b) { const s = S.sellers?.byHandle?.(b); return s ? { desc: clip(`${s.name} on Super App — ${s.bio || 'original football designs, printed and delivered across Egypt.'}`) } : {}; }
      if (a === 'legal' && b) { const d = SA.data.legal.DOCS.find((x) => x.key === b); return d ? { desc: clip(d.summary.en) } : {}; }
      if (a === 'community' && b) { const c = (S.db.all('communities') || []).find((x) => x.slug === b); return c ? { desc: clip(c.about || c.name) } : {}; }
    } catch (e) { console.warn('seo', e); }
    return {};
  };

  const apply = () => {
    const path = SA.core.router.location();
    const clean = path.split('?')[0].replace(/\/$/, '') || '/';
    const d = describe(path);
    const desc = d.desc || PAGES[clean] || DEFAULT;
    const url = `${SITE}${clean === '/' ? '/' : clean}`;
    const title = document.title || 'Super App';
    meta('name', 'description', desc);
    meta('name', 'robots', PRIVATE.test(clean) ? 'noindex, nofollow' : 'index, follow');
    meta('property', 'og:title', title); meta('property', 'og:description', desc); meta('property', 'og:url', url); meta('property', 'og:image', d.image || IMAGE);
    meta('property', 'og:locale', SA.i18n?.isAr ? 'ar_EG' : 'en_US');
    meta('name', 'twitter:title', title); meta('name', 'twitter:description', desc); meta('name', 'twitter:image', d.image || IMAGE);
    link('canonical', url);
    ld(d.ld || (clean === '/' ? { '@context': 'https://schema.org', '@type': 'Organization', name: 'Super App', url: `${SITE}/`, logo: IMAGE, areaServed: 'EG' } : null));
  };
  let t = 0;
  SA.core.seo = { apply, SITE, describe };
  SA.core.router.onChange(() => { clearTimeout(t); t = setTimeout(apply, 450); });
})(window.SA);
