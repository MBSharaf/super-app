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
