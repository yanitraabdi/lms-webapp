# Email via Resend (SMTP) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Send real email through Resend's SMTP relay, using configuration and docs only, and give admins a status card with a "Kirim email uji" button that proves the setup works.

**Architecture:**
- **Sending:** the existing `SmtpEmailSender` is unchanged as a sender. It only gains a test message (`IEmailSender.SendTestAsync`).
- **Diagnostics:** a small `IEmailDiagnosticsService` reports the active config without secrets and sends the test email to the signed-in admin. It maps `SmtpException`s to actionable Indonesian messages through a pure `SmtpErrorMessage.For`.
- **Config and docs:** these switch from Gmail to Resend.

**Tech Stack:** .NET 10 minimal APIs (TypedResults), System.Net.Mail, xUnit, Next.js App Router, TanStack Query, openapi-typescript client.

**Spec:** `docs/superpowers/specs/2026-10-06-resend-email-design.md`

## Global Constraints

- **GR-9.** The SMTP password (the Resend API key) and the username never appear in any response, log line, UI, or committed file. `.env` is never read, printed or edited by anyone working this plan.
- **Resend values:** host `smtp.resend.com`, port `587` (STARTTLS, `EnableSsl = true`), username `resend`, and the password is a Resend API key (`re_…`).
- **Exact messages:**
  - dev result: `Mode dev — email hanya ditulis ke log API, tidak benar-benar dikirim.`
  - success: `Email uji terkirim ke {email}. Periksa kotak masuk (dan folder spam).`
  - auth: `Login SMTP ditolak — periksa SMTP_USERNAME dan SMTP_PASSWORD (API key Resend).`
  - sender: `Alamat pengirim ditolak — pastikan domain SMTP_FROM_ADDRESS sudah Verified di Resend.`
  - connect: `Server SMTP tidak dapat dihubungi — periksa SMTP_HOST dan SMTP_PORT.`
  - other: `Pengiriman gagal (kode SMTP {(int)StatusCode}).`
- **Test email:** subject `Email uji INVERTA`. It only ever goes to the signed-in admin's own address.
- **Routes:** admin-only, under `/api/admin` (`RequireAuthorization("Admin")`). They are `GET /api/admin/email/status` and `POST /api/admin/email/test`, and the test route uses the `"auth"` rate-limit policy. SMTP failures become `AdminException(message, 502)`, which the existing handler maps to problem details.
- **UI copy:**
  - provider labels: `Dev — hanya log`, `SMTP`;
  - button: `Kirim email uji`;
  - hint: `Pengaturan email ada di .env server (EMAIL_PROVIDER, SMTP_*). Setelah mengubahnya, restart API.`
- **Startup validation stays as is:** an incomplete `smtp` config fails at boot.
- **Commits** end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- **Backend checks:** `dotnet build backend/Academy.slnx` with 0 warnings and `dotnet test backend/Academy.slnx` green.
- **Frontend checks:** `cd frontend && npm run lint && npm run build` green.

## File map

| File | Task | Responsibility |
|---|---|---|
| `backend/src/Application/Abstractions/Ports.cs` | 1 | `IEmailSender.SendTestAsync` |
| `backend/src/Infrastructure/Email/EmailTemplates.cs` | 1 | `Test(name)` |
| `backend/src/Infrastructure/Email/DevEmailSender.cs`, `SmtpEmailSender.cs` | 1 | Log / send the test email |
| `backend/src/Infrastructure/Email/SmtpErrorMessage.cs` (new) | 1 | `SmtpException` → Indonesian message |
| `backend/tests/Integration.Tests/CapturingEmailSender.cs` | 1 | Implement the new method |
| `backend/tests/Integration.Tests/EmailSendingTests.cs` | 1 | Template + mapping tests |
| `backend/src/Application/Admin/EmailDiagnosticsContracts.cs` (new) | 2 | DTOs + `IEmailDiagnosticsService` |
| `backend/src/Infrastructure/Email/EmailDiagnosticsService.cs` (new), `DependencyInjection.cs` | 2 | Status + test send |
| `backend/src/Api/Endpoints/AdminEndpoints.cs` | 2 | Two routes |
| `backend/tests/Integration.Tests/EmailDiagnosticsTests.cs` (new) | 2 | Service + endpoint tests |
| `.env.example`, `docker-compose.yml`, `docker-compose.tunnel.yml`, `EmailOptions.cs`, `DEPLOY.md` | 3 | Resend config + docs |
| `frontend/api-client/schema.ts`, `frontend/lib/admin.ts`, `frontend/app/admin/page.tsx` | 4 | Email card |

