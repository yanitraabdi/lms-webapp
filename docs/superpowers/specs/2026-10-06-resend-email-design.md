# Email via Resend (SMTP) — design

**Date:** 2026-10-06 · **Status:** approved in brainstorming (the PO approved the design and asked for spec → plan → review → execution)

## 1. Context and decision

The API already sends six real emails over SMTP through `SmtpEmailSender`:
- **Account emails:** verification, password reset, password changed.
- **Programme emails:** enrolment receipt, certificate, and the H-1 live-session reminder.

`Email:Provider` is `dev` by default, which only logs messages. The SMTP config and docs are written for Gmail with an app password.

**Decision (PO, 2026-10-06):** send through **Resend over SMTP**. This is a configuration change: the sending code stays as it is. Resend's HTTP API, bounce webhooks and a send queue are out of scope.

Resend's SMTP settings:

| Setting | Value |
|---|---|
| Host | `smtp.resend.com` |
| Port | `587` (STARTTLS) |
| Username | `resend` (literally) |
| Password | a Resend API key (`re_…`) |
| From | an address on a domain verified in Resend |

## 2. Changes

### 2.1 Configuration and docs
- **`.env.example`.** The email block defaults to Resend: `SMTP_HOST=smtp.resend.com`, `SMTP_PORT=587`, `SMTP_USERNAME=resend`. The comment explains that `SMTP_PASSWORD` is a Resend API key with Sending access, and lists Gmail (app password) as the alternative.
- **`docker-compose.tunnel.yml`.** The `SMTP_HOST` fallback becomes `smtp.resend.com`.
- **`docker-compose.yml` (local).** The `api` service gets the same `Email__*` environment block as the tunnel file, so real sending can be tried locally. The default stays `dev`.
- **`EmailOptions`.** The `Host` default becomes `smtp.resend.com`. The doc comments stop being Gmail-specific:
  - Username is "the SMTP login — `resend` for Resend; for Gmail a real mailbox".
  - Password is "a Resend API key or a Gmail app password".
  - FromAddress must be on a domain the relay is allowed to send for.

  The startup validation is unchanged: an incomplete `smtp` config still fails at boot.
- **`DEPLOY.md`.** The "Sending real email" section is rewritten for Resend:
  1. Create a Resend account and add the sending domain (a subdomain such as `mail.yourdomain.com` is recommended).
  2. Add the DNS records Resend shows (MX and SPF TXT for the bounce subdomain, plus the DKIM TXT), and optionally a DMARC record `_dmarc` with `v=DMARC1; p=none;`. Wait until the domain shows **Verified**.
  3. Create an API key with **Sending access**, restricted to that domain.
  4. Set `EMAIL_PROVIDER=smtp` and the `SMTP_*` values, then **restart the API**, since `.env` is read at start.
  5. Use **Admin → Kirim email uji** to confirm.

  It also notes:
  - the free plan is limited to 100 emails per day and 3,000 per month;
  - From must use the verified domain;
  - replies go to `SMTP_REPLY_TO`.

  The env-var table rows for `SMTP_*` are updated. Gmail stays as a short "alternative" paragraph.

### 2.2 Test email and status (admin)

**Port.** `IEmailSender` gains `Task SendTestAsync(string toEmail, string name, CancellationToken ct = default)`.
- `DevEmailSender` logs `[DEV EMAIL] Email uji → {Email}`.
- `SmtpEmailSender` sends `EmailTemplates.Test(name)`, which has:
  - **Subject:** `Email uji INVERTA`.
  - **Body:** a short Indonesian message saying email sending works and nothing needs to be done.

