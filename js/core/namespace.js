/**
 * Global namespace. Each folder maps to a future ES module / React folder:
 *   SA.core       -> src/core      (store, router, utils)
 *   SA.data       -> src/mocks     (seed data — removed once the API exists)
 *   SA.services   -> src/services  (API clients; UI never reads SA.data directly)
 *   SA.ui         -> src/components
 *   SA.pages      -> src/pages     (route components)
 */
// The demo site (GitHub Pages: no server, so no data-store / data-fantasy on <html>) closes these parts for now.
// Owner, 2026-11-23: the demo link goes to Paymob for review without the store, fantasy and the match schedule (matches
// added the same day at Paymob's request). Set them to false to reopen. The server's site (supermalaab.com) is not affected.
const SA_DEMO_CLOSED = { store: true, fantasy: true, matches: true };
const SA_HTML_FLAGS = document.documentElement.dataset;
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
  // The demo site (GitHub Pages, no server) had the store open for test orders (owner, 2026-10-28) without print on
  // demand; it is closed there for now by SA_DEMO_CLOSED above. Everything stays in the code.
  // How far ahead a pitch can be booked: the whole coming month (owner, 2026-11-11). The server enforces the same window
  // (BookingWindow.Days in src/SuperApp.Application/Features/Bookings).
  bookingDays: 31,
  demoClosed: SA_DEMO_CLOSED,
  storeEnabled: SA_HTML_FLAGS.store ? SA_HTML_FLAGS.store === 'on' : !SA_DEMO_CLOSED.store,
  // Fantasy is open on the server's site; data-fantasy="off" (or SA_DEMO_CLOSED on GitHub Pages) closes it.
  fantasyEnabled: SA_HTML_FLAGS.fantasy ? SA_HTML_FLAGS.fantasy !== 'off' : !SA_DEMO_CLOSED.fantasy,
  podEnabled: SA_HTML_FLAGS.pod === 'on',
  // The match schedule (live scores and fixtures) is open on the server's site; closed on GitHub Pages by SA_DEMO_CLOSED.
  matchesEnabled: SA_HTML_FLAGS.server === 'on' || !SA_DEMO_CLOSED.matches,
  // How store orders are paid (server setting Features:ShopPayment → <html data-shop-pay>): online = card or wallet,
  // cod = cash on delivery only (owner, 2026-11-23, until the payment gateway is ready), both. The server enforces it.
  shopPayment: SA_HTML_FLAGS.shopPay || 'online',
  /** Club badges in matches and fantasy (server setting Features:ClubLogos; on unless the page says off) */
  clubLogos: SA_HTML_FLAGS.logos !== 'off',
  // The Super Malaab server marks its pages <html data-server="on">: there the dashboards show only real data (owner,
  // 2026-11-23) and the demo data (pitches, sellers, orders, factories…) is never loaded. GitHub Pages keeps the demo.
  realData: SA_HTML_FLAGS.server === 'on',
  // Tournaments are back as "coming soon" (owner, 2026-11-15: the Ramadan tournament). Venues and the admin can already
  // prepare tournaments and offers; players see them but can't register until the server setting
  // Features:TournamentsOpen marks <html data-tournaments="open"> (the server refuses registrations until then too).
  tournamentsSoon: document.documentElement.dataset.tournaments !== 'open',
  // The owner's automation (n8n): the demo site sends every new store order to `orders`, and every pitch booking, weekly
  // package, published game and request to join a game to `bookings`, and fantasy events to `fantasy` (js/services/automation.js).
  // The server sends its own.
  automation: {
    orders: 'https://attitude-blighted-stunned.ngrok-free.dev/webhook/new_oreder',
    bookings: 'https://retract-monsieur-jovial.ngrok-free.dev/webhook/BOOKING-created',
    // Fantasy (league.created, league.member.joined, gameweek.finished) goes to the orders link too (owner's request, 2026-10-31)
    fantasy: 'https://attitude-blighted-stunned.ngrok-free.dev/webhook/new_oreder',
    // Cancellation requests (cancellation.requested / .approved / .rejected) go to the chatbot's AI agent (chatbot.url below),
    // never the orders link (owner, 2026-11-11). Set a link here only to send them to a separate workflow instead.
    cancellations: '',
    // Pitch partner dashboard (owner's link 2026-11-11): pitch.added / pitch.removed and review.created → Telegram to the
    // pitch owner; offer.created → email to players; slot.posted (free hours) → players following that area / pitch.
    dashboard: 'https://retract-monsieur-jovial.ngrok-free.dev/webhook/Dashboard_lplay',
  },
  // Chatbot support — the AI agent's webhook (n8n, owner's link 2026-11-07). POST JSON with the message, the conversation,
  // the customer's data on this device and knowledgeUrl (chatbot/knowledge.md); replies { reply, action? }.
  // Empty, or no answer = the built-in demo assistant answers from the Help center.
  chatbot: { url: 'https://attitude-blighted-stunned.ngrok-free.dev/webhook/support', hours: '' },
  // Public contact details (owner, 2026-11-23) — shown on Contact us (#/contact) and About us (#/about).
  contact: { phone: '+201024549574', phoneLabel: '+20 102 454 9574', email: 'supermalaab@gmail.com', address: 'Maadi, Cairo, Egypt' },
  // Social profiles — paste each link here. Empty = shown but not linked yet.
  social: { facebook: 'https://www.facebook.com/share/1A4odKYei2/', instagram: 'https://www.instagram.com/superapp0/', tiktok: '' },
} };
