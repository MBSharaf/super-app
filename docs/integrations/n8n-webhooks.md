# Super App → n8n: webhooks and API

This guide is for the team building the n8n workflows.

There are two directions:

1. **Webhooks (Super App → n8n).** Super App sends an event to your n8n Webhook node when something happens.
2. **API (n8n → Super App).** n8n reads more details, or does an action, with an agent API key.

---

## 1. Webhooks

### Events

| Event | When it is sent | `data.entityId` |
|---|---|---|
| `booking.created` | A player booked a pitch | booking code (`SA-XXXXXXXX`) |
| `booking.cancelled` | A booking was cancelled. `data.by` is `customer` or `venue` | booking code |
| `application.submitted` | Someone applied to be a pitch partner, seller or factory, or sent a fixed application again | application code |
| `pitch.submitted` | A partner added a pitch, or sent a rejected one again, for admin review | pitch slug |
| `review.created` | A player reviewed a pitch after playing | booking code |
| `support.ticket.created` | A support ticket was opened | ticket code |
| `checkin.accepted` | A player was checked in at the venue by QR | booking code |

### What n8n receives

Every event is an HTTP `POST` with a JSON body:

```json
{
  "id": "6f1c2a8e-4d1b-4e4a-9f0e-2b7c1d9a5e31",
  "type": "booking.created",
  "occurredAt": "2026-10-26T18:04:12.0000000Z",
  "data": {
    "entityType": "Booking",
    "entityId": "SA-7KQ2M9XD",
    "summary": "Booking SA-7KQ2M9XD at Turf 22 Arena",
    "by": null,
    "data": null
  }
}
```

The inner `data.data` field holds extra details for some events:

- `support.ticket.created`: `{ topic, dispute }`
- `checkin.accepted`: `{ pitch, customer, match }`

### Headers

| Header | Value |
|---|---|
| `X-SuperApp-Event` | The event type, e.g. `booking.created` |
| `X-SuperApp-Delivery` | The event id. It stays the same on every retry, so use it to skip duplicates |
| `X-SuperApp-Timestamp` | Unix time in seconds |
| `X-SuperApp-Signature` | `sha256=` + hex of HMAC-SHA256(secret, `timestamp + "." + rawBody`) |

### Answering and retries

- Answer with any **2xx** status within 10 seconds.
- If the delivery fails, Super App tries again after 1 minute, 5 minutes, 30 minutes, 2 hours and 6 hours, then stops.

### Checking the signature in n8n

1. In the Webhook node: method **POST**, and turn on **Raw Body**.
2. Then add a **Code** node:

```js
const crypto = require('crypto');
const secret = 'whsec_...';                       // the secret Super App showed when the webhook was added
const h = $input.first().json.headers;
const raw = $input.first().binary?.data
  ? Buffer.from($input.first().binary.data.data, 'base64').toString('utf8')
  : JSON.stringify($input.first().json.body);
const expected = 'sha256=' + crypto.createHmac('sha256', secret).update(`${h['x-superapp-timestamp']}.${raw}`).digest('hex');
if (expected !== h['x-superapp-signature']) throw new Error('Bad signature');
// Optional: reject events older than 5 minutes
if (Math.abs(Date.now() / 1000 - Number(h['x-superapp-timestamp'])) > 300) throw new Error('Too old');
return [{ json: JSON.parse(raw) }];
```

### Setting a webhook up

The Super App CEO does this. You need to send the CEO two things:

- **The production URL** of your Webhook node.
  - It must be `https://…`, or `http://localhost…` if n8n runs on the same computer as the Super App server.
  - If n8n runs on your own computer, expose it first, for example with n8n's tunnel or Cloudflare Tunnel, so you get a public `https://` URL.
- **The events you want**, or `*` for all of them.

The CEO then:

1. Calls `POST /api/v1/ceo/webhooks` with `{ "name": "n8n", "url": "<your URL>", "events": ["booking.created", "booking.cancelled"] }`.
2. Copies the `secret` from the answer and sends it to you. It is shown only once.
3. Calls `POST /api/v1/ceo/webhooks/{id}/test` to send a signed `webhook.test` event to you.

