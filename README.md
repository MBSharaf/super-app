# Super App

A frontend-only sports super app for Egypt: **pitch booking**, **find players / join a match**, **Egyptian Premier League fantasy** and a **sports shop**, with one account and one wallet.

There is no backend. Every backend-dependent feature (availability, payments, bookings, rankings, orders) runs against mock services and is saved in `localStorage`, so the app behaves like a working product on any device.

## Run it

You need any static file server, because browsers block some features on `file://`.

**Windows (no Node or Python needed)**

```bash
powershell -ExecutionPolicy Bypass -File tools/serve.ps1 -Port 5173
```

Then open http://localhost:5173.

**Alternatives:** `npx serve .` or `python -m http.server 5173`.

To reset all demo data, use **Profile → Reset demo**, or the footer link.

## Try these flows

| Flow | Path |
| --- | --- |
| Book a pitch | Book → allow location (or search an area) → open a pitch → date, duration, time → Review → pay with Wallet or Visa → confirmation |
| Fill your match | My bookings → **Find players** → set players needed, level and notes → Publish → share the link |
| Join a match | Find players → **Join** → pay your share (wallet, card or cash at the pitch) |
| Fantasy | Fantasy → Auto-pick (or pick 15) → set captain and formation → **Simulate GW** → Leagues → enter a prize league or create a private group |
| Shop | Shop → product → size → Add to cart → code `KICKOFF10` → Checkout → order tracking |

**Test payments:** any valid Visa number works, for example `4242 4242 4242 4242` with a future expiry and any CVC. Card numbers ending in `0002` simulate a bank decline. Join a private fantasy group with invite code `SA-GEZ5A1`.

## Architecture

```
index.html            app shell; loads scripts in dependency order
css/
  tokens.css          design tokens (colour, type, space, radius, depth, motion)
  base.css            reset, type scale, layout primitives
  components.css      buttons, chips, cards, forms, modals, toasts…
  layout.css          header, tab bar, footer, page scaffolding
  pages/*.css         page-specific styles
js/
  core/               namespace, utils, store (observable + localStorage), hash router, motion
  data/               mock seed data (pitches, matches, fantasy, products, user, media)
  services/           the ONLY layer that reads data or writes state
  components/         pure render functions (cards, ui, payment, map, shell, overlay)
  pages/              route components: render(ctx) → HTML, mount(el, ctx) → cleanup
  app.js              route table and bootstrap
vendor/               self-hosted Leaflet 1.9.4 and the Archivo variable font
tools/serve.ps1       zero-dependency static server
```

**Layering rule:** pages and components never touch `SA.data`. They call `SA.services.*`, which return Promises (with simulated latency) through `services/http.js`.

### Connecting a real backend

Each service documents its future endpoints at the top of its file. To go live, replace the body of each method with a `fetch` call and keep the same return shape. Nothing in `pages/` or `components/` changes.

| Service | Replace with |
| --- | --- |
| `pitchService` | `GET /pitches`, `GET /pitches/:id/slots?date=` |
| `bookingService` | `POST /bookings`, `GET /me/bookings`, `POST /bookings/:id/cancel` |
| `matchService` | `GET /matches`, `POST /matches`, `POST /matches/:id/join` |
| `fantasyService` | `/fantasy/bootstrap`, `/fantasy/team`, `/fantasy/leagues…`; `simulateGameweek` becomes the live scoring feed |
| `shopService` | `/products`, `/cart`, `POST /orders` |
| `paymentService` | Paymob, Fawry or Stripe SDK (card data never stored; only masked cards) |
| `locationService` | Browser geolocation plus a geocoding API |
| `components/map.js` | Adapter around Leaflet + OpenStreetMap tiles; swap for Mapbox or Google Maps |

### Moving to React

The structure maps one-to-one:

