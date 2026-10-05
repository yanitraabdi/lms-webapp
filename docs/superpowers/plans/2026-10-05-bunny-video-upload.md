# Bunny Video Upload Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Admins upload a video to Bunny Stream from the session video picker. The file goes from the browser straight to Bunny over TUS, using a short-lived signature minted by our API.

**Architecture:**
- **`IVideoLibrary`** gains create-upload and renew-ticket methods. `BunnyVideoLibrary` creates the Bunny video, using the API key server-side, and signs the TUS ticket with a pure `BunnyUploadSigner`.
- **Two admin routes** expose these methods.
- **`VideoPicker`** uploads with `tus-js-client` and refetches the library until the new video is encoded.

**Tech Stack:** .NET 10 minimal APIs (TypedResults), HttpClient, xUnit, Next.js App Router, TanStack Query, `tus-js-client` (new), openapi-typescript client.

**Spec:** `docs/superpowers/specs/2026-10-05-bunny-video-upload-design.md`

## Global Constraints

- **The API key never leaves the server (GR-9).** It must not appear in any response body or in the client bundle. The browser gets only `UploadTicketDto(VideoId, LibraryId, ExpiresAt, Signature, Endpoint)`.
- **Signature:** lowercase hex SHA-256 of the string `libraryId + apiKey + expiresAt + videoId` (concatenated), with `expiresAt` in Unix seconds and set to **2 hours** after issue.
- **Endpoint:** `https://video.bunnycdn.com/tusupload`. Create-video call: `POST https://video.bunnycdn.com/library/{libraryId}/videos` with the header `AccessKey` and the body `{"title": "..."}`, which returns `guid`.
- **Routes:** admin-only, rate limit policy `"media"`. They are `POST /api/admin/video-library/uploads` with body `{ title }`, and `POST /api/admin/video-library/uploads/{videoId}/ticket`.
- **Exact messages:**
  - 400 `Judul video wajib diisi (maksimal 200 karakter).` when the trimmed title is empty or over 200 characters;
  - 409 `Unggah video hanya tersedia saat Bunny aktif.` under the dev provider or with no API key;
  - 502 `Bunny menolak kunci API. Periksa BUNNY_API_KEY.` on Bunny 401/403;
  - 502 `Bunny membalas {status}. Coba lagi.` on another non-success;
  - 502 `Bunny tidak dapat dihubungi. Coba lagi.` on a network failure or unreadable body;
  - 400 `ID video tidak valid.` when the renew route gets a non-GUID.
- **Errors** are thrown as `ProgramException(message, status)`; the existing exception handler maps them to problem details.
- **Audit:** `video_upload_started` with `{ videoId, title }` on create, written through `AppDbContext.AuditLogs` the same way other admin services do. Renewing isn't audited.
- **UI copy:**
  - buttons: `Unggah video baru`, `Mulai unggah`, `Batal`, `Batalkan`;
  - progress: `Mengunggah {pct}% · {sent} dari {total}`;
  - statuses: `Terunggah — Bunny sedang memproses video.`, `Unggahan terputus. Pilih file yang sama untuk melanjutkan.`, `Unggahan tidak selesai`.
- **TUS settings:** `chunkSize` 50 MB (`50 * 1024 * 1024`), `retryDelays` `[0, 3000, 10000, 30000]`, metadata `{ filetype, title }`.
- **Frontend API types** come only from the regenerated `frontend/api-client/schema.ts`.
- **Commits** end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- **Backend checks:** `dotnet build backend/Academy.slnx` with 0 warnings and `dotnet test backend/Academy.slnx` green.
- **Frontend checks:** `cd frontend && npm run lint && npm run build` green.
- **Never** read, print or edit `.env`. Tests never call the real Bunny.

## File map

