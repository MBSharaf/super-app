/**
 * Real backend client (ASP.NET Core API at /api/v1).
 *
 * The site works in two modes:
 *   - Demo mode (GitHub Pages, or any copy without the server): everything uses the local demo data, as before.
 *   - API mode (the site served by the Super App server): sign-in, sign-up, applications and approvals use the
 *     real API. Parts not built on the server yet keep using the demo data until their phase.
 *
 * `SA.services.api.ready` resolves once the mode is known. Every change (POST/PUT/PATCH/DELETE) sends the
 * anti-forgery token in X-CSRF-TOKEN; the session itself is a secure HttpOnly cookie the browser keeps.
 */
(function (SA) {
  const BASE = '/api/v1';
  const { ApiError } = SA.services.http;
  let mode = 'demo';
  let csrf = null;

  /** The server answers { success, message, data, errors, meta }; errors become { field: message } for the forms */
  const toError = (status, body) => {
    const errors = body?.errors || {};
    const details = Object.fromEntries(Object.entries(errors).filter(([k]) => k !== 'code').map(([k, v]) => [k, Array.isArray(v) ? v[0] : v]));
    const err = new ApiError(body?.message || 'Something went wrong. Please try again.', errors.code?.[0] || (status === 400 ? 'invalid' : 'error'), details);
    err.status = status;
    return err;
  };

  const refreshCsrf = async () => {
    const res = await fetch(`${BASE}/auth/csrf`, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
    if (!res.ok) throw new Error('csrf');
    csrf = (await res.json()).data.token;
    return csrf;
  };

  const call = async (method, path, body, { form = false, retry = true } = {}) => {
    const headers = { Accept: 'application/json', 'Accept-Language': SA.i18n?.lang || 'en' };
    if (method !== 'GET') headers['X-CSRF-TOKEN'] = csrf || await refreshCsrf();
    if (body !== undefined && !form) headers['Content-Type'] = 'application/json';
    const res = await fetch(`${BASE}${path}`, { method, credentials: 'same-origin', headers, body: body === undefined ? undefined : form ? body : JSON.stringify(body) });
    const text = await res.text();
    const json = text ? JSON.parse(text) : null;
    // The anti-forgery token is tied to the signed-in person: after a sign-in elsewhere it is refreshed once
    if (res.status === 400 && json?.errors?.code?.[0] === 'csrf' && retry) { await refreshCsrf(); return call(method, path, body, { form, retry: false }); }
    if (!res.ok || json?.success === false) throw toError(res.status, json);
    return json ? (json.meta ? { items: json.data, meta: json.meta, message: json.message } : json.data) : null;
  };

  /** API mode only when this page is served by the Super App server (it answers the CSRF endpoint) */
  const detect = async () => {
    if (location.protocol === 'file:' || /github\.io$/.test(location.hostname)) return 'demo';
    try { await refreshCsrf(); return 'api'; } catch { return 'demo'; }
  };

  const api = {
    BASE,
    ready: detect().then((m) => { mode = m; document.documentElement.dataset.backend = m; return m; }),
    isLive: () => mode === 'api',
    refreshCsrf,
    get: (path) => call('GET', path),
    post: (path, body = {}) => call('POST', path, body),
    put: (path, body = {}) => call('PUT', path, body),
    patch: (path, body = {}) => call('PATCH', path, body),
    del: (path) => call('DELETE', path),
    /** multipart/form-data (file uploads): pass a FormData */
    form: (path, formData, method = 'POST') => call(method, path, formData, { form: true }),
    /** A data: URL (photos the site already reads) → a Blob to upload (decoded here: the security policy blocks fetch on data: URLs) */
    blob: (dataUrl) => {
      const [head, b64] = String(dataUrl).split(',');
      const bin = atob(b64 || '');
      const bytes = Uint8Array.from(bin, (ch) => ch.charCodeAt(0));
      return new Blob([bytes], { type: /data:([^;,]+)/.exec(head)?.[1] || 'application/octet-stream' });
    },
  };

  SA.services.api = api;
})(window.SA);
