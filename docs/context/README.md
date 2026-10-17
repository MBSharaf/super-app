# Working on Super App in several windows

Every window is a separate Claude Code session on the same folder (`D:\Super - App`), filed under the
**Super App** group in the sidebar. Windows do not see each other's chats, so everything a window needs
must be written down. The shared memory is:

| Shared by every window | What it holds |
|---|---|
| `CLAUDE.md` (repo root) | Product, rules, where things are — loaded automatically |
| `docs/context/` (this folder) | One brief per window + the project brief + the decisions log |
| `docs/backend/`, `docs/PLAN-*.md` | Full plans and checklists |
| Claude's memory for this folder | Short notes Claude keeps between sessions (loaded automatically) |
| The design system artifact | https://claude.ai/artifact/65e1JyQRGpGN3JMgobxF2f |
| The GitHub repository | All code and docs (push after each chunk so cloud windows see it) |

Skills, connectors and plugins are part of the Claude account, so every window has them; nothing to copy.

## The windows

| # | Window | Brief | Writes to |
|---|---|---|---|
| 1 | **Development** — website and backend code | [dev.md](dev.md) | code, `docs/backend/PROGRESS.md` |
| 2 | **Design system** — brand, components, content | [design-system.md](design-system.md) | the design system artifact, `docs/context/design-system.md` |
| 3 | **Marketing** — plan and social media | [marketing.md](marketing.md) | `docs/marketing/` |
| 4 | **AI agents** — building the company's agents | [ai-agents.md](ai-agents.md) | `docs/agents/`, later `agents/` |

More windows can be added the same way: a new brief here, a row in this table.

Common to all: [project-brief.md](project-brief.md) (what Super App is) and [decisions.md](decisions.md)
(every decision the owner made, by area).

## Opening a new window

1. In the Claude app: **New session** on the folder `D:\Super - App`, then drag it into the **Super App** group.
2. First message (replace the brief name):

   > اقرا CLAUDE.md و docs/context/README.md و docs/context/marketing.md وكل الملفات اللي بيشاوروا عليها، وبعدين قولي إنت فاهم إيه وهتبدأ بإيه.

3. The window works only inside its own area. When it changes something other windows must know, it updates
   its brief (and `decisions.md` for a new decision) and pushes.

## Handing work between windows

- Write the request in the other window's brief under **Inbox** (date, who asks, what is needed), push, and
  tell the owner to open that window.
- A window that finishes an inbox item moves it to **Done** in its brief with the result and links.
