# Super App — project brief

## What it is

An Egyptian sports super app: one account to book football pitches, find players for a game, play
fantasy football, and shop sports gear and printed designs. Cities first: Cairo, Giza, Alexandria.

## Who uses it

| Who | What they do | Where |
|---|---|---|
| Players (customers) | Book pitches (single hours or weekly packages with one QR), join or post games, tournaments, fantasy (Egyptian Premier League and English Premier League), shop, communities, points and wallet | the site |
| Pitch partners | Pitches, hours, prices, offers, bookings, QR check-in, free slots, tournaments, staff, money | `#/dash` |
| Sellers | Print-on-demand designs (tees, oversized tees, hoodies) and/or their own store, levels with perks, wallet | `#/seller`, studio `#/sell/studio` |
| Factory (print partner) | Print, quality check, pack and ship POD orders (DTF), staff with PIN | `#/factory` |
| Super App team (admins) | Approvals, people, orders, money, support, content, fantasy, communities | `#/admin` |
| CEO (owner) | Whole company: approvals over limits, departments and AI agents, kill switches, reports, every page | `#/ceo` |

## How the company runs

Mostly by **AI agents** (18 of them, 8 departments) under a few humans: the CEO, an admin manager, the CFO and an
external legal counsel. Agents run on Claude with n8n workflows, each with its own permissions and money limit;
above the limit a human approves. See [ai-agents.md](ai-agents.md).

## How it makes money

- **Pitch bookings:** commission, 10% default, can be set per partner. Plus a service fee of 3%.
- **Shop:** 12% commission on store products.
- **Print-on-demand:** the platform margin sits inside the base price. Example: hoodie base 300, the seller sells at 500 and earns 200.
- **Fantasy:** a monthly pass, 50 EGP for one league or 80 EGP for both. No cash prizes; prizes are sponsor rewards.
- **Promotions:** sponsors and promotions.

VAT is 14% and included in prices.

## State (2026-10-17)

- **Front end:** complete, demo data in the browser, English and Arabic, light and dark.
- **Backend:** Phase 1 (foundation) and Phase 2 (accounts and approvals) are done. Phase 3 (pitches and bookings) is in progress. The progress file is `docs/backend/PROGRESS.md`.
- **Launch needs:**
  - hosting (a Hostinger VPS)
  - domain and official email
  - a payment provider and a courier contract
  - the commercial register and tax card (in progress, so e-receipts stay off)
  - social media links
