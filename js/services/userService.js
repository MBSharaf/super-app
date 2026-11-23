/** Local profile, notifications and wallet view. Future API: GET /me, GET /me/notifications */
(function (SA) {
  const store = SA.core.store;
  const { request } = SA.services.http;
  const { uid } = SA.core.utils;

  const userService = {
    /** First boot: copy the seed profile into the store. */
    ensureSeeded() {
      const s = store.get();
      // The server's site (real data): a blank profile the signed-in account fills, never the demo player's stats,
      // photo, wallet or points. A browser that still holds the demo player is reset once.
      if (SA.config.realData) {
        if (s.user && s.realProfile) return;
        store.set({ user: { id: s.user?.id || null, name: '', firstName: '', email: '', phone: '', avatar: null, avatarUrl: null, city: '', area: '', position: '', level: '', foot: '',
          memberSince: '', matchesPlayed: 0, goals: 0, rating: null, addresses: [] }, wallet: { balance: 0, transactions: [] }, savedCards: [], notifications: [], loyalty: { entries: [] }, realProfile: true }, 'seed');
        return;
      }
      if (s.user) { if (!s.loyalty) store.set({ loyalty: structuredClone(SA.data.user.loyalty) }, 'seed'); return; }
      const seed = structuredClone(SA.data.user);
      store.set({ user: seed.profile, wallet: seed.wallet, savedCards: seed.savedCards, notifications: seed.notifications, loyalty: seed.loyalty }, 'seed');
    },
    me: () => store.get().user,
    /** Customers the Super Malaab team put on hold can browse but not book, order or join */
    blocked: () => { const id = store.get().user?.id; return !!(id && SA.services.db?.raw().podCustomers?.[id]?.blocked); },
    assertActive() { if (userService.blocked()) throw new SA.services.http.ApiError('Your account is on hold, so you can’t book or order right now. Contact Super Malaab support from your profile.', 'blocked'); },
    update: (patch) => request(() => { store.set((s) => ({ user: { ...s.user, ...patch } })); return store.get().user; }),
    notify(n) {
      store.set((s) => ({ notifications: [{ id: uid('n'), at: Date.now(), read: false, ...n }, ...s.notifications].slice(0, 40) }), 'notify');
    },
    notifications: () => store.get().notifications,
    unreadCount: () => store.get().notifications.filter((n) => !n.read).length,
    markAllRead() { store.set((s) => ({ notifications: s.notifications.map((n) => ({ ...n, read: true })) })); },
    markRead(id) { store.set((s) => ({ notifications: s.notifications.map((n) => (n.id === id ? { ...n, read: true } : n)) })); },
    wallet: () => store.get().wallet,
    addAddress: (a) => request(() => {
      const addr = { id: uid('ad'), ...a };
      store.set((s) => ({ user: { ...s.user, addresses: [...s.user.addresses, addr] } }));
      return addr;
    }),
  };

  SA.services.user = userService;
})(window.SA);
