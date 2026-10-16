# Front-end QA round — plan & findings

The user's decision (2026-10-16):
1. A focused QA round on the front-end now.
2. Then the missing features.
3. Then the backend.
4. Full QA (security, payments, load) after the backend.

## Method
- **Automated sweep:** `tools/qa-sweep.js` opens every page of the 7 systems and follows links (one page per route, plus tab variants). It records:
  - console errors
  - error pages
  - things sticking out of the screen
  - visible English text in Arabic
- **Passes:**
  1. English · light · desktop
  2. Arabic · light · desktop
  3. Arabic · dark · mobile 375px
  4. English · dark · mobile
- **Journeys by hand:**
  - booking + weekly package
  - printed tee order → factory → courier → delivered
  - new pitch partner
  - new seller
  - dispute + refund
  - withdrawal + approvals
  - CEO kill switch

## Checklist
- [x] Pass 1 — EN light desktop
- [x] Pass 2 — AR light desktop
- [x] Pass 3 — AR dark mobile
- [x] Pass 4 — EN dark mobile
- [x] Journeys
- [x] Fix everything found, re-run the sweep, commit

## Findings
### Sweep results
| Pass | Pages | Result |
| --- | --- | --- |
| 1 · EN light desktop | 254 | no console errors or error pages |
| 2 · AR light desktop | 271 | 2 technical problems, about 40 untranslated UI strings |
| 3 · AR dark mobile | 272 | community page wider than the phone; same map error |
| 4 · EN dark mobile (after the fixes) | 276 | clean |

The pages cover 128 of the 150 routes. The rest redirect (`/dash/print`, `/admin/print`, `/dash/seller/*`) or are admin-only for partners.

### Fixed
- **Map error:** leaving the pitch editor threw "Map container is being reused". Map creation and teardown are now safe (`js/components/map.js`).
- **Admin and partner charts in Arabic:** the axis numbers stuck out past the screen edge. The margins are now mirrored for right-to-left.
- **Community page on phones:** it was 40px wider than the screen. The column is now limited to the screen width.
- **Pitch partners weren't told about new bookings or weekly packages:** they now get a notification, and there is a new "Bookings" notification category.
- **Arabic gaps:**
  - empty states: wishlist, profile, matches, sellers
  - the seller dashboard texts
  - the tournament draw notes
  - the fantasy table note
  - community member counts
  - slot percentages
  - balance notifications
  - the CEO calendar owners and briefing deadlines
  - audit-log lines

  New file: `js/data/i18n-ar-qa.js`.

### Journeys (all pass)
- **Weekly package:**
  - payment and receipt PDF entry
  - customer notification
  - package code
  - partner check-in with the package QR
- **Printed tee:**
  - order → factory (printing → QC → packing → ready)
  - Bosta booked automatically
  - out for delivery → delivered
  - 6 customer notifications and 4 seller notifications
  - seller profit pending
- **New pitch partner:** application → shows in admin Approvals → approved → partner account and live pitch → customer told.
- **Dispute:**
  - partner told
  - refund to the customer's wallet with a receipt
  - deducted from the partner's balance, and the partner told
  - refunds over the limit go to the CEO
- **Withdrawals, approvals, kill switches:** tested in the CEO round.

### Left as is (not UI)
English text the users or the demo seed wrote: posts, tickets, tournament rules, reasons. It shows as written.
