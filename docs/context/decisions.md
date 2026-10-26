# Decisions log (by area)

Decisions the owner made, in short. Details and the full checklists are in the linked plans. Add new
decisions here (with the date) from any window.

## Product and money
- **Commission and fees:** booking commission 10% (can be set per partner), store commission 12%, service fee 3%, VAT 14% included.
- **Pitch partner offer (2026-10-18, sales window — replaces the booking line above for pitches):**
  - Booking commission is **5%, paid by the player**, not the pitch owner; the owner gets the full hour price.
  - Each pitch pays a **small monthly subscription** (amount not set yet).
  - **First month free**, on two conditions: Super App posters at the venue, and every booking goes through the app.
  - **Cash bookings:** the player pays a **deposit of 5%, at least 50 EGP**.
  - **No-show:** Super App covers **half the hour price** with the owner.
  - Payouts to the owner's bank (or InstaPay) weekly or monthly, as agreed.
- **Points:**
  - 20 points = 1 EGP.
  - Points go to vouchers only, from 2,000 points (= 100 EGP).
- **Withdrawals:**
  - The minimum is 500 EGP for everyone.
  - Seller profit is held 7 days after delivery.
- **Money limits (CEO):**
  - The CEO approves payouts over 10,000 EGP and refunds or credits over 1,000 EGP.
  - Anything over 50,000 EGP needs two approvals (CEO + CFO).
- **Package discount:** the CEO maximum defaults to 25% and can be changed in the CEO limits.
- **Fantasy:**
  - No cash prizes: prizes are sponsor rewards.
  - Pass: 50 EGP for one league, 80 EGP for both.
  - Egyptian and English Premier League.
  - Paid groups stay hidden until payments are live.
  - Club logos are not used for now.
  - Data comes from the free plan of API-FOOTBALL, with a usage bar in the CEO system.
- **Weekly package:**
  - The customer picks 1–12 matches, and the pitch owner sets which counts are allowed.
  - One QR covers the whole package.
  - A missed game counts as used, with one admin exception per package.
  - The owner sets a discount per count, and an admin approves it within the CEO maximum.
- **No private messages** between customers; communities cover that.

- **Walk-ins (2026-10-17):** a booking taken at the venue pays there, with no service fee and no commission.

- **Store paused (2026-10-26):** the store, sellers, print-on-demand factory and shipping are paused — the site shows "Coming soon" and their links are hidden (`SA.config.storeEnabled` in `js/core/namespace.js`), and the store APIs answer 423 `store_paused` (`Features:StoreEnabled` in `appsettings.json`). All code and data stay; set both to true to bring them back.

## Print on demand
- **Partner:** a contracted factory prints and ships, DTF only for now. One factory to start, with the code ready for several.
- **Courier:** Super App holds the courier contract (Bosta as a trial), behind a switchable adapter.
- **Designs:** any design is allowed, with categories, and every design is reviewed for trademarks and IP.
- **Returns:** printed items come back only for a print defect, and the factory reprints them.
- **Refused on delivery:** the platform absorbs the cost at first; repeat refusers must prepay.
- **Seller levels:** 4 levels (Starter, Rising, Pro, Elite) at 10 / 50 / 200 delivered sales, with design limits of 25 / 100 / 300 / unlimited.
- **Changes from the factory:** catalogue changes requested by the factory need admin approval before sellers see them.

## Accounts and team
- **Login:** a secure cookie; sign-in by email or mobile; staff sessions last 8 hours.
- **2FA:** two-step sign-in with an authenticator app is mandatory for every team account.
- **CEO:**
  - The CEO account is created from `.env`.
  - The CEO invites admins and sets their roles, permissions and money limits.
  - The CEO can give the Approvals permission to one admin.
- **Approvals:** pitch partners, sellers and factories apply with their ID (front and back), and admins approve or reject with a reason.

- **Partner staff (2026-10-17):** added by the email or mobile of an existing Super App account (they sign up first); one partner business per person; only the owner handles managers.

- **Test data (2026-10-17):** test accounts and test data stay in the local database until the backend is finished; they are deleted together with the demo data at launch (Phase 9).

- **SEO without a rewrite (2026-10-26):** the server writes search-engine HTML into the main public pages (home, pitches, a pitch, news, an article): title, description, canonical, share tags, JSON-LD and readable content, which the app replaces on load (`src/SuperApp.Api/Site/SeoPages.cs`). A React / Next.js move is not planned for now.

## Technology
- **Backend stack:** ASP.NET Core .NET 10 Web API with SQL Server (LocalDB in development), Clean Architecture. The existing site is served by the API at `/super-app/` and is unchanged.
- **Demo data:** it stays as seed data so the site is not empty; it is switched off before launch.
- **AI agents:** they run for real on Claude + n8n, with service accounts, API keys, permissions and limits. They are all paused until the CEO starts them.
- **Hosting:** free hosting for testing, then Hostinger (a VPS is needed for .NET and SQL Server).
- **Email:** emails are saved as files until the domain exists.
- **E-receipts (ETA):** built but switched off until the commercial register and tax card are ready.
- **Opening hours:** a pitch open after midnight keeps one row per day, with the closing hour up to 30 (26 = 2 AM).

## Website rules
- No WhatsApp or social links until the owner sends them.
- New pages go into the CEO "Every page" map.
- **Every action, payment and export:**
  - every action notifies everyone concerned
  - every payment gets a PDF
  - every export is Excel with a dashboard sheet

## Postponed
- Freelancer services: after the launch. The owner has his own idea, so ask him first.
- The 3D hoodie model: the GPU quota ran out (the tee is done).
- Referral and gift cards.
- The PWA: after the backend.