Other endpoints:

| Endpoint | What it does |
|---|---|
| `GET /api/v1/ceo/webhooks/{id}/deliveries` | Shows every attempt, with its status code and error |
| `PUT /api/v1/ceo/webhooks/{id}` | Changes the events, or turns the webhook on or off |
| `DELETE /api/v1/ceo/webhooks/{id}` | Deletes the webhook |

---

## 2. API: n8n calls Super App

1. The CEO creates an agent and gives it only the permissions it needs: `POST /api/v1/ceo/agents`, then `POST /api/v1/ceo/agents/{id}/keys`, then `POST /api/v1/ceo/agents/{id}/resume`. The key starts with `sa_` and is shown once.
2. In n8n, create a **Header Auth** credential with the name `X-Api-Key` and the key as its value.
3. Call `https://<server>/api/v1/...`:
   - `GET /api/v1/agent/me` checks that the key works.
   - The agent can call any admin endpoint it holds the permission for.
   - `POST /api/v1/agent/runs` reports the work it did and its cost.

Each key is limited to 600 requests per minute. A paused agent gets **423**, and a bad key gets **401**.

All answers have the same shape: `{ success, message, data, errors, meta }`.

---

## 3. The demo site (GitHub Pages, no server)

The demo site has no server, so the page itself posts each event to the links in `SA.config.automation`
(`js/core/namespace.js`, sender `js/services/automation.js`). The body is flat JSON (not the server's envelope above):
`{ "event": "...", "source": "demo-site", "site": "...", "sentAt": "...", ...details }`. In n8n, switch on
`{{ $json.body.event }}`. Nothing is sent from the browser in API mode.

| Link | `event` | When | Main fields |
|---|---|---|---|
| `orders` | `order.created` | A store order | `order`, `customer`, `address`, `stores[]` (items, commission, earning), `items[]` |
| `bookings` | `booking.created` | A pitch booking | `booking`, `pitch`, `partner`, `customer` |
| `bookings` | `package.created` | A weekly package | `package`, `sessions[]`, `pitch`, `partner`, `customer` |
| `bookings` | `game.published` | A host publishes a game | `game` (times, players needed / already / spots left, level, gender, type, price per player, total cost, notes, `players[]`), `booking`, `pitch`, `partner`, `host` |
| `bookings` | `game.join_requested` | A player asks to join a game | `request`, `game`, `pitch`, `partner`, `player` |
| `fantasy` (= orders link) | `league.created` | A player creates a private league | `league` (`leagueId`, `leagueName`, `type`, `code`, `owner`, `startGameweek`, `competition`, `link`), `createdBy` (`userId`, `name`, `email`) |
| `fantasy` | `league.member.joined` | A player joins a private league by code or enters a prize league | `league`, `member` (`userId`, `name`, `email`, `leagueId`, `leagueName`, `gameweekId`, `gameweekPoints`, `totalPoints`, `teamName`) |
| `fantasy` | `gameweek.finished` | A gameweek is finished — one event per league the player is in | `gameweek`, `gameweekId`, `competition`, `league`, `winners[]` (top 3 by gameweek points: `place`, `userId`, `name`, `email`, `gameweekPoints`, `totalPoints`), `participants[]` (every member: `userId`, `name`, `email`, `leagueId`, `leagueName`, `gameweekId`, `rank` and `points` = place and points in the league table, `totalPoints`, `gameweekRank`, `gameweekPoints`, `teamName`), `partialTable` (true for the big official leagues: only the top 10 and the player) |

Demo rivals are fictional: their `userId` is `demo-<name>` and their email is `<name>@example.com`.

## The server address

Right now the Super App server runs on the owner's computer. For n8n to reach it, and for Super App to reach n8n:

- **Same computer:** use `https://localhost:7254` for the API, and `http://localhost:5678/webhook/...` for webhooks.
- **Different computers:** both sides need a public address. Use a tunnel for testing, or the online server once it is deployed.