| File | Task | Responsibility |
|---|---|---|
| `backend/src/Application/Abstractions/Ports.cs` | 1 | `UploadTicketDto`, the two new `IVideoLibrary` methods |
| `backend/src/Infrastructure/Learning/BunnyUploadSigner.cs` (new) | 1 | Pure signature function |
| `backend/src/Infrastructure/Learning/BunnyVideoLibrary.cs` | 1 | Create video + ticket, renew |
| `backend/src/Infrastructure/Learning/UnavailableVideoLibrary.cs` | 1 | Refuses both with 409 |
| `backend/tests/Integration.Tests/VideoLibraryTests.cs` | 1, 2 | Service + endpoint tests |
| `backend/src/Api/Endpoints/ProgramAdminEndpoints.cs` | 2 | Two routes + title validation + audit |
| `frontend/package.json`, `lib/programs.ts`, `components/admin/VideoPicker.tsx`, `api-client/schema.ts` | 3 | Upload UI |

---

### Task 1: Signer and the library's upload methods

**Files:**
- Modify: `backend/src/Application/Abstractions/Ports.cs`
- Create: `backend/src/Infrastructure/Learning/BunnyUploadSigner.cs`
- Modify: `backend/src/Infrastructure/Learning/BunnyVideoLibrary.cs`
- Modify: `backend/src/Infrastructure/Learning/UnavailableVideoLibrary.cs`
- Test: `backend/tests/Integration.Tests/VideoLibraryTests.cs` (the existing `VideoLibraryTests` class with its `StubHandler`/`ThrowingHandler`)

**Interfaces:**
- Produces:
  - `record UploadTicketDto(string VideoId, string LibraryId, long ExpiresAt, string Signature, string Endpoint)`;
  - `IVideoLibrary.CreateUploadAsync(string title, CancellationToken ct = default) : Task<UploadTicketDto>`;
  - `IVideoLibrary.RenewUpload(string videoId) : UploadTicketDto`, which is synchronous because it never calls Bunny;
  - `static string BunnyUploadSigner.Sign(string libraryId, string apiKey, long expiresAt, string videoId)`.

- [ ] **Step 1: Write the failing tests** (add to `VideoLibraryTests`)

```csharp
    [Fact]
    public void Signature_is_sha256_hex_of_library_key_expiry_and_video()
    {
        // Independently computed: sha256("123456" + "library-api-key" + "1760000000" + "vid-1").
        var expected = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes("123456library-api-key1760000000vid-1"))).ToLowerInvariant();
        Assert.Equal(expected, BunnyUploadSigner.Sign("123456", "library-api-key", 1760000000, "vid-1"));
        Assert.Equal(64, expected.Length);
    }

    [Fact]
    public async Task Creating_an_upload_creates_the_video_and_returns_a_signed_ticket()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"guid":"6f1d2c3b-0000-4000-8000-0000000000aa","title":"Sesi 9","status":0}""");
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var ticket = await Library(handler).CreateUploadAsync("Sesi 9");

        Assert.Equal(HttpMethod.Post, handler.Last!.Method);
        Assert.Equal($"https://video.bunnycdn.com/library/{LibraryId}/videos", handler.Last.RequestUri!.ToString());
        Assert.Equal("library-api-key", handler.Last.Headers.GetValues("AccessKey").Single());
        Assert.Equal("6f1d2c3b-0000-4000-8000-0000000000aa", ticket.VideoId);
        Assert.Equal(LibraryId, ticket.LibraryId);
        Assert.Equal("https://video.bunnycdn.com/tusupload", ticket.Endpoint);
        Assert.InRange(ticket.ExpiresAt, before + 7200 - 5, before + 7200 + 5);
        Assert.Equal(BunnyUploadSigner.Sign(LibraryId, "library-api-key", ticket.ExpiresAt, ticket.VideoId), ticket.Signature);
        // GR-9: the key is never part of what the browser receives.
        Assert.DoesNotContain("library-api-key", JsonSerializer.Serialize(ticket));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Bunny menolak kunci API. Periksa BUNNY_API_KEY.")]
    [InlineData(HttpStatusCode.Forbidden, "Bunny menolak kunci API. Periksa BUNNY_API_KEY.")]
    [InlineData(HttpStatusCode.InternalServerError, "Bunny membalas 500. Coba lagi.")]
    public async Task Bunny_refusing_the_create_is_a_502_with_a_reason(HttpStatusCode code, string message)
    {
        var e = await Assert.ThrowsAsync<ProgramException>(
            () => Library(new StubHandler(code, "{}")).CreateUploadAsync("Sesi 9"));
        Assert.Equal(502, e.StatusCode);
        Assert.Equal(message, e.Message);
    }

    [Fact]
    public async Task Bunny_unreachable_on_create_is_a_502()
    {
        var e = await Assert.ThrowsAsync<ProgramException>(
            () => Library(new ThrowingHandler()).CreateUploadAsync("Sesi 9"));
        Assert.Equal(502, e.StatusCode);
        Assert.Equal("Bunny tidak dapat dihubungi. Coba lagi.", e.Message);
    }

    [Fact]
    public void Renewing_signs_a_fresh_ticket_for_the_same_video_without_calling_bunny()
    {
        var handler = new ThrowingHandler();           // any call would throw
        var ticket = Library(handler).RenewUpload("6f1d2c3b-0000-4000-8000-0000000000aa");
        Assert.Equal("6f1d2c3b-0000-4000-8000-0000000000aa", ticket.VideoId);
        Assert.Equal(BunnyUploadSigner.Sign(LibraryId, "library-api-key", ticket.ExpiresAt, ticket.VideoId), ticket.Signature);
    }

    [Fact]
    public async Task Unavailable_library_refuses_uploads_with_409()
    {
        var lib = new UnavailableVideoLibrary("dev");
        var e = await Assert.ThrowsAsync<ProgramException>(() => lib.CreateUploadAsync("x"));
        Assert.Equal(409, e.StatusCode);
        Assert.Equal("Unggah video hanya tersedia saat Bunny aktif.", e.Message);
        Assert.Equal(409, Assert.Throws<ProgramException>(() => lib.RenewUpload("6f1d2c3b-0000-4000-8000-0000000000aa")).StatusCode);
    }
```

