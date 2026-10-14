/** Location: browser geolocation with a searchable area list. Swap `searchAreas` for a geocoding API later. */
(function (SA) {
  const store = SA.core.store;
  const { request } = SA.services.http;

  const areas = [
    { label: 'New Cairo — Fifth Settlement', lat: 30.0283, lng: 31.4713 }, { label: 'Sheikh Zayed', lat: 30.0396, lng: 30.9876 },
    { label: 'Zamalek', lat: 30.0626, lng: 31.2197 }, { label: 'Maadi', lat: 29.9602, lng: 31.2569 }, { label: 'Heliopolis', lat: 30.0911, lng: 31.3253 },
    { label: 'Nasr City', lat: 30.0626, lng: 31.3459 }, { label: 'Dokki', lat: 30.0385, lng: 31.2120 }, { label: 'Mohandessin', lat: 30.0566, lng: 31.2000 },
    { label: '6th of October', lat: 29.9687, lng: 30.9492 }, { label: 'Mokattam', lat: 30.0128, lng: 31.3051 }, { label: 'Al Rehab', lat: 30.0588, lng: 31.4925 },
    { label: 'Madinaty', lat: 30.1067, lng: 31.6372 }, { label: 'Downtown Cairo', lat: 30.0444, lng: 31.2357 }, { label: 'Giza — Haram', lat: 29.9958, lng: 31.1580 },
    { label: 'Alexandria — Smouha', lat: 31.2156, lng: 29.9418 }, { label: 'Alexandria — Sidi Gaber', lat: 31.2190, lng: 29.9420 },
  ];

  const set = (loc) => { store.set({ location: loc }, 'location'); return loc; };

  const locationService = {
    current: () => store.get().location,
    /** Ask the browser for GPS. Resolves with a location or falls back (never rejects). */
    requestGPS() {
      return new Promise((resolve) => {
        if (!('geolocation' in navigator)) return resolve(set({ ...SA.config.fallbackLocation, source: 'fallback', reason: 'unsupported' }));
        navigator.geolocation.getCurrentPosition(
          (pos) => {
            const here = { lat: pos.coords.latitude, lng: pos.coords.longitude };
            // Outside Egypt (e.g. testing abroad)? Keep the demo meaningful by snapping to Cairo.
            const inEgypt = here.lat > 22 && here.lat < 31.8 && here.lng > 24.7 && here.lng < 36.9;
            resolve(set(inEgypt ? { ...here, label: 'Your location', source: 'gps' } : { ...SA.config.fallbackLocation, source: 'fallback', reason: 'outside' }));
          },
          (err) => resolve(set({ ...SA.config.fallbackLocation, source: 'fallback', reason: err.code === 1 ? 'denied' : 'unavailable' })),
          { enableHighAccuracy: false, timeout: 8000, maximumAge: 5 * 60e3 },
        );
      });
    },
    searchAreas: (q) => request(() => {
      const s = q.trim().toLowerCase();
      return s ? areas.filter((a) => a.label.toLowerCase().includes(s)).slice(0, 6) : areas.slice(0, 8);
    }, { latency: 90 }),
    useArea: (area) => set({ lat: area.lat, lng: area.lng, label: area.label, source: 'search' }),
    areas,
  };

  SA.services.location = locationService;
})(window.SA);