- `SA.ui.*` render functions → React components (props in, markup out).
- `SA.pages.*` → route components; `mount()` logic → `useEffect`.
- `SA.core.store` → Zustand or Redux; `subscribe` → selectors.
- `SA.core.router` route table → React Router with the same paths.
- `css/tokens.css` → a theme file or Tailwind config.

## Notes

- Photography is from Unsplash. Fantasy players, clubs and results are mock data shaped like the Egyptian Premier League; club kits are original vector shirts, not real crests.
- Map tiles come from OpenStreetMap (attribution shown). Use a commercial tile provider for production traffic.
- Motion respects `prefers-reduced-motion`.

## Motion

All animation lives in `css/motion.css` and `js/core/motion.js`:

- Page-to-page transitions (View Transitions API, with a CSS fallback)
- Scroll reveals with staggered timing on every page, plus word-by-word headings
- Count-up numbers; meters, bars and player pips that grow into view
- Parallax photography, a 3D floodlit hero pitch, a rotating headline word and a live activity ticker
- Ripple, magnetic pull and shine sweep on buttons; 3D tilt with glare on cards
- Sliding indicators in the top navigation and the mobile tab bar; the header hides as you scroll down
- Confetti on bookings, orders, joined matches, league entries and gameweek results
- A bouncing-football loader during payments
- Home page: a real-time 3D soccer ball (`js/components/ball3d.js`, no WebGL or library) that drops onto the hero pitch and jumps from section to section as you scroll. It squashes on landing, kicks up dust, and can be clicked to kick it.

Animations are on by default. Anyone can switch them to a reduced mode from **Footer → Animations**, and the choice is remembered on that device.

## Partner Dashboard (admins and pitch owners)

Open `#/dash` (also linked in the footer and the mobile menu). Sign in as one of the demo accounts — real email/password sign-in arrives with the backend.

| Account | Role | What they see |
| --- | --- | --- |
| Super App HQ | Admin | Everything on the platform, partner switcher, commission settings, store suspension, official store |
| Karim Adel · Turf 22 Group | Owner | 3 pitches + an existing store (Turf 22 Pro Shop) |
| Hazem Tarek, Mariam Fathy, Ali Reda | Owners | 3 pitches each, no store yet |
| Youssef Adel | New partner | No pitches, no store — the onboarding path |

**What partners can do**

- **Pitches** — add, edit (map location picker, photos, prices, hours, facilities, live preview), close/reopen for booking, delete (upcoming bookings are cancelled and refunded automatically).
- **Availability** — hour-by-hour board per pitch: booked / free / blocked; block single hours or date ranges; add walk-in bookings; open any booking to check in, mark no-show or cancel with a full refund.
- **Bookings** — filter, search, paginate, details drawer, **Excel export**.
- **Finance** — bookings vs store money, commission, online vs cash, per-pitch breakdown, daily chart, **Excel report** (Summary, Daily, By pitch, Bookings, Store sales, Payouts).
- **Wallet** — booking money and store money in separate pockets (available / pending / settled), weekly automatic settlements, withdrawal requests, **Excel statement**.
- **My store** — one store per account (name, logo, cover, description); add/edit/delete/publish products with photos, sizes, colours, specs and stock. Published products appear in the site shop immediately with "Sold by …", and every sale lands in the store wallet.

**How it connects to the site** — `js/services/db.js` is the shared platform database (pitches, products, stores, bookings, closures, sales, payouts, activity). The site's pitch and shop services read from it, so dashboard changes are live on the site, and site bookings/orders appear in the dashboard. Replace `db.js` + `dashService.js` with API calls when the backend is ready; the screens stay the same.

**Money rules (configurable by the admin)** — 10% commission on app bookings, 12% on store sales, none on walk-ins or the official store. Online booking money becomes available after the match; store money when the order is delivered; available money is settled every Monday.

**Excel** — `js/core/xlsx.js` writes real .xlsx files with no library (multiple sheets, EGP money format, bold totals, frozen headers, filters). Verified in Microsoft Excel.

### Operations (added in release 2)