Add `using Academy.Application.Programs;` for `ProgramException`. Check that `ProgramException` exposes a `StatusCode` property, and use whatever name it actually has.

Run: `dotnet test backend/tests/Integration.Tests --filter VideoLibraryTests`. Expected: compile failure.

- [ ] **Step 2: Implement**

In `Ports.cs`:
- change the `IVideoLibrary` summary to say it lists videos AND starts uploads, where the file goes browser → Bunny directly and Bunny remains the source of truth for files, with delete and rename staying in Bunny's dashboard;
- add the two methods:

```csharp
    /// <summary>Creates an empty Bunny video and returns a signed TUS ticket for it (spec 2026-10-05).
    /// The file itself never passes through this API. Throws ProgramException 409/502.</summary>
    Task<UploadTicketDto> CreateUploadAsync(string title, CancellationToken ct = default);

    /// <summary>A fresh ticket for an existing video, for an upload that outlived its ticket.</summary>
    UploadTicketDto RenewUpload(string videoId);
```

```csharp
/// <summary>What the browser needs to upload ONE file straight to Bunny over TUS. Never carries the
/// API key (GR-9) — the signature is scoped to this video and expires.</summary>
public record UploadTicketDto(string VideoId, string LibraryId, long ExpiresAt, string Signature, string Endpoint);
```

`BunnyUploadSigner.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace Academy.Infrastructure.Learning;

/// <summary>Bunny's documented TUS presigned-upload signature:
/// sha256_hex(libraryId + apiKey + expirationTime + videoId).</summary>
public static class BunnyUploadSigner
{
    public const string Endpoint = "https://video.bunnycdn.com/tusupload";
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(2);

    public static string Sign(string libraryId, string apiKey, long expiresAt, string videoId)
        => Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{libraryId}{apiKey}{expiresAt}{videoId}"))).ToLowerInvariant();
}
```

In `BunnyVideoLibrary`, add:

