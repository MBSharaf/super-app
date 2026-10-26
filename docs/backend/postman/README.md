# Postman

`SuperApp.postman_collection.json` — every API endpoint (526), one folder per area, each with an example body.
`openapi.json` — the OpenAPI document it was made from (Postman, Insomnia and most tools can import it too).

## Use it

1. Postman → Import → `SuperApp.postman_collection.json`.
2. Run the API (`https://localhost:7254`, or change the `baseUrl` collection variable).
3. `auth → POST /auth/sign-in` with a **test account** (never a real password). Postman keeps the session cookie.
4. Call anything. Before every change (POST / PUT / PATCH / DELETE) the collection fetches a fresh anti-forgery
   token and sends it as `X-CSRF-TOKEN` — nothing to copy by hand.
5. Admin and CEO endpoints need two-step sign-in: after sign-in send `auth → POST /auth/2fa/verify` with the
   authenticator code.

Other callers: `agent/*` sends `X-Api-Key: {{agentKey}}` (a key made in the CEO system → AI panel); `factory/*` sends
`X-Factory-Device` / `X-Factory-Session` (`{{factoryDevice}}`, `{{factorySession}}`).

## Rebuild after API changes

```bash
curl -sk https://localhost:7254/openapi/v1.json -o docs/backend/postman/openapi.json
python tools/docs/postman.py docs/backend/postman/openapi.json docs/backend/postman/SuperApp.postman_collection.json
```
