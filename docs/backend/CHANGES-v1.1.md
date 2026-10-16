# Review Fixes — Version 1.1
Subtitle: The five corrections made to the backend planning documents after the review of 17 October 2026.
Document: Change log for PRD, ERD, API and STRUCTURE
Version: 1.1
Date: 17 October 2026
Owner: Sharaf (CEO)
Status: Applied — the four documents and their PDFs are updated
Audience: Founders, backend engineers

# 1. Summary

<div dir="rtl" class="ar-box" markdown="1">

**الملخص بالعربي:**

- اتراجعت 8 ملاحظات على الكود الحالي والملفات الخمسة. **5 منهم كانوا صح واتصلحوا**، و3 طلعوا مش موجودين في الموقع فمااتغيرش فيهم حاجة.
- **التصليحات الخمسة:**
    1. **شحن محفظة العميل:** اتضاف endpoint للشحن واتعمل له تيست، واتضاف للـ PRD كمتطلب، واتضاف نوعين حركات للـ Ledger في الـ ERD.
    2. **شاشات الـ CEO الجديدة:** شاشة الربط مع الشركات، وكتالوج الـ APIs، والـ Webhooks اتضافوا كمتطلب. واتصلحت الجملة اللي كانت بتمنع أي تعديل في الواجهة: الشاشات الحالية مش هتتغير، والجديدة هتتبني بنفس التصميم.
    3. **الـ 18 وكيل:** بيتعملوا مع الداتا التجريبية في حالة **إيقاف**. ولما الداتا التجريبية تتقفل وقت الإطلاق، اللوحة بتبدأ فاضية.
    4. **مفاتيح الإيقاف:** بقوا 9، وزاد عليهم مفتاح "الانضمام للماتشات" اللي موجود في الموقع.
    5. **Docker:** Caddy بقى ليه أماكن تخزين دايمة، فشهادات HTTPS مش هتضيع لما الحاوية تتعاد.

</div>

| # | Item | Verdict | Documents changed |
|---|---|---|---|
| 1 | Customer wallet top-up | **Fixed** | API, PRD, ERD |
| 2 | Booking add-ons (referee, ball, bibs) and split payment | Not a gap — not in the site | — |
| 3 | Fantasy head-to-head leagues | Not a gap — not in the site | — |
| 4 | Seller tiers Bronze / Silver / Gold / Platinum | Not a gap — the site uses Starter / Rising / Pro / Elite | — |
| 5 | CEO screens for integrations, API catalogue and webhooks | **Fixed** | PRD, STRUCTURE |
| 6 | The 18 AI agents in the seed | **Fixed** | PRD, ERD |
| 7 | Kill switch keys | **Fixed** | PRD, API, ERD |
| 8 | Docker and Caddy persistence | **Partly fixed** (Caddy volumes added; the `volumes:` block was already correct) | STRUCTURE |

# 2. The five fixes in detail

## Fix 1 — Customer wallet top-up
**Found:** the site's wallet page lets customers top up their balance (`paymentService.topUp`, quick amounts 200 / 500 / 1,000 EGP). The ERD already had "Wallet top-up" as a payment purpose (`money.Payments.Purpose`) and a receipt kind (`money.Receipts.Kind`), and the PRD mentioned top-up receipts, but **the API had no endpoint** for it and the ledger had no entry kind for it.

**Changed:**

| Document | Where | Change |
|---|---|---|
| API | §2.2 My account | **Added** `POST /me/wallet/top-up` `{ amount }` — Customer + `Idempotency-Key` → 201 with a payment session; 422 outside the limits (default 50 – 10,000 EGP, from settings); 423 when payments are paused. The balance is credited **only** by the gateway's signed webhook, then a top-up receipt PDF is issued |
| API | §2.2 My account | **Added** `GET /me/wallet/transactions` — the customer's wallet movements (paged) |
| API | §11.2 Edge cases | **Added** a "Wallet top-up" row: amount 0 or over the maximum → 422; same Idempotency-Key twice → one top-up; browser says paid without a webhook → no credit; failed payment → no credit |
| PRD | §6.2 Wallets | **Added** FR-WAL-09: customer top-up rules — limits, webhook-only credit, receipt, what the wallet can pay for, and that wallet money is not withdrawable as cash |
| ERD | B85 `money.LedgerEntries.Kind` | CHECK widened from 0–12 to **0–14**, adding **Wallet top-up** (13) and **Wallet payment** (14) so every movement of the customer wallet is in the ledger |

## Fix 5 — CEO screens for the new backend modules
**Found:** the CEO system has 17 screens (overview, briefing, approvals, departments, agents, goals, budgets, calendar, reports, what-if, control, systems, audit, every page, legal…). None of them manages **integrations**, the **API catalogue** or **webhooks**, which the PRD and API require. The STRUCTURE document also said "No HTML or CSS is edited", which would forbid adding them.