---

### Task 1: Test message and SMTP error mapping

**Files:**
- Modify: `backend/src/Application/Abstractions/Ports.cs`
- Modify: `backend/src/Infrastructure/Email/EmailTemplates.cs`, `DevEmailSender.cs`, `SmtpEmailSender.cs`
- Create: `backend/src/Infrastructure/Email/SmtpErrorMessage.cs`
- Modify: `backend/tests/Integration.Tests/CapturingEmailSender.cs` and any other `IEmailSender` implementation (`grep -rln ": IEmailSender\|IEmailSender$" backend`)
- Test: `backend/tests/Integration.Tests/EmailSendingTests.cs`

**Interfaces:**
- Produces:
  - `IEmailSender.SendTestAsync(string toEmail, string name, CancellationToken ct = default)`;
  - `static EmailBody EmailTemplates.Test(string name)`;
  - `static string SmtpErrorMessage.For(SmtpException e)`.

- [ ] **Step 1: Write the failing tests** (add to `EmailSendingTests`; add `using System.Net.Mail; using System.Net.Sockets;`)

```csharp
    [Fact]
    public void The_test_email_has_its_subject_and_greets_by_name()
    {
        var body = EmailTemplates.Test("Budi");
        Assert.Equal("Email uji INVERTA", body.Subject);
        Assert.Contains("Budi", body.Text);
        Assert.Contains("Budi", body.Html);
    }

    [Theory]
    [InlineData(SmtpStatusCode.ClientNotPermitted, "x")]
    [InlineData((SmtpStatusCode)535, "x")]
    [InlineData(SmtpStatusCode.GeneralFailure, "5.7.8 Authentication credentials invalid")]
    public void Auth_failures_point_at_username_and_password(SmtpStatusCode code, string message)
        => Assert.Equal("Login SMTP ditolak — periksa SMTP_USERNAME dan SMTP_PASSWORD (API key Resend).",
            SmtpErrorMessage.For(new SmtpException(code, message)));

    [Theory]
    [InlineData(SmtpStatusCode.MailboxUnavailable, "x")]
    [InlineData(SmtpStatusCode.MailboxNameNotAllowed, "x")]
    [InlineData(SmtpStatusCode.TransactionFailed, "The mail.example.com domain is not verified")]
    [InlineData(SmtpStatusCode.TransactionFailed, "Unauthorized sender domain")]
    public void Sender_rejections_point_at_the_verified_domain(SmtpStatusCode code, string message)
        => Assert.Equal("Alamat pengirim ditolak — pastikan domain SMTP_FROM_ADDRESS sudah Verified di Resend.",
            SmtpErrorMessage.For(new SmtpException(code, message)));

    [Fact]
    public void Connection_failures_point_at_host_and_port()
    {
        var e = new SmtpException("Failure sending mail.", new SocketException((int)SocketError.ConnectionRefused));
        Assert.Equal("Server SMTP tidak dapat dihubungi — periksa SMTP_HOST dan SMTP_PORT.", SmtpErrorMessage.For(e));
    }

    [Fact]
    public void Anything_else_reports_the_smtp_code_without_secrets()
        => Assert.Equal("Pengiriman gagal (kode SMTP 552).",
            SmtpErrorMessage.For(new SmtpException(SmtpStatusCode.ExceededStorageAllocation, "x")));
```

Ordering matters. Auth is checked first, then the sender, then the connection, then "other". The connection test uses `new SmtpException(message, inner)`, whose `StatusCode` is `GeneralFailure` (-1). Check that this message text doesn't trip the auth or sender text checks.

