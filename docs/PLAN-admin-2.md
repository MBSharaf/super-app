# Admin console — round 2 (missing powers)

Agreed with the user on 2026-10-15: build the 5 "important" items and the 4 "medium" ones. The backend items come later.

**Resume rule:** if the session stops, continue from the first unticked item. Each item is committed and pushed when done; don't redo finished items.

## Checklist
- [x] 1. **Approve new pitches.** A pitch a partner adds is "waiting for review" and hidden from the site until an admin approves it; rejecting it needs a reason. The partner sees the status. Page: `#/admin/approvals`.
- [x] 2. **Commission deal per partner.** Booking and store commission can be overridden for one partner; the deal is used in all money calculations. Set it in the partner drawer.
- [x] 3. **Shipping and service fees** are editable in Platform settings: standard and express delivery, free-delivery threshold, booking service fee.
- [x] 4. **Hide one product** from a partner's store, with a reason. The product disappears from the shop and the partner is notified. Done from the partner drawer and a products list.
- [x] 5. **Tournament oversight** in the admin console: see all tournaments, open them, cancel one with refunds.
- [ ] 6. **Adjust a customer's loyalty points** (credit or debit, with a reason) from the customer drawer.
- [x] 7. **Maintenance mode:** pause bookings and/or shop checkout with a message. Shown on the site and enforced at checkout.
- [ ] 8. **Shop categories:** rename, hide, reorder and add categories.
- [x] 9. **Approve new sellers** (a switch in settings): new sellers wait for approval before their store and designs go live. This is a tab on `#/admin/approvals`.
- [ ] 10. Arabic strings, check every page, README, push.
