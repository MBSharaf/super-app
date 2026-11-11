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
  // The demo site (GitHub Pages, no server) shows the store (owner's request, 2026-10-28: test orders there) without
  // print on demand. Everything stays in the code.
  // How far ahead a pitch can be booked: the whole coming month (owner, 2026-11-11). The server enforces the same window
  // (BookingWindow.Days in src/SuperApp.Application/Features/Bookings).
  bookingDays: 31,
  storeEnabled: (document.documentElement.dataset.store || 'on') === 'on',
  podEnabled: document.documentElement.dataset.pod === 'on',
  // The owner's automation (n8n): the demo site sends every new store order to `orders`, and every pitch booking, weekly
  // package, published game and request to join a game to `bookings`, and fantasy events to `fantasy` (js/services/automation.js).
  // The server sends its own.
  automation: {
    orders: 'https://attitude-blighted-stunned.ngrok-free.dev/webhook-test/new_oreder',
    bookings: 'https://retract-monsieur-jovial.ngrok-free.dev/webhook-test/BOOKING-created',
    // Fantasy (league.created, league.member.joined, gameweek.finished) goes to the orders link too (owner's request, 2026-10-31)
    fantasy: 'https://attitude-blighted-stunned.ngrok-free.dev/webhook-test/new_oreder',
    // Cancellation requests (cancellation.requested / .approved / .rejected) go to the chatbot's AI agent (chatbot.url below),
    // never the orders link (owner, 2026-11-11). Set a link here only to send them to a separate workflow instead.
    cancellations: '',
    // Pitch partner dashboard (owner's link 2026-11-11): pitch.added / pitch.removed and review.created → Telegram to the
    // pitch owner; offer.created → email to players; slot.posted (free hours) → players following that area / pitch.
    dashboard: 'https://retract-monsieur-jovial.ngrok-free.dev/webhook-test/Dashboard_lplay',
  },
  // Chatbot support — the AI agent's webhook (n8n, owner's link 2026-11-07). POST JSON with the message, the conversation,
  // the customer's data on this device and knowledgeUrl (chatbot/knowledge.md); replies { reply, action? }.
  // Empty, or no answer = the built-in demo assistant answers from the Help center.
  chatbot: { url: 'https://attitude-blighted-stunned.ngrok-free.dev/webhook-test/support', hours: '' },
  // Social profiles — paste each link here. Empty = shown but not linked yet.
  social: { facebook: '', instagram: '', tiktok: '' },
} };