Run: `dotnet test backend/tests/Integration.Tests --filter EmailSendingTests`. Expected: compile failure.

- [ ] **Step 2: Implement**

`Ports.cs`, in `IEmailSender`, after `SendPasswordChangedAsync`:

```csharp
    /// <summary>Admin "Kirim email uji" (spec 2026-10-06): proves the relay config works.</summary>
    Task SendTestAsync(string toEmail, string name, CancellationToken ct = default);
```

`EmailTemplates.cs`. Follow the existing `Build(...)` helper's real signature: read `Verification` and `PasswordChanged` first and mirror them exactly. The template looks like this:

```csharp
    /// <summary>The admin test email: nothing to do, it only proves sending works.</summary>
    public static EmailBody Test(string name) => Build(
        subject: "Email uji INVERTA",
        /* greeting + body as the other templates do, with this paragraph: */
        "Halo {name}, ini email uji dari INVERTA. Jika Anda menerimanya, pengiriman email sudah berfungsi. Tidak ada yang perlu dilakukan.");
```

Use HTML-encoding of `name` exactly as the other templates do. Update the class summary's count of messages.

`DevEmailSender`:

```csharp
    public virtual Task SendTestAsync(string toEmail, string name, CancellationToken ct = default)
    {
        logger.LogInformation("[DEV EMAIL] Email uji → {Email}", toEmail);
        return Task.CompletedTask;
    }
```

`SmtpEmailSender`:

```csharp
    public override Task SendTestAsync(string toEmail, string name, CancellationToken ct = default)
        => SendAsync(toEmail, name, EmailTemplates.Test(name), ct);
```

Update its class summary: "Sends the seven real emails…".

`SmtpErrorMessage.cs`:

```csharp
using System.Net.Mail;
using System.Net.Sockets;

namespace Academy.Infrastructure.Email;

/// <summary>Turns an SmtpException into one actionable Indonesian sentence for the admin test
/// button. Never includes the exception message verbatim — it can echo server details — and never
/// any credential (GR-9).</summary>
public static class SmtpErrorMessage
{
    private const string Auth = "Login SMTP ditolak — periksa SMTP_USERNAME dan SMTP_PASSWORD (API key Resend).";
    private const string Sender = "Alamat pengirim ditolak — pastikan domain SMTP_FROM_ADDRESS sudah Verified di Resend.";
    public const string Unreachable = "Server SMTP tidak dapat dihubungi — periksa SMTP_HOST dan SMTP_PORT.";

    public static string For(SmtpException e)
    {
        var text = (e.Message + " " + e.InnerException?.Message).ToLowerInvariant();
        var code = (int)e.StatusCode;

        // Status codes first, then text: a sender rejection such as "unauthorized sender domain"
        // contains "auth", so the sender text check must run before the auth text check.
        if (e.StatusCode == SmtpStatusCode.ClientNotPermitted || code == 535)
            return Auth;

        if (e.StatusCode is SmtpStatusCode.MailboxUnavailable or SmtpStatusCode.MailboxNameNotAllowed
            || text.Contains("domain") || text.Contains("not verified"))
            return Sender;

        if (text.Contains("auth"))
            return Auth;

        if (e.InnerException is SocketException or IOException || e.StatusCode == SmtpStatusCode.GeneralFailure)
            return Unreachable;

        return $"Pengiriman gagal (kode SMTP {code}).";
    }
}
```

The 535 case: `(SmtpStatusCode)535` is not a named enum value, but casting an int keeps it as 535. Implement `SendTestAsync` in `CapturingEmailSender`, and any other test double, following its existing pattern (record the call).

- [ ] **Step 3: Run and commit**

Run: `dotnet test backend/tests/Integration.Tests --filter EmailSendingTests`, then the 0-warning build and the full suite. Expected: all pass.

