(function (SA) {
  const { esc, isoDate, addDays, today, fmtDate, debounce, plural, DAY } = SA.core.utils;
  const { icon, cards } = SA.ui;
  const c = SA.ui.c;
  const S = SA.services;

  const state = { q: '', format: '', indoor: '', maxPrice: '', minRating: '', sort: 'distance', date: '', onlyAvailable: false, view: 'list' };

  const locBanner = (loc) => {
    if (!loc) return `<div class="loc-prompt">
        <span class="loc-pulse">${icon('locate', 22)}</span>
        <div class="grow"><b>See the pitches closest to you</b><p class="small soft">Allow location access and we’ll sort pitches by distance and show live slots near you. Your location stays on this device.</p></div>
        <div class="row wrap"><button class="btn btn-primary" data-act="gps">${icon('locate', 16)} Allow location</button><button class="btn btn-outline" data-act="focus-search">Search an area</button></div>
      </div>`;
    const msg = loc.source === 'gps' ? 'Using your current location' : loc.source === 'search' ? `Showing pitches near ${esc(loc.label)}` : loc.reason === 'denied' ? 'Location access is off — showing pitches near Downtown Cairo' : `Showing pitches near ${esc(loc.label)}`;
    return `<div class="loc-bar"><span class="loc-dot ${loc.source === 'gps' ? 'is-live' : ''}"></span><span class="small"><b>${msg}</b></span>${loc.source !== 'gps' ? `<button class="link link-blaze small" data-act="gps">Use my location</button>` : `<button class="link small" data-act="focus-search">Change area</button>`}</div>`;
  };

  const dateStrip = (sel) => `<div class="date-strip" role="tablist" aria-label="Date">${Array.from({ length: 10 }, (_, i) => {
    const d = addDays(today(), i), iso = isoDate(d);
    return `<button role="tab" class="date-chip ${iso === sel ? 'is-active' : ''}" aria-selected="${iso === sel}" data-date="${iso}"><span class="xs">${i === 0 ? 'Today' : i === 1 ? 'Tmrw' : DAY[d.getDay()]}</span><b class="num">${d.getDate()}</b></button>`;
  }).join('')}</div>`;

  const filtersHTML = () => `
    <div class="filter-row">
      <div class="chips" role="group" aria-label="Pitch size">${['', '5-a-side', '7-a-side', '11-a-side'].map((f) => `<button class="chip" data-f="format" data-v="${f}" aria-pressed="${state.format === f}">${f || 'All sizes'}</button>`).join('')}</div>
      <div class="chips" role="group" aria-label="Setting">
        <button class="chip" data-f="indoor" data-v="indoor" aria-pressed="${state.indoor === 'indoor'}">${icon('ac', 15)} Indoor</button>
        <button class="chip" data-f="indoor" data-v="outdoor" aria-pressed="${state.indoor === 'outdoor'}">Outdoor</button>
        <button class="chip" data-f="minRating" data-v="4.6" aria-pressed="${state.minRating === '4.6'}">${icon('star', 13)} 4.6+</button>
        <button class="chip" data-f="maxPrice" data-v="700" aria-pressed="${state.maxPrice === '700'}">Under 700 EGP</button>
        <button class="chip" data-f="onlyAvailable" data-v="1" aria-pressed="${state.onlyAvailable}">Free slots only</button>
      </div>
    </div>`;

  SA.pages.pitches = {
    title: 'Book a pitch — Super App',
    render(ctx) {
      if (ctx.query.sort) state.sort = ctx.query.sort;
      if (ctx.query.q !== undefined) state.q = ctx.query.q;
      state.date ||= isoDate(today());
      return `
        ${c.pageHead({ crumbs: [['Home', '#/'], ['Book a pitch']], title: 'Book a pitch', text: 'Live availability from pitches around you. Pick a time, pay, and you’re on.', actions: `<a class="btn btn-outline" href="#/bookings">${icon('ticket', 16)} My bookings</a>` })}
        <div class="container">
          <div class="loc-slot">${locBanner(S.location.current())}</div>
          <div class="search-panel">
            <div class="search-box">
              <div class="input-icon grow">${icon('search', 18)}<input class="input" id="pitch-search" type="search" placeholder="Search a pitch or area — e.g. Maadi, Zayed, New Cairo" value="${esc(state.q)}" autocomplete="off" aria-label="Search pitches or areas" /></div>
              <div class="area-suggest" hidden></div>
            </div>
            <label class="sort-select"><span class="sr-only">Sort by</span>${icon('sort', 16)}<select class="select" id="pitch-sort">
              ${[['distance', 'Nearest first'], ['price', 'Lowest price'], ['rating', 'Top rated'], ['availability', 'Most availability']].map(([v, l]) => `<option value="${v}" ${state.sort === v ? 'selected' : ''}>${l}</option>`).join('')}
            </select></label>
          </div>
          ${dateStrip(state.date)}
          <div class="filters-slot">${filtersHTML()}</div>
          <div class="view-toggle tabs" role="tablist"><button class="tab" role="tab" data-view="list" aria-selected="${state.view === 'list'}">${icon('list', 16)} List</button><button class="tab" role="tab" data-view="map" aria-selected="${state.view === 'map'}">${icon('map', 16)} Map</button></div>
          <div class="pitch-layout view-${state.view}">
            <div class="pitch-results">
              <p class="small muted results-count" aria-live="polite"></p>
              <div class="pitch-list">${c.skeletonCards(4, 300)}</div>
            </div>
            <aside class="pitch-map-wrap"><div class="pitch-map" id="pitch-map" role="region" aria-label="Map of pitches"></div>
              <div class="map-legend xs"><span><i class="lg-me"></i>You</span><span><i class="lg-free"></i>Slots free</span><span><i class="lg-full"></i>Fully booked</span></div></aside>
          </div>
        </div>`;
    },
    mount(el, ctx) {
      let map = null;
      const list = el.querySelector('.pitch-list');
      const count = el.querySelector('.results-count');
      const center = () => S.location.current() || SA.config.fallbackLocation;
      let lastItems = [];
      const refit = () => { if (!map) return; map.raw.invalidateSize(); map.fit([center(), ...lastItems.slice(0, 6)]); };

      const load = async () => {
        list.classList.add('is-loading');
        const items = await S.pitches.list({ ...state, maxPrice: +state.maxPrice || 0, minRating: +state.minRating || 0 });
        lastItems = items;
        list.classList.remove('is-loading');
        count.textContent = `${plural(items.length, 'pitch', 'pitches')} · ${fmtDate(state.date, 'long')}`;
        list.innerHTML = items.length ? items.map((p) => cards.pitch(p, { date: state.date })).join('') : c.empty({ icon: 'pitch', title: 'No pitches match these filters', text: 'Try another area, remove a filter or pick a different day.', action: '<button class="btn btn-outline" data-act="clear">Clear filters</button>' });
        SA.core.motion.enhance(list);
        if (map) {
          map.setUser(center());
          map.setPitches(items, { onSelect: (id) => { const card = list.querySelector(`[data-pitch="${id}"]`); card?.scrollIntoView({ behavior: 'smooth', block: 'center' }); card?.classList.add('is-flash'); setTimeout(() => card?.classList.remove('is-flash'), 1200); map.highlight(id); } });
          map.fit([center(), ...items.slice(0, 6)]);
        }
      };

      let alive = true;
      SA.ui.map.ready().then((ok) => {
        if (!ok || !alive || map) return;
        map = SA.ui.map.create(el.querySelector('#pitch-map'), { center: center(), zoom: 11 });
        load();
      });

      list.addEventListener('mouseover', (e) => { const card = e.target.closest('[data-pitch]'); if (card && map) map.highlight(card.dataset.pitch); });

      const refreshLoc = () => { el.querySelector('.loc-slot').innerHTML = locBanner(S.location.current()); if (map) map.flyTo(center(), 12); load(); };
      const askGPS = async (btn) => {
        if (btn) btn.innerHTML = '<span class="spinner"></span> Locating…';
        const loc = await S.location.requestGPS();
        if (loc.source !== 'gps') SA.ui.overlay.toast(loc.reason === 'denied' ? 'Location is blocked in your browser. Search an area instead.' : 'We couldn’t get your location. Showing Downtown Cairo.', { type: 'info', duration: 5000 });
        refreshLoc();
      };

      el.addEventListener('click', (e) => {
        const a = e.target.closest('[data-act]');
        if (a?.dataset.act === 'gps') askGPS(a);
        if (a?.dataset.act === 'focus-search') el.querySelector('#pitch-search').focus();
        if (a?.dataset.act === 'clear') { Object.assign(state, { q: '', format: '', indoor: '', maxPrice: '', minRating: '', onlyAvailable: false }); el.querySelector('#pitch-search').value = ''; el.querySelector('.filters-slot').innerHTML = filtersHTML(); load(); }
        const f = e.target.closest('[data-f]');
        if (f) {
          const k = f.dataset.f, v = f.dataset.v;
          if (k === 'onlyAvailable') state.onlyAvailable = !state.onlyAvailable;
          else state[k] = state[k] === v && k !== 'format' ? '' : v;
          el.querySelector('.filters-slot').innerHTML = filtersHTML();
          load();
        }
        const d = e.target.closest('[data-date]');
        if (d) { state.date = d.dataset.date; el.querySelectorAll('[data-date]').forEach((b) => { b.classList.toggle('is-active', b === d); b.setAttribute('aria-selected', b === d); }); load(); }
        const v = e.target.closest('[data-view]');
        if (v) { state.view = v.dataset.view; el.querySelector('.pitch-layout').className = `pitch-layout view-${state.view}`; el.querySelectorAll('[data-view]').forEach((b) => b.setAttribute('aria-selected', b === v)); setTimeout(refit, 60); }
        const area = e.target.closest('[data-area]');
        if (area) { const a2 = S.location.areas[+area.dataset.area]; S.location.useArea(a2); state.q = ''; el.querySelector('#pitch-search').value = ''; suggest.hidden = true; refreshLoc(); }
      });

      const input = el.querySelector('#pitch-search');
      const suggest = el.querySelector('.area-suggest');
      const showSuggest = async () => {
        const areas = await S.location.searchAreas(input.value);
        suggest.innerHTML = `<p class="xs muted" style="padding:8px 12px 4px">Search near an area</p>${areas.map((a) => `<button class="area-opt" data-area="${S.location.areas.findIndex((x) => x.label === a.label)}">${icon('pin', 16)} ${esc(a.label)}</button>`).join('') || '<p class="small muted" style="padding:8px 12px">No matching areas</p>'}`;
        suggest.hidden = false;
      };
      input.addEventListener('focus', showSuggest);
      input.addEventListener('input', debounce(() => { state.q = input.value; showSuggest(); load(); }, 220));
      const outside = (e) => { if (!e.target.closest('.search-box')) suggest.hidden = true; };
      document.addEventListener('click', outside);
      input.addEventListener('keydown', (e) => { if (e.key === 'Escape') suggest.hidden = true; });
      el.querySelector('#pitch-sort').addEventListener('change', (e) => { state.sort = e.target.value; load(); });

      if (ctx.query.locate && S.location.current()?.source !== 'gps') askGPS(el.querySelector('[data-act="gps"]'));
      else load();
      return () => { alive = false; map?.destroy(); document.removeEventListener('click', outside); };
    },
  };
})(window.SA);