**Changed:**

| Document | Where | Change |
|---|---|---|
| PRD | §8.4 CEO Command Center | **Added** FR-CEO-07a: three new front-end screens — `/ceo/integrations` (registry, usage bars, secrets status, test call), `/ceo/api-catalogue` (endpoint groups, on/off) and `/ceo/webhooks` (n8n subscriptions, delivery log) — in the **same design** as the existing CEO system, in Arabic and English, linked from the CEO navigation and the Every page map |
| STRUCTURE | §7 The existing site | Rule 1 rewritten: **existing screens keep their design exactly**; new screens that the backend needs are **added** in the same design (same layout, components and CSS classes), starting with the three CEO screens |

## Fix 6 — The 18 AI agents in the seed
**Found:** the front end (`js/data/org.js`) has **18 AI agents** in 8 departments, but the PRD and ERD said the agents panel starts empty, so testing on the new backend would not match the preview.

**Changed:**

| Document | Where | Change |
|---|---|---|
| PRD | §9.2 FR-AI-08 | With demo data **on** (`SEED__DEMODATA=true`), the seed creates the 18 agents with their duties, permissions and money limits, **paused** and without API keys. With demo data **off** (real launch), the panel starts empty and the CEO adds the real agents |
| ERD | §12 Seed, step 4 | Org chart positions now list the human holders (CEO, admin manager, CFO, legal counsel) |
| ERD | §12 Seed, step 11 | Demo data now includes the 18 agents: service accounts, positions under their departments, permissions, limits, status Paused, no keys |

## Fix 7 — Kill switch keys
**Found:** the site's control room has 8 switches, including **Joining games** (free-slot games). The PRD and API listed 8 switches that added **AI agents** but left out **games**.

**Changed:** there are now **9 switches** everywhere: `kill.bookings`, `kill.shop`, `kill.pod`, `kill.payouts`, `kill.applications`, `kill.fantasy`, `kill.community`, `kill.games`, `kill.agents`.

| Document | Where | Change |
|---|---|---|
| PRD | §8.4 FR-CEO-09 | Kill switches (9), adding joining games |
| API | §8 CEO → Kill switches | The 9 keys listed by name |
| ERD | A7 `ops.FeatureFlags.Key` | The 9 seeded kill-switch keys listed, plus the API group examples |

## Fix 8 — Docker and Caddy persistence
**Found:** the top-level `volumes:` block was already at the root of `docker-compose.yml`, so that part of the note did not apply. But the **Caddy** service had no persistent storage, so its HTTPS certificates would be lost every time the container is recreated, and it had no ports or Caddyfile.

**Changed (STRUCTURE §8.2):**
```yaml
  proxy:
    image: caddy        # HTTPS certificates automatically, reverse proxy to api and n8n
    ports: ["80:80", "443:443"]
    volumes: [./Caddyfile:/etc/caddy/Caddyfile, caddy_data:/data, caddy_config:/config]   # certificates survive restarts
volumes: { sqldata: {}, n8ndata: {}, caddy_data: {}, caddy_config: {} }
```

# 3. Items checked and not changed

| # | Item | What was checked | Result |
|---|---|---|---|
| 2 | Booking add-ons and split payment | The booking checkout (`js/pages/bookingCheckout.js`) and the booking service | There is **no add-ons step**. "Referee" appears only in the free-text description of two pitches. Paying a share per player already exists as **game shares** (`POST /games/{code}/requests`, `booking.GameRequests`). If add-ons are wanted, they are a **new feature** to plan, not a gap |
| 3 | Fantasy head-to-head | All fantasy pages and data | **No head-to-head** anywhere. The site has official leagues, private groups and Fantasy Pass leagues, which match `fantasy.FantasyGroups` exactly. Head-to-head can be added later as a new feature |
| 4 | Seller tiers Bronze / Silver / Gold / Platinum | Seller pages, seller service, admin settings | "Platinum" does not exist in the site. Seller levels are **Starter / Rising / Pro / Elite** (same as the PRD and ERD). Bronze / Silver / Gold are the **customer loyalty tiers**, also already in the documents |

# 4. Updated files
| File | Status |
|---|---|
| `docs/backend/PRD.md` and `pdf/PRD.pdf` | updated (FR-WAL-09, FR-CEO-07a, FR-CEO-09, FR-AI-08) |
| `docs/backend/ERD.md` and `pdf/ERD.pdf` | updated (A7, B85, seed steps 4 and 11) |
| `docs/backend/API.md` and `pdf/API.pdf` | updated (§2.2, §8, §11.2) |
| `docs/backend/STRUCTURE.md` and `pdf/STRUCTURE.pdf` | updated (§7, §8.2) |
| `docs/backend/INTEGRATIONS.md` | unchanged |
