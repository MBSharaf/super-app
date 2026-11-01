# Pitch deposits, QR check-in time and 1 point per EGP (owner's request, 2026-11-01)

Status: **done on the demo site (GitHub Pages)**. Backend (`src/`) still charges the full amount — see "Backend follow-up".

## 1. Deposit only
- [x] `bookingService.depositFor(total)`: deposit = max(20% of total, 100 EGP), capped at the total; `payAtPitch` = the rest. Rates in `db.settings().deposit` (`{ pct: 0.2, min: 100 }`).
- [x] Checkout (`#/book/:id`) charges only the deposit; shows "Pay now — deposit" and "Pay at the pitch"; button "Pay deposit X".
- [x] Weekly packages: deposit per game, summed and paid once; the rest at the pitch each week.
- [x] Booking page, invoice PDF, notifications (player and owner) show deposit + rest at the pitch.
- [x] Cancellation (customer or venue) refunds the deposit under the cancellation policy.
- [x] Venue money: the deposit's pitch part (deposit − service fee) goes to the partner wallet less commission; the rest is cash at the venue.

## 2. QR check-in by the pitch owner
- [x] Owner opens Partner dashboard → Check-in on the phone and scans the QR (camera, photo or typed ID).
- [x] The scan shows timing: early ("Kick-off in N min"), on time, or late ("15 min late — 45 min left of the booking. It still ends at 6 PM").
- [x] "Check in & collect X EGP" records the check-in time and the cash collected (`balancePaidAt`, `balancePaid`).
- [x] Live countdown on the scan result, the arrivals list and the player's booking page; the player is notified.

## 3. Super Points: 1 point per EGP + pop-up
- [x] `paymentService.charge` → `loyalty.earn(amount)` for every payment (voucher part excluded); `refund` → `loyalty.reverse` (goodwill credits keep points).
- [x] Rest paid at the pitch and COD orders earn when paid. Top-ups earn nothing. Older activity keeps the old rule.
- [x] `js/components/pointsPop.js`: "Congratulations! You earned N Super Points" pop-up after every earning; waits for dialogs, never on business dashboards.

## Tests
`/tmp` Playwright script (run 2026-11-01, 37 checks): deposit rule, checkout, charge = deposit, pop-up, check-in 15 min late on a phone screen, collection, second scan, player countdown, points = total paid, cancel refunds deposit and takes back points, shop payment points, top-up no points, venue money, Arabic strings, no JS errors.

## Backend follow-up (not done)
The ASP.NET API (`src/`, live mode on localhost) still charges full price and has no deposit/collection fields. It needs: deposit fields on Booking, charge the deposit, a check-in endpoint that records collection and timing, and points per EGP. The owner applies migrations himself.
