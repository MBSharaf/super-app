# Factory system (POD production) — plan & progress

Agreed with the user on 2026-10-15:

- Start with **one** factory, but the code is ready for several.
- **Shipping** is Super App's contract with a courier, and it is connected to the factory automatically. Bosta is used now as a trial. The courier sits behind one adapter (`SA.services.shipping`) and can be swapped at any time from the admin console.
- **The factory owns its materials and stock.** Super App only cares that the product arrives with good quality. So there is no blanks inventory for the admin.
- It is a **separate system with its own link**: `#/factory`, with its own session.
- Printing is **DTF only** for now.

## How an item flows

```
shop checkout → pod.place → routed to the factory (job, due in SLA hours)
 new → printing → qc → packing → ready ──(all items of the order packed)──> shipment created with the courier (auto)
 courier: awaiting pickup → picked up (item "shipped") → out for delivery → delivered | refused
 QC fail → back to printing (reprint at the factory's cost, counted in the defect rate)
 problem (can't make it) → flagged to supervisor + admin
```

`podOrders.status` keeps the existing customer and seller meaning: received, printing, shipped, delivered, refused, cancelled. The new `stage` field holds the factory's internal steps.

## Roles in the factory

| Role | Sees / does |
|---|---|
| Owner | everything: team, money, prices, settings, reports |
| Supervisor | all production + shipping + reports, assign jobs, pause intake; no money/team |
| Printer | new + printing jobs, start/finish printing |
| QC | quality check: pass / fail with reason |
| Packer | packing + shipping, labels |

- Each staff member signs in with a name and a 4-digit PIN, so a shared tablet works.
- Every action is logged with the staff member's name.

## Checklist

### Chunk 1 — services
- [x] `factoryService`:
  - factories and staff, with PIN sign-in and session
  - routing, stages and transitions with permission checks
  - batches, problems, reprints
  - stats: on-time, defect rate, average hours, per-staff output
  - money: earned, deductions, payouts, owed
  - product pause
  - log
- [x] `shippingService`:
  - provider registry (Bosta test, Aramex, Mylerz)
  - `create` shipment per order and package
  - simulated courier updates (sweep) → `pod.advance`
  - manual "next step" for testing
- [x] Migration:
  - factory `fc-1` plus staff
  - stage, due date and factory id on existing podOrders
  - shipments for the ones already shipped
- [x] Hooks:
  - `pod.place` routes jobs and notifies the factory
  - shop checkout passes the address and phone
  - `pod.live` respects the factory's intake and paused products

### Chunk 2 — factory pages (`#/factory`)
- [x] Layout and sign-in (staff picker + PIN pad), role-based nav
- [x] Overview: KPIs, today's load vs capacity, late jobs, pipeline, messages from Super App
- [x] Production board: stage tabs, "mine", batches by product and colour, job cards with role actions
- [x] Job page: details, mockups, print file, timeline with names, job ticket PDF with QR
- [x] Scan: type or paste a job code, then one big "next step" button

### Chunk 3 — factory pages (continued)
- [x] Shipping: ready for pickup, in transit, delivered or refused; label PDF; courier name; demo "courier update"
- [x] Team (owner): add, edit, deactivate, reset PIN; per-person output
- [x] Money (owner): earned per piece, deductions, payouts, statement
- [x] Reports: on-time %, defect rate, average production hours, daily chart, per staff
- [x] Settings (owner): capacity/day, SLA, intake pause, product/colour pause, price list (request a change)
- [x] Activity log

### Chunk 4 — admin and links
- [x] Admin `#/admin/factories`:
  - overview, late and problem jobs (reassign, cancel, send for reprint, resolve)
  - shipping provider settings (switch provider, test/live)
  - price list and change requests
  - money (settle, deductions)
  - status (pause or suspend), staff list
- [x] Admin alerts: late jobs, problems, price requests, factory paused
- [x] Remove the old "printer" partner role (`#/dash/print`); the print partner becomes the factory owner
- [x] Seller and customer see the factory stages through the existing statuses and tracking

### Chunk 5 — finish
- [x] Arabic (`js/data/i18n-ar-factory.js`)
- [x] Sharaf section: factory links + sign in as each staff member
- [x] README, browser tests (desktop, mobile, Arabic), commit and push
