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
| `SMTP_HOST/PORT` | Relay. Defaults `smtp.resend.com` / `587` (STARTTLS). |
| `SMTP_USERNAME` | `resend` for Resend (a real mailbox for Gmail). Required when `EMAIL_PROVIDER=smtp`. |
| `SMTP_PASSWORD` | A Resend **API key** (`re_…`); for Gmail, an app password. Required when `EMAIL_PROVIDER=smtp`. |
| `SMTP_FROM_ADDRESS` | Visible From. Required when `EMAIL_PROVIDER=smtp`. |
| `SMTP_FROM_NAME` | Display name (default `INVERTA`). |
| `SMTP_REPLY_TO` | Where replies go. Set this if From is a noreply mailbox. |
| `VIDEO_PROVIDER` | `dev` (default — one test stream for every session) or `bunny` (real videos). |
| `BUNNY_PULL_ZONE` | The video library's pull zone hostname, e.g. `vz-1a2b3c4d-e5f.b-cdn.net`. |
| `BUNNY_LIBRARY_ID` | Bunny Stream video library id. |
| `BUNNY_TOKEN_KEY` | The library's **Token Authentication key** (not the API key). |
| `BUNNY_CAPTIONS_LANGUAGE` | Caption track to offer, e.g. `id`. Blank offers none. |
| `BUNNY_API_KEY` | The video **library's** API key, for the admin video picker. Optional. |

Set `PUBLIC_SITE_URL` to your tunnel hostname for correct SEO URLs (optional — the app works
without it).

### Playing real videos

**Until `VIDEO_PROVIDER=bunny` is set, every video session plays the same public test stream**,
whatever asset id the session carries. The "Bunny asset id" field in the admin session form is
recorded but ignored.

1. In Bunny Stream, create a **video library** and upload the lesson videos. Each upload gets a
   video GUID.
2. Open that library's pull zone: **Stream → your library → API → Pull Zone → Manage**. Every
   Stream library has its own pull zone, and this is where the setting we need lives.
3. On the pull zone: **Security**, switch on **Token Authentication**, and copy the
   **URL Token Authentication Key**.

   Two near-miss values to avoid. The library's own *Embed View Token Authentication* is a
   DIFFERENT feature — it protects Bunny's iframe player, which this app does not use, and its key
   will not sign a direct URL. The account **API key** is a third value again, used for uploads,
   not playback.
4. Copy the pull zone's **hostname**, of the form `vz-xxxxxxxx-xxx.b-cdn.net`. Each library has its
   own; the account hostname will not work.
5. Put them in `.env`, which is gitignored and must stay that way:

```
VIDEO_PROVIDER=bunny
BUNNY_PULL_ZONE=vz-1a2b3c4d-e5f.b-cdn.net
BUNNY_LIBRARY_ID=123456
BUNNY_TOKEN_KEY=xxxxxxxx-xxxx-xxxx-xxxxxxxxxxxx
```

6. `docker compose -f docker-compose.tunnel.yml up -d api`.
7. Optional but recommended: copy the **library's API key** (Stream → your library → API) into
   `.env` as `BUNNY_API_KEY`. The admin session form then lists the library and fills in each
   video's id and length when you pick it. Without it, paste each video's GUID by hand.
8. In Admin → Program → Sesi, use **Ubah** on each video session and choose its video.

Notes:

- Playback URLs are signed per viewing with a short expiry, minted only after the server's access
  check. Nothing is public and nothing is stored.
- The token signs the video's whole **directory**, so the HLS segments the player fetches are
  covered too. A playlist-only token 403s on every segment and presents as a broken video.
- "Durasi (menit)" is only a label shown to learners and admins; the video picker fills it from
  Bunny. Watch progress is computed from the video file's own length, so the label never affects
  completion or the linear lock.
- An incomplete `bunny` config — or one still carrying the dev signing key — **fails at startup**
  rather than 403-ing for every learner at play time.

### Sending real email

**Until `EMAIL_PROVIDER=smtp` is set, no email leaves the server** — every message is written to
the API container's log instead. Email verification is required before purchase, so on the default
setting no real learner can complete one. This is the single setting standing between the app and
taking money.

Resend setup:

1. Create an account at resend.com.
2. **Domains → Add Domain.** A subdomain such as `mail.yourdomain.com` is recommended.
3. Add the DNS records Resend lists at your DNS host: MX and SPF TXT on the bounce subdomain
   (`send.…`), and the DKIM TXT. Optionally add a `_dmarc` TXT with `v=DMARC1; p=none;`.
4. Wait until the domain shows **Verified**.
5. **API Keys → Create API Key** with **Sending access**, restricted to that domain. Treat it as a
   credential and keep it out of the repo.
6. Put it in `.env` — which is gitignored, and must stay that way:

```
EMAIL_PROVIDER=smtp
SMTP_HOST=smtp.resend.com
SMTP_PORT=587
SMTP_USERNAME=resend
SMTP_PASSWORD=re_xxxxxxxxxxxxxxxx
SMTP_FROM_ADDRESS=noreply@mail.yourdomain.com
SMTP_REPLY_TO=halo@yourdomain.com
```

7. **Restart the API.** `.env` is read only at start:
   `docker compose -f docker-compose.tunnel.yml up -d api`
8. Confirm with **Admin → Kirim email uji**. It sends a test message to your own admin address and
   reports one of:
   - `Login SMTP ditolak …` — wrong `SMTP_USERNAME` (must be `resend`) or `SMTP_PASSWORD` (API key).
   - `Alamat pengirim ditolak …` — the domain of `SMTP_FROM_ADDRESS` is not **Verified** in Resend.
   - `Server SMTP tidak dapat dihubungi …` — wrong `SMTP_HOST`/`SMTP_PORT`, or the network blocks
     outbound 587.
   - `Pengiriman gagal (kode SMTP …)` — any other relay rejection; the code is the SMTP status.

Notes:

- The Resend free plan is limited to **100 emails per day** and **3,000 per month**.
- `SMTP_FROM_ADDRESS` must be on the verified domain, or Resend rejects the send.
- An incomplete `smtp` config **fails at startup** rather than falling back to the log. That is
  deliberate: a healthy API silently dropping verification mail is the failure being fixed here.
- Six messages send: verification, password reset, password-changed, the enrolment receipt, the
  certificate and the H-1 live-session reminder (plus the admin test). Only the five archived
  subscription messages still log, and nothing sends those.
- Live-session times are printed in **WIB** (fixed UTC+7). The API container runs UTC, so this is a
  conversion, not a label. A cohort outside western Indonesia needs this made configurable.
- Gmail also works: `SMTP_HOST=smtp.gmail.com`, `SMTP_USERNAME` a real mailbox (Google refuses an
  alias), `SMTP_PASSWORD` a Google app password (2-Step Verification on), and `SMTP_FROM_ADDRESS`
  that mailbox or one it may send as. Roughly 2,000 recipients a day on Workspace.

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
