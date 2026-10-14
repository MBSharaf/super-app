# Seller layer: print-on-demand, stores, levels and fantasy changes

Agreed with the owner on 2026-10-14. This file is the build checklist, so work can stop and resume between sessions. Tick items as they are finished; each chunk must leave the site working.

## Decisions
- One account for everyone. Any user can switch on **seller mode** and choose POD, their own store, or both.
- **Print partner:** a contracted partner prints and ships. The partner gets its own dashboard login (print queue only); an API comes later.
- **Designs:** any design is allowed. The shop has categories (Sports, Anime, Egyptian phrases, Gaming, …), and the admin can add categories.
- **Money:**
  - POD: our margin sits inside the base price (e.g. hoodie base 300; seller sells at 500 and earns 200).
  - Own store: normal commission.
  - The admin can edit every number.
- **Holding period:** seller profit is pending until delivery, then 7 more days, then available.
- **Minimum withdrawal:** 500 EGP for all levels. ID check before the first withdrawal.
- **Refused cash on delivery (POD):** the platform absorbs the cost. Customers who refuse repeatedly must pay upfront.
- **Returns:** printed items are returnable only for print defects; the partner reprints at its own cost.
- **Design review:** every design goes through admin review (approve, or reject with a reason). Product pages have a copyright/trademark report button; repeat offenders get strikes and then a suspension.
- **Fantasy:**
  - No cash prizes. Prizes are sponsor rewards and discount coupons.
  - Subscription: 50 EGP a month for one league, 80 EGP a month for both.
  - Add an **English Premier League** fantasy next to the Egyptian league; the user picks the league. Mock data for now, API-FOOTBALL later.
  - The owner is checking the law.

## Seller levels
The level is set by the **total number of delivered items sold** (lifetime). Rating and copyright strikes can freeze a level-up.

| | New | Rising | Pro | Elite |
|---|---|---|---|---|
| Delivered sales needed | 0 | 10 | 50 | 200 |
| Minimum rating to level up | — | 4.0 | 4.3 | 4.6 |
| Active designs | 25 | 100 | 300 | Unlimited |
| Design review | Normal | Normal | Priority | Instant publish + spot check |
| POD base-price discount | — | 10 EGP | 20 EGP | 35 EGP |
| Store commission | 12% | 11% | 10% | 8% |
| Active discount codes | 2 | 5 | 15 | Unlimited |
| Search boost | — | Small | Medium | Top |
| Badge | — | ✓ | ✓ | ✓ special |
| Home page and event collections | — | — | Eligible | Featured |
| Marketing calendar | Matches + holidays | + weather | + trending searches | + early event alerts |
| Analytics | Basic | Basic | Advanced | Advanced + export |
| Bulk upload | — | — | ✓ | ✓ |
| Samples | At base price | At base price | 30% off | 1 free a month |
| Team kits (names and numbers) | — | — | ✓ | ✓ |
| Early access to new products | — | — | — | ✓ |
| Fantasy subscription | Full price | Full price | 50% off | Free |
| Support | Normal | Normal | Priority | Account manager |

- Copyright strike: the level is frozen for 90 days.
- The dashboard shows a progress bar ("12 sales to Pro").
- The admin can edit every value in Platform settings.

## Build chunks (in order)
- [x] **1. Data and services** (done 2026-10-14; console-tested: levels, profit, review, orders, refusal → prepay, strikes, withdrawal rules)
  - `js/data/pod.js`: catalogue, categories, levels, 3 demo sellers, original SVG art.
  - db migrate: `seedSellers()` adds sellers, designs, podOrders, podCatalog, podCategories, reports, sellerPayouts and podCustomers; seller accounts acc-s1..3 and printer acc-print; settings.pod / sellerLevels / fantasySubs.
  - `js/services/sellerService.js` (SA.services.sellers): start, mine, levelOf, perks, canAddDesign, strike, follow.
  - `js/services/podService.js` (SA.services.pod): catalogue, designs (draft/submit/approve/reject), reports, place/advance/reprint orders, earnings, withdraw.
  - Store state has a new `following` field.
