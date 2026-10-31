# Window 1 — Development (website + backend)

**Job:** build and fix the website and the backend. This window owns the code.

## Where we are (2026-10-17)

| Phase | State |
|---|---|
| Front end (all layers: site, partner, seller, studio, factory, admin, CEO, communities, legal, SEO) | Done — `docs/PLAN-*.md` |
| Backend 1 — foundation (162 tables, seed, API base) | Done |
| Backend 2 — accounts, 2FA, team, applications, approvals, site in API mode | Done |
| Backend 3 — pitches & bookings | **In progress**: 3.1 pitches API, 3.2 hour grid, 3.3 bookings API, 3.1b site pitch pages and booking flow in API mode, 3.4a partner venues / pitches / blocked times, 3.4b partner bookings + Excel export, 3.4c photos / delete / admin pitch review, 3.4 partner side (API + dashboard in API mode) done; admin pitch review in API mode, 3.5 packages and QR check-in (API, site, dashboard, admin) done; next: 3.6 waitlist, free slots, alerts, reviews |
| Backend 4–9 — shop & POD, money, fantasy & content, admin & CEO, hardening, launch | Planned — PRD §14 |

**Resume point:** `docs/backend/PROGRESS.md` → first unticked item. The owner says «كمل».

## How to work

- Build into a separate folder so it does not clash with the owner's running copy: `dotnet build --artifacts-path %TEMP%\sa-art`. Tests run the same way. Tests use throw-away LocalDB databases and drop them.
- After a change, check it in the browser. Use a throw-away database for anything that writes.
- The owner runs `dotnet ef database update` himself. Create the migration files and tell him.
- Sessions can end mid-task: commit after each chunk and keep PROGRESS current.

## Owner still to answer
- *(nothing open)* — test data (e.g. `browser.test@superapp.test`, "Browser Test Arena") is deleted at the end of the backend, with the demo data (Phase 9).

## Inbox (requests from other windows)
- **2026-10-31, marketing window — Meta Pixel + Conversions API (needed before paid sign-up and booking campaigns, see `marketing/PLAN-30-DAYS.md` §6):** browser Pixel and server-side Conversions API sending the same events with one shared `event_id` for deduplication: `PageView`, `ViewContent` (pitch page), `Search` (pitch search), `CompleteRegistration`, `InitiateCheckout` (booking started), `Purchase` (booking paid through Paymob, with value and currency EGP), `Lead` (pitch-partner application). Pixel id and CAPI token come from the owner's `.env`; nothing fires until they are set. A cookie-consent check before firing. Later, the same events for GA4/GTM and the TikTok Pixel.

## Done
- *(empty)*
