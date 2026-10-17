/**
 * The real signed-in account (API mode only). In demo mode `me()` is always null and the site keeps its demo user.
 *
 * After a real sign-in the demo profile on this device takes the real name, email and mobile, so the parts of the
 * site that still run on demo data greet the right person.
 */
(function (SA) {
  const api = () => SA.services.api;
  const subs = new Set();
  let me = null;
  let loaded = false;

  const emit = () => subs.forEach((fn) => { try { fn(me); } catch (e) { console.error(e); } });

  /** Copies the real account onto the demo profile (name, email, mobile) */
  const syncProfile = () => {
    if (!me) return;
    SA.core.store.set((s) => ({ user: { ...s.user, name: me.fullName, email: me.email, phone: me.phone || s.user?.phone || '' } }), 'session');
  };

  const set = (next) => { me = next; loaded = true; syncProfile(); emit(); return me; };

  const session = {
    me: () => me,
    loaded: () => loaded,
    subscribe(fn) { subs.add(fn); return () => subs.delete(fn); },
    can: (permission) => !!me?.permissions?.includes(permission),
    has: (dashboard) => !!me?.dashboards?.includes(dashboard),

    /** Loads the current account (null when signed out) */
    async load() {
      await api().ready;
      if (!api().isLive()) { loaded = true; return null; }
      try { return set(await api().get('/me')); } catch { return set(null); }
    },

    /** → { signedIn, requires2fa, me } */
    async signIn(login, password) {
      const r = await api().post('/auth/sign-in', { login, password, remember: true });
      await api().refreshCsrf();
      if (r.signedIn) set(r.me);
      return r;
    },
    async verify2fa(code, isRecoveryCode = false) {
      const r = await api().post('/auth/2fa/verify', { code, isRecoveryCode, remember: true });
      await api().refreshCsrf();
      return set(r.me);
    },
    async signUp(data) {
      const r = await api().post('/auth/sign-up', data);
      await api().refreshCsrf();
      return set(r);
    },
    async signOut(everywhere = false) {
      await api().post(everywhere ? '/auth/sign-out-everywhere' : '/auth/sign-out');
      await api().refreshCsrf();
      set(null);
    },
    forgot: (email) => api().post('/auth/forgot-password', { email }),
    reset: (userId, token, newPassword) => api().post('/auth/reset-password', { userId, token, newPassword }),
    confirm: (userId, token) => api().post('/auth/confirm-email', { userId, token }),
    resendConfirmation: () => api().post('/auth/resend-confirmation'),
    acceptInvite: (userId, token, password) => api().post('/auth/accept-invite', { userId, token, password }),
    setup2fa: () => api().post('/auth/2fa/setup'),
    async enable2fa(code) {
      const r = await api().post('/auth/2fa/enable', { code });
      await api().refreshCsrf();
      await session.load();
      return r;
    },
  };

  SA.services.session = session;
  session.load();
})(window.SA);
