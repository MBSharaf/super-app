# Site-wide fixes — the user's check-up (2026-10-15)

The user found these on desktop, in dark mode, in English. Every fix is also checked on mobile, in Arabic and in light mode.

Items are numbered as in the user's note: **G** general, **M** main site, **P** partner dashboard, **A** admin, **F** factory. Work from the first unticked item. Decisions and answers from the user are under each item.

## Waiting for the user's answers
- [ ] Q1 Separate dashboards (G1): confirm the proposal.
- [ ] Q2 Pitch partner application (G2): confirm the required fields.
- [ ] Q3 Seller application (G2): confirm the required fields.
- [ ] Q4 Weekly bookings (M1): the missed-game rule and the admin exception.
- [ ] Q5 Points (M8): where vouchers can be used, and the minimum.
- [ ] Q6 Support email (M9): test@gmail.com (the note says test#gmail.com).
- [ ] Q7 F7 is empty in the note: what is the issue?

## General
- [ ] G1 Pitch partners and sellers are mixed in one "Partner dashboard". Make two separate dashboards with two separate buttons.
- [ ] G2 Profile → become a seller / become a pitch partner, each with an application form and requirements. Admin approval. A pitch partner can also become a seller from inside their dashboard. "Become a seller" must not open a store automatically.

## Main site
- [ ] M1 Weekly booking (for example 12 games):
  - The confirmation and invoice show only 1 game.
  - The QR code is valid for the booked number of games, then it expires.
  - The admin sees how many times it was scanned.
  - A missed game counts as used. The admin can make an exception once, after agreeing it with the pitch manager.
- [ ] M2 Joining a game:
  - The "I'll arrive 10 minutes early" checkbox should not be mandatory.
  - No PDF after paying. Every payment needs a PDF.
- [ ] M3 Profile: "My games" — the matches I posted, to manage them easily.
- [ ] M4 Home "Live & next up": the UI looks bad. Redesign it.
- [ ] M5 Footer:
  - Remove the "Animations: On" link.
  - "Theme: Dark" and "العربية" have no space between them.
- [ ] M6 Header: the profile picture is not aligned.
- [ ] M7 Active/hover states are not aligned (site-wide).
- [ ] M8 Points:
  - 20 points = 1 EGP (it was 5 = 1).
  - No cash. Points can only become a voucher used in the app (store, tournaments, bookings, POD, Fantasy Pass, …).
  - Update every place that shows points.
- [ ] M9 Help & support:
  - Send an email to test@gmail.com (placeholder).
  - The admin gets a notification.
  - The customer gets an email saying "we reply within 24 hours".
- [ ] M10 A PDF invoice or receipt for every payment or transaction, even small ones.
- [ ] M11 Profile: a "Partner dashboard" entry, animated and highlighted (today it is only in the footer).

## Partner dashboard (seller)
- [ ] P1 "Back to the site": highlighted and animated.
- [ ] P2 Store link: add a copy button.
- [ ] P3 Tagline → "Tags", with a working tag system across the site (search, filters, tracking).
- [ ] P4 Analytics: give basic analytics at every level; Pro and Elite get more.
- [ ] P5 "Print orders" → "Orders" (POD + own store), and in the overview too.
- [ ] P6 Wallet/withdraw: "Print-on-demand profit" → their orders (POD or own store), with a PDF.
- [ ] P7 Notifications section: explain it, then keep or change it.

## Admin
- [ ] A1 Sign-in screen: 3D animation (it is static now).
- [ ] A2 Overview, "Where revenue comes from": the bar animation overflows the text.
- [ ] A3 Withdrawals:
  - Show who is asking (player, pitch owner, seller, factory) with full details.
  - Approve: notification + PDF + transfer time (SLA).
  - Reject: a mandatory reason, and a notification with the reason.
- [ ] A4 Revenue export: Excel with a dashboard sheet (formulas and charts that update automatically) plus full details.
- [ ] A5 Customers:
  - The search UI looks bad; the filters must work.
  - Excel export with a dashboard.
  - Every admin action in the customer drawer notifies the customer.
- [ ] A6 Partners:
  - Separate pitch owners, sellers and factories, each with details and an overview.
  - The same search UI and export as A5.
  - Notifications.
- [ ] A7 Tournaments: explain the admin's powers there.
- [ ] A8 Support & disputes:
  - Every reply or action notifies the other side (pitch owner, seller or factory) and the customer.
  - Show clearly who opened the dispute and against whom.
  - The chat comes later.
- [ ] A9 Super App Store: explain it. It is Super App's own store, so the admin gets full access.
- [ ] A10 Reviews:
  - Same search/UI issue.
  - Explain what is reviewed (pitches).
  - Notifications.
- [ ] A11 Design review:
  - The UI changes a few seconds after load.
  - Approve and reject (with reason) notify the seller.
  - Reports get the same fix and notifications.
  - Suspending a seller notifies them with the reason.
- [ ] A12 Print catalogue: explain it.
- [ ] A13 Free slots & games: explain it.
- [ ] A14 Analytics: fix the bars UI.
- [ ] A15 Platform settings and Audit log: admin manager only. This comes with the backend and the admin team roles.

## Factory
- [ ] F1 Sign-in screen: 3D animation.
- [ ] F2 Sidebar: low colour contrast, nothing is readable.
- [ ] F3 Production: notifications for everyone concerned (customer, seller, admin).
- [ ] F4 Scan a job: explain it.
- [ ] F5 Shipping label:
  - Add the Super App logo.
  - Notifications for every action.
- [ ] F6 Reports and Money: Excel export with a dashboard.
- [ ] F7 Production — the note is empty: ask the user.
- [ ] F8 Supervisor vs owner: explain the difference.
- [ ] F9 Every factory PDF (ticket, label) gets the Super App logo.
- [ ] F10 The sidebar UI across the whole factory system.
