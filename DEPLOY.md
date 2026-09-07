# Deploy — local Docker behind a Cloudflare tunnel

This packages the full stack (PostgreSQL + .NET API + Next.js frontend) to run on local Docker
and be served through an **already-running Cloudflare tunnel**. You bind **one** service — the
frontend — and everything else stays internal.

## Architecture (single origin)

```
 Browser ──https──▶ Cloudflare edge ──▶ cloudflared ──▶ frontend:3000 (Next.js)
                                                          │
                                       /api/auth/*, /api/revalidate  ─ handled by Next (auth BFF)
                                       everything else /api/*        ─ proxied to ▼
                                                          └──────────▶ api:8080 (.NET) ──▶ postgres:5432
```

The browser only ever talks to the frontend's public hostname. The frontend is built with
`NEXT_PUBLIC_API_BASE_URL=""`, so all client API calls are **relative** (`/api/...`); Next's
`afterFiles` rewrite (see `frontend/next.config.ts`) proxies them to the .NET API over the internal
Docker network. No public API hostname, no CORS, no domain baked into the bundle.

> Why this matters: the dev build bakes `http://localhost:8080` as the API base, which a remote
> browser coming through the tunnel cannot reach. The tunnel build fixes that by going same-origin.

## 1. Configure `.env`

`.env` (gitignored) already holds the deploy settings:

| Var | Purpose |
|-----|---------|
| `ADMIN_PASSWORD` | SuperAdmin password (replaces the well-known dev default). **Required.** |
| `MEDIA_SIGNING_KEY` | HMAC key for signed media (listening audio) URLs (replaces the well-known dev default). **Required.** |
| `PUBLIC_SITE_URL` | Public origin for sitemap/robots/canonical, e.g. `https://academy.example.com`. Optional. |
| `FRONTEND_PORT` | Host port the frontend is published on (default `3001`). The tunnel binds this. |
| `REVALIDATE_SECRET` | Shared secret for the API→Next ISR revalidation call. Change for a real deploy. |
| `POSTGRES_USER/PASSWORD/DB` | Database credentials. |
| `EMAIL_PROVIDER` | `dev` (default — logs mail to the API console) or `smtp` (actually sends). |
| `SMTP_HOST/PORT` | Relay. Defaults `smtp.gmail.com` / `587` (STARTTLS). |
| `SMTP_USERNAME` | Authenticating mailbox. Required when `EMAIL_PROVIDER=smtp`. |
| `SMTP_PASSWORD` | Google **app password**, not the account password. Required when `EMAIL_PROVIDER=smtp`. |
| `SMTP_FROM_ADDRESS` | Visible From. Required when `EMAIL_PROVIDER=smtp`. |
| `SMTP_FROM_NAME` | Display name (default `INVERTA`). |
| `SMTP_REPLY_TO` | Where replies go. Set this if From is a noreply mailbox. |

Set `PUBLIC_SITE_URL` to your tunnel hostname for correct SEO URLs (optional — the app works
without it).

### Sending real email

**Until `EMAIL_PROVIDER=smtp` is set, no email leaves the server** — every message is written to
the API container's log instead. Email verification is required before purchase, so on the default
setting no real learner can complete one. This is the single setting standing between the app and
taking money.

Google Workspace, using an app password:

1. Enable 2-Step Verification on the sending Workspace account.
2. Create an **app password** (Google Account → Security → App passwords). It is a 16-character
   string; treat it as a credential and keep it out of the repo.
3. Put it in `.env` — which is gitignored, and must stay that way:

```
EMAIL_PROVIDER=smtp
SMTP_USERNAME=noreply@yourdomain.com
SMTP_PASSWORD=xxxxxxxxxxxxxxxx
SMTP_FROM_ADDRESS=noreply@yourdomain.com
SMTP_REPLY_TO=halo@yourdomain.com
```

4. `docker compose -f docker-compose.tunnel.yml up -d` and register a throwaway address to confirm
   a real message arrives.

Notes:

- `SMTP_USERNAME` must be a real mailbox. Google refuses to authenticate an address that exists
  only as an alias, and `SMTP_FROM_ADDRESS` must be that mailbox or an address it is permitted to
  send as (Gmail "Send mail as", or a Workspace alias) — otherwise Google rewrites the From and
  the learner sees the wrong sender.
- Workspace allows roughly **2,000 recipients a day**. Ample now; not a permanent answer.
- An incomplete `smtp` config **fails at startup** rather than falling back to the log. That is
  deliberate: a healthy API silently dropping verification mail is the failure being fixed here.
- Only the three authentication emails send. The enrolment receipt, certificate and live-session
  reminder are still console stubs — their bodies have not been written.

## 2. Build & run

```bash
cd "/Users/yanitra/Development/LMS App/lms-webapp"
docker compose -f docker-compose.tunnel.yml up -d --build
```

This reuses the `academy` project and its `academy_pgdata` volume. To start from a **clean
database** (so the seed catalog + the `ADMIN_PASSWORD` apply fresh):

```bash
docker compose -f docker-compose.tunnel.yml down -v
docker compose -f docker-compose.tunnel.yml up -d --build
```

Postgres and the API are **not** published to the host — only the frontend is.

## 3. Point the Cloudflare tunnel at the frontend

cloudflared is already running, so just add an ingress rule to its config.

- **Host-level cloudflared** (runs on this machine): bind to the published frontend port.

  ```yaml
  # ~/.cloudflared/config.yml  (or the dashboard "Public Hostname" → Service)
  ingress:
    - hostname: academy.example.com
      service: http://localhost:3001        # = FRONTEND_PORT
    - service: http_status:404
  ```

- **Containerized cloudflared**: join it to this stack's network and use the service name.

  ```yaml
  ingress:
    - hostname: academy.example.com
      service: http://frontend:3000         # container port, on network "academy_default"
    - service: http_status:404
  ```

  ```bash
  docker network connect academy_default <your-cloudflared-container>
  ```

Then reload/restart cloudflared so it picks up the rule.

## 4. Verify

```bash
# Frontend up (host)
curl -I http://localhost:3001/

# Same-origin proxy works: the frontend forwards /api/* to the .NET API
curl -s http://localhost:3001/api/catalog | head -c 200

# Public (through the tunnel)
curl -I https://academy.example.com/
```

Sign in to `/login`, then visit `/admin` with:

- **Email:** `admin@academy.local`
- **Password:** value of `ADMIN_PASSWORD` in `.env`

## Notes & caveats

- **Dev-sim adapters are still active** (payments, video, email) because no real provider
  credentials exist. Payments use the in-app dev gateway (`/checkout/dev-pay`), video plays a public
  HLS sample, and emails are logged to the API container (`docker compose -f docker-compose.tunnel.yml logs -f api`).
  Swapping to Xendit/Bunny/SES is a config change (`Billing:Provider`, `Video:Provider`, SES creds) — no code change.
- **Entitlements** still flow only through the (dev) payment webhook; the same server-side rules apply.
- This is a single-node local deploy (one Postgres container, app-managed migrations on boot). It is
  meant for a tunnel-fronted demo/staging, not a hardened multi-node production cluster.
- To stop: `docker compose -f docker-compose.tunnel.yml down` (add `-v` to also wipe the database).
