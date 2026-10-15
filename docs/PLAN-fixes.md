# Site-wide fixes — the user's check-up (2026-10-15)

The user found these on desktop, in dark mode, in English. Every fix is also checked on mobile, in Arabic and in light mode.

Items are numbered as in the user's note: **G** general, **M** main site, **P** partner dashboard, **A** admin, **F** factory. Work from the first unticked item. Decisions and answers from the user are under each item.

## The user's answers (2026-10-15)
- **Q1, two dashboards — agreed.**
  - Pitch partners use `/dash`; sellers use `/seller`. Each has its own sign-in and its own button.
  - A pitch partner can apply to sell from inside their dashboard. After approval, one button switches between the two dashboards.
- **Q2, pitch partner application — agreed, without the commercial register and tax card.**
  - Fields: owner name, phone, email, national ID number + photo, pitch name, area, address, map pin, number and format of pitches, photos, payout method (bank or InstaPay), terms.
  - The admin approves, or rejects with a reason.
- **Q3, seller application — agreed, without a store link** (the store doesn't exist yet).
  - Fields: name, store name, type (POD / own products / both), national ID number + photo, phone, payout method, about the store, terms.
  - The admin approves.
- **Q4, weekly package.** Example: a 12-game package where the customer played 5 and didn't come to game 6. Game 6 counts as used.
  - The admin can make **one** exception per package (the missed game isn't counted, so they get it back).
  - A new package allows a new exception.
- **Q5, points.**
  - 20 points = 1 EGP.
  - The smallest voucher is 100 EGP = **2,000 points**.
  - Vouchers work in bookings, the store, POD, tournaments, Fantasy Pass and game shares. No cash.
- **Q6, support email.** Use test@gmail.com. Real sending comes with the backend; for now it is simulated end to end.
- **Q7, F7.** Skipped for now.

## General
- [x] G1 Pitch partners and sellers are mixed in one "Partner dashboard". Make two separate dashboards with two separate buttons.
- [x] G2 Profile → become a seller / become a pitch partner, each with an application form and requirements. Admin approval. A pitch partner can also become a seller from inside their dashboard. "Become a seller" must not open a store automatically.

## Main site
- [x] M1 Weekly booking (for example 12 games):
  - The confirmation and invoice show only 1 game.
  - The QR code is valid for the booked number of games, then it expires.
  - The admin sees how many times it was scanned.
  - A missed game counts as used. The admin can make an exception once, after agreeing it with the pitch manager.
- [x] M2 Joining a game:
  - The "I'll arrive 10 minutes early" checkbox should not be mandatory.
  - No PDF after paying. Every payment needs a PDF.
- [x] M3 Profile: "My games" — the matches I posted, to manage them easily.
- [x] M4 Home "Live & next up": the UI looks bad. Redesign it.
- [x] M5 Footer:
  - Remove the "Animations: On" link.
  - "Theme: Dark" and "العربية" have no space between them.
- [x] M6 Header: the profile picture is not aligned.
- [x] M7 Active/hover states are not aligned (site-wide).
- [x] M8 Points:
  - 20 points = 1 EGP (it was 5 = 1).
  - No cash. Points can only become a voucher used in the app (store, tournaments, bookings, POD, Fantasy Pass, …).
  - Update every place that shows points.
- [x] M9 Help & support:
  - Send an email to test@gmail.com (placeholder).
  - The admin gets a notification.
  - The customer gets an email saying "we reply within 24 hours".
- [x] M10 (customer side done; partner/seller payment advice PDFs added with A3: every payment/refund/top-up/voucher has a receipt PDF; partner side with A3/P6) A PDF invoice or receipt for every payment or transaction, even small ones.
- [x] M11 Profile: a "Partner dashboard" entry, animated and highlighted (today it is only in the footer).

## Partner dashboard (seller)
- [x] P1 "Back to the site": highlighted and animated.
- [x] P2 Store link: add a copy button.
- [x] P3 Tagline → "Tags", with a working tag system across the site (search, filters, tracking).
- [x] P4 Analytics: give basic analytics at every level; Pro and Elite get more.
- [x] P5 "Print orders" → "Orders" (POD + own store), and in the overview too.
- [x] P6 Wallet/withdraw: "Print-on-demand profit" → their orders (POD or own store), with a PDF.
- [x] P7 Notifications section: explain it, then keep or change it.

## Admin
- [x] A1 Sign-in screen: 3D animation (it is static now).
- [x] A2 Overview, "Where revenue comes from": the bar animation overflows the text.
- [x] A3 Withdrawals:
  - Show who is asking (player, pitch owner, seller, factory) with full details.
  - Approve: notification + PDF + transfer time (SLA).
  - Reject: a mandatory reason, and a notification with the reason.
- [x] A4 Revenue export: Excel with a dashboard sheet (formulas and charts that update automatically) plus full details.
- [x] A5 Customers:
  - The search UI looks bad; the filters must work.
  - Excel export with a dashboard.
  - Every admin action in the customer drawer notifies the customer.
- [x] A6 Partners:
  - Separate pitch owners, sellers and factories, each with details and an overview.
  - The same search UI and export as A5.
  - Notifications.
- [x] A7 Tournaments: explain the admin's powers there.
- [x] A8 Support & disputes:
  - Every reply or action notifies the other side (pitch owner, seller or factory) and the customer.
  - Show clearly who opened the dispute and against whom.
  - The chat comes later.
- [x] A9 Super App Store: explain it. It is Super App's own store, so the admin gets full access.
- [x] A10 Reviews:
  - Same search/UI issue.
  - Explain what is reviewed (pitches).
  - Notifications.
- [x] A11 Design review:
  - The UI changes a few seconds after load.
  - Approve and reject (with reason) notify the seller.
  - Reports get the same fix and notifications.
  - Suspending a seller notifies them with the reason.
- [x] A12 Print catalogue: explain it.
- [x] A13 Free slots & games: explain it.
- [x] A14 Analytics: fix the bars UI.
- [~] A15 (deferred to the backend + admin team roles — needs real sign-in) Platform settings and Audit log: admin manager only. This comes with the backend and the admin team roles.

## Factory
- [x] F1 Sign-in screen: 3D animation.
- [x] F2 Sidebar: low colour contrast, nothing is readable.
- [x] F3 Production: notifications for everyone concerned (customer, seller, admin).
- [x] F4 Scan a job: explain it.
- [x] F5 Shipping label:
  - Add the Super App logo.
  - Notifications for every action.
- [x] F6 Reports and Money: Excel export with a dashboard.
- [x] F7 Skipped (the user said leave it for now).
- [x] F8 Supervisor vs owner: explain the difference.
- [x] F9 Every factory PDF (ticket, label) gets the Super App logo.
- [x] F10 The sidebar UI across the whole factory system.
