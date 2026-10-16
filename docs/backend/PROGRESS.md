# Backend planning — progress checkpoint

If a session ends, the next one continues from the first unchecked item. Each document is written as Markdown in this folder and exported to its own PDF in `docs/backend/pdf/` (`python tools/docs/md2pdf.py docs/backend/X.md` (needs: pip install markdown; Chrome or Edge)).
Language: English (it goes to engineers). Decisions: see the memory file backend-rules and the chat on 2026-10-16.

## Planning documents (no code until the user approves them)
- [x] 1. PRD.md → pdf/PRD.pdf — product requirements: 7 systems, roles and permissions, flows, business rules
- [x] 2. ERD.md → pdf/ERD.pdf — every table (lookup → child → junction), 5 constraints, ON DELETE, indexes, Mermaid diagram
- [x] 3. API.md → pdf/API.pdf — endpoints per system, who calls them and with which permission, unified response
- [x] 4. INTEGRATIONS.md → pdf/INTEGRATIONS.pdf — payments, shipping, email, SMS, API-FOOTBALL, n8n, AI agents (Claude), ETA e-receipt; questions for each company
- [x] 5. STRUCTURE.md → pdf/STRUCTURE.pdf — solution and folder layout, packages and why, how to run it, migrations, testing

- [x] Review fixes v1.1 (CHANGES-v1.1.md): wallet top-up, CEO screens, 18 demo agents, 9 kill switches, Caddy volumes
- [x] v1.2 (CHANGES-v1.2.md): public data APIs — geocoding (Nominatim), weather (Open-Meteo, commercial plan before launch), public holidays (new table ops.PublicHolidays → 162 tables), local phone/email validation; rejected APIs listed

## Code — Phase 1 (foundation) — approved by the owner on 2026-10-17
- [x] 1.1 Solution: SuperApp.sln at the repo root, src/ (Domain, Application, Infrastructure, Api) + tests/; references follow the dependency rule; Directory.Build.props (net10.0, nullable, warnings as errors), Directory.Packages.props (central versions + security pin), .editorconfig, .env.example, .gitignore; all packages installed; clean build
- [ ] 1.2 Domain: base classes, enums, constants (permissions, roles), entities for all 162 tables by schema
- [ ] 1.3 Infrastructure: AppDbContext + Fluent API configurations (constraints, ON DELETE, indexes), Identity mapping to auth.*
- [ ] 1.4 Verify: build + generate the migration SQL on a throw-away database (dropped after), owner runs the real Add-Migration / Update-Database
- [ ] 1.5 Api foundation: Program.cs, unified ApiResponse, global error handler, validation filter, correlation id, security headers, Serilog, health checks, OpenAPI + Scalar, static site from the repo root
- [ ] 1.6 Seed (reference data + CEO from .env), then demo data
- [ ] 1.7 Tests for the foundation; README backend section; commit and push

The site files stay at the repo root (GitHub Pages keeps working); the Api serves them from there instead of copying them into wwwroot, so there is one copy only.
