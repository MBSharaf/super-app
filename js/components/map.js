/**
 * Map adapter around Leaflet + CARTO basemap. Pages talk to this adapter only, so the provider
 * (Google Maps, Mapbox) can be swapped without touching page code.
 */
(function (SA) {
  const { money, esc } = SA.core.utils;

  const create = (el, { center, zoom = 12, interactive = true } = {}) => {
    // A page that re-mounts quickly (fast navigation) can reach the same node twice: Leaflet refuses a second map
    if (!window.L || !el || el._leaflet_id) return null;
    const map = L.map(el, { zoomControl: interactive, scrollWheelZoom: false, dragging: interactive, attributionControl: true, tap: interactive })
      .setView([center.lat, center.lng], zoom);
    // OpenStreetMap standard tiles (attribution required). Swap for Mapbox/Google with an API key in production.
    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19,
      attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
    }).addTo(map);
    const markers = new Map();
    let me = null;

    const api = {
      raw: map,
      setUser(loc) {
        if (!loc) return;
        if (me) me.setLatLng([loc.lat, loc.lng]);
        else me = L.marker([loc.lat, loc.lng], { icon: L.divIcon({ className: 'me-marker', html: '<span></span>', iconSize: [22, 22] }), zIndexOffset: 1000, keyboard: false }).addTo(map).bindTooltip(esc(loc.label || 'You'), { direction: 'top', offset: [0, -10] });
      },
      setPitches(list, { onSelect } = {}) {
        markers.forEach((m) => m.remove()); markers.clear();
        list.forEach((p) => {
          const m = L.marker([p.lat, p.lng], {
            icon: L.divIcon({ className: 'price-marker-wrap', html: `<span class="price-marker ${p.freeSlots === 0 ? 'is-full' : ''}">${money(p.price).replace(' EGP', '')}</span>`, iconSize: null }),
            title: p.name, riseOnHover: true,
          }).addTo(map);
          m.on('click', () => onSelect?.(p.id));
          markers.set(p.id, m);
        });
      },
      highlight(id) {
        markers.forEach((m, key) => m.getElement()?.querySelector('.price-marker')?.classList.toggle('is-active', key === id));
        const m = markers.get(id); if (m) m.setZIndexOffset(900);
      },
      fit(points, pad = 40) {
        if (!points.length || !el.clientWidth) return; // hidden maps are re-fitted when shown
        map.fitBounds(L.latLngBounds(points.map((p) => [p.lat, p.lng])), { padding: [pad, pad], maxZoom: 14 });
      },
      flyTo(loc, z = 13) { if (!el.clientWidth) return; map.flyTo([loc.lat, loc.lng], z, { duration: 0.8 }); },
      invalidate() { setTimeout(() => map.invalidateSize(), 60); },
      // Leaflet throws if the page already replaced the container; the map is gone either way
      destroy() { try { map.remove(); } catch { /* already detached */ } },
    };
    return api;
  };

  /** Resolves once Leaflet has loaded (it loads async so a slow CDN never blocks the app). */
  const ready = () => new Promise((resolve) => {
    if (window.L) return resolve(true);
    const started = Date.now();
    const t = setInterval(() => {
      if (window.L) { clearInterval(t); resolve(true); }
      else if (Date.now() - started > 15000) { clearInterval(t); resolve(false); }
    }, 120);
  });

  SA.ui.map = { create, ready };
})(window.SA);
