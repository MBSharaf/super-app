# Admin system — build plan

Agreed with the user on 2026-10-15.
- **Separate system:** the admin has its own console at `#/admin`, with its own sign-in, layout and session, apart from the Partner Dashboard (`#/dash`).
- **Scope:** parts 1, 2 and 3 of the proposal, plus part 4 except bulk design upload.
- **Deferred:**
  - admin team roles and permissions (the user may work alone);
  - bulk design upload.

## Decisions
- `#/admin` is admin-only. Admin accounts can no longer sign in to the partner dashboard. The admin can open any partner's dashboard ("Open their dashboard"), and that is logged.
- Existing admin pages (design review, print catalogue, print queue, platform settings, partners) move into the admin console.
- The audit log records every admin action: who, what and when.
- Withdrawals (partners, sellers, print partner) stay "processing" until an admin approves or rejects them. A rejected withdrawal goes back to the balance.
- Refunds and disputes: the admin decides who pays (Super App, the partner or the seller). Partner and seller shares are taken from their wallet as an adjustment.
- Customers:
  - an account can be blocked (no bookings or orders);
  - pay-upfront can be forced on or off, overriding the automatic refusal limit;
  - private notes can be added.
- Promo codes:
  - platform-wide codes funded by Super App, for the shop, bookings or both;
  - the old fixed shop codes (KICKOFF10, PITCH50, FREESHIP) become editable platform codes;
  - on bookings, the partner still earns on the full price.
- Fantasy:
  - sponsors;
  - prizes per league;
  - announcing winners creates voucher codes;
  - Fantasy Pass sales are recorded for the revenue report.
- Seller levels:
  - promotion is immediate (as today);
  - a drop below a level's requirements first gives a **grace month** at the monthly review, and the level goes down only at the next review if it isn't fixed.
- Samples:
  - sellers order samples of their designs;
  - price: Starter and Rising pay the base price, Pro gets 30% off, Elite gets 1 free a month;
  - the admin approves, then the print partner prints the sample.
- Analytics:
  - advanced platform analytics with a multi-sheet Excel export;
  - sellers get advanced analytics at Pro and export at Elite.

## Chunks
- [x] 1. Admin console shell: sign-in, layout, nav, bell, routes, overview, audit log. Existing admin pages move here.
- [x] 2. Money:
  - withdrawal approvals (partners, sellers, print partner settlement);
  - adjustments;
  - platform revenue report with export;
  - Fantasy Pass sales ledger.
- [x] 3. People:
  - customers (block, pay-upfront, notes);
  - partners (suspend, open dashboard);
  - sellers (suspend, strikes, ID, levels).
  - Blocking and suspension are enforced on the site.
- [x] 4. Operations:
  - all orders and bookings in one place, with admin cancel and refund;
  - support tickets and disputes, plus "Get help" on the site's order and booking pages.
- [ ] 5. Marketing: broadcast notifications, platform promo codes (with site checkout integration).
- [ ] 6. Content: home banners, the announcement bar, news (write, edit, hide, breaking), pickup-game moderation.
- [ ] 7. Fantasy: sponsors, league prizes, announcing winners with vouchers, pass subscribers.
- [ ] 8. Part 4:
  - monthly level review with a grace month;
  - samples (seller, admin and print queue);
  - advanced analytics and export (admin and seller).
- [ ] 9. Full-site pass: Arabic strings, dark mode, mobile. Then report to the user what admin powers are still missing.