```bash
git add backend
git commit -m "feat: a test email and plain-language SMTP failure messages

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Email diagnostics service and admin routes

**Files:**
- Create: `backend/src/Application/Admin/EmailDiagnosticsContracts.cs`
- Create: `backend/src/Infrastructure/Email/EmailDiagnosticsService.cs`
- Modify: `backend/src/Infrastructure/DependencyInjection.cs` (register it as scoped, next to the email sender)
- Modify: `backend/src/Api/Endpoints/AdminEndpoints.cs`
- Test: `backend/tests/Integration.Tests/EmailDiagnosticsTests.cs`

**Interfaces:**
- Consumes: Task 1's `IEmailSender.SendTestAsync` and `SmtpErrorMessage.For`.
- Produces:
  - `record EmailStatusDto(string Provider, string Host, int Port, string FromAddress, string FromName, string ReplyTo)`;
  - `record EmailTestResultDto(bool Sent, string Message)`;
  - `IEmailDiagnosticsService.GetStatus()`;
  - `IEmailDiagnosticsService.SendTestAsync(Guid adminId, CancellationToken)`;
  - routes `GET /api/admin/email/status` and `POST /api/admin/email/test`.

- [ ] **Step 1: Write the failing tests** (`EmailDiagnosticsTests.cs`; use `AuthApiFactory` and the token helpers other admin tests use, e.g. `VideoLibraryEndpointTests`)

1. `Email_routes_require_an_admin`: anonymous gets 401 and a learner gets 403 on both routes.
2. `Status_reports_the_provider_and_never_the_password`: as admin, `GET /api/admin/email/status` returns 200 with `provider == "dev"`, and the raw JSON does not contain `password` or `username` (case-insensitive).
3. `Test_email_in_dev_mode_says_it_only_logs`: as admin, `POST /api/admin/email/test` returns 200 with `sent == false` and the exact dev message.
4. `An_smtp_failure_becomes_a_502_with_an_actionable_message`, service-level:
   - Construct `EmailDiagnosticsService` directly with `new EmailOptions { Provider = "smtp", Host = "smtp.resend.com", Username = "resend", Password = "re_secret", FromAddress = "noreply@mail.example.com" }`.
   - Use a fake `IEmailSender` whose `SendTestAsync` throws `new SmtpException(SmtpStatusCode.MailboxUnavailable, "domain not verified")`.
   - Use an `AppDbContext` from `factory.Services` holding a registered user, created via the API as other tests do.
   - `SendTestAsync(userId)` throws `AdminException` with `StatusCode` 502 and the sender-rejected message, and the message does not contain `re_secret`.
5. `An_smtp_success_names_the_recipient`: same setup, with a fake sender that succeeds. The result has `Sent == true` and message `Email uji terkirim ke {that user's email}. Periksa kotak masuk (dan folder spam).`
6. `A_relay_that_never_answers_times_out_as_unreachable`: a fake sender whose `SendTestAsync` awaits `Task.Delay(Timeout.Infinite, ct)`. Build the service with `SendTimeout = TimeSpan.FromMilliseconds(200)` (object initializer; make the test project see internals via `InternalsVisibleTo` if it doesn't already, or make the property public with a comment). `SendTestAsync` then throws `AdminException` 502 with `Server SMTP tidak dapat dihubungi — periksa SMTP_HOST dan SMTP_PORT.`

The fake `IEmailSender` can derive from `DevEmailSender`, overriding `SendTestAsync`, with `NullLogger<DevEmailSender>.Instance`, so it doesn't have to implement the whole interface.

Run: `dotnet test backend/tests/Integration.Tests --filter EmailDiagnosticsTests`. Expected: failures or compile errors.

- [ ] **Step 2: Implement**

`EmailDiagnosticsContracts.cs`:

```csharp
namespace Academy.Application.Admin;

/// <summary>The active email relay, for the admin card. Never carries Username or Password (GR-9).</summary>
public record EmailStatusDto(string Provider, string Host, int Port, string FromAddress, string FromName, string ReplyTo);

public record EmailTestResultDto(bool Sent, string Message);

public interface IEmailDiagnosticsService
{
    EmailStatusDto GetStatus();

    /// <summary>Sends the test email to the signed-in admin's OWN address — never anyone else's.
    /// Throws AdminException 502 with an actionable message when the relay refuses.</summary>
    Task<EmailTestResultDto> SendTestAsync(Guid adminId, CancellationToken ct = default);
}
```

`EmailDiagnosticsService.cs`:

```csharp
using System.Net.Mail;
using Academy.Application.Abstractions;
using Academy.Application.Admin;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Email;

public class EmailDiagnosticsService(EmailOptions options, IEmailSender sender, AppDbContext db) : IEmailDiagnosticsService
{
    /// <summary>Bounds one test send. Settable (init) so tests can shorten it.</summary>
    internal TimeSpan SendTimeout { get; init; } = TimeSpan.FromSeconds(20);

    public EmailStatusDto GetStatus() => new(
        options.IsSmtp ? "smtp" : "dev", options.Host, options.Port,
        options.FromAddress, options.FromName, options.ReplyTo);

    public async Task<EmailTestResultDto> SendTestAsync(Guid adminId, CancellationToken ct = default)
    {
        var admin = await db.Users.Where(u => u.Id == adminId)
            .Select(u => new { u.Email, u.Name }).FirstOrDefaultAsync(ct)
            ?? throw new AdminException("Akun admin tidak ditemukan.", 404);

        if (!options.IsSmtp)
            return new(false, "Mode dev — email hanya ditulis ke log API, tidak benar-benar dikirim.");

        // SmtpClient.Timeout does not apply to SendMailAsync, so an unreachable host (dropped SYN)
        // would hang the button; bound it here. A timeout surfaces as OperationCanceledException,
        // not SmtpException, and must not become a 500.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(SendTimeout);
        try
        {
            await sender.SendTestAsync(admin.Email, admin.Name, timeout.Token);
        }
        catch (SmtpException e)
        {
            throw new AdminException(SmtpErrorMessage.For(e), 502);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AdminException(SmtpErrorMessage.Unreachable, 502);
        }
        return new(true, $"Email uji terkirim ke {admin.Email}. Periksa kotak masuk (dan folder spam).");
    }
}
```

Check the `User` entity's real property names (`Email`, `Name`, or `FullName`) and use them. Register it with `services.AddScoped<IEmailDiagnosticsService, EmailDiagnosticsService>();` right after the `IEmailSender` registration.

Routes in `AdminEndpoints.cs`, inside the existing admin group `g`:

```csharp
        // ---- email relay (spec 2026-10-06) ----
        g.MapGet("/email/status", Ok<EmailStatusDto> (IEmailDiagnosticsService s) => TypedResults.Ok(s.GetStatus()));

        // Only ever mails the caller themself; rate-limited like auth so it cannot be used to hammer the relay.
        g.MapPost("/email/test", async Task<Ok<EmailTestResultDto>> (
                ClaimsPrincipal u, IEmailDiagnosticsService s, CancellationToken ct) =>
            TypedResults.Ok(await s.SendTestAsync(u.UserId(), ct)))
            .RequireRateLimiting("auth");
```

- [ ] **Step 3: Run and commit**

Run: the new tests, then the 0-warning build and the full suite. Expected: all pass.

```bash
git add backend
git commit -m "feat: admin email status and a test-send button endpoint

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Resend configuration and docs

**Files:**
- Modify: `.env.example` (the email block only; this is the EXAMPLE file, never `.env`)
- Modify: `docker-compose.yml` (the `api` service `environment`)
- Modify: `docker-compose.tunnel.yml` (the `SMTP_HOST` fallback)
- Modify: `backend/src/Infrastructure/Email/EmailOptions.cs` (the `Host` default and doc comments)
- Modify: `DEPLOY.md` (the env table rows for `EMAIL_PROVIDER`/`SMTP_*` and the "Sending real email" section)

**Interfaces:** none (config and docs).

- [ ] **Step 1: `.env.example`**

Replace the email block, keeping the variable names, with:

```bash
# ---- Email ----
# dev = log to the API console (default). smtp = actually send.
# Resend (recommended): SMTP_HOST=smtp.resend.com, SMTP_PORT=587, SMTP_USERNAME=resend,
# SMTP_PASSWORD = a Resend API key (re_…) with "Sending access" for your verified domain.
# SMTP_FROM_ADDRESS must be on that verified domain. See DEPLOY.md → "Sending real email".
# Alternative: Gmail — smtp.gmail.com, a real mailbox as username, and a Google APP PASSWORD.
EMAIL_PROVIDER=dev
SMTP_HOST=smtp.resend.com
SMTP_PORT=587
SMTP_USERNAME=resend
SMTP_PASSWORD=
SMTP_FROM_ADDRESS=
SMTP_FROM_NAME=INVERTA
SMTP_REPLY_TO=
```

- [ ] **Step 2: Compose files**

In `docker-compose.yml`, add the same block to the `api` service `environment` that `docker-compose.tunnel.yml` has, with the host fallback `smtp.resend.com`:

```yaml
      Email__Provider: "${EMAIL_PROVIDER:-dev}"
      Email__Host: "${SMTP_HOST:-smtp.resend.com}"
      Email__Port: "${SMTP_PORT:-587}"
      Email__Username: "${SMTP_USERNAME:-}"
      Email__Password: "${SMTP_PASSWORD:-}"
      Email__FromAddress: "${SMTP_FROM_ADDRESS:-}"
      Email__FromName: "${SMTP_FROM_NAME:-INVERTA}"
      Email__ReplyTo: "${SMTP_REPLY_TO:-}"
```

In `docker-compose.tunnel.yml`, change `${SMTP_HOST:-smtp.gmail.com}` to `${SMTP_HOST:-smtp.resend.com}`.

- [ ] **Step 3: `EmailOptions.cs`**

- `Host` default: `"smtp.resend.com"`.
- Class summary: "Provider `smtp` sends over any SMTP relay — Resend (recommended) or Gmail."
- `Username` doc: "The SMTP login: literally `resend` for Resend; a real mailbox for Gmail (Google refuses an alias)."
- `Password` doc: "A Resend API key (`re_…`) or a Gmail app password. Server config / secret store only, never the repo (GR-9)."
- `FromAddress` doc: "Must be on a domain the relay may send for — for Resend, a domain shown as Verified; otherwise the relay rejects it."

The validation code is unchanged.

- [ ] **Step 4: `DEPLOY.md`**

Update the env-table rows:
- `SMTP_HOST/PORT`: defaults `smtp.resend.com` / `587`.
- `SMTP_USERNAME`: `resend` for Resend.
- `SMTP_PASSWORD`: a Resend API key.

Rewrite "### Sending real email" to cover, in this order:
1. Why it matters: verification is required before purchase, and on `dev` nothing leaves the server.
2. Resend setup:
   - create an account;
   - add a domain (a subdomain like `mail.yourdomain.com` is recommended);
   - add the DNS records Resend lists (MX and SPF TXT on the bounce subdomain, the DKIM TXT, and optionally `_dmarc` `v=DMARC1; p=none;`);
   - wait for **Verified**;
   - create an API key with **Sending access** restricted to that domain.
3. The `.env` block (Step 1's values with `EMAIL_PROVIDER=smtp` and example From/Reply-To on the verified domain).
4. **Restart the API.** `.env` is read at start: `docker compose -f docker-compose.tunnel.yml up -d api`.
5. Confirm with **Admin → Kirim email uji**, and what each error message means.
6. Notes:
   - the free plan is limited to 100 emails per day and 3,000 per month;
   - From must be on the verified domain;
   - an incomplete `smtp` config fails at startup on purpose;
   - Gmail as an alternative, in one short paragraph.

- [ ] **Step 5: Verify and commit**

Run: `dotnet build backend/Academy.slnx` (0 warnings) and `docker compose config -q && docker compose -f docker-compose.tunnel.yml config -q`. Both compose files must parse; run the latter with no `.env` changes. Then run the full `dotnet test backend/Academy.slnx` once, since the default host changed. Expected: green.

```bash
git add .env.example docker-compose.yml docker-compose.tunnel.yml backend/src/Infrastructure/Email/EmailOptions.cs DEPLOY.md
git commit -m "docs: send email through Resend SMTP; local compose can send too

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Email card on the admin home

**Files:**
- Modify: `frontend/api-client/schema.ts` (regenerated)
- Modify: `frontend/lib/admin.ts`
- Modify: `frontend/app/admin/page.tsx`

**Interfaces:**
- Consumes: Task 2's routes and DTOs.
- Produces:
  - `getEmailStatus(t)`;
  - `sendTestEmail(t)`.

- [ ] **Step 1: Regenerate the client**

```bash
dotnet build backend/Academy.slnx
cd backend/src/Api
ASPNETCORE_ENVIRONMENT=Development RunMigrations=false SeedSampleData=false \
  dotnet run --no-build --urls http://localhost:8091 > /tmp/api-openapi.log 2>&1 &
until curl -sf http://localhost:8091/openapi/v1.json -o /tmp/openapi.json; do sleep 2; done
pkill -f "urls http://localhost:8091"
cd ../../../frontend
npx openapi-typescript /tmp/openapi.json -o api-client/schema.ts
grep -c "EmailStatusDto\|EmailTestResultDto" api-client/schema.ts
```

Expected: the count is at least 2.

- [ ] **Step 2: `lib/admin.ts`**

```ts
export type EmailStatus = components["schemas"]["EmailStatusDto"];
export type EmailTestResult = components["schemas"]["EmailTestResultDto"];

export const getEmailStatus = (t: string) => api<EmailStatus>("GET", "/api/admin/email/status", t);
export const sendTestEmail = (t: string) => api<EmailTestResult>("POST", "/api/admin/email/test", t);
```

Use that file's existing `api` helper and `components` import. If the helper throws an `Error` whose message is the problem `title`, the 502 message reaches the UI as `e.message`.

- [ ] **Step 3: The card in `app/admin/page.tsx`**

Below the KPI grid, add an `EmailCard` component in the same file. It is styled like the KPI cards (`rounded-lg border border-border bg-surface p-5 shadow-sm`):
- **Data:** `useQuery(["admin-email-status"], getEmailStatus)`.
- **Header:** `Email`, with a small badge: `SMTP` when `provider === "smtp"`, otherwise `Dev — hanya log`.
- **Details:** a definition list of `Server` (`{host}:{port}`), `Pengirim` (`{fromName} <{fromAddress}>`) and `Balasan ke` (`{replyTo}`). Each row is shown only when its value is non-empty.
- **Dev-mode warning:** when `provider !== "smtp"`, render the badge in the warning tone (`text-warning` / `bg-warning-soft`, as `QuestionPicker`'s badge does), plus a line under the header: `Email belum benar-benar dikirim. Atur EMAIL_PROVIDER=smtp untuk mengirim.`
- **Recipient** (muted, small, above the button): `Dikirim ke {useAuth().user?.email}`, so the admin knows where to look before clicking.
- **Button:** `<Button>` `Kirim email uji`. It calls `sendTestEmail` through a `useMutation`, and is disabled with a spinner while pending.
- **Result** goes in a `<p aria-live="polite">` below the button:
  - success: `result.message` in normal ink;
  - `sent === false`: the message in muted ink;
  - error: `error.message` in the danger colour.
- **Hint** (muted, small): `Pengaturan email ada di .env server (EMAIL_PROVIDER, SMTP_*). Setelah mengubahnya, restart API.`
- The card never displays a username or password; the DTO has neither.

- [ ] **Step 4: Lint, build, commit**

Run: `cd frontend && npm run lint && npm run build`. Expected: both pass.

```bash
git add frontend
git commit -m "feat: email status card with a test-send button on the admin home

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Self-review notes

- **Rate limit.** Spec §2.2 says the route is rate-limited with the `"auth"` policy, and the plan follows that.
- **The 502 path is tested at service level,** with a fake sender. There is no live relay in tests (spec §3).
- **No database changes,** so no `/postgresql-table-design` review is needed.
