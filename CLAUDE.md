# Super App — shared instructions for every Claude window

This file is read automatically by every Claude Code window opened on this folder (`D:\Super - App`) or this
repository. The project is worked on in several windows, each with its own job. They all share this file,
`docs/context/`, the planning documents in `docs/`, and Claude's memory for this folder.

**Start here:** read `docs/context/README.md`. It lists the windows, their briefs and how to hand work between them.

## The product

An Egyptian sports super app.
- **For players:** book football pitches, find players for a game, play fantasy (Egyptian and English leagues), and shop. The shop sells sports gear and print-on-demand designs by sellers.
- **For the business:** a partner dashboard for pitch owners, a seller dashboard, a factory system, an admin console and a CEO system (Command Center).
- **How the company runs:** mostly by AI agents under a few humans.

| | |
|---|---|
| Repository | github.com/MBSharaf/super-app (public — never commit secrets) |
| Demo site (no server) | mbsharaf.github.io/super-app/ |
| Local site + API | `https://localhost:7254/super-app/` (run `src/SuperApp.Api`) |
| Design system | Artifact "Super App" — https://claude.ai/artifact/65e1JyQRGpGN3JMgobxF2f (read `project/README.md` first) |

## Rules every window follows

1. **Language:** reply to the owner in **Egyptian Arabic**. Code, comments, commit messages and docs are in English. The site UI is English with an Arabic mode, and every new UI string needs its Arabic entry in `js/data/i18n-ar*.js`.
2. **Commits:** commit and push after each tested chunk. Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` (or the model in use).
3. **Secrets:** never commit secrets or personal data. Secrets live only in `.env` (ignored by git).
4. **Links:** no WhatsApp or social media links until the owner sends them. The config placeholders are in `js/core/namespace.js`.
5. **Simulation:** payments are simulated and every person or company in demo data is fictional.
6. **Plans:** plans and decisions go into a doc in `docs/` (checklist) and into memory, so work can resume in any window.
7. **New pages:** they go into the CEO system's "Every page" map (`js/pages/devLinks.js` GROUPS).
8. **Notifications and documents:** every action notifies everyone concerned, every payment has a PDF, and every export is an Excel file with a dashboard sheet.
9. **Design:** the design of the existing site must not change when the backend is wired in.
10. **Backend:** ASP.NET Core (.NET 10) Web API, SQL Server code-first, Clean Architecture, controllers/services/repositories, DI, a unified response, correct HTTP codes, methods of 30–40 lines max, validation on every input. The owner applies database updates (`dotnet ef database update`) himself.

## Where things are

| What | Where |
|---|---|
| Front end (vanilla JS) | `index.html`, `js/` (core, services, components, pages, data), `css/` |
| Backend | `src/` (Domain, Application, Infrastructure, Api), `tests/` |
| Backend planning (PRD, ERD, API, INTEGRATIONS, STRUCTURE) | `docs/backend/*.md` (+ PDFs in `docs/backend/pdf/`) |
| Backend progress (resume point) | `docs/backend/PROGRESS.md` |
| Front-end feature plans (all done) | `docs/PLAN-*.md` |
| Shared context for all windows | `docs/context/` |
| Tools (seed export, PDF, codegen, QA) | `tools/` |
