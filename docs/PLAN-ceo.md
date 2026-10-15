# CEO system "Command Center" (`/ceo`) — plan & progress

The user's decisions (2026-10-15):
- **Location:** a separate system at `/ceo`, with its own sign-in and session. It sits above every other system: the site, `/dash`, `/seller`, `/factory` and `/admin`.
- **AI agents:** every role is fully defined (title, department, duties, permissions, money limit). Each agent's log shows the real actions that happen in its area, marked "demo". Real agents plug in with the backend.
- **Money limits that need the CEO:**
  - payouts and settlements over **10,000 EGP**
  - customer refunds and credits over **1,000 EGP**
  - anything over **50,000 EGP** needs **two approvals**

  All three can be changed in the system.
- **Extras:** all four were chosen:
  - morning briefing + OKR goals
  - what-if simulator
  - two-person rule + compliance calendar
  - department budgets + admin team roles

## Org model (from the org-structure answer)
- **8 departments:**
  1. Operations (COO)
  2. Customer support & trust (under the COO)
  3. Business development (under the COO)
  4. Finance (CFO)
  5. Legal & compliance
  6. Marketing & social (CMO)
  7. Technology & product (CTO)
  8. AI agents operations
- **Every role has:** a title, a department, duties, the admin sections it may open, the actions it may take, a money limit, and a holder (AI agent or human).
- **Separation of duties:**
  - preparing a payout ≠ approving it
  - adjusting a balance ≠ approving it
  - creating promo codes ≠ reviewing revenue
  - nobody deletes the audit log

## Checklist

### Chunk 1 — services
- [ ] `ceoService`:
  - CEO session
  - org: departments, roles, agents (pause/resume, change permissions and limit, emergency stop)
  - agent activity, mapped from the audit log
  - approvals queue, with the two-person rule
  - limits, kill switches
  - goals/OKRs, budgets
  - compliance calendar
  - morning briefing
  - what-if simulator
  - company KPIs and department health
  - reports data
  - "open as" into every system (logged)
- [ ] Hooks:
  - Payouts, settlements, refunds and credits over the limit become approval requests.
  - Kill switches are enforced: bookings, shop, printed designs, payouts, new applications, fantasy, community posting.
  - Admin team roles:
    - admin accounts get a role
    - the admin navigation and actions follow the role's permissions
    - agents sign in as their role

### Chunk 2 — CEO pages
- [ ] Sign-in + layout
- [ ] Command overview: live KPIs, department health, "needs your decision", smart alerts
- [ ] Morning briefing
- [ ] Approvals inbox
- [ ] Departments + interactive org chart; department page
- [ ] AI workforce: list, agent page (duties, permissions, limit, log, errors, cost), emergency stop
- [ ] Reports: finance, operations, customers, partners, sellers, marketing, support, agents — date range, Excel with dashboard, PDF
- [ ] Control: kill switches, limits, permissions matrix, company settings (commissions, fees, points, payout day)
- [ ] Goals (OKRs), budgets, compliance calendar, what-if simulator
- [ ] All systems: open as any partner, seller, factory staff, admin; the site
- [ ] Company audit log

### Chunk 3 — finish
- [ ] Arabic, Sharaf links, README, tests (desktop/mobile, Arabic, light/dark), commit and push