| Feature | Where | How it connects |
| --- | --- | --- |
| **QR check-in** | `#/dash/checkin` | Every app booking shows a signed QR code on the customer's ticket. Staff scan it with the camera (built-in `BarcodeDetector` or the vendored `vendor/jsqr`, loaded only on this page), from a photo, or by typing the booking ID. Edited codes, other venues' bookings, wrong days, cancelled and already-used bookings are rejected with a clear reason. The customer's ticket then shows "Checked in". |
| **Weekly bookings & memberships** | Site: pitch page → "Play every week?" (4/8/12 weeks, prepaid, partner's weekly discount). Dashboard: `#/dash/teams` | Taken weeks are skipped and shown before paying. Venue memberships book 4 games at a time and renew automatically (`renewMemberships`, a nightly job on the backend). Ending a series cancels future games (app players get a full refund). |
| **Review replies** | `#/dash/reviews` | Players review a pitch after a played booking (booking page). Partners reply publicly (templates included); replies appear under the review on the pitch page and notify the player. Partners can report reviews; admins hide/restore them. |
| **Invoices, statements, VAT** | Booking page, order page, booking drawer, order drawer, Finance → "Monthly statement (PDF)" | `js/core/pdf.js` writes real PDF files with no library (pages are drawn on canvas, so Arabic works). Prices include VAT (14%, editable by the admin in Platform settings); every document shows the VAT inside each amount. The statement includes the Super App commission as a tax invoice. |
| **Cancellation policies** | Pitch editor → "Cancellation policy" (Flexible / Standard / Moderate / Strict) | Shown on the pitch page and at checkout; each booking keeps the policy it was made under. The partner keeps the non-refunded share of late cancellations (minus commission); venue cancellations always refund in full. |
| **Order fulfilment & low stock** | `#/dash/orders`, `#/dash/store` | Partner orders move New → Packed → Shipped (courier + tracking) → Delivered, or are cancelled (stock returned, customer refunded incl. delivery if the whole order is cancelled). Store money becomes available on delivery. Each product has its own low-stock alert level and a quick Restock action. |
| **Forecast & offers** | `#/dash/insights` | 30-day revenue forecast (booked + typical demand by weekday/hour over 8 weeks, with a range) and month-end projection. Quiet hours (<30% booked) get a suggested discount and estimated monthly gain; "Create offer" changes the price on the site straight away for those days/hours (badge on pitch cards, struck-through prices on slots). |
| **To-do bell & live updates** | Dashboard top bar and sidebar badges | Orders to send, low stock, reviews waiting, reported reviews (admin) and arrivals in the next 2 hours. New bookings/orders made on the site show a toast instantly — also across browser tabs. |

### Engagement, Arabic and dark mode (added in release 3)

| Feature | Where | How it connects |
| --- | --- | --- |
| **Discount codes** | `#/dash/codes` (`js/services/codeService.js`) | Pitch owners create booking codes (all their pitches or chosen ones); store owners create codes for their store. Percent or fixed amount, minimum spend, total uses, uses per player, start/end dates, pause/resume. Players enter booking codes at checkout (or via `?code=` in the link) and store codes in the cart; the code only discounts that owner's pitches/products, and the owner funds the discount. The list shows uses, discount given and revenue. Demo codes: `TURF22`, `TURFKIT10`, `GEZIRA100`. |
| **Waitlist** | Pitch page → tap a booked hour; `#/bookings` → Waitlist tab | When the hour frees up (cancellation, unblocked hour, ended series, freed tournament hour), everyone waiting gets a notification with a link straight to checkout. Entries show as Waiting / Free now / Booked / Expired. Partners see waitlist counts on the availability board. |
| **Loyalty points** | Account → Wallet; admin: Platform settings → Loyalty | 10 points per 10 EGP on played bookings, delivered orders and tournament fees (+20 for a review). Tiers: Bronze, Silver (×1.25), Gold (×1.5). 500 points minimum, 1 point = 0.20 EGP, redeemed as cash into the wallet. All values editable by the admin. |
| **Tournaments & leagues** | Site: `#/tournaments`; Dashboard: `#/dash/tournaments` (`js/services/tournamentService.js`) | Venues create knockout cups (4/8/16 teams) or leagues (3–10 teams, round robin) with entry fee, prize, dates and hours. Match hours are reserved on the pitch automatically. Teams register and pay from the site; the organiser draws fixtures, enters results (penalties for cup draws), and the next round / table / champion update live. Entry fees appear in Finance and the wallet; cancelling refunds every team. |
| **Dark mode** | Header moon/sun button, mobile menu, footer, dashboard sidebar (`js/core/theme.js`, `css/dark.css`) | Auto (follows the device), Light or Dark; remembered per device with no flash on load. |
| **Arabic (RTL)** | Header ع/EN button, mobile menu, footer, dashboard sidebar and sign-in (`js/core/i18n.js`, `js/data/i18n-ar*.js`, `css/rtl.css`) | Whole site and dashboard switch to Arabic, right to left, with the Cairo font. The translator works on rendered text (numbers, dates and money are kept and localised), so new screens only need dictionary entries. Proper names (pitches, teams, products, players) stay as written. When the backend arrives, move the dictionaries to translation files and store content in both languages. |

### Sellers, print on demand and Fantasy Pass (added in release 4)

| Feature | Where | How it connects |
| --- | --- | --- |
| **Seller mode** | Site: `#/sell` (footer → Shop → Sell on Super App) | Any user switches on seller mode: **print on demand**, **own store**, or **both**. Store name, link (`#/s/<handle>`), payout method and seller rules. One account and one wallet, plus a seller account in the dashboard. |
| **Design studio** | `#/sell/studio` | Upload artwork (PNG/JPG/WebP/SVG); place it on the front, back or both of a tee, oversized tee, hoodie or cap; check print quality (dpi); pick colours; write the listing; set the price with live profit. Save a draft or send it for review (Elite sellers publish instantly). |
| **Print catalogue** | Admin: `#/admin/catalog` | Products made by the print partner: partner cost (hidden), **base price** (includes the Super App margin), price limits, back-print and big-size extras, colours, sizes and design categories. |
| **Design review & reports** | Admin: `#/admin/designs` | Review queue (Pro first, Elite spot checks), reject with a reason, copyright strikes (level frozen 90 days, 3 strikes = suspension), customer reports from the product page, seller list with ID checks. |
| **Shop** | `#/shop?cat=designs`, product pages, `#/s/<handle>` | Live designs are sold as made-to-order products with garment mockups per colour and design sections. Seller store pages have follow and share. Big sizes cost more. Customers who refuse delivery twice must pay online. |
| **Print partner** | Dashboard sign-in "Print partner": `#/dash/print` | Print queue: download the print file (artwork + placement JSON) → printing → shipped (courier + tracking) → delivered or refused. Reprints for print faults are logged at the partner’s cost. Super App absorbs refused deliveries. |
| **Seller dashboard** | Dashboard sign-in as a seller (or `#/sell` → Seller dashboard): `#/dash/seller…` | Overview (level, progress, profit chart, best designs), designs, print orders, **wallet** (profit on hold 7 days after delivery, 500 EGP minimum withdrawal, ID check), **marketing calendar** (big matches, Egyptian occasions, weather, what’s selling). Sellers with a store also get Products, Store orders and Discount codes. |
| **Seller levels** | Seller page, dashboard, admin settings | Starter → Rising (10 items, 4.0★) → Pro (50, 4.3★) → Elite (200, 4.6★). Perks: more active designs (25/100/300/unlimited), base-price discount (0/10/20/35 EGP), store commission (12/11/10/8%), discount codes (2/5/15/unlimited), review priority, calendar depth and Fantasy Pass discount (Pro 50%, Elite free). All numbers are editable in Platform settings. |
| **Fantasy: two leagues + Fantasy Pass** | `#/fantasy` (league switcher in the header) | Play the Egyptian Premier League or the English Premier League (each has its own team and groups). Prize leagues need a **Fantasy Pass** (50 EGP/month per league, 80 for both). Prizes are sponsor prizes, vouchers and discount codes, never cash. |

Money: the customer pays the seller’s price. The base price stays with Super App (partner cost + our margin), and the rest is the seller’s profit. Store sales pay the commission of the seller’s level.
### Admin console (added in release 5)

A separate system for the Super App team at **`#/admin`**, with its own sign-in and session (admins no longer sign in to the partner dashboard). Every admin action is written to the audit log. Plan and decisions: `docs/PLAN-admin.md`.

| Area | Where | What the admin can do |
| --- | --- | --- |
| **Overview** | `#/admin` | Money through Super App, platform revenue by stream and day, what needs attention (withdrawals, designs, ID checks, reported reviews, tickets, samples, level review), platform counts, latest activity. |
| **Withdrawals** | `#/admin/payouts` | Approve (with a transfer reference) or reject (with a reason) withdrawals from pitch owners and sellers; checks for ID, suspension, disputes and payout account; pay the print partner what it is owed; credit or debit any partner or seller balance. |
| **Revenue** | `#/admin/revenue` | Booking commission, service fees, store commission, official store, print margin and Fantasy Pass; costs (refused prints, refunds, Super App promo codes); take rate; change vs the previous period; top partners and sellers; Excel export. |
| **Customers / partners / sellers** | `#/admin/customers`, `/partners`, `/sellers` | Block customers (no booking or ordering), force or waive upfront payment for printed items, reset refusals, notes, wallet credits. Suspend partners (pitches and store hidden), close single pitches, open any partner's dashboard. Sellers: levels, ID checks, strikes, feature in the shop, suspend. |
| **Orders & bookings** | `#/admin/orders` | Every booking, shop order and printed item with filters; open one to act on it; cancel printed items before they ship. |
| **Support & disputes** | `#/admin/support` | Tickets from customers (`#/support`, "Get help" on bookings and orders), partners and sellers (`#/dash/support`); reply, resolve, priority; refund a dispute and choose who pays (Super App or the partner/seller). |
| **Print on demand** | `#/admin/designs`, `/levels`, `/samples`, `/print`, `/catalog` | Design review, monthly **level review with a grace month**, **samples** (Pro 30% off, Elite one free a month), print queue and catalogue. |
| **Marketing & content** | `#/admin/broadcast`, `/promos`, `/content`, `/fantasy` | Broadcast messages (now or scheduled); Super App promo codes for the shop and/or bookings (Super App pays the discount); home banners, announcement bar, news and pickup-game moderation; fantasy prize leagues, sponsors, winners with voucher codes, Fantasy Pass sales. |
| **Analytics & audit** | `#/admin/analytics`, `/audit` | Platform analytics (money by product and day, booking heatmap, pitches, best sellers, cities, payments) with a full multi-sheet Excel export; audit log with filters and export. Sellers get their own analytics by level (Pro: advanced, Elite: + export). |

#### Admin console — round 2

| Power | Where |
| --- | --- |
| **Approve new pitches** before they go live; decline with a reason, and the owner can fix it and resend. **Approve new sellers** (on by default, can be switched off). | `#/admin/approvals` |
| **Commission deal per partner** (bookings and/or store %), used in every money calculation | Partners → partner drawer |
| **Hide one product** from a partner's store, with a reason | Partners → partner drawer → Products |
| **Tournaments** at every venue: details, results, cancel with refunds | `#/admin/tournaments` |
| **Super App Store** (official store) products and orders | `#/admin/store` |
| **Super Points**: add or remove a customer's points with a reason | Customers → customer drawer |
| **Delivery & service fees** and **maintenance mode** (pause bookings and/or shop checkout) | Platform settings |
| **Shop categories**: rename, icon, order, hide, add | Site content → Shop categories |