```csharp
    public async Task<UploadTicketDto> CreateUploadAsync(string title, CancellationToken ct = default)
    {
        var url = $"https://video.bunnycdn.com/library/{Uri.EscapeDataString(options.LibraryId)}/videos";
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(new { title }) };
        req.Headers.Add("AccessKey", options.ApiKey);

        try
        {
            using var res = await http.SendAsync(req, ct);
            if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new ProgramException("Bunny menolak kunci API. Periksa BUNNY_API_KEY.", 502);
            if (!res.IsSuccessStatusCode)
                throw new ProgramException($"Bunny membalas {(int)res.StatusCode}. Coba lagi.", 502);

            var video = await res.Content.ReadFromJsonAsync<BunnyVideo>(Json, ct);
            if (video is null || string.IsNullOrWhiteSpace(video.Guid))
                throw new ProgramException("Bunny tidak dapat dihubungi. Coba lagi.", 502);
            return RenewUpload(video.Guid);
        }
        catch (Exception e) when ((e is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
                                  && !ct.IsCancellationRequested)
        {
            throw new ProgramException("Bunny tidak dapat dihubungi. Coba lagi.", 502);
        }
    }

    public UploadTicketDto RenewUpload(string videoId)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(BunnyUploadSigner.Lifetime).ToUnixTimeSeconds();
        return new UploadTicketDto(videoId, options.LibraryId, expiresAt,
            BunnyUploadSigner.Sign(options.LibraryId, options.ApiKey, expiresAt, videoId), BunnyUploadSigner.Endpoint);
    }
```

Add `using Academy.Application.Programs;`. `BunnyVideo` already has `Guid`, and the extra fields are ignored. Also update the class summary: it lists videos and starts uploads.

In `UnavailableVideoLibrary`:

```csharp
    private const string NotActive = "Unggah video hanya tersedia saat Bunny aktif.";

    public Task<UploadTicketDto> CreateUploadAsync(string title, CancellationToken ct = default)
        => throw new ProgramException(NotActive, 409);

    public UploadTicketDto RenewUpload(string videoId) => throw new ProgramException(NotActive, 409);
```

`CreateUploadAsync` here is not `async`, so the throw happens synchronously. That is fine for `Assert.ThrowsAsync`, which still observes it through the lambda. If the analyzer complains, return `Task.FromException<UploadTicketDto>(new ProgramException(NotActive, 409))` instead.

- [ ] **Step 3: Run and commit**

Run: `dotnet test backend/tests/Integration.Tests --filter VideoLibraryTests`, then the 0-warning build and the full suite. Expected: all pass.

```bash
git add backend
git commit -m "feat: video library can start a direct-to-Bunny upload with a signed ticket

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Admin upload routes

**Files:**
- Modify: `backend/src/Api/Endpoints/ProgramAdminEndpoints.cs` (next to `GET /video-library`)
- Test: `backend/tests/Integration.Tests/VideoLibraryTests.cs` (the existing `VideoLibraryEndpointTests` class, which runs on the dev provider)

**Interfaces:**
- Consumes: Task 1's `IVideoLibrary.CreateUploadAsync`/`RenewUpload` and `UploadTicketDto`.
- Produces:
  - `POST /api/admin/video-library/uploads` with body `CreateVideoUploadRequest(string Title)`, returning 200 `UploadTicketDto`;
  - `POST /api/admin/video-library/uploads/{videoId}/ticket`, returning 200 `UploadTicketDto`.

- [ ] **Step 1: Write the failing tests** (in `VideoLibraryEndpointTests`, using its existing `Token(UserRole)` / request helpers; add a `Post` helper like its `Get` if there is none)

1. `Upload_routes_require_an_admin`: anonymous gets 401 and a learner gets 403 on `POST /api/admin/video-library/uploads` with body `{ title = "x" }`.
2. `An_empty_or_overlong_title_is_400`: as admin, `title = "  "` and `title = new string('a', 201)` each return 400, with a body containing `Judul video wajib diisi (maksimal 200 karakter).`
3. `Upload_on_the_dev_provider_is_409`: as admin, `title = "Sesi 9"` returns 409, with a body containing `Unggah video hanya tersedia saat Bunny aktif.`
4. `Renew_with_a_non_guid_is_400`: as admin, `POST /api/admin/video-library/uploads/not-a-guid/ticket` returns 400 with `ID video tidak valid.`. With a valid GUID, the dev provider returns 409.

Run: `dotnet test backend/tests/Integration.Tests --filter VideoLibraryEndpointTests`. Expected: 404s or failures.

- [ ] **Step 2: Implement**

In `ProgramAdminEndpoints.cs`, add the request record at the bottom of the file, or wherever that file keeps its request records: `public record CreateVideoUploadRequest(string Title);`. Then add these routes:

```csharp
        // Upload straight to Bunny: this mints the video + a short-lived signed ticket; the browser
        // sends the file to Bunny over TUS. The file never crosses this API or the tunnel (spec D2).
        g.MapPost("/video-library/uploads", async Task<Ok<UploadTicketDto>> (
                CreateVideoUploadRequest r, ClaimsPrincipal u, IVideoLibrary library, AppDbContext db, CancellationToken ct) =>
            {
                var title = r.Title?.Trim() ?? "";
                if (title.Length is 0 or > 200)
                    throw new ProgramException("Judul video wajib diisi (maksimal 200 karakter).", 400);

                var ticket = await library.CreateUploadAsync(title, ct);
                db.AuditLogs.Add(new AuditLog
                {
                    Id = Guid.CreateVersion7(), ActorUserId = u.UserId(), Action = "video_upload_started",
                    Target = ticket.VideoId, Metadata = JsonSerializer.Serialize(new { videoId = ticket.VideoId, title }),
                });
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ticket);
            })
            .RequireRateLimiting("media");

        g.MapPost("/video-library/uploads/{videoId}/ticket", Ok<UploadTicketDto> (string videoId, IVideoLibrary library) =>
            {
                if (!Guid.TryParseExact(videoId, "D", out _))
                    throw new ProgramException("ID video tidak valid.", 400);
                return TypedResults.Ok(library.RenewUpload(videoId));
            })
            .RequireRateLimiting("media");
