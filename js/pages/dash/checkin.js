/** Partner Dashboard — check-in at reception: scan the booking QR (camera or photo) or type the ID. */
(function (SA) {
  const { esc, money, fmtHour, fmtRange, fmtDate } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const D = () => SA.services.dash;

  /** jsQR (Apache-2.0) is only loaded on this page, and only if the browser has no BarcodeDetector */
  let jsqr = null;
  const loadJsQR = () => (window.jsQR ? Promise.resolve(window.jsQR) : jsqr || (jsqr = new Promise((res, rej) => {
    const s = document.createElement('script'); s.src = 'vendor/jsqr/jsQR.js'; s.onload = () => res(window.jsQR); s.onerror = () => { jsqr = null; rej(new Error('Scanner failed to load')); }; document.head.appendChild(s);
  })));
  let detector;
  const nativeDetector = async () => {
    if (detector !== undefined) return detector;
    try { detector = 'BarcodeDetector' in window && (await window.BarcodeDetector.getSupportedFormats()).includes('qr_code') ? new window.BarcodeDetector({ formats: ['qr_code'] }) : null; } catch { detector = null; }
    return detector;
  };
  /** Read a QR code from a canvas → text or null */
  const decode = async (canvas) => {
    const det = await nativeDetector();
    if (det) { try { const r = await det.detect(canvas); if (r[0]?.rawValue) return r[0].rawValue; } catch { /* fall back */ } }
    const lib = await loadJsQR();
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    const img = ctx.getImageData(0, 0, canvas.width, canvas.height);
    return lib(img.data, img.width, img.height, { inversionAttempts: 'attemptBoth' })?.data || null;
  };

  const TONE = { ok: 'ok', early: 'warn', already: 'info', 'wrong-day': 'warn', ended: 'warn', cancelled: 'bad', 'no-show': 'bad', 'wrong-venue': 'bad', 'not-found': 'bad', invalid: 'bad', expired: 'bad' };
  const TITLE = { expired: 'Package used up', ok: 'Ready to check in', early: 'Early arrival', already: 'Already checked in', 'wrong-day': 'Not today', ended: 'Game finished', cancelled: 'Cancelled booking', 'no-show': 'Marked no-show', 'wrong-venue': 'Different venue', 'not-found': 'Booking not found', invalid: 'Code not valid' };

  const resultCard = (res) => {
    const b = res.booking;
    const tone = TONE[res.state] || 'bad';
    return `<div class="ci-result ci-${tone}" role="status">
      <div class="ci-head"><span class="ci-ico">${icon(tone === 'ok' ? 'check' : tone === 'bad' ? 'x' : tone === 'info' ? 'check' : 'alert', 22, 2.4)}</span>
        <div class="grow"><b class="h4">${TITLE[res.state]}</b><p class="small">${esc(res.message)}</p></div></div>
      ${res.package ? `<div class="ci-pkg">${icon('repeat', 16)}<span class="grow"><b class="small">Weekly package <span class="num">${esc(res.package.id)}</span></b><span class="xs muted" style="display:block">${res.package.played} played · ${res.package.missed} missed · ${res.package.left} left of ${res.package.total} · scanned ${res.package.scans.length}×</span></span></div>` : ''}
      ${b ? `<dl class="kv ci-kv">
        <div><dt>Customer</dt><dd><b>${esc(b.customer.name)}</b></dd></div>
        <div><dt>Booking</dt><dd class="num">${esc(b.id)}${res.manual ? ' <span class="badge">Typed in</span>' : ''}</dd></div>
        <div><dt>Pitch</dt><dd>${esc(b.pitchName)}</dd></div>
        <div><dt>When</dt><dd class="num">${fmtDate(b.date)} · ${fmtRange(b.hour, b.duration)}</dd></div>
        <div><dt>Payment</dt><dd>${esc(b.payment)}${b.money.online ? ' · paid' : ` · <b style="color:var(--blaze)">collect ${money(b.amount)}</b>`}</dd></div>
      </dl>` : ''}
      <div class="row wrap" style="--gap:8px;margin-top:14px">
        ${b && ['ok', 'early'].includes(res.state) ? `<button class="btn btn-turf" data-do-checkin="${b.id}">${icon('check', 16)} Check in${res.state === 'early' ? ' anyway' : ''}</button>` : ''}
        ${b && res.state !== 'wrong-venue' ? `<button class="btn btn-outline" data-open="${b.id}">Booking details</button>` : ''}
        <button class="btn btn-ghost" data-next>${icon('scan', 16)} Scan next</button>
      </div>
    </div>`;
  };

  SA.pages.dashCheckin = C.guard({
    title: 'Check-in — Super App Partner',
    render() {
      const sc = C.scope();
      const list = D().arrivals(sc.ownerId);
      const done = list.filter((b) => b.checkedInAt || b.status === 'checked-in' || (b.status === 'completed' && b.checkedIn)).length;
      const waiting = list.filter((b) => ['upcoming', 'in-progress'].includes(b.status)).length;
      const body = `
        <div class="kpi-grid kpi-sm">
          ${C.kpi({ label: 'Games today', value: list.length, count: list.length, ic: 'ticket' })}
          ${C.kpi({ label: 'Checked in', value: done, count: done, ic: 'check', tone: 'k-turf' })}
          ${C.kpi({ label: 'Still to arrive', value: waiting, count: waiting, ic: 'clock', tone: 'k-blaze' })}
          ${C.kpi({ label: 'No-shows', value: list.filter((b) => b.status === 'no-show').length, ic: 'alert' })}
        </div>
        <div class="ci-layout">
          <section class="dash-card ci-scan">
            <div class="row-between"><h2 class="h4">Scan a booking</h2><span class="xs muted" data-engine></span></div>
            <div class="ci-stage" data-stage>
              <video playsinline muted data-video></video>
              <div class="ci-frame" aria-hidden="true"><i></i><i></i><i></i><i></i><span class="ci-line"></span></div>
              <div class="ci-idle" data-idle>${icon('qr', 46, 1.4)}<p class="small">Point the camera at the QR code on the customer’s booking.</p>
                <button class="btn btn-blaze" data-start>${icon('camera2', 16)} Start camera</button></div>
            </div>
            <div class="row wrap ci-tools">
              <button class="btn btn-outline btn-sm" data-stop hidden>${icon('x', 14)} Stop camera</button>
              <label class="btn btn-outline btn-sm">${icon('image', 14)} Scan from a photo<input type="file" accept="image/*" hidden data-photo /></label>
            </div>
            <form class="ci-manual" data-manual novalidate>
              <label class="small strong" for="ci-code">Or type the booking ID</label>
              <div class="row" style="--gap:8px"><input class="input num" id="ci-code" name="code" placeholder="e.g. SA-K3PX4821" autocomplete="off" autocapitalize="characters" /><button class="btn btn-primary" type="submit">Check</button></div>
            </form>
            <div data-result aria-live="polite"></div>
          </section>
          <section class="dash-card ci-arrivals">
            <div class="row-between"><h2 class="h4">Today’s arrivals</h2><span class="xs muted">${fmtDate(SA.core.utils.today(), 'long')}</span></div>
            <ul class="arr-list">${list.map((b, i) => {
              const inn = b.checkedInAt || b.status === 'checked-in' || (b.status === 'completed' && b.checkedIn);
              return `<li style="--i:${i}" class="${inn ? 'is-in' : ''}"><span class="nl-time"><b class="num">${fmtHour(b.hour)}</b><span class="xs">${b.duration * 60} min</span></span>
                <button class="grow arr-who" data-open="${b.id}"><b class="small">${esc(b.customer.name)}</b><span class="xs muted" style="display:block">${esc(b.pitchName)} · ${esc(b.sourceLabel)}</span></button>
                ${inn ? `<span class="badge badge-turf">${icon('check', 12, 2.6)} ${b.checkedInAt ? new Date(b.checkedInAt).toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' }) : 'In'}</span>`
                  : ['upcoming', 'in-progress'].includes(b.status) ? `<button class="btn btn-outline btn-sm" data-do-checkin="${b.id}">Check in</button>` : C.statusBadge(b.status)}</li>`;
            }).join('') || `<li class="small muted">No games booked today.</li>`}</ul>
          </section>
        </div>`;
      return C.layout('checkin', { title: 'Check-in', subtitle: 'Scan the QR code on the customer’s booking at reception', body });
    },
    mount(el) {
      const video = el.querySelector('[data-video]');
      const out = el.querySelector('[data-result]');
      const stage = el.querySelector('[data-stage]');
      let stream = null, raf = 0, busy = false, last = 0, paused = false;
      const cv = document.createElement('canvas');
      nativeDetector().then((d) => { el.querySelector('[data-engine]').textContent = d ? 'Built-in scanner' : 'Scanner ready'; });

      const show = (res) => {
        out.innerHTML = resultCard(res);
        out.querySelector('.ci-result')?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        if (navigator.vibrate) navigator.vibrate(res.state === 'ok' ? 60 : [40, 60, 40]);
      };
      const handle = (text) => { paused = true; stage.classList.add('is-hit'); setTimeout(() => stage.classList.remove('is-hit'), 600); show(D().checkIn(text)); };

      const stop = () => {
        cancelAnimationFrame(raf); raf = 0;
        stream?.getTracks().forEach((t) => t.stop()); stream = null;
        video.srcObject = null; stage.classList.remove('is-live');
        el.querySelector('[data-idle]').hidden = false; el.querySelector('[data-stop]').hidden = true;
      };
      const loop = async (t) => {
        raf = requestAnimationFrame(loop);
        if (busy || paused || t - last < 140 || video.readyState < 2) return;
        last = t; busy = true;
        try {
          const w = video.videoWidth, h = video.videoHeight, k = Math.min(1, 720 / Math.max(w, h));
          cv.width = Math.round(w * k); cv.height = Math.round(h * k);
          cv.getContext('2d', { willReadFrequently: true }).drawImage(video, 0, 0, cv.width, cv.height);
          const text = await decode(cv);
          if (text && raf) handle(text);
        } catch (e) { console.warn(e); } finally { busy = false; }
      };
      el.querySelector('[data-start]').addEventListener('click', async () => {
        if (!navigator.mediaDevices?.getUserMedia) { out.innerHTML = `<div class="notice gold">${icon('alert', 18)}<span>This browser can’t open the camera. Scan from a photo or type the booking ID.</span></div>`; return; }
        try {
          stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment', width: { ideal: 1280 } }, audio: false });
          video.srcObject = stream; await video.play();
          stage.classList.add('is-live'); paused = false;
          el.querySelector('[data-idle]').hidden = true; el.querySelector('[data-stop]').hidden = false;
          loadJsQR().catch(() => {});
          raf = requestAnimationFrame(loop);
        } catch (e) {
          out.innerHTML = `<div class="notice gold">${icon('alert', 18)}<span>${e.name === 'NotAllowedError' ? 'Camera access was blocked. Allow the camera in the browser’s address bar, or scan from a photo.' : 'No camera found on this device. Scan from a photo or type the booking ID.'}</span></div>`;
        }
      });
      el.querySelector('[data-stop]').addEventListener('click', stop);
      el.querySelector('[data-photo]').addEventListener('change', async (e) => {
        const file = e.target.files[0]; e.target.value = '';
        if (!file) return;
        try {
          const url = URL.createObjectURL(file);
          const img = await new Promise((res, rej) => { const i = new Image(); i.onload = () => res(i); i.onerror = rej; i.src = url; });
          const k = Math.min(1, 1400 / Math.max(img.width, img.height));
          cv.width = Math.round(img.width * k); cv.height = Math.round(img.height * k);
          const ctx = cv.getContext('2d', { willReadFrequently: true }); ctx.fillStyle = '#fff'; ctx.fillRect(0, 0, cv.width, cv.height); ctx.drawImage(img, 0, 0, cv.width, cv.height);
          URL.revokeObjectURL(url);
          const text = await decode(cv);
          if (text) handle(text);
          else out.innerHTML = `<div class="notice gold">${icon('alert', 18)}<span>We couldn’t find a QR code in that photo. Make sure the whole code is in the picture and in focus.</span></div>`;
        } catch { out.innerHTML = `<div class="notice gold">${icon('alert', 18)}<span>We couldn’t read that image. Try another photo.</span></div>`; }
      });
      el.querySelector('[data-manual]').addEventListener('submit', (e) => {
        e.preventDefault();
        const v = e.target.code.value.trim();
        if (!v) { e.target.code.focus(); return; }
        handle(v);
      });
      el.addEventListener('click', async (e) => {
        const go = e.target.closest('[data-do-checkin]');
        if (go) {
          try {
            const b = await D().bookingAction(go.dataset.doCheckin, 'check-in');
            SA.ui.overlay.toast(`${b.customer.name} checked in`, { type: 'success' });
            stop(); SA.app.rerender();
          } catch (err) { SA.ui.overlay.toast(err.message, { type: 'error' }); }
          return;
        }
        const open = e.target.closest('[data-open]'); if (open) { SA.pages.dashBookings.openBooking(open.dataset.open); return; }
        if (e.target.closest('[data-next]')) { out.innerHTML = ''; paused = false; const f = el.querySelector('#ci-code'); f.value = ''; if (!stream) f.focus(); }
      });
      return stop;
    },
  });
})(window.SA);
