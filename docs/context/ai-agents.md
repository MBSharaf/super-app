# Window 4 — Building the AI agents

**Job:** design and build the AI agents that run the company: their instructions (prompts), tools,
n8n workflows, guardrails and tests. Work in `docs/agents/` (one spec per agent) and later `agents/`
(prompts, n8n workflow exports). Backend endpoints the agents need are requested from window 1 through its Inbox.

## Decisions already made
- **Platform:** agents run on **Claude** (Anthropic API) with **n8n** workflows. MCP comes later.
- **Accounts:** each agent is a service account with its own API key (header `X-Api-Key`), its own permissions and a money limit. Above the limit it asks a human: `POST /agent/approvals`.
- **Status:** all 18 agents exist in the database **paused, with no API keys**. The CEO starts each one from the Command Center.
- **Privacy:** agents never receive national IDs, payout details, full addresses or card data. Personal data is masked.
- **Control:**
  - a monthly budget per agent;
  - every run is logged with tokens and cost;
  - emergency stop for all agents (`kill.agents`).
- **Model:** `claude-sonnet-5-5` by default (set per agent), with a stronger model for decisions and writing.

## The org chart (8 departments)

| Key | Agent | Role | Department | Reports to | Money limit (EGP) |
|---|---|---|---|---|---|
| coo | Atlas | Operations lead | Operations | CEO | 0 |
| bookings-ops | Kick | Pitch & bookings agent | Operations | coo | 0 |
| orders-ops | Loom | Orders & factory agent | Operations | coo | 0 |
| logistics | Route | Courier & delivery agent | Operations | coo | 0 |
| support-lead | Haven | Support & trust lead | Customer support & trust | coo | 1,000 |
| support-agent | Echo | Frontline support agent | Customer support & trust | support-lead | 300 |
| trust | Shield | Trust & safety agent | Customer support & trust | support-lead | 0 |
| partners-agent | Bridge | Partnerships agent | Business development | coo | 0 |
| sellers-agent | Spark | Sellers agent | Business development | partners-agent | 0 |
| payables | Ledger | Payables agent | Finance | CFO | 0 |
| analyst | Abacus | Revenue analyst | Finance | CFO | 0 |
| compliance | Clause | Compliance agent | Legal & compliance | legal counsel | 0 |
| cmo | Pulse | Marketing lead | Marketing & social | CEO | 0 |
| social | Buzz | Social media & content agent | Marketing & social | cmo | 0 |
| arena | Arena | Fantasy & games agent | Marketing & social | cmo | 0 |
| cto | Forge | Technology lead | Technology & product | CEO | 0 |
| qa | Probe | Quality & monitoring agent | Technology & product | cto | 0 |
| conductor | Conductor | AI agents supervisor | AI agents operations | CEO | 0 |

Humans: CEO, admin manager (HQ), CFO, external legal counsel. Duties, permissions and screens of every role:
`js/data/org.js`, `src/SuperApp.Infrastructure/Persistence/Seed/Data/Demo/agents.json`.

## Sources
- **Agent API, webhooks and approvals:** `docs/backend/INTEGRATIONS.md` §8 (model, n8n setup, first workflows) and `docs/backend/API.md` §9.
- **Database:** `docs/backend/ERD.md` (schema `ai`: Agents, AgentPermissions, AgentTasks, AgentRuns, ApiKeys).
- **What the CEO controls:** `docs/PLAN-ceo.md` and the Command Center (`#/ceo/org`, AI panel).

## Suggested first tasks
1. A spec per agent: goal, inputs, tools (API endpoints), decisions it may take, when to ask a human, and a tone guide.
2. The first n8n workflows from INTEGRATIONS §8.3: support triage, design pre-review, payout preparation, morning briefing.
3. An evaluation set per agent: real-looking cases with the expected action.
4. A list of Agent API endpoints window 1 must build (Phase 7), written into dev.md's Inbox.

## Inbox (requests from other windows)
- *(empty)*

## Done
- *(empty)*
