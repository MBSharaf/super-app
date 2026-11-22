/**
 * "Continue with Google / Facebook" in the browser (API mode only). The provider's own script runs the sign-in
 * and hands back a token; the token goes to the server, which checks it with Google or Facebook itself.
 * Nothing here decides who the person is.
 *   - Google: Google Identity Services draws its button; its callback gives an ID token.
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
    await load('https://accounts.google.com/gsi/client');
    window.google.accounts.id.initialize({ client_id: clientId, callback: (r) => r.credential && onToken(r.credential), ux_mode: 'popup', use_fedcm_for_prompt: true });
    window.google.accounts.id.renderButton(box, { type: 'standard', theme: 'outline', size: 'large', shape: 'pill', text: 'continue_with', logo_alignment: 'left', locale: lang(), width: Math.min(400, Math.max(200, box.clientWidth || 320)) });
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
