/**
 * Android app (Capacitor) glue. On the website this file does nothing.
 * In the app: the phone's back button goes back a page (or closes the app on the home page),
 * and the status bar follows the site's light/dark theme.
 */
(function (SA) {
  const cap = window.Capacitor;
  const isApp = !!(cap && cap.isNativePlatform && cap.isNativePlatform());
  SA.core.native = { isApp };
  if (!isApp) return;
  document.documentElement.dataset.native = cap.getPlatform();

  const { App, StatusBar } = cap.Plugins || {};

  if (App) {
    App.addListener('backButton', ({ canGoBack }) => {
      // An open modal or drawer closes first (same as its X button)
      const overlays = document.querySelectorAll('#overlay-root .overlay:not(.is-closing)');
      const top = overlays[overlays.length - 1];
      if (top) { top.querySelector('[data-close]')?.click(); return; }
      if (canGoBack && location.pathname !== '/') history.back();
      else App.exitApp();
    });
  }

  if (StatusBar) {
    const paint = () => {
      const dark = document.documentElement.getAttribute('data-theme') === 'dark';
      const color = getComputedStyle(document.documentElement).getPropertyValue('--paper').trim() || (dark ? '#0A0F0D' : '#F1F2EE');
      StatusBar.setOverlaysWebView({ overlay: false }).catch(() => {});
      StatusBar.setStyle({ style: dark ? 'DARK' : 'LIGHT' }).catch(() => {});
      StatusBar.setBackgroundColor({ color: /^#[0-9a-f]{6}$/i.test(color) ? color : (dark ? '#0A0F0D' : '#F1F2EE') }).catch(() => {});
    };
    paint();
    new MutationObserver(paint).observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
  }
})(window.SA);

/**
 * Push notifications in the Android app (Firebase). When someone is signed in, the app asks once for permission,
 * gets this phone's token and gives it to the server (POST /me/push-devices); the server then sends every
 * notification of the bell to the phone too. Signing out takes the phone off first. Tapping a notification opens its page.
 */
(function (SA) {
  const Push = SA.core.native.isApp ? window.Capacitor.Plugins?.PushNotifications : null;
  if (!Push) return;
  const t = (s) => SA.i18n?.t?.(s) || s;
  let token = null;
  let sentFor = null;   // the account the token was last given to

  const send = async () => {
    const me = SA.services.session?.me();
    if (!token || !me || sentFor === me.id) return;
    try { await SA.services.api.post('/me/push-devices', { token, platform: 'android' }); sentFor = me.id; } catch (e) { console.warn('push register', e); }
  };

  const start = async () => {
    try {
      let perm = await Push.checkPermissions();
      if (perm.receive === 'prompt' || perm.receive === 'prompt-with-rationale') perm = await Push.requestPermissions();
      if (perm.receive !== 'granted') return;
      await Push.createChannel({ id: 'default', name: t('Super Malaab notifications'), description: t('Bookings, games, orders and offers'), importance: 4, visibility: 1 }).catch(() => {});
      await Push.register();
    } catch (e) { console.warn('push start', e); }
  };

  Push.addListener('registration', ({ value }) => { token = value; sentFor = null; send(); });
  Push.addListener('registrationError', (e) => console.warn('push registration', e));
  Push.addListener('pushNotificationActionPerformed', ({ notification }) => {
    const link = notification?.data?.link;
    if (link && /^#?\//.test(link)) SA.core.router.go(link.startsWith('#') ? link : `#${link}`);
  });

  const wire = () => {
    const session = SA.services.session;
    if (!session) { setTimeout(wire, 300); return; }
    session.subscribe((me) => { if (me) (token ? send() : start()); });
    if (session.me()) start();
    // Before signing out, this phone stops getting the account's notifications
    const signOut = session.signOut;
    session.signOut = async (...args) => {
      if (token && sentFor) await SA.services.api.post('/me/push-devices/remove', { token }).catch(() => {});
      sentFor = null;
      return signOut(...args);
    };
  };
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', wire); else wire();
})(window.SA);
