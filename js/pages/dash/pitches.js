(function (SA) {
  const { esc, money, isoDate, today, addDays, fmtHour } = SA.core.utils;
  const { icon } = SA.ui;
  const C = SA.pages.dashCommon;
  const D = () => SA.services.dash;
  const media = SA.data.media;
  const db = () => SA.services.db;

  const SURFACES = ['Artificial turf', 'Artificial turf (FIFA Quality)', 'Artificial turf (60 mm)', 'Natural grass', 'Futsal court', 'Indoor turf'];
  const CITIES = ['Cairo', 'Giza', 'Alexandria', 'Port Said', 'Suez', 'Ismailia', 'Mansoura', 'Tanta', 'Hurghada', 'Sharm El Sheikh'];
  const PITCH_LIBRARY = ['nightMatch', 'litField', 'aerialPitch', 'birdsEyeLights', 'goalTrees', 'turfLine', 'turfKick', 'stringLights', 'turfGoal', 'indoorTurf', 'aerialField', 'futsalGoal', 'futsalArena', 'goalNet', 'fieldGoal', 'nightGrass', 'fieldLights', 'ballsInGoal'].map((k) => media.photos[k]);

  const switchHtml = (p) => `<button class="switch ${p.status === 'active' ? 'is-on' : ''}" role="switch" aria-checked="${p.status === 'active'}" data-toggle="${p.id}" aria-label="${p.status === 'active' ? 'Close' : 'Open'} ${esc(p.name)} for bookings"><span class="switch-knob"></span><span class="switch-label xs">${p.status === 'active' ? 'Open for booking' : 'Closed'}</span></button>`;

  /* ======================= List ======================= */
  SA.pages.dashPitches = C.guard({
    title: 'My pitches — Super App Partner',
    render() {
      const sc = C.scope();
      const from = isoDate(addDays(today(), -29)), to = isoDate(today());
      const s = D().summary({ from, to, ...sc });
      const owners = Object.fromEntries(db().all('accounts').map((a) => [a.id, a]));
      const cards = s.pitches.map((p, i) => {
        const L = s.ledger.filter((b) => b.pitchId === p.id && b.status !== 'cancelled');
        const hours = L.reduce((t, b) => t + b.duration, 0);
        const occ = hours / Math.max(1, (p.open[1] - p.open[0]) * 30);
        const up = D().upcomingFor(p.id).length;
        return `<article class="dp-card ${p.status === 'closed' ? 'is-closed' : ''}">
          <div class="dp-media media"><img src="${media.photo(p.images[0], 640, 400)}" alt="" loading="lazy" />
            <span class="badge badge-glass dp-format">${esc(p.format)}${p.indoor ? ' · Indoor' : ''}</span>
            ${p.review === 'pending' ? '<span class="dp-closed dp-review"><b>Waiting for Super App review</b><span class="xs">We check new pitches within one working day. It goes live when approved.</span></span>' : p.review === 'rejected' ? `<span class="dp-closed"><b>Not approved</b><span class="xs">${esc(p.reviewReason || '')} — edit the pitch to send it again.</span></span>` : ''}
            ${p.status === 'closed' ? `<span class="dp-closed"><b>Closed for booking</b>${p.closedReason ? `<span class="xs">${esc(p.closedReason)}</span>` : ''}</span>` : ''}</div>
          <div class="dp-body">
            <div class="row-between" style="align-items:flex-start"><div class="grow"><h3 class="h4 truncate">${esc(p.name)}</h3><p class="xs muted truncate">${esc(p.area)}, ${esc(p.city)}${D().isAdmin() ? ` · ${esc(owners[p.ownerId]?.company || '')}` : ''}</p></div><span class="price">${money(p.price)}</span></div>
            <div class="dp-stats">
              <div><b class="num">${L.length}</b><span class="xs">bookings (30d)</span></div>
              <div><b class="num">${Math.round(occ * 100)}%</b><span class="xs">occupancy</span></div>
              <div><b class="num">${up}</b><span class="xs">upcoming</span></div>
            </div>
            <div class="meter ${occ > 0.6 ? '' : 'is-hot'}"><span style="width:${Math.min(100, occ * 100)}%"></span></div>
            <div class="dp-foot">${switchHtml(p)}
              <div class="row" style="--gap:4px">
                <a class="icon-btn" href="#/dash/availability?pitch=${p.id}" title="Availability" aria-label="Availability for ${esc(p.name)}">${icon('calendar', 17)}</a>
                <a class="icon-btn" href="#/pitches/${p.id}" target="_blank" rel="noopener" title="View on the site" aria-label="View ${esc(p.name)} on the site">${icon('eye', 17)}</a>
                <button class="icon-btn" data-delete="${p.id}" title="Delete" aria-label="Delete ${esc(p.name)}" style="color:var(--danger)">${icon('trash', 17)}</button>
                <a class="btn btn-primary btn-sm" href="#/dash/pitches/${p.id}">${icon('edit', 14)} Edit</a>
              </div></div>
          </div></article>`;
      }).join('');
      return C.layout('pitches', {
        title: 'My pitches', subtitle: `${s.pitches.length} pitch${s.pitches.length === 1 ? '' : 'es'} · ${s.pitches.filter((p) => p.status === 'active').length} open for booking`,
        actions: `<a class="btn btn-primary btn-sm" href="#/dash/pitches/new">${icon('plus', 15)} Add pitch</a>`,
        body: s.pitches.length ? `<div class="dp-grid">${cards}</div>` : `<div class="dash-card">${SA.ui.c.empty({ icon: 'pitch', title: 'No pitches yet', text: 'Add your first pitch with photos, prices and opening hours. It appears on Super App as soon as you save it.', action: `<a class="btn btn-primary" href="#/dash/pitches/new">${icon('plus', 16)} Add a pitch</a>` })}</div>`,
      });
    },
    mount(el) {
      el.addEventListener('click', async (e) => {
        const t = e.target.closest('[data-toggle]');
        if (t) {
          const p = D().pitch(t.dataset.toggle);
          if (p.status === 'active') {
            SA.ui.overlay.open({
              title: `Close ${esc(p.name)}?`, subtitle: 'The pitch disappears from search and nobody can book it until you reopen it. Existing bookings stay.',
              body: `<form id="close-form" novalidate class="stack" style="--gap:12px"><div class="chips wrap" style="flex-wrap:wrap">${['Maintenance', 'Private event', 'Bad weather', 'Turf replacement'].map((r) => `<button type="button" class="chip" data-reason="${r}">${r}</button>`).join('')}</div>
                <div class="field"><label for="cr">Reason (shown to your team)</label><input class="input" id="cr" name="reason" placeholder="e.g. Turf replacement until Friday" /></div></form>`,
              footer: `<button class="btn btn-outline" data-close>Keep open</button><button class="btn btn-blaze" form="close-form" type="submit">Close pitch</button>`,
              onMount(m, close) {
                m.querySelectorAll('[data-reason]').forEach((b) => b.addEventListener('click', () => { m.querySelector('#cr').value = b.dataset.reason; m.querySelectorAll('[data-reason]').forEach((x) => x.setAttribute('aria-pressed', x === b)); }));
                m.querySelector('#close-form').addEventListener('submit', (ev) => { ev.preventDefault(); D().setPitchStatus(p.id, 'closed', ev.target.reason.value.trim()); close(); SA.ui.overlay.toast(`${p.name} is closed for booking`, { type: 'info' }); SA.app.rerender(); });
              },
            });
          } else {
            D().setPitchStatus(p.id, 'active');
            SA.ui.overlay.toast(`${p.name} is open for booking again`); SA.app.rerender();
          }
          return;
        }
        const d = e.target.closest('[data-delete]');
        if (d) {
          const p = D().pitch(d.dataset.delete);
          const up = D().upcomingFor(p.id).length;
          const ok = await SA.ui.overlay.confirm({ title: `Delete ${esc(p.name)}?`, text: `The pitch is removed from Super App.${up ? ` <b>${up} upcoming booking${up > 1 ? 's' : ''} will be cancelled</b> and customers refunded in full.` : ''} Past bookings and earnings stay in your reports. This can’t be undone.`, confirmLabel: 'Delete pitch', cancelLabel: 'Keep pitch', danger: true });
          if (!ok) return;
          const r = await D().deletePitch(p.id);
          SA.ui.overlay.toast(`${p.name} deleted${r.cancelled ? ` — ${r.cancelled} booking${r.cancelled > 1 ? 's' : ''} cancelled and refunded` : ''}`);
          SA.app.rerender();
        }
      });
    },
  });

  /* ======================= Editor ======================= */
  const blank = () => ({ name: '', area: '', city: 'Cairo', address: '', lat: null, lng: null, format: '5-a-side', surface: 'Artificial turf', indoor: false, price: 600, peakPrice: 750, open: [9, 25], facilities: ['lights', 'water', 'wc'], images: [], about: '', tags: [], status: 'active', cancelPolicy: 'standard', weekly: { enabled: true, discount: 0.1 } });
  const hourOpts = (from, to, sel) => { let o = ''; for (let h = from; h <= to; h++) o += `<option value="${h}" ${h === sel ? 'selected' : ''}>${fmtHour(h)}${h >= 24 ? ' (next day)' : ''}</option>`; return o; };

  SA.pages.dashPitchEdit = C.guard({
    title: 'Edit pitch — Super App Partner',
    render(ctx) {
      const isNew = !ctx.params.id;
      const p = isNew ? blank() : D().pitch(ctx.params.id);
      this._p = p;
      const F = SA.data.pitches.facilities;
      const owners = D().isAdmin() ? db().all('accounts').filter((a) => a.role === 'owner') : [];
      const body = `<form id="pitch-form" class="pe" novalidate>
        <div class="pe-main">
          ${isNew && owners.length ? `<section class="dash-card"><h2 class="h4">Owner</h2><div class="field" style="margin-top:12px"><label for="pe-owner">Which partner owns this pitch?</label><select class="select" id="pe-owner" name="ownerId">${owners.map((o) => `<option value="${o.id}" ${C.ownerFilter() === o.id ? 'selected' : ''}>${esc(o.name)} · ${esc(o.company)}</option>`).join('')}</select></div></section>` : ''}
          <section class="dash-card"><h2 class="h4">Basics</h2>
            <div class="form-grid" style="margin-top:14px">
              <div class="field span-2"><label for="pe-name">Pitch name</label><input class="input" id="pe-name" name="name" value="${esc(p.name)}" maxlength="48" placeholder="e.g. Turf 22 Arena" /><span class="field-error" data-err="name"></span></div>
              <div class="field"><label for="pe-area">Area</label><input class="input" id="pe-area" name="area" list="pe-areas" value="${esc(p.area)}" placeholder="e.g. Maadi" /><datalist id="pe-areas">${SA.services.location.areas.map((a) => `<option value="${esc(a.label.split(' — ')[0])}">`).join('')}</datalist><span class="field-error" data-err="area"></span></div>
              <div class="field"><label for="pe-city">City</label><select class="select" id="pe-city" name="city">${CITIES.map((x) => `<option ${x === p.city ? 'selected' : ''}>${x}</option>`).join('')}</select></div>
              <div class="field span-2"><label for="pe-address">Street address</label><input class="input" id="pe-address" name="address" value="${esc(p.address)}" placeholder="Street, building, landmark" /><span class="field-error" data-err="address"></span></div>
            </div></section>
          <section class="dash-card"><div class="row-between"><h2 class="h4">Location on the map</h2><span class="xs muted" data-coords>${p.lat ? `${(+p.lat).toFixed(4)}, ${(+p.lng).toFixed(4)}` : 'Not set'}</span></div>
            <p class="small soft" style="margin-top:6px">Click the map where the pitch is. Players use this to find the closest pitches.</p>
            <div class="pe-map" id="pe-map"></div><span class="field-error" data-err="location"></span></section>
          <section class="dash-card"><h2 class="h4">Pitch</h2>
            <div class="form-grid" style="margin-top:14px">
              <fieldset class="field span-2"><legend class="field-label">Size</legend><div class="seg">${['5-a-side', '7-a-side', '11-a-side'].map((f) => `<label class="seg-opt"><input type="radio" name="format" value="${f}" ${p.format === f ? 'checked' : ''} /><span>${f}<i class="xs">${{ '5-a-side': 10, '7-a-side': 14, '11-a-side': 22 }[f]} players</i></span></label>`).join('')}</div></fieldset>
              <div class="field"><label for="pe-surface">Surface</label><select class="select" id="pe-surface" name="surface">${SURFACES.map((x) => `<option ${x === p.surface ? 'selected' : ''}>${x}</option>`).join('')}</select></div>
              <label class="choice" style="align-self:end"><input type="checkbox" name="indoor" ${p.indoor ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span><span class="grow"><b>Indoor / covered</b></span>${icon('ac', 18)}</label>
            </div></section>
          <section class="dash-card"><h2 class="h4">Prices and opening hours</h2>
            <div class="form-grid" style="margin-top:14px">
              <div class="field"><label for="pe-price">Standard price per hour (EGP)</label><input class="input num" id="pe-price" name="price" type="number" min="50" step="10" value="${p.price}" /><span class="field-hint">Weekdays before 6 PM</span><span class="field-error" data-err="price"></span></div>
              <div class="field"><label for="pe-peak">Peak price per hour (EGP)</label><input class="input num" id="pe-peak" name="peakPrice" type="number" min="50" step="10" value="${p.peakPrice}" /><span class="field-hint">After 6 PM, Fridays and Saturdays</span><span class="field-error" data-err="peakPrice"></span></div>
              <div class="field"><label for="pe-o1">Opens at</label><select class="select" id="pe-o1" name="open0">${hourOpts(5, 22, p.open[0])}</select></div>
              <div class="field"><label for="pe-o2">Closes at</label><select class="select" id="pe-o2" name="open1">${hourOpts(8, 28, p.open[1])}</select><span class="field-error" data-err="open"></span></div>
            </div>
            <p class="small soft" data-hours-note style="margin-top:10px"></p></section>
          <section class="dash-card"><h2 class="h4">Cancellation policy</h2>
            <p class="small soft" style="margin-top:6px">What players get back if they cancel. Shown on your pitch page and at checkout; each booking keeps the policy it was made under. If you cancel, players always get a full refund.</p>
            <div class="pol-grid" style="margin-top:12px">${Object.values(SA.services.pitches.POLICIES).map((pol) => `<label class="pol-opt"><input type="radio" name="cancelPolicy" value="${pol.key}" ${(p.cancelPolicy || 'standard') === pol.key ? 'checked' : ''} />
              <span><b>${pol.name}</b><span class="pol-bar" aria-hidden="true"><i style="--w:${Math.min(100, (pol.full / 48) * 100)}%"></i></span><span class="xs soft">${pol.summary}</span></span></label>`).join('')}</div></section>
          <section class="dash-card"><h2 class="h4">Weekly bookings</h2>
            <div class="form-grid" style="margin-top:12px">
              <label class="choice span-2"><input type="checkbox" name="weeklyEnabled" ${p.weekly?.enabled !== false ? 'checked' : ''} /><span class="radio-dot" style="border-radius:6px"></span><span class="grow"><b>Let players book the same time every week</b><span class="xs soft" style="display:block">They pay for 4, 8 or 12 weeks up front — regular income for you.</span></span>${icon('repeat', 18)}</label>
              <div class="field"><label for="pe-wd">Weekly discount (%)</label><input class="input num" id="pe-wd" name="weeklyDiscount" type="number" min="0" max="30" step="1" value="${Math.round((p.weekly?.discount ?? 0.1) * 100)}" /><span class="field-hint">0–30% off every game in the series</span><span class="field-error" data-err="weeklyDiscount"></span></div>
              <div class="field"><span class="field-label">Regular teams who pay at the venue</span><a class="btn btn-outline btn-sm" href="#/dash/teams?new=1${p.id ? `&pitch=${p.id}` : ''}" style="justify-self:start;margin-top:4px">${icon('users', 14)} Add a team or membership</a></div>
            </div></section>
          <section class="dash-card"><h2 class="h4">Facilities</h2>
            <div class="fac-grid" style="margin-top:12px">${Object.entries(F).map(([k, l]) => `<label class="fac"><input type="checkbox" name="facilities" value="${k}" ${p.facilities.includes(k) ? 'checked' : ''} /><span>${icon(SA.ui.c.facilityIcon(k), 18)} ${l}</span></label>`).join('')}</div></section>
          <section class="dash-card"><div class="row-between"><h2 class="h4">Photos</h2><span class="xs muted">Up to 6 · first photo is the cover</span></div><div data-photos style="margin-top:12px"></div></section>
          <section class="dash-card"><h2 class="h4">Description</h2>
            <div class="field" style="margin-top:12px"><label for="pe-about">What should players know?</label><textarea class="textarea" id="pe-about" name="about" maxlength="500" placeholder="Lighting, parking, changing rooms, how to find the entrance…">${esc(p.about)}</textarea><span class="field-error" data-err="about"></span></div>
            <div class="field" style="margin-top:12px"><label for="pe-tags">Highlights <span class="muted">(up to 3, separated by commas)</span></label><input class="input" id="pe-tags" name="tags" value="${esc(p.tags.join(', '))}" placeholder="e.g. Top rated, Match recording" /></div></section>
        </div>
        <aside class="pe-side">
          <div class="pe-preview"><p class="xs muted" style="margin-bottom:8px">${icon('eye', 13)} Live preview — how players see it</p><div data-preview></div></div>
          ${!isNew ? `<div class="dash-card"><h2 class="h4">Status</h2><div style="margin-top:12px">${switchHtml(p)}</div><p class="xs muted" style="margin-top:10px">Closed pitches are hidden from search and can’t be booked.</p></div>` : ''}
        </aside>
        <div class="save-bar"><span class="small soft grow" data-dirty>${isNew ? 'New pitch — fill in the details and save.' : 'Changes go live on Super App when you save.'}</span><a class="btn btn-outline" href="#/dash/pitches">Cancel</a><button class="btn btn-primary" type="submit">${icon('check', 16)} ${isNew ? 'Publish pitch' : 'Save changes'}</button></div>
      </form>`;
      return C.layout('pitches', { title: isNew ? 'Add a pitch' : `Edit ${esc(p.name)}`, subtitle: isNew ? 'It appears on Super App as soon as you publish it.' : `Last updated ${SA.core.utils.timeAgo(p.updatedAt)}`, actions: !isNew ? `<a class="btn btn-outline btn-sm" href="#/pitches/${p.id}" target="_blank" rel="noopener">${icon('eye', 15)} View on site</a>` : '', body });
    },
    mount(el, ctx) {
      const form = el.querySelector('#pitch-form');
      const p = this._p;
      let lat = p.lat, lng = p.lng;
      const photos = C.photoManager(el.querySelector('[data-photos]'), { images: p.images, library: PITCH_LIBRARY, max: 6 });

      const read = () => {
        const fd = new FormData(form);
        return { name: fd.get('name') || '', area: fd.get('area') || '', city: fd.get('city'), address: fd.get('address') || '', lat, lng, format: fd.get('format'), surface: fd.get('surface'), indoor: !!fd.get('indoor'),
          price: +fd.get('price'), peakPrice: +fd.get('peakPrice'), open: [+fd.get('open0'), +fd.get('open1')], facilities: fd.getAll('facilities'), images: photos.get(), about: fd.get('about') || '',
          tags: (fd.get('tags') || '').split(',').map((t) => t.trim()).filter(Boolean), ownerId: fd.get('ownerId'),
          cancelPolicy: fd.get('cancelPolicy') || 'standard', weeklyEnabled: !!fd.get('weeklyEnabled'), weeklyDiscount: fd.get('weeklyDiscount') };
      };
      const preview = () => {
        const d = read();
        const pv = { id: p.id || 'preview', name: d.name || 'Your pitch name', area: d.area || 'Area', city: d.city, format: d.format, indoor: d.indoor, price: d.price || 0, rating: p.rating || 5, reviews: p.reviews || 0, images: d.images.length ? d.images : ['turfLine'], tags: d.tags, distanceKm: 1.2, freeSlots: Math.max(0, d.open[1] - d.open[0]) };
        el.querySelector('[data-preview]').innerHTML = SA.ui.cards.pitch(pv).replace(/href="#\/pitches\/[^"]*"/, 'href="#" tabindex="-1"');
        const span = d.open[1] - d.open[0];
        el.querySelector('[data-hours-note]').textContent = span > 0 ? `Open ${span} hours a day · ${fmtHour(d.open[0])} to ${fmtHour(d.open[1])}${d.open[1] > 24 ? ' (after midnight)' : ''}.` : 'Closing time must be after opening time.';
      };
      form.addEventListener('input', preview);
      form.addEventListener('change', preview);
      el.querySelector('[data-photos]').addEventListener('click', () => setTimeout(preview, 50));
      el.querySelector('[data-photos]').addEventListener('change', () => setTimeout(preview, 600));
      // Area suggestion → centre the map there
      form.area.addEventListener('change', () => {
        const a = SA.services.location.areas.find((x) => x.label.toLowerCase().startsWith(form.area.value.trim().toLowerCase()));
        if (a && map) { map.raw.setView([a.lat, a.lng], 14); if (!lat) setPoint(a.lat, a.lng); }
      });

      // Map picker
      let map = null, marker = null;
      const setPoint = (la, ln) => {
        lat = +la.toFixed(6); lng = +ln.toFixed(6);
        el.querySelector('[data-coords]').textContent = `${lat.toFixed(4)}, ${lng.toFixed(4)}`;
        if (marker) marker.setLatLng([lat, lng]); else if (map) marker = L.marker([lat, lng], { icon: L.divIcon({ className: 'price-marker-wrap', html: '<span class="price-marker is-active">Here</span>', iconSize: null }), draggable: true }).addTo(map.raw).on('dragend', (ev) => { const ll = ev.target.getLatLng(); setPoint(ll.lat, ll.lng); });
      };
      SA.ui.map.ready().then((ok) => {
        const node = el.querySelector('#pe-map'); if (!ok || !node?.isConnected) return;
        map = SA.ui.map.create(node, { center: lat ? { lat, lng } : SA.config.fallbackLocation, zoom: lat ? 15 : 11 });
        if (!map) return;
        map.raw.scrollWheelZoom.enable();
        if (lat) setPoint(lat, lng);
        map.raw.on('click', (ev) => setPoint(ev.latlng.lat, ev.latlng.lng));
      });

      el.querySelector('.pe-side [data-toggle]')?.addEventListener('click', (e) => {
        e.preventDefault();
        const cur = D().pitch(p.id);
        D().setPitchStatus(p.id, cur.status === 'active' ? 'closed' : 'active', cur.status === 'active' ? 'Closed from the editor' : '');
        SA.ui.overlay.toast(cur.status === 'active' ? 'Pitch closed for booking' : 'Pitch open for booking', { type: 'info' });
        SA.app.rerender();
      });

      form.addEventListener('submit', (e) => {
        e.preventDefault();
        const d = read();
        try {
          const out = ctx.params.id ? D().updatePitch(p.id, d) : D().createPitch(d, d.ownerId);
          SA.core.motion.confetti({ count: 50, spread: 0.6 });
          SA.ui.overlay.toast(ctx.params.id ? 'Changes saved — live on Super App' : `${out.name} is live on Super App`, { action: 'View', onAction: () => window.open(`#/pitches/${out.id}`, '_blank') });
          SA.core.router.go('/dash/pitches');
        } catch (err) {
          if (err.details) C.showErrors(form, err.details);
          SA.ui.overlay.toast(err.message === 'storage_full' ? 'Not enough storage on this device — use fewer or smaller photos.' : err.message, { type: 'error' });
        }
      });
      preview();
      return () => map?.destroy();
    },
  });
})(window.SA);
