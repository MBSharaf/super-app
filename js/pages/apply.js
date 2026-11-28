/**
 * Grow with Super Malaab: apply to list your pitch (#/partner/apply), apply to sell from the pitch partner dashboard
 * (#/dash/sell), and the profile cards that open the right dashboard (or show the application's status).
 */
(function (SA) {
  const { esc, money } = SA.core.utils;
  const { icon } = SA.ui;
  const c = SA.ui.c;
  const S = SA.services;
  const toast = (m, type = 'success') => SA.ui.overlay.toast(m, { type });
  const showErrors = (root, errs = {}) => {
    root.querySelectorAll('[data-err]').forEach((n) => (n.textContent = errs[n.dataset.err] || ''));
    root.querySelector(`[data-err="${Object.keys(errs)[0]}"]`)?.closest('.field, fieldset, section')?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  };
  /** ID / pitch photos are made small before they are kept */
  const readPhoto = (file, max = 900) => SA.pages.dashCommon.readImage(file, max, 0.72);
  const field = (name, label, input, hint = '') => `<div class="field"><label for="ap-${name}">${label}</label>${input}${hint ? `<span class="field-hint">${hint}</span>` : ''}<p class="field-error" data-err="${name}"></p></div>`;
  const idBlock = (prefix = '') => `
      ${field('nationalId', 'National ID number', `<input class="input num" id="ap-nationalId" name="nationalId" inputmode="numeric" maxlength="14" autocomplete="off" dir="ltr" placeholder="14 digits" />`, 'Only the last 4 digits are kept on Super Malaab.')}
      <div class="field"><span class="field-label">Photo of your ID (front)</span><label class="ap-photo" data-pick="idPhoto${prefix}"><input type="file" accept="image/*" hidden /><span data-preview>${icon('camera', 22)}<span class="small">Add a photo</span></span></label><p class="field-error" data-err="idPhoto"></p></div>
      <div class="field"><span class="field-label">Photo of your ID (back)</span><label class="ap-photo" data-pick="idPhotoBack${prefix}"><input type="file" accept="image/*" hidden /><span data-preview>${icon('camera', 22)}<span class="small">Add a photo</span></span></label><p class="field-error" data-err="idPhotoBack"></p></div>`;
  const bindPhoto = (el, key, onPick) => el.querySelectorAll(`[data-pick="${key}"]`).forEach((lbl) => lbl.querySelector('input').addEventListener('change', async (e) => {
    try { const url = await readPhoto(e.target.files[0]); lbl.querySelector('[data-preview]').innerHTML = `<img src="${url}" alt="" />`; onPick(url); } catch (err) { toast(err.message, 'error'); }
  }));

  /* ======================= API mode (the site served by the Super Malaab server) ======================= */
  const live = () => S.api?.isLive();
  /** Server field names → the names used by these forms */
  const FIELD = { areaId: 'area', lng: 'lat', acceptTerms: 'agree', idFront: 'idPhoto', idBack: 'idPhotoBack', storeName: 'name', bio: 'bio', payoutDetail: 'instapay' };
  const apiErrors = (details = {}) => Object.fromEntries(Object.entries(details).map(([k, v]) => [FIELD[k] || k, v]));
  const signInFirst = (title) => `<div class="container section-tight ap-page"><div class="page-head"><a class="back-link" href="#/profile">${icon('chevronLeft', 16)} Profile</a><h1 class="h1" style="margin-top:10px">${title}</h1></div>
    <section class="panel panel-pad acc-real">${icon('lock', 26)}<div class="grow"><h2 class="h4">Sign in to apply</h2><p class="small soft">Applications are linked to your Super Malaab account.</p></div><a class="btn btn-primary" href="#/auth/sign-in?next=${encodeURIComponent(SA.core.router.location())}">${icon('lock', 16)} Sign in</a></section></div>`;
  /** The latest application of one type from the server, in the shape the status card reads */
  const latestLive = async (type) => {
    const a = (await S.api.get('/applications/mine')).find((x) => x.type === type);
    return a && { status: a.status.toLowerCase(), kind: type === 'Seller' ? 'seller' : 'partner', pitchName: a.answers.pitchName || a.answers.storeName, area: '', reason: a.rejectReason, code: a.code };
  };
  /** Adds the photos the form read (data: URLs) to the upload */
  const addPhoto = async (form, field, dataUrl, name) => { if (dataUrl) form.append(field, await S.api.blob(dataUrl), name); };
  const PAYOUT = { 'Bank transfer': 'Bank', InstaPay: 'InstaPay' };

  /* ---------- Shared by the pitch and seller applications: payout fields + checks before sending ---------- */
  /** "How we pay you": InstaPay needs one field; a bank transfer needs the bank, the account holder and the IBAN */
  const payoutFields = (methods = ['InstaPay', 'Bank transfer']) => `
      ${field('payoutMethod', 'Method', `<select class="select" id="ap-payoutMethod" name="payoutMethod" data-payout>${methods.map((p) => `<option value="${p}">${p}</option>`).join('')}</select>`)}
      <div data-pay="InstaPay">${field('instapay', 'InstaPay address or mobile', '<input class="input" id="ap-instapay" name="instapay" maxlength="60" dir="ltr" placeholder="name@instapay or 01xxxxxxxxx" />', 'The address or number your InstaPay account uses.')}</div>
      <div data-pay="Bank transfer" hidden>${field('bankName', 'Bank', '<input class="input" id="ap-bankName" name="bankName" maxlength="40" placeholder="e.g. Banque Misr" />')}</div>
      <div data-pay="Bank transfer" hidden>${field('holder', 'Account holder name', '<input class="input" id="ap-holder" name="holder" maxlength="60" placeholder="As written at the bank" />')}</div>
      <div class="span-2" data-pay="Bank transfer" hidden>${field('iban', 'IBAN or account number', '<input class="input num" id="ap-iban" name="iban" maxlength="34" dir="ltr" placeholder="EG00 0000 0000 0000 0000 0000 0000 0" />', 'The IBAN is on your bank app or statement (starts with EG, 29 characters).')}</div>`;
  /** Shows the right payout fields for the chosen method */
  const bindPayout = (root) => {
    const sel = root.querySelector('[data-payout]'); if (!sel) return;
    const sync = () => root.querySelectorAll('[data-pay]').forEach((n) => (n.hidden = n.dataset.pay !== sel.value));
    sel.addEventListener('change', sync); sync();
  };
  const clean = (v) => String(v || '').replace(/\s+/g, ' ').trim();
  /** The payout answer the server keeps: one line, with every part the team needs to send the money */
  const payoutDetail = (fd) => (fd.payoutMethod === 'Bank transfer'
    ? `Bank: ${clean(fd.bankName)} | Holder: ${clean(fd.holder)} | IBAN: ${clean(fd.iban).replace(/\s/g, '').toUpperCase()}`
    : clean(fd.instapay));
  const payoutErrors = (fd, e) => {
    if (fd.payoutMethod === 'Bank transfer') {
      if (clean(fd.bankName).length < 2) e.bankName = 'Write the bank name.';
      if (clean(fd.holder).split(' ').length < 2) e.holder = 'The account holder’s full name.';
      const iban = clean(fd.iban).replace(/\s/g, '').toUpperCase();
      if (!/^EG\d{27}$/.test(iban) && !/^\d{8,20}$/.test(iban)) e.iban = 'An IBAN (EG + 27 digits) or an account number (8–20 digits).';
    } else if (!/^(01[0125]\d{8}|[\w.-]{2,40}@instapay)$/i.test(clean(fd.instapay).replace(/\s/g, ''))) e.instapay = 'Your InstaPay address (name@instapay) or the mobile number on it.';
  };
  /** 14 digits, starts with 2 or 3 (as the server checks), and a real month and day of birth inside */
  const nationalIdOk = (v) => /^[23]\d{13}$/.test(v) && +v.slice(3, 5) >= 1 && +v.slice(3, 5) <= 12 && +v.slice(5, 7) >= 1 && +v.slice(5, 7) <= 31;
  const mobileOk = (v) => /^(\+?20|0)?1[0125]\d{8}$/.test(String(v || '').replace(/[\s-]/g, ''));
  /** Checks shared by every application: who you are, your ID photos and the agreement */
  const personErrors = (fd, data, nameKey, e) => {
    const name = clean(fd[nameKey]);
    if (name.split(' ').length < 2 || name.length > 120 || /[<>]/.test(name)) e[nameKey] = 'Your full name, as on your ID.';
    if (!mobileOk(fd.phone)) e.phone = 'A mobile number that starts with 010, 011, 012 or 015.';
    if (!nationalIdOk(clean(fd.nationalId))) e.nationalId = 'The national ID number is 14 digits and starts with 2 or 3.';
    if (!data.idPhoto) e.idPhoto = 'Add a photo of the front of your ID.';
    if (!data.idPhotoBack) e.idPhotoBack = 'Add a photo of the back of your ID.';
  };
  /** Shows every problem at once (in the person's language) and stops the send */
  const stopIf = (form, e) => {
    if (!Object.keys(e).length) return false;
    showErrors(form, e);
    toast(`Fix ${Object.keys(e).length} highlighted ${Object.keys(e).length === 1 ? 'field' : 'fields'} and send again.`, 'error');
    return true;
  };

  /** Areas grouped by governorate for the area picker ("Other area" last in each governorate) */
  const areaOptions = async () => {
    const ar = !!SA.i18n?.isAr;
    if (!live()) return [...new Set(S.db.all('pitches').map((p) => p.area))].sort().map((n) => `<option value="${esc(n)}">${esc(n)}</option>`).join('');
    const [govs, areas] = await Promise.all([S.api.get('/lookups/governorates'), S.api.get('/lookups/areas')]);
    const other = (a) => /^Other area/.test(a.nameEn);
    const name = (x) => (ar ? x.nameAr : x.nameEn);
    return govs.map((g) => {
      const list = areas.filter((a) => a.governorateId === g.id).sort((a, b) => other(a) - other(b) || name(a).localeCompare(name(b), ar ? 'ar' : 'en'));
      return list.length ? `<optgroup label="${esc(name(g))}">${list.map((a) => `<option value="${a.id}" data-lat="${a.lat ?? ''}" data-lng="${a.lng ?? ''}">${esc(name(a))}</option>`).join('')}</optgroup>` : '';
    }).join('');
  };
  /** Reads a place from a Google Maps link or "lat, lng" text; null when there is none */
  const coordsFrom = (text) => {
    const s = decodeURIComponent(String(text || ''));
    const m = s.match(/@(-?\d{1,2}\.\d+),(-?\d{1,3}\.\d+)/) || s.match(/[?&](?:q|query|ll|destination)=(-?\d{1,2}\.\d+),\s*(-?\d{1,3}\.\d+)/) || s.match(/!3d(-?\d{1,2}\.\d+)!4d(-?\d{1,3}\.\d+)/) || s.match(/^\s*(-?\d{1,2}\.\d+)\s*,\s*(-?\d{1,3}\.\d+)\s*$/);
    return m ? { lat: +m[1], lng: +m[2] } : null;
  };
  const inEgypt = (p) => p && p.lat >= 22 && p.lat <= 32 && p.lng >= 24 && p.lng <= 37;

  const pitchErrors = (fd, f, data) => {
    const e = {};
    personErrors(fd, data, 'ownerName', e);
    const pitchName = clean(fd.pitchName);
    if (pitchName.length < 3 || pitchName.length > 120) e.pitchName = 'The pitch name players will see (3 letters or more).';
    if (!fd.area) e.area = 'Choose the area from the list.';
    if (clean(fd.address).length < 6) e.address = 'Write the street and a landmark.';
    if (!inEgypt(data)) e.lat = 'Pin the pitch on the map, or paste its Google Maps link.';
    const count = +fd.count;
    if (!(Number.isInteger(count) && count >= 1 && count <= 20)) e.count = 'From 1 to 20 pitches.';
    const price = +fd.price;
    if (!(price >= 100 && price <= 5000)) e.price = 'Between 100 and 5,000 EGP an hour.';
    if (!f.querySelector('[name="formats"]:checked')) e.formats = 'Choose at least one pitch type.';
    if (!data.photos.filter(Boolean).length) e.photos = 'Add at least one photo of the pitch.';
    payoutErrors(fd, e);
    if (!f.agree.checked) e.agree = 'Tick the box to accept the agreement.';
    return e;
  };

  const applyPitchLive = async (fd, f, data) => {
    const answers = {
      ownerName: clean(fd.ownerName), phone: clean(fd.phone), nationalId: clean(fd.nationalId), pitchName: clean(fd.pitchName), areaId: +fd.area || 0, address: clean(fd.address),
      lat: data.lat, lng: data.lng, count: +fd.count, formats: [...f.querySelectorAll('[name="formats"]:checked')].map((x) => (x.value === 'Padel court' ? 2 : parseInt(x.value, 10))),   // 2 = padel (2 vs 2)
      price: +fd.price, payoutMethod: PAYOUT[fd.payoutMethod] || fd.payoutMethod, payoutDetail: payoutDetail(fd), acceptTerms: f.agree.checked, note: clean(fd.note) || null,
    };
    const form = new FormData();
    form.append('answers', JSON.stringify(answers));
    await addPhoto(form, 'idFront', data.idPhoto, 'id-front.jpg');
    await addPhoto(form, 'idBack', data.idPhotoBack, 'id-back.jpg');
    for (const [i, p] of data.photos.filter(Boolean).entries()) await addPhoto(form, 'photos', p, `pitch-${i + 1}.jpg`);
    // A rejected application is fixed and sent again; otherwise it is a new one
    return f.dataset.code ? S.api.form(`/applications/${f.dataset.code}`, form, 'PUT') : S.api.form('/applications/pitch-owner', form);
  };

  /* ======================= Pitch partner application ======================= */
  const statusCard = (a) => {
    if (a.status === 'pending') return `<section class="panel panel-pad ap-status">${icon('clock', 28)}<div><h2 class="h3">We’re reviewing your application</h2><p class="soft">“${esc(a.pitchName)}”${a.area ? ` in ${esc(a.area)}` : ''} — the Super Malaab team replies within 2 working days. You’ll get a notification.</p></div></section>`;
    if (a.status === 'approved' && a.kind === 'seller') return `<section class="panel panel-pad ap-status is-ok">${icon('check', 28)}<div><h2 class="h3">You’re a Super Malaab seller</h2><p class="soft">“${esc(a.pitchName)}” is open. Add designs and products from your seller dashboard.</p><a class="btn btn-primary" href="#/seller" style="margin-top:12px">${icon('bag', 16)} Open my seller dashboard</a></div></section>`;
    if (a.status === 'approved') return `<section class="panel panel-pad ap-status is-ok">${icon('check', 28)}<div><h2 class="h3">You’re a Super Malaab pitch partner</h2><p class="soft">“${esc(a.pitchName)}” is live. Manage bookings, hours and money from your dashboard.</p><button class="btn btn-primary" data-open-partner style="margin-top:12px">${icon('pitch', 16)} Open my pitch dashboard</button></div></section>`;
    return `<section class="panel panel-pad ap-status is-bad">${icon('alert', 28)}<div><h2 class="h3">Your application wasn’t approved</h2><p class="soft">${esc(a.reason || '')}</p><p class="small">Fix it below and apply again.</p></div></section>`;
  };
  SA.pages.partnerApply = {
    title: 'List your pitch — Super Malaab',
    async render() {
      await S.api.ready;
      if (live() && !S.session.loaded()) await S.session.load();
      if (live() && !S.session.me()) return signInFirst('List your pitch');
      const a = live() ? await latestLive('PitchOwner') : S.partners.mine();
      const u = S.user.me();
      if (a && a.status !== 'rejected') return `<div class="container section-tight ap-page"><div class="page-head"><a class="back-link" href="#/profile">${icon('chevronLeft', 16)} Profile</a><h1 class="h1" style="margin-top:10px">List your pitch</h1></div>${statusCard(a)}</div>`;
      const areas = await areaOptions();
      return `<div class="container section-tight ap-page">
        <div class="page-head"><a class="back-link" href="#/profile">${icon('chevronLeft', 16)} Profile</a><h1 class="h1" style="margin-top:10px">List your pitch on Super Malaab</h1>
          <p class="lead">Players book and pay online, you get paid every week. Tell us about you and your pitch — we review it within 2 working days.</p></div>
        ${a ? statusCard(a) : ''}
        <form class="stack ap-form" style="--gap:18px" data-apply ${a?.status === 'rejected' && a.code ? `data-code="${esc(a.code)}"` : ''} novalidate>
          <section class="panel panel-pad"><h2 class="h4">1. About you</h2><div class="form-grid" style="margin-top:12px">
            ${field('ownerName', 'Full name (as on your ID)', `<input class="input" id="ap-ownerName" name="ownerName" value="${esc(u.name || '')}" maxlength="120" autocomplete="name" />`)}
            ${field('phone', 'Mobile', `<input class="input num" id="ap-phone" name="phone" value="${esc(u.phone || '')}" inputmode="tel" maxlength="16" dir="ltr" placeholder="01xxxxxxxxx" autocomplete="tel" />`)}
            ${idBlock()}
          </div></section>
          <section class="panel panel-pad"><h2 class="h4">2. Your pitch</h2><div class="form-grid" style="margin-top:12px">
            ${field('pitchName', 'Pitch name', '<input class="input" id="ap-pitchName" name="pitchName" maxlength="120" placeholder="e.g. Nasr City Arena" />')}
            ${field('area', 'Area', `<select class="select" id="ap-area" name="area" data-area><option value="">Choose the area</option>${areas}</select>`, 'Not in the list? Choose “Other area” in your governorate and write it in the address.')}
            <div class="field span-2"><label for="ap-address">Address</label><input class="input" id="ap-address" name="address" maxlength="300" placeholder="Street, building, landmark" /><p class="field-error" data-err="address"></p></div>
            <div class="field span-2"><span class="field-label">Location on the map <span class="xs muted" data-coords>choose the area, then tap the pitch on the map</span></span>
              <div class="ap-map" data-map></div>
              <div class="row wrap" style="--gap:8px;margin-top:8px"><button class="btn btn-outline btn-sm" type="button" data-locate>${icon('pin', 14)} Use my location</button></div>
              <input class="input" name="mapLink" data-maplink dir="ltr" style="margin-top:8px" placeholder="Or paste the pitch’s Google Maps link" />
              <p class="field-error" data-err="lat"></p></div>
            ${field('count', 'Number of pitches', '<input class="input num" id="ap-count" name="count" type="number" inputmode="numeric" min="1" max="20" value="1" />')}
            ${field('price', 'Starting price per hour (EGP)', '<input class="input num" id="ap-price" name="price" type="number" inputmode="numeric" min="100" max="5000" value="600" />', 'One starting price. You set a price for each pitch, padel court and peak hour from your dashboard after approval.')}
            <fieldset class="field span-2"><legend class="field-label">Pitch types (football and padel)</legend><div class="chips">${S.partners.FORMATS.map((ft, i) => `<label class="chip chip-check"><input type="checkbox" name="formats" value="${ft}" ${i === 0 ? 'checked' : ''} /> ${ft}</label>`).join('')}</div><p class="field-error" data-err="formats"></p></fieldset>
            <div class="field span-2"><span class="field-label">Photos of the pitch (1 to 4)</span><div class="ap-photos">${[0, 1, 2, 3].map((i) => `<label class="ap-photo" data-pick="photo${i}"><input type="file" accept="image/*" hidden /><span data-preview>${icon('image', 20)}</span></label>`).join('')}</div><p class="field-error" data-err="photos"></p></div>
            <div class="field span-2"><label for="ap-note">Anything else? <span class="muted">(optional)</span></label><textarea class="input" id="ap-note" name="note" rows="2" maxlength="400" placeholder="${esc(SA.i18n?.t?.('Facilities, parking, opening hours…') || 'Facilities, parking, opening hours…')}"></textarea></div>
          </div></section>
          <section class="panel panel-pad"><h2 class="h4">3. How we pay you</h2><div class="form-grid" style="margin-top:12px">${payoutFields(['InstaPay', 'Bank transfer'])}</div>
            <p class="xs muted" style="margin-top:8px">${icon('info', 12)} Super Malaab takes 10% of each booking. The rest is sent to you every week.</p></section>
          <section class="panel panel-pad"><label class="choice"><input type="checkbox" name="agree" /><span class="radio-dot" style="border-radius:6px"></span><span class="grow"><b>I agree to the Super Malaab <a class="link" href="#/legal/partner" target="_blank" rel="noopener">pitch partner agreement</a></b><span class="xs muted" style="display:block">Accurate prices and hours, honour every booking, and Super Malaab’s cancellation and refund rules.</span></span></label><p class="field-error" data-err="agree"></p></section>
          <button class="btn btn-blaze btn-lg" type="submit">${icon('pitch', 18)} Send my application</button>
        </form></div>`;
    },
    mount(el) {
      el.querySelector('[data-open-partner]')?.addEventListener('click', () => { S.partners.openDashboard(); SA.core.router.go('/dash'); });
      const f = el.querySelector('[data-apply]'); if (!f) return;
      const data = { idPhoto: '', idPhotoBack: '', photos: [], lat: 0, lng: 0 };
      bindPhoto(el, 'idPhoto', (u) => (data.idPhoto = u));
      bindPhoto(el, 'idPhotoBack', (u) => (data.idPhotoBack = u));
      [0, 1, 2, 3].forEach((i) => bindPhoto(el, `photo${i}`, (u) => (data.photos[i] = u)));
      bindPayout(f);
      // Pin the pitch: tap the map, use the phone's location, or paste a Google Maps link
      let map = null, pin = null;
      const coords = el.querySelector('[data-coords]');
      const place = (p, zoom = 16) => {
        data.lat = +p.lat.toFixed(6); data.lng = +p.lng.toFixed(6);
        coords.textContent = `${data.lat.toFixed(5)}, ${data.lng.toFixed(5)} ✓`;
        f.querySelector('[data-err="lat"]').textContent = '';
        if (!map) return;
        if (pin) pin.setLatLng([data.lat, data.lng]); else pin = window.L.marker([data.lat, data.lng]).addTo(map.raw);
        map.flyTo({ lat: data.lat, lng: data.lng }, zoom);
      };
      SA.ui.map.ready().then((ok) => {
        const box = el.querySelector('[data-map]'); if (!ok || !box) { if (box) box.hidden = true; return; }
        map = SA.ui.map.create(box, { center: { lat: 30.0444, lng: 31.2357 }, zoom: 11 });
        map?.raw.on('click', (e) => place(e.latlng, map.raw.getZoom()));
      });
      f.querySelector('[data-area]').addEventListener('change', (e) => {
        const o = e.target.selectedOptions[0];
        if (map && !data.lat && o?.dataset.lat) map.flyTo({ lat: +o.dataset.lat, lng: +o.dataset.lng }, 15);
      });
      f.querySelector('[data-locate]').addEventListener('click', () => {
        if (!navigator.geolocation) return toast('Your device can’t share its location here. Tap the map or paste a Google Maps link.', 'error');
        navigator.geolocation.getCurrentPosition((p) => place({ lat: p.coords.latitude, lng: p.coords.longitude }),
          () => toast('Location is off or not allowed. Tap the map or paste a Google Maps link.', 'error'), { enableHighAccuracy: true, timeout: 10000 });
      });
      f.querySelector('[data-maplink]').addEventListener('input', (e) => {
        const p = coordsFrom(e.target.value);
        if (p && inEgypt(p)) place(p);
        else if (e.target.value.trim()) f.querySelector('[data-err="lat"]').textContent = SA.i18n?.t?.('Open the place in Google Maps, tap Share, copy the link and paste it here.') || 'Open the place in Google Maps, tap Share, copy the link and paste it here.';
      });
      f.addEventListener('submit', async (e) => {
        e.preventDefault();
        const fd = SA.core.utils.formData(f);
        if (stopIf(f, pitchErrors(fd, f, data))) return;
        showErrors(f, {});
        const btn = f.querySelector('[type="submit"]'); btn.disabled = true;
        try {
          if (live()) await applyPitchLive(fd, f, data);
          else {
            const areaName = f.querySelector('[data-area]').selectedOptions[0]?.textContent || fd.area;
            S.partners.apply({ ...fd, area: areaName, email: S.user.me().email, payoutDetail: payoutDetail(fd), formats: [...f.querySelectorAll('[name="formats"]:checked')].map((x) => x.value), agree: f.agree.checked, idPhoto: data.idPhoto, photos: data.photos.filter(Boolean), lat: data.lat, lng: data.lng });
          }
          toast('Application sent — we’ll review it within 2 working days');
          SA.core.motion.confetti({ count: 60 }); SA.app.rerender(); scrollTo(0, 0);
        } catch (err) { showErrors(f, live() ? apiErrors(err.details) : err.details || {}); toast(err.message, 'error'); } finally { btn.disabled = false; }
      });
      return () => map?.destroy();
    },
  };
  /* ======================= Seller application (shared by #/sell and #/dash/sell) ======================= */
  /** The "About you" part of the seller application: identity + contact */
  SA.ui.sellerIdFields = (u = {}, title = 'About you') => `<section class="panel panel-pad"><h2 class="h4">${title}</h2><div class="form-grid" style="margin-top:12px">
      ${field('fullName', 'Full name (as on your ID)', `<input class="input" id="ap-fullName" name="fullName" value="${esc(u.name || '')}" maxlength="120" autocomplete="name" />`)}
      ${field('phone', 'Mobile', `<input class="input num" id="ap-phone" name="phone" value="${esc(u.phone || '')}" inputmode="tel" maxlength="16" dir="ltr" placeholder="01xxxxxxxxx" autocomplete="tel" />`)}
      ${idBlock()}
    </div></section>`;
  SA.ui.bindSellerId = (el, data) => { bindPhoto(el, 'idPhoto', (u) => (data.idPhoto = u)); bindPhoto(el, 'idPhotoBack', (u) => (data.idPhotoBack = u)); };
  /** API-mode helpers shared with the seller application (#/sell) */
  SA.ui.applyLive = { live, signInFirst, latestLive, apiErrors, addPhoto, PAYOUT, statusCard, payoutFields, bindPayout, payoutDetail, payoutErrors, personErrors, stopIf, clean };
  SA.ui.showApplyErrors = showErrors;

  /** Pitch partner → apply to sell, from inside their dashboard */
  const C = () => SA.pages.dashCommon;
  const D = () => S.dash;
  SA.pages.dashSell = SA.pages.dashCommon.guard({
    roles: ['owner'],
    title: 'Sell on Super Malaab — Super Malaab Partner',
    render() {
      if (live()) {
        const body = `<section class="dash-card ap-status">${icon('bag', 28)}<div><h2 class="h3">Open a store with the same account</h2>
          <p class="soft">Sell sports gear you already have. The application takes about 2 minutes, and the Super Malaab team replies within one working day. Your store has its own dashboard; switch between it and your pitches in one tap.</p>
          <a class="btn btn-blaze" href="#/sell" style="margin-top:12px">${icon('bag', 16)} Apply to sell</a></div></section>`;
        return C().layout('sell', { title: 'Sell on Super Malaab', subtitle: 'Open a store next to your pitches', body });
      }
      const a = D().me();
      const s = S.sellers.byAccount(a.id);
      if (s) {
        const body = `<section class="dash-card ap-status ${s.status === 'active' ? 'is-ok' : ''}">${icon(s.status === 'active' ? 'check' : 'clock', 28)}<div><h2 class="h3">${s.status === 'active' ? `Your store “${esc(s.name)}” is open` : s.status === 'pending' ? 'We’re reviewing your seller application' : 'Your seller application wasn’t approved'}</h2>
          <p class="soft">${s.status === 'pending' ? 'The Super Malaab team replies within one working day.' : s.status === 'active' ? 'Designs, products, orders and your seller wallet are in the seller dashboard.' : esc(s.reviewReason || '')}</p>
          ${s.status !== 'rejected' ? `<button class="btn btn-primary" data-switch="seller" style="margin-top:12px">${icon('bag', 16)} Open the seller dashboard</button>` : ''}</div></section>`;
        return C().layout('sell', { title: 'Sell on Super Malaab', body });
      }
      const body = `<form class="stack" style="--gap:18px;max-width:820px" data-sell novalidate>
        <section class="dash-card"><p class="soft">Sell print-on-demand designs (we print and ship), products you already stock, or both — with the same account. Your seller dashboard is separate from your pitches; switch between them in one tap.</p></section>
        <section class="dash-card"><h2 class="h4">Your store</h2><div class="form-grid" style="margin-top:12px">
          <fieldset class="field span-2"><legend class="field-label">How do you want to sell?</legend><div class="seg">${[['pod', 'Print on demand'], ['store', 'My own products'], ['both', 'Both']].map(([v, l], i) => `<label class="seg-opt"><input type="radio" name="mode" value="${v}" ${i === 1 ? 'checked' : ''} /><span>${l}</span></label>`).join('')}</div></fieldset>
          ${field('name', 'Store name', `<input class="input" id="ap-name" name="name" maxlength="40" value="${esc(a.company || '')} Shop" />`, 'Your store link is made from the name — you can change it later.')}
          <div class="field span-2"><label for="ap-bio">About your store <span class="muted">(optional)</span></label><textarea class="input" id="ap-bio" name="bio" rows="2" maxlength="280"></textarea></div>
        </div></section>
        ${SA.ui.sellerIdFields({ name: a.name, phone: a.phone })}
        <section class="dash-card"><h2 class="h4">How we pay you</h2><div class="form-grid" style="margin-top:12px">
          ${field('payoutMethod', 'Method', `<select class="select" id="ap-payoutMethod" name="payoutMethod">${['InstaPay', 'Bank transfer'].map((p) => `<option value="${p}" ${a.payout?.method === p ? 'selected' : ''}>${p}</option>`).join('')}</select>`)}
          ${field('payoutDetail', 'Account', `<input class="input" id="ap-payoutDetail" name="payoutDetail" maxlength="60" dir="ltr" value="${esc(a.payout?.detail || '')}" />`)}
        </div></section>
        <section class="dash-card"><label class="choice"><input type="checkbox" name="agree" /><span class="radio-dot" style="border-radius:6px"></span><span class="grow"><b>I agree to the <a class="link" href="#/legal/seller" target="_blank" rel="noopener">seller agreement</a></b></span></label><p class="field-error" data-err="agree"></p></section>
        <button class="btn btn-blaze btn-lg" type="submit" style="justify-self:start">${icon('bag', 18)} Send my application</button></form>`;
      return C().layout('sell', { title: 'Sell on Super Malaab', subtitle: 'Open a store next to your pitches', body });
    },
    mount(el) {
      const f = el.querySelector('[data-sell]'); if (!f) return;
      const data = { idPhoto: '' };
      SA.ui.bindSellerId(el, data);
      f.addEventListener('submit', (e) => {
        e.preventDefault();
        try { S.sellers.start({ ...SA.core.utils.formData(f), agree: f.agree.checked, idPhoto: data.idPhoto, accountId: D().me().id, requireId: true }); toast('Application sent'); SA.app.rerender(); }
        catch (err) { showErrors(f, err.details || {}); toast(err.message, 'error'); }
      });
    },
  });

  /* ======================= Profile: grow with Super Malaab ======================= */
  SA.ui.growCards = () => {
    const seller = S.sellers.mine();
    const app = S.partners.mine();
    const sellerCard = !seller ? ['bag', 'Sell on Super Malaab', 'Your designs printed on demand, or your own products — one store, one wallet.', '<a class="btn btn-blaze btn-sm" href="#/sell">Apply to sell</a>', '']
      : seller.status === 'pending' ? ['clock', 'Seller application', `“${esc(seller.name)}” is being reviewed — usually within one working day.`, '<a class="btn btn-outline btn-sm" href="#/sell">See my application</a>', 'is-wait']
      : seller.status === 'rejected' ? ['alert', 'Seller application', esc(seller.reviewReason || 'Not approved.'), '<a class="btn btn-outline btn-sm" href="#/support">Contact us</a>', 'is-bad']
      : ['bag', 'Seller dashboard', `“${esc(seller.name)}” — designs, products, orders and your wallet.`, '<button class="btn btn-blaze btn-sm" data-grow="seller">Open seller dashboard</button>', 'is-on'];
    const partnerCard = !app || app.status === 'rejected' ? ['pitch', app ? 'Pitch partner application' : 'List your pitch', app ? esc(app.reason || 'Not approved — fix it and apply again.') : 'Own a pitch? Get bookings and online payments, paid every week.', `<a class="btn btn-${app ? 'outline' : 'primary'} btn-sm" href="#/partner/apply">${app ? 'Apply again' : 'Apply as a pitch partner'}</a>`, app ? 'is-bad' : '']
      : app.status === 'pending' ? ['clock', 'Pitch partner application', `“${esc(app.pitchName)}” is being reviewed — within 2 working days.`, '<a class="btn btn-outline btn-sm" href="#/partner/apply">See my application</a>', 'is-wait']
      : ['pitch', 'Pitch partner dashboard', `“${esc(app.pitchName)}” — bookings, hours, check-in and your money.`, '<button class="btn btn-primary btn-sm" data-grow="partner">Open pitch dashboard</button>', 'is-on'];
    const card = ([ic, t, text, cta, cls]) => `<article class="grow-card ${cls}"><span class="grow-ico">${icon(ic, 22)}</span><div class="grow"><b>${t}</b><p class="small soft">${text}</p></div>${cta}</article>`;
    return `<section class="section-tight grow-wrap"><div class="sec-head"><h2 class="h3">${icon('sparkle', 20)} Grow with Super Malaab</h2></div><div class="grow-grid">${card(sellerCard)}${card(partnerCard)}</div></section>`;
  };
  document.addEventListener('click', (e) => {
    const g = e.target.closest('[data-grow]'); if (!g) return;
    if (g.dataset.grow === 'seller') { S.sellers.openDashboard(); SA.core.router.go('/seller'); }
    else { S.partners.openDashboard(); SA.core.router.go('/dash'); }
  });
})(window.SA);
