/**
 * "Continue with Google / Facebook" in the browser (API mode only). The provider's own script runs the sign-in
 * and hands back a token; the token goes to the server, which checks it with Google or Facebook itself.
 * Nothing here decides who the person is.
 *   - Google: Google Identity Services draws its button; its callback gives an ID token.
 *     In the Android app Google blocks that inside the app's web view, so the app asks the phone itself
 *     (Credential Manager, via the SocialLogin plugin) and gets the same kind of ID token for the same client id.
 *   - Facebook: the JavaScript SDK's login pop-up gives an access token (with the "email" permission).
 */
(function (SA) {
  const loaded = {};
  const load = (src) => (loaded[src] ||= new Promise((res, rej) => {
    const s = document.createElement('script');
    s.src = src; s.async = true; s.onload = res;
    s.onerror = () => { delete loaded[src]; rej(new Error('Could not reach the sign-in service. Check your connection and try again.')); };
    document.head.appendChild(s);
  }));

  const lang = () => (SA.i18n?.lang === 'en' ? 'en' : 'ar');

  /** Draws Google's button into `box`; `onToken(idToken)` runs after the person picks their Google account */
  const google = async (box, clientId, onToken) => {
    const Native = SA.core.native?.isApp ? window.Capacitor?.Plugins?.SocialLogin : null;
    if (Native) return nativeGoogle(Native, box, clientId, onToken);
    await load('https://accounts.google.com/gsi/client');
    window.google.accounts.id.initialize({ client_id: clientId, callback: (r) => r.credential && onToken(r.credential), ux_mode: 'popup', use_fedcm_for_prompt: true });
    window.google.accounts.id.renderButton(box, { type: 'standard', theme: 'outline', size: 'large', shape: 'pill', text: 'continue_with', logo_alignment: 'left', locale: lang(), width: Math.min(400, Math.max(200, box.clientWidth || 320)) });
  };

  const G_ICON = '<svg width="20" height="20" viewBox="0 0 48 48" aria-hidden="true"><path fill="#FFC107" d="M43.6 20.5H42V20H24v8h11.3C33.7 32.7 29.2 36 24 36c-6.6 0-12-5.4-12-12s5.4-12 12-12c3.1 0 5.8 1.2 7.9 3.1l5.7-5.7C34 6.1 29.3 4 24 4 12.9 4 4 12.9 4 24s8.9 20 20 20 20-8.9 20-20c0-1.3-.1-2.4-.4-3.5z"/><path fill="#FF3D00" d="m6.3 14.7 6.6 4.8C14.7 15.1 19 12 24 12c3.1 0 5.8 1.2 7.9 3.1l5.7-5.7C34 6.1 29.3 4 24 4 16.3 4 9.7 8.3 6.3 14.7z"/><path fill="#4CAF50" d="M24 44c5.2 0 9.9-2 13.4-5.2l-6.2-5.2C29.2 35.1 26.7 36 24 36c-5.2 0-9.6-3.3-11.3-8l-6.5 5C9.5 39.6 16.2 44 24 44z"/><path fill="#1976D2" d="M43.6 20.5H42V20H24v8h11.3c-.8 2.2-2.2 4.2-4.1 5.6l6.2 5.2C37 39.2 44 34 44 24c0-1.3-.1-2.4-.4-3.5z"/></svg>';
  let nativeReady = null;

  /** The app's own "Continue with Google" button: the phone shows its Google account picker and returns an ID token */
  const nativeGoogle = (Native, box, clientId, onToken) => {
    box.innerHTML = `<button class="btn btn-outline btn-lg auth-wide" type="button">${G_ICON} <span>${SA.i18n?.t?.('Continue with Google') || 'Continue with Google'}</span></button>`;
    const btn = box.firstElementChild;
    btn.addEventListener('click', async () => {
      btn.disabled = true;
      try {
        await (nativeReady ||= Native.initialize({ google: { webClientId: clientId, mode: 'online', skipAuthOnlyAuthorizationAfterSignIn: true } }).catch((e) => { nativeReady = null; throw e; }));
        // No scopes: email and profile come by default, and asking for any scope makes the plain app screen refuse
        const r = await Native.login({ provider: 'google', options: {} });
        const token = r?.result?.idToken;
        if (token) await onToken(token);
      } catch (err) {
        const why = String(err?.message || err || '').slice(0, 140);
        // Closing the account picker on purpose says nothing; anything else shows Google's reason too
        if (!/^(user )?cancel(l)?ed|USER_CANCELED|activity is cancelled by the user/i.test(why)) {
          // Google's long technical text becomes a short code the team can look up; the full text stays in the log
          const code = (why.match(/\[(\d+)\]/) || [])[1];
          const msg = SA.i18n?.t?.('Google sign-in didn’t work. Try again or use your email.') || 'Google sign-in didn’t work. Try again or use your email.';
          SA.ui.overlay.toast(code ? `${msg} (${SA.i18n?.t?.('code') || 'code'} ${code})` : `${msg} (${SA.core.utils.esc(why.slice(0, 80))})`, { type: 'error' });
        }
        console.warn('google native', err);
      } finally { btn.disabled = false; }
    });
  };

  /** Opens Facebook's login pop-up → the access token, or null when the person closed it */
  const facebook = async (appId) => {
    if (!window.FB) {
      await load(`https://connect.facebook.net/${lang() === 'ar' ? 'ar_AR' : 'en_US'}/sdk.js`);
      window.FB.init({ appId, version: 'v21.0', cookie: false, xfbml: false });
    }
    return new Promise((res) => window.FB.login((r) => res(r.authResponse?.accessToken || null), { scope: 'email', auth_type: 'rerequest' }));
  };

  SA.services.socialLogin = { google, facebook };
})(window.SA);
