/**
 * Tiny observable store persisted to localStorage.
 * Shape intentionally mirrors what a Redux/Zustand store would hold in the React build.
 * Only services write to it; components read via SA.core.store.get() and subscribe().
 */
(function (SA) {
  const KEY = SA.config.storageKey;

  const initialState = () => ({
    version: 1,
    user: null,            // filled from SA.data.user on first boot
    location: null,        // { lat, lng, label, source: 'gps' | 'search' | 'fallback' }
    wallet: null,          // { balance, transactions: [] }
    savedCards: [],        // masked only: { id, brand, last4, exp }
    bookings: [],
    publishedMatches: [],  // matches the user created from a booking
    joinedMatchIds: [],
    matchRequests: {},     // matchId -> 'joined' | 'requested'
    cart: [],              // { key, productId, variant: { size, color }, qty }
    wishlist: [],
    orders: [],
    coupon: null,
    fantasy: { team: null, joinedLeagues: [], createdGroups: [] },      // Egyptian Premier League
    fantasyEpl: { team: null, joinedLeagues: [], createdGroups: [] },   // English Premier League
    fantasyLeague: 'egy',  // which league the fantasy pages show
    fantasyPass: null,     // { egy: endsAt, epl: endsAt } monthly Fantasy Pass for prize leagues, per league
    notifications: [],
    favoritesPitches: [],
    recentlyViewed: [],
    reminders: [],         // match ids the user wants a kick-off reminder for
    savedNews: [],
    loyalty: null,         // { entries: [] } Super Points bonuses and redemptions (earnings are derived)
    following: [],         // seller ids this shopper follows
    payments: [],          // every payment, refund and top-up — each one has a PDF receipt
    vouchers: [],          // Super Points vouchers: { code, amount, balance, uses: [] } — spent at checkout, never cash
  });

  let state;
  try { state = { ...initialState(), ...(JSON.parse(localStorage.getItem(KEY)) || {}) }; }
  catch { state = initialState(); }

  const subs = new Set();
  let saveTimer;
  const persist = () => {
    clearTimeout(saveTimer);
    saveTimer = setTimeout(() => { try { localStorage.setItem(KEY, JSON.stringify(state)); } catch { /* storage full or blocked */ } }, 60);
  };

  const store = {
    get: () => state,
    /** set(partial) or set(prev => partial) */
    set(patch, meta = '') {
      const next = typeof patch === 'function' ? patch(state) : patch;
      state = { ...state, ...next };
      persist();
      subs.forEach((fn) => { try { fn(state, meta); } catch (e) { console.error(e); } });
    },
    subscribe(fn) { subs.add(fn); return () => subs.delete(fn); },
    reset() { localStorage.removeItem(KEY); state = initialState(); SA.services.user.ensureSeeded(); SA.services.db?.reset(); subs.forEach((fn) => fn(state, 'reset')); },
  };

  SA.core.store = store;
})(window.SA);