- [x] **2. Seller onboarding** (done 2026-10-14; browser-tested)
  - `js/pages/sell.js`, route `#/sell`, footer link "Sell on Super App".
  - Hero, mode choice, store name and link with a live availability check, payout method, rules and agreement.
  - Earnings calculator and level ladder.
  - The welcome view shows the level card and next steps.
  - Shared helpers in `SA.ui.seller`: `ladder`, `levelCard`, `MODES`.
  - CSS: `css/pages/seller.css`.
  - Fixed: SVG art no longer gets a duplicate font-family.
- [x] **3. POD catalogue in admin** (done 2026-10-14; browser-tested)
  - `js/pages/dash/podCatalog.js`, route `#/dash/pod-catalog`, admin nav entry "Print catalogue".
  - Cards show the margin; edit modal for product, cost, base, limits, areas, sizes and colours; hide/show; categories (add).
  - `js/components/garment.js` (`SA.ui.garment`): SVG tee/hoodie/cap mockups with art placement, plus `clampPlace` and `area`. The studio uses these.
  - Designs on hidden catalogue products are excluded from `pod.live()`.
  - Dashboard sign-in lists only admin and owner roles for now (`SIGNIN_ROLES` in dash/common.js). **Add 'seller' and 'printer' in chunks 7–8.**
- [x] **4. Design studio** (done 2026-10-14; browser-tested with a real uploaded PNG)
  - `js/pages/studio.js`, routes `#/sell/studio` and `#/sell/studio/:id`.
  - Upload or drag-drop artwork (PNG/JPG/WebP/SVG, 8 MB max); it is downscaled to 1000 px WebP for storage and keeps the original size for the DPI check.
  - Front/back/both placement; drag, arrow keys, size slider, centre; colour previews.
  - Listing details; price with live profit (back print included); save draft or submit (Elite sellers publish instantly).
  - The seller page (`sell.js` welcome view) now has a "My designs" panel (edit / pause / resume / delete) and exports `SA.ui.seller.designStatus`.
- [x] **5. Review queue, reports and strikes** (done 2026-10-14; browser-tested)
  - `js/pages/dash/podReview.js`, route `#/dash/pod-review` with tabs: queue (priority lanes + Elite spot checks), reports (dismiss / take down + strike), sellers (level, strikes, suspend/reactivate).
  - Admin nav "Design review" with a badge; dashService alerts entry `pod-review`.
  - Seed data: 2 designs in review (ds-12, ds-13) and 1 open trademark report on ds-2.
  - **To do in chunk 6:** when a seller with an own store is suspended, hide their store products in the shop too.
- [x] **6. Shop integration** (done 2026-10-15; browser-tested: product page, 2XL pricing, COD order → podOrders, tracking, store page, follow, design sections)
  - shopService: live designs become virtual products (id `pod-<designId>`, images `pod:<id>:<colour>[:back]`); `media.u` renders them through `SA.services.pod.mockUrl`, which caches the garment mockup as an SVG data URL.
  - Category "Designs" plus the `dcat` filter for design sections; the `seller` filter; search covers tags.
  - Cart unit price includes the big-size extra.
  - placeOrder: printed lines go to `pod.place` (no store sale, no stock); COD is blocked when `needsPrepay`.
  - Tracking shows a "Printed for you by Super App" group driven by podOrders statuses.
  - Product page: "Designed by", made-to-order assurances, the mockup switches with the colour, and a "Report this design" form.
  - Shop landing has a "Designs by Egyptian creators" strip; the designs category has section chips and a "Start selling" banner.
  - `js/pages/sellerStore.js`, route `#/s/:handle`: cover, level, stats, follow, share, designs/products tabs.
  - Suspending a seller also suspends their own store. The "Sold by" link goes to `/s/handle` when the store belongs to a seller.
- [x] **7. Print partner dashboard and order flow** (done 2026-10-15; browser-tested: printer sign-in, start, ship with tracking, deliver, refuse → loss + refusal count, reprint keeps status/profit, admin view)
  - `js/pages/dash/print.js`, route `#/dash/print`. Tabs: to print / on the way / delivered / refused.
  - Each item can download its print file (artwork plus a job JSON with placement); there is also "start all new".
  - Admin sees prices, cost, seller profit and refused-delivery losses.
  - dash/common.js:
    - `SIGNIN_ROLES` includes printer; `ROLE_LABEL`, `ROLE_NAV` and `ROLE_HOME`.
    - The guard sends role users to their home page unless `page.roles` allows them.
    - Admin nav has "Print orders".
  - dashService alerts: a printer gets a 'print' badge.
  - pod.advance notifies the customer on this device when an item ships or is delivered. `pod.reprint` only logs.
  - Seed podOrders now carry courier, tracking and loss.
