# Parallel development (several windows at once)

Decided by the owner on 2026-10-17: the backend phases that don't depend on each other are built at the same time
in separate windows. Nothing is postponed, the plan (PRD §14) stays the same, and the owner's site and database are
not touched. One quick browser check per step (main window), and branch screens are checked when they are merged.

## Who does what

| Window | Branch | Work | Its part of PROGRESS.md |
|---|---|---|---|
| Main (development) | `main` | Phase 3 (finish), then Phase 5 money, Phase 7 admin & CEO, Phases 8–9; merges the other branches | Phases 3, 5, 7, 8, 9 |
| Shop & POD | `phase-4-shop` | Phase 4: stores, products, orders, POD catalogue, designs, studio uploads, factory, courier adapter, returns, and the site / seller / factory screens in API mode | Phase 4 |
| Admin & CEO (part 1) | `phase-7-admin` | Phase 7 without money screens: admin console and CEO system on the API, AI-agent API keys (`X-Api-Key`), org chart, switches, integrations registry, API catalogue. Money screens wait for Phase 5 | Phase 7 |
| Fantasy & content | `phase-6-fantasy` | Phase 6: API-FOOTBALL sync and scoring, fantasy, groups, content, pop-ups, communities, support chat, and their screens in API mode | Phase 6 |

Money (Phase 5) waits for Phases 3 and 4, because bookings and orders call it; the main window builds it.

## Rules for every branch window

1. **Own branch, own worktree.** Commit and push your branch after each tested chunk; never push to `main` and never
   merge. The main window merges the branches into `main`, in order, and runs every test after each merge.
2. **No migrations.** The 162 tables of the ERD already exist. If you need a schema change, don't create a migration:
   write it under "Schema requests" in your PROGRESS section (table, column, type, why). The main window creates one
   migration at a time, so the snapshot never conflicts, and tells the owner to run `dotnet ef database update`.
3. **Never touch the owner's database or running site.** No `dotnet ef database update`, no server on port 7254.
   Tests make their own throw-away databases (unique names) and drop them.
4. **Own build folder:** `dotnet build --artifacts-path %TEMP%\sa-art-<branch>` and the same for `dotnet test`.
5. **Automated tests after every chunk** (fast, run in the background): every endpoint gets its tests, as in Phase 3.
   The whole suite must pass before each push. **Branch windows don't use the browser** (it is shared and the
   windows would collide); the main window checks each branch's screens in the browser when it merges them, on a
   throw-away database. The main window also does one quick browser check after each of its own steps.
6. **Shared files — keep the change to one line or one block, so merges stay trivial:**
   - `src/SuperApp.Application/DependencyInjection.cs` and `src/SuperApp.Infrastructure/DependencyInjection.cs`:
     put your registrations in your own extension method in its own file (e.g. `ShopRegistration.AddShop()`), and add
     only the one call line.
   - Arabic strings: your own file `js/data/i18n-ar-<area>.js` (plus one `<script>` line in `index.html`).
   - Front-end bridges: your own file `js/services/live<Area>.js` (as `liveBookings.js` / `livePartner.js` do).
   - `SuperApp.Api.http`: add your requests at the end, under a heading for your phase.
   - `docs/backend/PROGRESS.md`: edit only your phase's section. `docs/context/dev.md`: only your row.
7. **Don't change Phase 1–3 code** (bookings, partners, accounts, check-in, packages) except to call it. If you need
   a change there, write it under "Requests to main" in your PROGRESS section.
8. Same rules as everywhere: `CLAUDE.md`, the backend rules (controllers / services / repositories, DI, unified
   response, correct codes, methods of 30–40 lines, validation on every input), English code and docs, design unchanged.

## Merging (main window)

Merge order: `main` work first, then `phase-4-shop`, then `phase-6-fantasy`. After each merge: build, run the
whole suite, fix the one-line conflicts (DI calls, script tags), push.
