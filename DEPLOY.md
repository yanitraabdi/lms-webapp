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
| `PUBLIC_SITE_URL` | Public origin for sitemap/robots/canonical, e.g. `https://academy.example.com`. Optional. |
| `FRONTEND_PORT` | Host port the frontend is published on (default `3001`). The tunnel binds this. |
| `REVALIDATE_SECRET` | Shared secret for the API→Next ISR revalidation call. Change for a real deploy. |
| `POSTGRES_USER/PASSWORD/DB` | Database credentials. |

Set `PUBLIC_SITE_URL` to your tunnel hostname for correct SEO URLs (optional — the app works
without it).

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
