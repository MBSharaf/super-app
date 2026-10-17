# Window 1 — Development (website + backend)

**Job:** build and fix the website and the backend. This window owns the code.

## Where we are (2026-10-17)

| Phase | State |
|---|---|
| Front end (all layers: site, partner, seller, studio, factory, admin, CEO, communities, legal, SEO) | Done — `docs/PLAN-*.md` |
| Backend 1 — foundation (162 tables, seed, API base) | Done |
| Backend 2 — accounts, 2FA, team, applications, approvals, site in API mode | Done |
| Backend 3 — pitches & bookings | **In progress**: 3.1 pitches API, 3.2 hour grid, 3.3 bookings API, 3.1b site pitch pages and booking flow in API mode, 3.4a partner venues / pitches / blocked times, 3.4b partner bookings + Excel export done; next: 3.4c photos, delete, admin review of new pitches |
| Backend 4–9 — shop & POD, money, fantasy & content, admin & CEO, hardening, launch | Planned — PRD §14 |

**Resume point:** `docs/backend/PROGRESS.md` → first unticked item. The owner says «كمل».

## How to work

- Build into a separate folder so it does not clash with the owner's running copy: `dotnet build --artifacts-path %TEMP%\sa-art`. Tests run the same way. Tests use throw-away LocalDB databases and drop them.
- After a change, check it in the browser. Use a throw-away database for anything that writes.
- The owner runs `dotnet ef database update` himself. Create the migration files and tell him.
- Sessions can end mid-task: commit after each chunk and keep PROGRESS current.

## Owner still to answer
- Delete the test customer `browser.test@superapp.test` and its application "Browser Test Arena" from the local database?

## Inbox (requests from other windows)
- *(empty)*

## Done
- *(empty)*
