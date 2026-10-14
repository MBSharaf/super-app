/**
 * Site content managed from the admin console: home banners, the announcement bar, Super App news posts (plus hide /
 * breaking flags on feed stories) and hidden pickup games. The site reads it here; changes go through SA.services.admin.
 * Future API: GET /content/banners, GET /content/announcement, GET /news (merged), /admin/content/*
 */
(function (SA) {
  const db = () => SA.services.db;
  const { isoDate, today } = SA.core.utils;
  const settings = () => db().settings();

  const content = {
    /** Live home banners (max 3, in order, inside their dates) */
    banners() {
      const t = isoDate(today());
      return (db().all('banners') || []).filter((b) => b.active && (!b.from || b.from <= t) && (!b.to || b.to >= t)).sort((a, b) => a.order - b.order).slice(0, 3);
    },
    /** Announcement bar at the top of the site (null when switched off or expired) */
    announcement() {
      const a = settings().announcement;
      if (!a?.on || !a.text) return null;
      if (a.until && a.until < isoDate(today())) return null;
      return a;
    },
    dismissed: (a) => { try { return localStorage.getItem('superapp.announce.closed') === String(a.v); } catch { return false; } },
    dismiss: (a) => { try { localStorage.setItem('superapp.announce.closed', String(a.v)); } catch { /* ignore */ } },
    /** Feed stories + Super App posts, with the admin's hide / breaking flags applied */
    stories() {
      const edits = settings().newsEdits || {};
      const own = (db().all('newsPosts') || []).filter((p) => !p.deleted).map((p) => ({ ...p, own: true }));
      return [...own, ...SA.data.news.stories].map((s) => ({ ...s, ...(edits[s.id] || {}) })).filter((s) => !s.hidden);
    },
    allStories() {
      const edits = settings().newsEdits || {};
      const own = (db().all('newsPosts') || []).filter((p) => !p.deleted).map((p) => ({ ...p, own: true }));
      return [...own, ...SA.data.news.stories].map((s) => ({ ...s, ...(edits[s.id] || {}) })).sort((a, b) => b.at - a.at);
    },
    /** Pickup games hidden by the Super App team */
    matchHidden: (id) => !!(settings().hiddenMatches || {})[id],
  };
  SA.services.content = content;
})(window.SA);
