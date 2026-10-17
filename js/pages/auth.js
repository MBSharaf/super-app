/**
 * Real account pages (API mode): sign in (+ two-step code), sign up, forgot / reset password, confirm email,
 * accept a team invitation, and set up the authenticator app. In demo mode they explain that real accounts work
 * when Super App runs on its server.
 */
(function (SA) {
  const { esc } = SA.core.utils;
  const { icon } = SA.ui;
  const S = SA.services;
  const R = SA.core.router;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });

  /** Only addresses inside the site are allowed after sign-in (no open redirects) */
  const nextOf = (ctx, fallback = '/profile') => { const n = ctx.query.next || ''; return /^\/[a-z0-9/_-]*$/i.test(n) ? n : fallback; };
  const withNext = (path, ctx) => (ctx.query.next ? `${path}?next=${encodeURIComponent(ctx.query.next)}` : path);

  const field = (name, label, type = 'text', extra = '') => `<div class="field"><label for="au-${name}">${label}</label><input class="input" id="au-${name}" name="${name}" type="${type}" ${extra} /><p class="field-error" data-err="${name}"></p></div>`;
  const card = (title, sub, body, foot = '') => `<div class="container section-tight auth-page"><div class="panel panel-pad auth-card">
      <a class="auth-brand" href="#/"><span class="brand-mark"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2.6" stroke-linecap="round"><path d="M17 7.2C15.9 6 14.3 5.4 12.4 5.4c-2.7 0-4.5 1.4-4.5 3.4 0 4.6 9.2 2.6 9.2 7 0 2-1.9 3.4-4.8 3.4-2.1 0-3.9-.7-5.3-2"/></svg></span><b>SUPER APP</b></a>
      <h1 class="h2">${title}</h1>${sub ? `<p class="soft">${sub}</p>` : ''}${body}${foot ? `<div class="auth-foot small">${foot}</div>` : ''}</div></div>`;
  const demoOnly = () => card('Real accounts', 'Signing in with your own email and password works when Super App runs on its server. This demo site keeps a demo account on your device.',
    `<a class="btn btn-primary btn-lg auth-wide" href="#/profile">${icon('user', 18)} Open my demo profile</a>`);

  /** Shows the server's field errors under the fields (and the message as a toast) */
  const fail = (form, err) => {
    form.querySelectorAll('[data-err]').forEach((n) => (n.textContent = err.details?.[n.dataset.err] || ''));
    toast(err.message, 'error');
  };
  const busy = (form, on) => form.querySelectorAll('button[type="submit"]').forEach((b) => { b.disabled = on; b.classList.toggle('is-loading', on); });

  /** Submit handler with the busy state and error display shared by every form here */
  const onSubmit = (el, run) => {
    const form = el.querySelector('form'); if (!form) return;
    form.addEventListener('submit', async (e) => {
      e.preventDefault(); busy(form, true);
      try { await run(SA.core.utils.formData(form), form); } catch (err) { fail(form, err); } finally { busy(form, false); }
    });
  };

  /** Where to go after a successful sign-in: the authenticator setup first for team accounts */
  const afterSignIn = (me, ctx) => {
    if (me?.requires2faSetup) return R.go(withNext('/auth/2fa-setup', ctx), { replace: true });
    toast(`Welcome, ${esc(me.fullName.split(' ')[0])}`);
    R.go(nextOf(ctx), { replace: true });
  };

  const page = (title, render, mount) => ({
    title: `${title} — Super App`,
    async render(ctx) { await S.api.ready; return S.api.isLive() ? render(ctx) : demoOnly(); },
    mount(el, ctx) { if (S.api.isLive()) return mount?.(el, ctx); },
  });

  SA.pages.authSignIn = page('Sign in', (ctx) => card('Sign in', 'Use your email or mobile number.',
    `<form class="stack" style="--gap:12px" novalidate>${field('login', 'Email or mobile', 'text', 'autocomplete="username" dir="ltr"')}${field('password', 'Password', 'password', 'autocomplete="current-password" dir="ltr"')}
      <button class="btn btn-primary btn-lg auth-wide" type="submit">${icon('lock', 18)} Sign in</button></form>`,
    `<a class="link" href="#${withNext('/auth/forgot', ctx)}">Forgot your password?</a> · New here? <a class="link" href="#${withNext('/auth/sign-up', ctx)}">Create an account</a>`),
  (el, ctx) => onSubmit(el, async (d) => {
    const r = await S.session.signIn(d.login, d.password);
    if (r.requires2fa) return R.go(withNext('/auth/2fa', ctx), { replace: true });
    afterSignIn(r.me, ctx);
  }));

  SA.pages.authTwoFactor = page('Two-step sign-in', (ctx) => card('Enter your code', 'Open your authenticator app and type the 6-digit code for Super App.',
    `<form class="stack" style="--gap:12px" novalidate>${field('code', 'Code', 'text', 'inputmode="numeric" autocomplete="one-time-code" maxlength="20" dir="ltr"')}
      <label class="choice"><input type="checkbox" name="recovery" /><span class="radio-dot" style="border-radius:6px"></span><span class="grow small">I’m using one of my recovery codes</span></label>
      <button class="btn btn-primary btn-lg auth-wide" type="submit">${icon('shield', 18)} Continue</button></form>`,
    `<a class="link" href="#${withNext('/auth/sign-in', ctx)}">Back to sign in</a>`),
  (el, ctx) => onSubmit(el, async (d, form) => afterSignIn(await S.session.verify2fa(d.code, form.recovery.checked), ctx)));

  SA.pages.authSignUp = page('Create an account', (ctx) => card('Create your account', 'Book pitches, join games, shop and play fantasy with one account.',
    `<form class="stack" style="--gap:12px" novalidate>${field('fullName', 'Full name', 'text', 'autocomplete="name" maxlength="120"')}${field('email', 'Email', 'email', 'autocomplete="email" dir="ltr"')}
      ${field('phone', 'Mobile', 'tel', 'autocomplete="tel" inputmode="tel" dir="ltr" placeholder="010 1234 5678"')}${field('password', 'Password', 'password', 'autocomplete="new-password" dir="ltr"')}
      <p class="xs muted" style="margin-top:-6px">At least 8 characters, with a number.</p>
      <label class="choice"><input type="checkbox" name="acceptTerms" /><span class="radio-dot" style="border-radius:6px"></span><span class="grow small">I agree to the <a class="link" href="#/legal/terms" target="_blank" rel="noopener">terms of use</a> and the <a class="link" href="#/legal/privacy" target="_blank" rel="noopener">privacy policy</a></span></label><p class="field-error" data-err="acceptTerms"></p>
      <button class="btn btn-blaze btn-lg auth-wide" type="submit">${icon('sparkle', 18)} Create my account</button></form>`,
    `Already have an account? <a class="link" href="#${withNext('/auth/sign-in', ctx)}">Sign in</a>`),
  (el, ctx) => onSubmit(el, async (d, form) => {
    const me = await S.session.signUp({ fullName: d.fullName, email: d.email, phone: d.phone, password: d.password, acceptTerms: form.acceptTerms.checked, language: SA.i18n?.lang || 'ar' });
    toast('Your account is ready — check your email to confirm it');
    afterSignIn(me, ctx);
  }));

  SA.pages.authForgot = page('Forgot password', (ctx) => card('Forgot your password?', 'Enter your email and we’ll send you a link to choose a new one.',
    `<form class="stack" style="--gap:12px" novalidate>${field('email', 'Email', 'email', 'autocomplete="email" dir="ltr"')}<button class="btn btn-primary btn-lg auth-wide" type="submit">${icon('message', 18)} Send the link</button></form>`,
    `<a class="link" href="#${withNext('/auth/sign-in', ctx)}">Back to sign in</a>`),
  (el) => onSubmit(el, async (d, form) => {
    await S.session.forgot(d.email);
    form.outerHTML = `<div class="auth-done">${icon('message', 28)}<p>If <b>${esc(d.email)}</b> has an account, a link is on its way. It works for 30 minutes.</p></div>`;
  }));

  SA.pages.authReset = page('New password', () => card('Choose a new password', 'At least 8 characters, with a number.',
    `<form class="stack" style="--gap:12px" novalidate>${field('newPassword', 'New password', 'password', 'autocomplete="new-password" dir="ltr"')}<button class="btn btn-primary btn-lg auth-wide" type="submit">${icon('lock', 18)} Save my password</button></form>`),
  (el, ctx) => onSubmit(el, async (d) => {
    await S.session.reset(ctx.query.u, ctx.query.t, d.newPassword);
    toast('Password changed — sign in with the new one');
    R.go('/auth/sign-in', { replace: true });
  }));

  SA.pages.authInvite = page('Join the team', () => card('Welcome to the Super App team', 'Choose your password. Next you’ll sign in and set up your authenticator app.',
    `<form class="stack" style="--gap:12px" novalidate>${field('password', 'Password', 'password', 'autocomplete="new-password" dir="ltr"')}<button class="btn btn-primary btn-lg auth-wide" type="submit">${icon('check', 18)} Accept the invitation</button></form>`),
  (el, ctx) => onSubmit(el, async (d) => {
    await S.session.acceptInvite(ctx.query.u, ctx.query.t, d.password);
    toast('Password set — now sign in');
    R.go('/auth/sign-in?next=/admin', { replace: true });
  }));

  SA.pages.authConfirm = page('Confirm email', () => card('Confirming your email…', '', '<div class="auth-done" data-result><span class="spinner"></span></div>'),
    (el, ctx) => {
      const box = el.querySelector('[data-result]');
      S.session.confirm(ctx.query.u, ctx.query.t)
        .then(() => { box.innerHTML = `${icon('check', 28)}<p>Your email is confirmed. Thank you!</p><a class="btn btn-primary" href="#/profile">Go to my profile</a>`; S.session.load(); })
        .catch((err) => { box.innerHTML = `${icon('alert', 28)}<p>${esc(err.message)}</p><a class="btn btn-outline" href="#/profile">My profile</a>`; });
    });

  SA.pages.authTwoFactorSetup = page('Set up two-step sign-in', () => card('Set up two-step sign-in', 'Team accounts use an authenticator app (Google or Microsoft Authenticator) for every sign-in.',
    `<div data-setup class="stack" style="--gap:14px"><span class="spinner"></span></div>`),
  async (el, ctx) => {
    const box = el.querySelector('[data-setup]');
    try {
      const s = await S.session.setup2fa();
      box.innerHTML = `<ol class="small auth-steps"><li>Open the app and add an account.</li><li>Scan this code, or type the key.</li><li>Enter the 6-digit code it shows.</li></ol>
        <div class="auth-qr">${s.qrSvg}</div><p class="small center">Key: <b class="num" dir="ltr">${esc(s.sharedKey)}</b></p>
        <form class="stack" style="--gap:12px" novalidate>${field('code', '6-digit code', 'text', 'inputmode="numeric" autocomplete="one-time-code" maxlength="7" dir="ltr"')}<button class="btn btn-primary btn-lg auth-wide" type="submit">${icon('shield', 18)} Turn it on</button></form>`;
      onSubmit(box, async (d) => {
        const { recoveryCodes } = await S.session.enable2fa(d.code);
        box.innerHTML = `<div class="auth-done">${icon('check', 28)}<p><b>Two-step sign-in is on.</b> Keep these recovery codes somewhere safe — each one works once if you lose your phone.</p></div>
          <pre class="auth-codes num" dir="ltr">${recoveryCodes.map(esc).join('\n')}</pre><a class="btn btn-primary btn-lg auth-wide" href="#${nextOf(ctx)}">Continue</a>`;
      });
    } catch (err) { box.innerHTML = `<p class="small">${esc(err.message)}</p><a class="btn btn-outline" href="#/auth/sign-in">Sign in</a>`; }
  });

  /**
   * Team sign-in on the admin console and the Command Center (API mode): email + password, then the authenticator
   * code. A team account without an authenticator is sent to set it up first. `done(me)` opens the console.
   */
  SA.ui.teamSignIn = {
    html: (board) => `<form class="stack team-signin" style="--gap:10px;margin-top:22px" data-team-signin="${board}" novalidate>
        <h2 class="h4">Sign in with your team account</h2><p class="xs muted">Your email, password and the code from your authenticator app.</p>
        ${field('login', 'Email', 'email', 'autocomplete="username" dir="ltr"')}${field('password', 'Password', 'password', 'autocomplete="current-password" dir="ltr"')}
        <div data-step2 hidden>${field('code', '6-digit code', 'text', 'inputmode="numeric" autocomplete="one-time-code" maxlength="20" dir="ltr"')}</div>
        <button class="btn btn-primary btn-lg auth-wide" type="submit">${icon('lock', 18)} Sign in</button></form>`,
    /** Opens the console straight away when this browser already has a verified team session */
    ready: (board) => { const me = S.session.me(); return !!me && me.twoFactorEnabled && S.session.has(board); },
    mount(el, board, done) {
      const form = el.querySelector(`[data-team-signin="${board}"]`); if (!form) return;
      const finish = async (me) => {
        if (me.requires2faSetup) return R.go(`/auth/2fa-setup?next=/${board}`);
        if (!S.session.has(board)) { await S.session.signOut(); throw new SA.services.http.ApiError('This account can’t open this system.', 'forbidden'); }
        done(me);
      };
      form.addEventListener('submit', async (e) => {
        e.preventDefault(); busy(form, true);
        const d = SA.core.utils.formData(form);
        try {
          if (form.dataset.step === '2') return await finish(await S.session.verify2fa(d.code));
          const r = await S.session.signIn(d.login, d.password);
          if (!r.requires2fa) return await finish(r.me);
          form.dataset.step = '2';
          form.querySelector('[data-step2]').hidden = false;
          form.querySelector('[name="code"]').focus();
          toast('Enter the 6-digit code from your authenticator app.', 'info');
        } catch (err) { fail(form, err); } finally { busy(form, false); }
      });
    },
  };
})(window.SA);
