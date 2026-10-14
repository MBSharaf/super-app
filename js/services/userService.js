/** Local profile, notifications and wallet view. Future API: GET /me, GET /me/notifications */
(function (SA) {
  const store = SA.core.store;
  const { request } = SA.services.http;
  const { uid } = SA.core.utils;

  const userService = {
    /** First boot: copy the seed profile into the store. */
    ensureSeeded() {
      const s = store.get();
      if (s.user) { if (!s.loyalty) store.set({ loyalty: structuredClone(SA.data.user.loyalty) }, 'seed'); return; }
      const seed = structuredClone(SA.data.user);
      store.set({ user: seed.profile, wallet: seed.wallet, savedCards: seed.savedCards, notifications: seed.notifications, loyalty: seed.loyalty }, 'seed');
    },
    me: () => store.get().user,
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
