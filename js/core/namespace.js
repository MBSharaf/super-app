/**
 * Global namespace. Each folder maps to a future ES module / React folder:
 *   SA.core       -> src/core      (store, router, utils)
 *   SA.data       -> src/mocks     (seed data — removed once the API exists)
 *   SA.services   -> src/services  (API clients; UI never reads SA.data directly)
 *   SA.ui         -> src/components
 *   SA.pages      -> src/pages     (route components)
 */
window.SA = { core: {}, data: {}, services: {}, ui: {}, pages: {}, config: {
  currency: 'EGP',
  locale: 'en-EG',
  // Simulated network latency for mock services (ms)
  latency: [180, 420],
  // Fallback when location permission is denied: Tahrir Square, Cairo
  fallbackLocation: { lat: 30.0444, lng: 31.2357, label: 'Downtown Cairo' },
  storageKey: 'superapp.state.v1',
  // The store (sellers' own products, shipping) and print on demand (studio, designs, factory) are switched on the server
  // (settings Features:StoreEnabled / Features:PodEnabled), which marks <html data-store="on" data-pod="on">.
  // GitHub Pages (no server) keeps both off. Everything stays in the code.
  storeEnabled: document.documentElement.dataset.store === 'on',
  podEnabled: document.documentElement.dataset.pod === 'on',
  // WhatsApp support — paste the chat link here (e.g. https://wa.me/20XXXXXXXXXX). Empty = not live yet.
  whatsapp: { url: '', hours: 'Daily 9 AM – 1 AM' },
  // Social profiles — paste each link here. Empty = shown but not linked yet.
  social: { facebook: '', instagram: '', tiktok: '' },
} };
