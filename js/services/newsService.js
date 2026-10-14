/** News. Future API: GET /news?category=, GET /news/:id — swap for a licensed news feed. */
(function (SA) {
  const store = SA.core.store;
  const { request, ApiError } = SA.services.http;
  const { seeded } = SA.core.utils;
  const N = () => SA.data.news;

  const decorate = (s) => ({ ...s, category: N().categories.find((c) => c.id === s.cat), saved: (store.get().savedNews || []).includes(s.id), views: Math.round(2000 + seeded(s.id) * 48000) });

  const newsService = {
    categories: () => N().categories,
    list: (f = {}) => request(() => {
      let out = N().stories.map(decorate);
      if (f.cat) out = out.filter((s) => s.cat === f.cat || s.clubs.includes(f.cat));
      if (f.type === 'video') out = out.filter((s) => s.video);
      return out.sort((a, b) => b.at - a.at);
    }),
    get: (id) => request(() => {
      const s = N().stories.find((x) => x.id === id);
      if (!s) throw new ApiError('This story is no longer available.', 'not_found');
      return decorate(s);
    }),
    related: (id, n = 3) => request(() => {
      const s = N().stories.find((x) => x.id === id);
      return N().stories.filter((x) => x.id !== id && (x.cat === s.cat || x.clubs.some((c) => s.clubs.includes(c)))).concat(N().stories.filter((x) => x.id !== id))
        .filter((x, i, arr) => arr.indexOf(x) === i).slice(0, n).map(decorate);
    }),
    mostRead: (n = 5) => N().stories.map(decorate).sort((a, b) => b.views - a.views).slice(0, n),
    breaking: () => N().stories.filter((s) => s.breaking || s.hoursAgo < 12).map(decorate),
    toggleSave(id) {
      const cur = store.get().savedNews || [];
      const on = !cur.includes(id);
      store.set({ savedNews: on ? [id, ...cur] : cur.filter((x) => x !== id) }, 'news');
      return on;
    },
  };

  SA.services.news = newsService;
})(window.SA);