- [x] **8. Seller dashboard** (done 2026-10-15; browser-tested with 3 demo sellers and a new site seller)
  - `js/pages/dash/seller.js`, routes:
    - `#/dash/seller`: level card, wallet, KPIs, 30-day profit bars, best designs, latest orders, ladder.
    - `#/dash/seller/designs` and `#/dash/seller/orders`.
    - `#/dash/seller/wallet`: POD pocket, withdraw (500 minimum, ID check required), ID form (name, 14-digit ID, front/back photos), withdrawals table; link to the store wallet when the seller has a store.
  - Navigation: role nav is a function of the seller's mode. Store pages (dashStore / ProductEdit / Orders / Wallet / Codes) open only for sellers with a store (`roles` can be a function).
  - `sellers.current()` (the studio uses it), `openDashboard()`, `submitKyc`, `verifyKyc`.
  - The admin sellers tab has an ID-check column (Verify / Reject) and a 'kyc' alert.
  - Store commission follows the seller's level (`storeRate` in dashService). `levelOf` is memoised and cleared on any db change.
  - Code limits per level are enforced in `codes.save`.
  - The site `/sell` page has a "Seller dashboard" button.
  - **Known limit:** discount codes only work on store products, not POD designs (designs have no storeId). Possible later: seller codes for designs.
- [x] **9. Marketing calendar** (done 2026-10-15; browser-tested with Rising, Pro and Elite)
  - `js/pages/dash/marketing.js`, route `#/dash/seller/calendar`, seller nav "Marketing calendar".
  - Big matches (from the schedule service, importance ≥ 40) plus Egyptian occasions and shopping moments; lunar dates are marked expected.
  - Horizon grows with the level: 30 / 45 / 60 / 120 days; Elite gets early alerts.
  - Weather (Cairo monthly averages + product tip) from Rising up.
  - "What's selling" (tags, sections, products, colours from the last 30 days of print orders) from Pro up.
  - Locked cards show which level unlocks each feature.
- [x] **10. Fantasy** (done 2026-10-15; browser-tested both leagues, pass purchase, seller discounts, all fantasy pages)
  - `js/data/fantasy.js` is now `build(cfg)`; EGY and EPL datasets go into `SA.data.fantasyLeagues`.
    - EPL: 20 clubs, 38 gameweeks, fixture ids `fx-epl-*`.
    - Leagues have `kind: 'official' | 'pass'`, `prizeValue`, and sponsor/voucher prizes (no cash, no entry fees).
  - fantasyService:
    - `league()`, `use(key)`, `leagues()`, `leagueInfo()`; per-league state (`fantasy` / `fantasyEpl`); `byId`, `teamById`, `D` are getters.
    - Fantasy Pass: `passPlans` (50 / 50 / 80 from settings.fantasySubs, minus the seller level discount), `pass()`, `passCovers()`, `buyPass` (each league has its own end date, +30 days).
    - `joinOfficial` needs a pass for 'pass' leagues.
  - Pages:
    - fantasy.js: league switcher in the header; it resets swap and filter state.
    - fantasyLeagues.js: cards show 1st prize and prize value; Fantasy Pass modal (`SA.ui.fantasyPass.open`); league detail text.
    - fantasySquad.js: the draft resets on league change.
    - home.js: the teaser uses 'pass' leagues.
  - Store state: `fantasyEpl`, `fantasyLeague`, `fantasyPass`.
  - Admin Platform settings: seller rules (hold days, minimum withdrawal, refusal limit), Fantasy Pass prices, and the seller levels table editor (validated).
- [x] **11. Arabic, checks, README** (done 2026-10-15)
  - `js/data/i18n-ar-sellers.js` covers all new screens; the crawl left only proper names, product names and tags.
  - The first level was renamed "Starter" in English (shown as مبتدئ), because "New" already means جديد; db migrate renames it.
  - Checked RTL on mobile and light/dark; no errors in any crawl.
  - README has a "release 4" section.
