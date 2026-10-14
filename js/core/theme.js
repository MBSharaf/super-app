/**
 * Theme (light / dark / auto) and language (en / ar) preferences.
 * The <head> boot snippet in index.html applies both before first paint; this module changes them.
 */
(function (SA) {
  const KEY = 'superapp.theme';
  const mq = matchMedia('(prefers-color-scheme: dark)');
  const read = () => { try { return localStorage.getItem(KEY) || 'auto'; } catch { return 'auto'; } };
  const resolved = (pref = read()) => (pref === 'auto' ? (mq.matches ? 'dark' : 'light') : pref);
  const apply = (animate) => {
    const root = document.documentElement;
    if (animate) { root.classList.add('theme-anim'); setTimeout(() => root.classList.remove('theme-anim'), 450); }
    root.dataset.theme = resolved();
    document.querySelector('meta[name="theme-color"]')?.setAttribute('content', root.dataset.theme === 'dark' ? '#0A0F0D' : '#F1F2EE');
    document.querySelectorAll('[data-theme-label]').forEach((n) => (n.textContent = SA.i18n?.t(label()) || label()));
  };
  const label = () => ({ auto: 'Auto', light: 'Light', dark: 'Dark' }[read()]);
  mq.addEventListener?.('change', () => { if (read() === 'auto') apply(true); });

  SA.core.theme = {
    pref: read, current: () => resolved(), label,
    set(pref) { try { localStorage.setItem(KEY, pref); } catch { /* private mode */ } apply(true); },
    /** Header button: flip between light and dark (leaves "auto" once used) */
    toggle() { SA.core.theme.set(resolved() === 'dark' ? 'light' : 'dark'); return resolved(); },
    /** Footer/menu: cycle Auto → Light → Dark */
    cycle() { const order = ['auto', 'light', 'dark']; SA.core.theme.set(order[(order.indexOf(read()) + 1) % 3]); return read(); },
    apply,
  };
  apply(false);
})(window.SA);
