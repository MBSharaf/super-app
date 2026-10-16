# Backend planning — progress checkpoint

If a session ends, the next one continues from the first unchecked item. Each document is written as Markdown in this folder and exported to its own PDF in `docs/backend/pdf/` (`python tools/docs/md2pdf.py docs/backend/X.md` (needs: pip install markdown; Chrome or Edge)).
Language: English (it goes to engineers). Decisions: see the memory file backend-rules and the chat on 2026-10-16.

## Planning documents (no code until the user approves them)
- [x] 1. PRD.md → pdf/PRD.pdf — product requirements: 7 systems, roles and permissions, flows, business rules
- [ ] 2. ERD.md → pdf/ERD.pdf — every table (lookup → child → junction), 5 constraints, ON DELETE, indexes, Mermaid diagram
- [ ] 3. API.md → pdf/API.pdf — endpoints per system, who calls them and with which permission, unified response
- [ ] 4. INTEGRATIONS.md → pdf/INTEGRATIONS.pdf — payments, shipping, email, SMS, API-FOOTBALL, n8n, AI agents (Claude), ETA e-receipt; questions for each company
- [ ] 5. STRUCTURE.md → pdf/STRUCTURE.pdf — solution and folder layout, packages and why, how to run it, migrations, testing

## After approval (not started)
- Code: solution setup → Domain → Infrastructure (DbContext, configurations, seed) → Application → Api → wiring the site → tests and Postman collection