- **Layer complete.** Ideas for later: seller codes for POD designs, team-kit ordering flow (Pro+), print partner API, real ID verification, payouts via a payment provider.

## Review round 1 (2026-10-15): real product photos + 3D
- `js/components/photoMock.js` (`SA.ui.photoMock`, `SA.ui.garmentView`) uses real blank-garment photos:
  - Tee: Pexels 12025472 / 12039633 (flat lay, front / back).
  - Hoodie: Pexels 8217410 / 8217415 (model, front / back).
  - Cap: Unsplash photo-1691256676359 (front).
  - Each outline was traced by hand and is snapped to the photo edges; the face opening is a hole and skin is filtered out.
  - Recolouring keeps the photo shading; the print is multiplied by the fabric shading and clipped to the garment.
  - `print` rects match the catalogue print areas, so existing placements still work.
- Used by: shop images (`pod.mockUrl` → `compose`), the 3D viewer faces, the studio canvas (photo-space SVG; drag converts with `toTemplate`), the product picker, catalogue, review, print queue and My designs.
- Photos load in the background at app start (around 1–2 s). Drawn mockups (`garment.js`) show until then; `onReady` swaps them (app rerender, except in the studio, which swaps its own canvas).
- Production: pre-render these composites on the server and host the base photos on our own CDN.
## Review round 2 (2026-10-15): store settings for sellers
- New page `#/dash/seller/store` ("Store settings" in the seller nav): name, tagline, link (live availability; the old link stops working), about, logo and cover upload/remove (`SA.ui.pickImage` in `js/components/imagePick.js`: crop + compress), cover position, store colour (8 presets + custom), featured design, selling mode, take-a-break (until date + message), payout account. It shows a live header preview.
- `sellers.update` accepts handle/tagline/accent/featured/vacation*; it syncs the own-store record and the account company name. New: `sellers.setPayout`, `sellers.onBreak` (on break → designs and products hidden from the shop).
- The store page `#/s/:handle` shows: accent colour, tagline, featured design first with a badge, and a break notice. The owner gets "Edit store" plus quick cover/logo change buttons on the page.
- The `/sell` page has a "Store settings" button.
## Remaining work in the seller layer (decided 2026-10-15)
**Now:**
- [x] Seller notification system (done 2026-10-15): `js/services/notifyService.js` (`SA.services.notify`) with a db `notifications` collection per account.
  - Categories: designs, orders, money, level, store, account. Account notices are always on; the rest can be muted per account in preferences.
  - `sweep()` (start-up, data changes, every minute) handles profit released after the hold, level up/down, near-next-level, and unfrozen.
  - Events: design submitted/approved/rejected/strike; POD order new/printing/shipped/delivered/refused/reprinted; withdrawal requested; catalogue product paused/back/base-price change/new product; seller welcome; ID received/verified/rejected; suspended/reactivated; store orders, low stock / sold out and discount code used (owners and sellers); admin (design to review, report, ID check); print partner (new print job).
  - UI: the dashboard bell shows notifications + to-do (mark all read); every role has "Notifications" in the sidebar with an unread badge; `#/dash/notifications` has filters, day groups, mark read / delete, preferences and browser pop-ups (Notification API). New notifications show as live toasts, also across tabs.
  - A seller who is the shopper on this device also gets a copy in the site bell. Email/SMS/WhatsApp come with the backend.
- [x] Search ranking by level (done 2026-10-15): `podProduct.rank` = (sales×6 + views/60 + rating×8 + featured 15) × (1 + boost×0.25) + boost×30. The shop's default "featured" sort uses it (designs ×8 to sit on the same scale as shop products). Pro/Elite cards show a "Pro creator" / "Elite creator" badge.

**Saved for later (do not build yet):**
- Payout approvals for seller withdrawals → part of the **admin system** (next layer).
- Followers and customers (follow notifications, real buyer reviews and ratings) → the user wants to rethink this first.
- Phase 3 → much of it depends on admin work: monthly level review + grace month, sample orders (Pro 30% off, Elite one free a month), bulk upload, advanced analytics + export, early access to new catalogue products.
- Phase 4 → not important now: team kits for tournaments, seller discount codes on POD designs (sellers already have store codes).
- After the "now" items: discuss the **admin system** and what it can do (users, money/payouts, orders, fantasy/sponsors, content, promotions, admin team roles, support).