```

Add any missing usings: `System.Text.Json`, `Academy.Domain.Entities` for `AuditLog`, and `Academy.Infrastructure.Persistence` for `AppDbContext`. The Api project already references Infrastructure; follow how other endpoint files get `AppDbContext`. If endpoints in this codebase never inject `AppDbContext` directly, move the audit write behind a small method on an existing admin service, such as `IProgramAdminService.AuditAsync`, and report which you did.

- [ ] **Step 3: Run and commit**

Run: the endpoint tests, then the 0-warning build and the full suite. Expected: all pass.

```bash
git add backend
git commit -m "feat: admin routes to start and renew a direct-to-Bunny video upload

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Upload inside the video picker

**Files:**
- Modify: `frontend/package.json` and lockfile (`npm install tus-js-client`)
- Modify: `frontend/api-client/schema.ts` (regenerated)
- Modify: `frontend/lib/programs.ts`
- Modify: `frontend/components/admin/VideoPicker.tsx`

**Interfaces:**
- Consumes: Task 2's routes and `UploadTicketDto`.
- Produces:
  - `startVideoUpload(t, title) : Promise<UploadTicket>`;
  - `renewVideoUpload(t, videoId) : Promise<UploadTicket>`.

- [ ] **Step 1: Dependency and client**

```bash
cd frontend && npm install tus-js-client
```

Regenerate the client:

```bash
dotnet build backend/Academy.slnx
cd backend/src/Api
ASPNETCORE_ENVIRONMENT=Development RunMigrations=false SeedSampleData=false \
  dotnet run --no-build --urls http://localhost:8091 > /tmp/api-openapi.log 2>&1 &
until curl -sf http://localhost:8091/openapi/v1.json -o /tmp/openapi.json; do sleep 2; done
pkill -f "urls http://localhost:8091"
cd ../../../frontend
npx openapi-typescript /tmp/openapi.json -o api-client/schema.ts
grep -c "UploadTicketDto" api-client/schema.ts
```

Expected: the count is at least 1.

In `lib/programs.ts`, using its `api` helper:

```ts
export type UploadTicket = components["schemas"]["UploadTicketDto"];

export const startVideoUpload = (t: string, title: string) =>
  api<UploadTicket>("POST", "/api/admin/video-library/uploads", t, { title });

export const renewVideoUpload = (t: string, videoId: string) =>
  api<UploadTicket>("POST", `/api/admin/video-library/uploads/${videoId}/ticket`, t);
```

- [ ] **Step 2: Upload UI in VideoPicker**

Add a `VideoUploader` sub-component in the same file, rendered above the search input only when the list query has data and no `unavailable`. Its states are idle → preparing (file chosen) → uploading → done or failed. It works as follows.

**Idle.** A button `Unggah video baru` triggers a hidden `<input type="file" accept="video/*">`.