**Service.** `IEmailDiagnosticsService` is an Application port with an Infrastructure implementation that uses `EmailOptions`, `IEmailSender` and `AppDbContext`.
- **`GetStatus()`** returns `EmailStatusDto(string Provider, string Host, int Port, string FromAddress, string FromName, string ReplyTo)`. It **never** includes the Username or the Password (GR-9). In `dev` mode, Host and From are returned as configured (they may be empty).
- **`SendTestAsync(Guid adminId, CancellationToken ct)`** looks up the admin's email and name, then calls `IEmailSender.SendTestAsync`. It returns `EmailTestResultDto(bool Sent, string Message)`:
  - in `dev` mode: `Sent=false`, Message `Mode dev — email hanya ditulis ke log API, tidak benar-benar dikirim.`;
  - in `smtp` mode, on success: `Sent=true`, Message `Email uji terkirim ke {email}. Periksa kotak masuk (dan folder spam).`;
  - in `smtp` mode, on `SmtpException`: throws `AdminException(message, 502)`, with the message from `SmtpErrorMessage.For(e)`.
  - in `smtp` mode, when the send takes longer than **20 seconds** (`SmtpClient.Timeout` does not apply to `SendMailAsync`): throws `AdminException("Server SMTP tidak dapat dihubungi — periksa SMTP_HOST dan SMTP_PORT.", 502)`.

  The card also shows the recipient before sending ("Dikirim ke {email}"), and in `dev` mode a warning-toned badge with `Email belum benar-benar dikirim. Atur EMAIL_PROVIDER=smtp untuk mengirim.`

**`SmtpErrorMessage.For(SmtpException e)`** is a pure function in Infrastructure/Email that returns Indonesian text with no secrets:

| Condition | Message |
|---|---|
| Authentication failure: `StatusCode` is `ClientNotPermitted` or 535, or the message contains "auth" (case-insensitive) | `Login SMTP ditolak — periksa SMTP_USERNAME dan SMTP_PASSWORD (API key Resend).` |
| Sender or mailbox rejected: `MailboxUnavailable`/550, `MailboxNameNotAllowed`/553, or the message contains "domain" or "not verified" | `Alamat pengirim ditolak — pastikan domain SMTP_FROM_ADDRESS sudah Verified di Resend.` |
| Cannot connect: `GeneralFailure`, or an inner `SocketException`/`IOException` | `Server SMTP tidak dapat dihubungi — periksa SMTP_HOST dan SMTP_PORT.` |
| Otherwise | `Pengiriman gagal (kode SMTP {(int)StatusCode}).` |

**Routes.** Both are admin-only, under `/api/admin`.
- `GET /api/admin/email/status` returns `EmailStatusDto`.
- `POST /api/admin/email/test` returns `EmailTestResultDto`, or a 502 problem. It is rate-limited with the existing `"auth"` policy.
- The test email only ever goes to the signed-in admin's own address, so the route can't be used to email anyone else.

### 2.3 Admin UI

An **Email** card on `/admin`, under the KPI grid:
- **Contents:** the provider label (`Dev — hanya log` or `SMTP`), plus host:port, From and Reply-To when set.
- **Button:** `Kirim email uji`. While it runs, it shows a spinner and is disabled.
- **Result:** shown below the button in an `aria-live="polite"` region. A success message is plain; a dev-mode message is muted; an error (the 502 problem title) uses the error colour.
- **Hint** in the card: `Pengaturan email ada di .env server (EMAIL_PROVIDER, SMTP_*). Setelah mengubahnya, restart API.`

## 3. Tests

- **`SmtpErrorMessage.For`:** one test per row of the table, including a GeneralFailure with an inner SocketException.
- **`EmailTemplates.Test`:** the subject, plus a body containing the name.
- **Diagnostics service:**
  - with a fake `IEmailSender` that throws `SmtpException(SmtpStatusCode.MailboxUnavailable, "domain not verified")`, `SendTestAsync` throws `AdminException` 502 with the sender-rejected message;
  - in `dev` mode it returns `Sent=false` with the dev message.
- **Endpoints (dev provider):**
  - both routes return 401 anonymously and 403 for a learner;
  - `GET status` as admin returns `Provider=dev`, and the raw JSON contains no `password` key;
  - `POST test` as admin returns `Sent=false` with the dev message.
- **Startup validation:** the existing behaviour stays green.

## 4. Out of scope

- Resend's HTTP API.
- Webhooks for bounces or complaints.
- A delivery log.
- An outbox or retry queue.
- Editing email settings from the admin UI. Settings stay in server `.env` (GR-9).