**Preparing.** A row shows the file name and a title input prefilled with `file.name.replace(/\.[^.]+$/, "")`, plus `Mulai unggah` and `Batal`. `Mulai unggah` is disabled when the trimmed title is empty or over 200 characters.

**Uploading.** On `Mulai unggah`:

```ts
const ticket = await startVideoUpload(token, title.trim());
const headersOf = (t: UploadTicket) => ({
  AuthorizationSignature: t.signature,
  AuthorizationExpire: String(t.expiresAt),
  VideoId: t.videoId,
  LibraryId: t.libraryId,
});
const upload = new tus.Upload(file, {
  endpoint: ticket.endpoint,
  chunkSize: 50 * 1024 * 1024,
  retryDelays: [0, 3000, 10000, 30000],
  headers: headersOf(ticket),
  metadata: { filetype: file.type, title: title.trim() },
  onProgress: (sent, total) => setProgress({ sent, total }),
  onSuccess: () => setPhase("done"),
  onError: async (err) => {
    // An upload that outlived its ticket: renew once and resume.
    const status = (err as { originalResponse?: { getStatus(): number } }).originalResponse?.getStatus();
    if ((status === 401 || status === 403) && !renewed.current) {
      renewed.current = true;
      const fresh = await renewVideoUpload(token, ticket.videoId);
      upload.options.headers = headersOf(fresh);
      upload.start();
      return;
    }
    setPhase("failed");
  },
});
const previous = await upload.findPreviousUploads();
if (previous.length) upload.resumeFromPreviousUpload(previous[0]);
upload.start();
```

Keep `upload` in a ref so `Batalkan` can call `upload.abort()` and return to idle. While uploading, register a `beforeunload` handler that calls `e.preventDefault()`, and remove it on done, failed or cancel. Show a `<progress max={total} value={sent} aria-label="Progres unggah video">` plus the text `Mengunggah {pct}% · {mb(sent)} dari {mb(total)}`, where `mb(n)` is `${Math.round(n / 1048576)} MB`, and `Batalkan`.

**Done.** The text is `Terunggah — Bunny sedang memproses video.`, and the uploader calls an `onUploaded(videoId)` prop.

**Failed.** The text is `Unggahan terputus. Pilih file yang sama untuk melanjutkan.`, and the uploader returns to idle so the admin can choose the file again. tus-js-client's default fingerprint storage resumes it.

Put the status texts in an `aria-live="polite"` region. Show API errors (`startVideoUpload` / `renewVideoUpload` rejections) using the error's message.

**Changes to VideoPicker itself:**
- Keep `pendingIds: string[]` in state, filled by `onUploaded`.
- The library `useQuery` gets `refetchInterval: (query) => …`. It returns `10_000` while any item is in a non-final status (anything other than `Finished`, `Error` or `UploadFailed`) or any `pendingIds` entry is not yet listed as `Finished`, and `false` otherwise.
- A row whose `status === "Created"` and whose id is not the upload currently running in this tab shows `Unggahan tidak selesai` and is disabled. Extend the existing `note` logic in `VideoRow`, passing the active upload id down.
- There is no auto-select: `onPick` is only ever called by a click.

- [ ] **Step 3: Lint, build, commit**

Run: `cd frontend && npm run lint && npm run build`. Expected: both pass. Then run `grep -rn "AccessKey\|BUNNY_API_KEY" frontend/components frontend/lib`; expected: no match (GR-9).

```bash
git add frontend
git commit -m "feat: upload a video to Bunny from the session video picker

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 4: Browser check (controller, with the PO)**

This uploads a small real file to the Bunny library, so do it with the PO watching:
1. Rebuild the stack with `docker compose up -d --build api frontend`.
2. In a video session's parts editor, pick a ~5 MB file and upload it.
3. Check the progress bar runs and the row reads `Terunggah…`.
4. Within a few minutes, check the video becomes pickable.
5. Check the network tab shows the file going to `video.bunnycdn.com`, not to our API.

---

## Self-review notes

- **The renew route never calls Bunny** (spec §3). It only re-signs for a GUID-shaped id, so a forged id gets a ticket that Bunny rejects; no harm, and no lookup cost.
- **The spec says `Unavailable` hides the button.** The plan renders the uploader only when the list has data without `unavailable`, and the server's 409 backs that up.
