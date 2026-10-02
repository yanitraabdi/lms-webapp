# Admin Session Editing and Bunny Video Picker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let an admin edit an existing programme session and attach a Bunny Stream video to it by picking from the library, without the edit silently detaching quizzes or corrupting session order.

**Architecture:** The existing update endpoint is made safe on the server (it stops overwriting the quiz link, order and type, and re-arms the live reminder on reschedule). A new read-only port `IVideoLibrary` lists the Bunny library server-side with the library API key, exposed through one admin endpoint. The frontend gains an "Ubah" button that reuses `SessionForm` in edit mode, with a `VideoPicker` that fills the video id and duration.

**Tech Stack:** .NET 10 minimal APIs (TypedResults), EF Core 10 + Npgsql, xUnit + Testcontainers integration tests, Next.js App Router, TanStack Query, openapi-typescript generated client.

**Spec:** `docs/superpowers/specs/2026-10-02-admin-session-crud-design.md`

## Global Constraints

- All user-facing strings are Bahasa Indonesia.
- Minimal API endpoints return `TypedResults` so OpenAPI carries response schemas.
- The Bunny library API key is server-side config only (GR-9). It never appears in a DTO, a log line, or the frontend bundle.
- `TreatWarningsAsErrors` is on for the backend; a warning fails the build.
- No database migration. `ReminderSentAt` already exists on `ProgramSession`; the API key is config.
- Frontend API types come from the generated client (`frontend/api-client/schema.ts`). Never hand-write a DTO type.
- `UpdateSessionAsync` must keep `AssessmentId`, `OrderIndex` and `Type` as stored, whatever the request says. `CreateSessionAsync` keeps setting all three.
- Only Bunny videos in status `Finished` are pickable.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## How to run things

```bash
# backend build
dotnet build backend/Academy.slnx

# one integration test class (Testcontainers starts its own Postgres; Docker must be running)
dotnet test backend/tests/Integration.Tests --filter "FullyQualifiedName~SessionUpdateTests"

# full backend suite
dotnet test backend/Academy.slnx

# frontend checks
cd frontend && npx tsc --noEmit && npx next lint
```

## File map

| File | Task | Responsibility |
|---|---|---|
| `backend/src/Infrastructure/Programs/ProgramAdminService.cs` | 1, 2 | Safe update semantics; video-id rule |
| `backend/tests/Integration.Tests/SessionUpdateTests.cs` | 1 | New: update preserves quiz/order/type, re-arms reminder |
| `backend/tests/Integration.Tests/SessionAssetValidationTests.cs` | 2 | New: video-id rule |
| `backend/src/Application/Abstractions/Ports.cs` | 3 | `IVideoLibrary` port and its DTOs |
| `backend/src/Infrastructure/Learning/BunnyVideoLibrary.cs` | 3 | New: lists the Bunny library |
| `backend/src/Infrastructure/Learning/UnavailableVideoLibrary.cs` | 3 | New: the "no library" fallback |
| `backend/src/Infrastructure/Learning/VideoOptions.cs` | 3 | `ApiKey` setting |
| `backend/src/Infrastructure/DependencyInjection.cs` | 3 | Registration |
| `docker-compose.tunnel.yml`, `.env.example`, `DEPLOY.md` | 3 | `BUNNY_API_KEY` |
| `backend/tests/Integration.Tests/VideoLibraryTests.cs` | 3, 4 | New: mapping, failure modes, endpoint auth |
| `backend/src/Api/Endpoints/ProgramAdminEndpoints.cs` | 4 | `GET /api/admin/video-library` |
| `frontend/api-client/schema.ts` | 4 | Regenerated |
| `frontend/lib/programs.ts` | 4 | `listVideoLibrary` client |
| `frontend/components/admin/VideoPicker.tsx` | 5 | New: library picker |
| `frontend/components/admin/SessionForm.tsx` | 5 | Edit mode, picker, no `"sample"` default |
| `frontend/components/admin/SessionManager.tsx` | 5 | "Ubah" button |

---

### Task 1: Make the session update endpoint safe to call

The update endpoint has never been called by any screen or test. `UpdateSessionAsync` runs every field through `ApplySession`, which overwrites the quiz link, order and type from the request. An edit form sending `assessmentId: null` would silently detach the quiz, and the session would then complete on watching alone.

**Files:**
- Modify: `backend/src/Infrastructure/Programs/ProgramAdminService.cs` (method `UpdateSessionAsync`, around line 141)
- Create: `backend/tests/Integration.Tests/SessionUpdateTests.cs`

**Interfaces:**
- Consumes: existing `PUT /api/admin/sessions/{id}` with body `UpsertSessionRequest(string Type, string Title, string? Description, int OrderIndex, string? ProviderAssetId, int? DurationSeconds, DateTimeOffset? ScheduledAt, string? LiveMode, string? JoinUrl, string? Location, Guid? AssessmentId)`.
- Produces: the same endpoint, now ignoring `Type`, `OrderIndex` and `AssessmentId`, and resetting `ReminderSentAt` to null when `ScheduledAt` changes. Task 5's form relies on this.

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/Integration.Tests/SessionUpdateTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Academy.Application.Assessments;
using Academy.Application.Auth;
using Academy.Application.Programs;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>
/// The session update endpoint existed for months with no screen or test calling it. It ran every
/// field through ApplySession, which overwrote the quiz link, the order and the type from the
/// request — so the first edit form wired to it would have silently detached the session's quiz,
/// and the session would then complete on watching alone. These pin the safe semantics: an edit
/// owns the session's CONTENT; the quiz, the order and the type have their own endpoints.
/// </summary>
public class SessionUpdateTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    private static readonly DateTimeOffset LiveAt = new(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private record Ctx(string Admin, Guid VideoId, Guid AssessmentId, Guid LiveId);

    // ---- the quiz stays attached ----

    [Fact]
    public async Task An_edit_that_sends_no_quiz_does_not_detach_the_quiz()
    {
        var c = await Seed();

        var res = await Put(c, c.VideoId, Body("Video", "Judul baru", asset: "sample", duration: 600));
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        var stored = await Session(c.VideoId);
        Assert.Equal(c.AssessmentId, stored.AssessmentId);
    }

    // ---- order and type have their own rules ----

    [Fact]
    public async Task An_edit_cannot_move_the_session()
    {
        // The live session holds order 2. Sending 2 for the video used to collide with
        // UNIQUE(program_id, order_index) and fail as "Urutan sesi bentrok".
        var c = await Seed();

        var res = await Put(c, c.VideoId, Body("Video", "Sesi video", orderIndex: 2, asset: "sample", duration: 900));
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        Assert.Equal(1, (await Session(c.VideoId)).OrderIndex);
    }

    [Fact]
    public async Task An_edit_cannot_change_the_session_type()
    {
        // Watch progress and completions recorded against a video must not end up attached to a
        // session whose rules are a live session's.
        var c = await Seed();

        await Put(c, c.VideoId, Body("Live", "Sesi video", at: LiveAt));

        Assert.Equal(SessionType.Video, (await Session(c.VideoId)).Type);
    }

    // ---- the edit actually edits ----

    [Fact]
    public async Task An_edit_changes_the_content_fields()
    {
        var c = await Seed();
        var videoId = Guid.NewGuid().ToString();

        await Put(c, c.VideoId, Body("Video", "Sesi 1: Format TOEFL", asset: videoId, duration: 912));

        var stored = await Session(c.VideoId);
        Assert.Equal("Sesi 1: Format TOEFL", stored.Title);
        Assert.Equal(videoId, stored.ProviderAssetId);
        Assert.Equal(912, stored.DurationSeconds);
    }

    // ---- a rescheduled live session is reminded again ----

    [Fact]
    public async Task Rescheduling_a_live_session_re_arms_its_reminder()
    {
        // The reminder sweep claims a session by stamping ReminderSentAt and never revisits it.
        // Without a reset, learners get a reminder for the old slot and none for the new one.
        var c = await Seed();
        await MarkReminderSent(c.LiveId);

        await Put(c, c.LiveId, Body("Live", "Sesi Live", orderIndex: 2, at: LiveAt.AddDays(1)));

        Assert.Null((await Session(c.LiveId)).ReminderSentAt);
    }

    [Fact]
    public async Task Editing_a_live_session_without_moving_it_keeps_the_reminder_claimed()
    {
        // Renaming must not cause a second reminder for the same slot.
        var c = await Seed();
        await MarkReminderSent(c.LiveId);

        await Put(c, c.LiveId, Body("Live", "Sesi Live — nama baru", orderIndex: 2, at: LiveAt));

        Assert.NotNull((await Session(c.LiveId)).ReminderSentAt);
    }

    // ================================================================ helpers

    private static object Body(
        string type, string title, int orderIndex = 1, string? asset = null, int? duration = null,
        DateTimeOffset? at = null) => new
    {
        type, title, description = (string?)null, orderIndex,
        providerAssetId = asset, durationSeconds = duration,
        scheduledAt = at, liveMode = at is null ? null : "Zoom",
        joinUrl = (string?)null, location = (string?)null,
        assessmentId = (Guid?)null,          // what a form that does not model the quiz sends
    };

    /// <summary>A program holding a video session (order 1, with a quiz) and a live session (order 2).</summary>
    private async Task<Ctx> Seed()
    {
        var admin = await AdminToken();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var program = await PostJson<AdminProgramDto>("/api/admin/programs", admin, new
        {
            name = $"Edit {suffix}", slug = $"edit-{suffix}", description = "Uji ubah sesi",
            summary = (string?)null, priceIdr = 100000m, published = false,
        });

        var quiz = await PostJson<AdminAssessmentDto>("/api/admin/assessments", admin, new
        {
            kind = "Gating", title = "Kuis",
            config = new { passThreshold = 1, retakeCap = (int?)null, proctoringEnabled = false, sections = Array.Empty<object>() },
        });

        var video = await PostJson<AdminSessionDto>($"/api/admin/programs/{program.Id}/sessions", admin, new
        {
            type = "Video", title = "Sesi video", description = (string?)null, orderIndex = 1,
            providerAssetId = "sample", durationSeconds = 900,
            scheduledAt = (DateTimeOffset?)null, liveMode = (string?)null,
            joinUrl = (string?)null, location = (string?)null, assessmentId = quiz.Id,
        });

        var live = await PostJson<AdminSessionDto>($"/api/admin/programs/{program.Id}/sessions", admin, new
        {
            type = "Live", title = "Sesi Live", description = (string?)null, orderIndex = 2,
            providerAssetId = (string?)null, durationSeconds = (int?)null,
            scheduledAt = LiveAt, liveMode = "Zoom",
            joinUrl = (string?)null, location = (string?)null, assessmentId = (Guid?)null,
        });

        return new Ctx(admin, video.Id, quiz.Id, live.Id);
    }

    private Task<HttpResponseMessage> Put(Ctx c, Guid sessionId, object body)
        => Authed(HttpMethod.Put, $"/api/admin/sessions/{sessionId}", c.Admin, body);

    private async Task<Domain.Entities.ProgramSession> Session(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .ProgramSessions.AsNoTracking().FirstAsync(s => s.Id == id);
    }

    private async Task MarkReminderSent(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await db.ProgramSessions.FirstAsync(x => x.Id == id);
        s.ReminderSentAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    private async Task<string> AdminToken()
    {
        var email = $"adm{Guid.NewGuid():N}@test.local";
        (await _client.PostAsJsonAsync("/api/auth/register", new { name = "Adm", email, password = Pw }))
            .EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Users.FirstAsync(x => x.Email == email);
            u.Role = UserRole.Admin;
            await db.SaveChangesAsync();
        }
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Pw });
        return (await login.Content.ReadFromJsonAsync<AuthTokens>())!.AccessToken;
    }

    private Task<HttpResponseMessage> Authed(HttpMethod method, string url, string token, object? body = null)
    {
        var req = new HttpRequestMessage(method, url)
        { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };
        if (body is not null) req.Content = JsonContent.Create(body);
        return _client.SendAsync(req);
    }

    private async Task<T> PostJson<T>(string url, string token, object body)
    {
        var res = await Authed(HttpMethod.Post, url, token, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Integration.Tests --filter "FullyQualifiedName~SessionUpdateTests"`

Expected: 4 failures.

| Test | Expected failure |
|---|---|
| `An_edit_that_sends_no_quiz_does_not_detach_the_quiz` | AssessmentId is null |
| `An_edit_cannot_move_the_session` | 409 Conflict, not 204 |
| `An_edit_cannot_change_the_session_type` | type is `Live` |
| `Rescheduling_a_live_session_re_arms_its_reminder` | ReminderSentAt still set |

`An_edit_changes_the_content_fields` and `Editing_a_live_session_without_moving_it_keeps_the_reminder_claimed` already pass. That's correct: they pin behaviour the fix must not break.

- [ ] **Step 3: Implement**

In `backend/src/Infrastructure/Programs/ProgramAdminService.cs`, replace the whole `UpdateSessionAsync` method with:

```csharp
    public async Task UpdateSessionAsync(Guid actor, Guid sessionId, UpsertSessionRequest req, CancellationToken ct = default)
    {
        var session = await db.ProgramSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new ProgramException("Sesi tidak ditemukan.", 404);

        // An edit owns the session's CONTENT. Three fields have their own endpoints and are kept as
        // stored, whatever the request says. Taking them from the request is what made this unsafe:
        //   AssessmentId — a form that does not model the quiz sends null, which DETACHED it, and
        //                  the session then completed on watching alone. Attach: PUT …/assessment.
        //   OrderIndex   — a stale index collided with UNIQUE(program_id, order_index) or quietly
        //                  moved the session. Reorder: POST …/sessions/reorder.
        //   Type         — a Video could become Live after learners had watched it. Immutable.
        var (type, order, assessmentId, scheduledAt) =
            (session.Type, session.OrderIndex, session.AssessmentId, session.ScheduledAt);

        ApplySession(session, req);

        session.Type = type;
        session.OrderIndex = order;
        session.AssessmentId = assessmentId;

        // The H-1 sweep claims a session by stamping ReminderSentAt and never revisits it, so a
        // rescheduled session would get no reminder for its new slot. Re-arm it.
        if (session.ScheduledAt != scheduledAt) session.ReminderSentAt = null;

        Audit(actor, "session_updated", sessionId, new { session.Title, Type = session.Type.ToString() });
        await SaveSessionsAsync(session.ProgramId, ct);
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Integration.Tests --filter "FullyQualifiedName~SessionUpdateTests"`

Expected: `Passed! - Failed: 0, Passed: 6`.

- [ ] **Step 5: Run the full backend suite**

Run: `dotnet test backend/Academy.slnx`

Expected: no failures. No existing test calls this endpoint, so nothing else should move.

- [ ] **Step 6: Commit**

```bash
git add backend/src/Infrastructure/Programs/ProgramAdminService.cs backend/tests/Integration.Tests/SessionUpdateTests.cs
git commit -m "fix: an edit no longer detaches a session's quiz, moves it, or retypes it

UpdateSessionAsync overwrote every field from the request, including the quiz
link, the order and the type. Nothing had called it yet; the first edit form
would have sent assessmentId: null and silently removed the quiz, leaving the
session to complete on watching alone.

Those three are now kept as stored — each has its own endpoint — and a change
of schedule re-arms the live reminder, which otherwise never fires again.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Under Bunny, a video session must carry a real video id

`SessionForm` defaults new video sessions to the asset id `"sample"`. Under the Bunny provider that's not a video, so playback returns 403 and nothing at save time says so. Bunny video ids are GUIDs, so the rule is: when the provider is Bunny, a Video session's asset id must parse as a GUID.

**Files:**
- Modify: `backend/src/Infrastructure/Programs/ProgramAdminService.cs` (constructor, `CreateSessionAsync`, `UpdateSessionAsync`)
- Create: `backend/tests/Integration.Tests/SessionAssetValidationTests.cs`

**Interfaces:**
- Consumes: `VideoOptions` (`Academy.Infrastructure.Learning`), already registered as a singleton; its `IsBunny` property.
- Produces: `public static void ValidateVideoAsset(SessionType type, string? assetId, VideoOptions video)` on `ProgramAdminService`, throwing `ProgramException` (status 400).

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/Integration.Tests/SessionAssetValidationTests.cs`:

```csharp
using Academy.Application.Programs;
using Academy.Domain.Enums;
using Academy.Infrastructure.Learning;
using Academy.Infrastructure.Programs;

namespace Academy.Integration.Tests;

/// <summary>
/// The admin form used to default new video sessions to the asset id "sample". Under Bunny that is
/// not a video: the session saves fine and every learner gets a 403. Bunny video ids are GUIDs, so
/// under Bunny anything else is refused at save time, where an admin can still see the mistake.
///
/// Pure tests: the rule is a static function. Under the dev provider — which the integration
/// suite runs — it is a no-op by design, so seeding and tests keep using "sample".
/// </summary>
public class SessionAssetValidationTests
{
    private static readonly VideoOptions Dev = new() { Provider = "dev" };
    private static readonly VideoOptions Bunny = new() { Provider = "bunny" };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sample")]
    [InlineData("not-a-guid")]
    public void Under_bunny_a_video_session_without_a_real_video_id_is_refused(string? asset)
    {
        var e = Assert.Throws<ProgramException>(
            () => ProgramAdminService.ValidateVideoAsset(SessionType.Video, asset, Bunny));

        Assert.Equal(400, e.StatusCode);
    }

    [Fact]
    public void Under_bunny_a_video_session_with_a_bunny_id_is_accepted()
    {
        ProgramAdminService.ValidateVideoAsset(SessionType.Video, Guid.NewGuid().ToString(), Bunny);
    }

    [Fact]
    public void Under_dev_the_placeholder_is_still_accepted()
    {
        // The seeder and the integration suite create video sessions with "sample".
        ProgramAdminService.ValidateVideoAsset(SessionType.Video, "sample", Dev);
        ProgramAdminService.ValidateVideoAsset(SessionType.Video, null, Dev);
    }

    [Theory]
    [InlineData(SessionType.Live)]
    [InlineData(SessionType.FinalAssessment)]
    public void Sessions_that_are_not_videos_need_no_video_id(SessionType type)
    {
        ProgramAdminService.ValidateVideoAsset(type, null, Bunny);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Integration.Tests --filter "FullyQualifiedName~SessionAssetValidationTests"`

Expected: build error, `'ProgramAdminService' does not contain a definition for 'ValidateVideoAsset'`.

- [ ] **Step 3: Implement**

In `backend/src/Infrastructure/Programs/ProgramAdminService.cs`:

1. Add `using Academy.Infrastructure.Learning;` to the usings.

2. Change the class declaration to take `VideoOptions`:

```csharp
public class ProgramAdminService(
    AppDbContext db, IContentRevalidator revalidator, IObjectStorage storage, VideoOptions video)
    : IProgramAdminService
```

3. In `CreateSessionAsync`, directly after `ApplySession(session, req);`, add:

```csharp
        ValidateVideoAsset(session.Type, session.ProviderAssetId, video);
```

4. In `UpdateSessionAsync` (from Task 1), directly after `session.AssessmentId = assessmentId;`, add:

```csharp
        ValidateVideoAsset(session.Type, session.ProviderAssetId, video);
```

It validates the **stored** type, because the request's type is ignored on update.

5. Add this method next to `ApplySession`:

```csharp
    /// <summary>
    /// Under Bunny, a Video session must point at a real Bunny video, and Bunny video ids are
    /// GUIDs. Anything else — the old form default "sample", a blank field, a pasted title — saves
    /// fine and then 403s for every learner, with nothing at save time to say why. Refusing it here
    /// is where an admin can still see the mistake.
    ///
    /// A no-op under the dev provider, which plays one test stream whatever the id says; the
    /// seeder and the integration suite rely on that.
    ///
    /// Deliberately NOT checked: that the video exists in the library or has finished encoding.
    /// That would make every session save depend on Bunny being reachable (spec §8).
    /// </summary>
    public static void ValidateVideoAsset(SessionType type, string? assetId, VideoOptions video)
    {
        if (!video.IsBunny || type != SessionType.Video) return;
        if (!Guid.TryParse(assetId, out _))
            throw new ProgramException(
                "Sesi video memerlukan ID video Bunny yang valid. Pilih video dari pustaka.", 400);
    }
```

6. Confirm nothing constructs the service by hand:

```bash
grep -rn "new ProgramAdminService(" backend
```

Expected: no output. DI resolves `VideoOptions` already.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Integration.Tests --filter "FullyQualifiedName~SessionAssetValidationTests|FullyQualifiedName~SessionUpdateTests"`

Expected: all pass. The Task 1 tests still pass because the suite runs under the dev provider.

- [ ] **Step 5: Commit**

```bash
git add backend/src/Infrastructure/Programs/ProgramAdminService.cs backend/tests/Integration.Tests/SessionAssetValidationTests.cs
git commit -m "fix: under Bunny, refuse a video session that has no real video id

The admin form defaulted new video sessions to \"sample\", which under Bunny
saves fine and 403s for every learner. Bunny ids are GUIDs, so anything else is
refused at save time. A no-op under the dev provider.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: List the Bunny library server-side

A read-only port that lists the videos in the Bunny library, using the library API key. When the key is missing or the provider is dev, it reports why instead of failing, so the picker can fall back to manual entry.

**Files:**
- Modify: `backend/src/Application/Abstractions/Ports.cs`
- Create: `backend/src/Infrastructure/Learning/BunnyVideoLibrary.cs`
- Create: `backend/src/Infrastructure/Learning/UnavailableVideoLibrary.cs`
- Modify: `backend/src/Infrastructure/Learning/VideoOptions.cs`
- Modify: `backend/src/Infrastructure/DependencyInjection.cs`
- Modify: `docker-compose.tunnel.yml`, `.env.example`, `DEPLOY.md`
- Create: `backend/tests/Integration.Tests/VideoLibraryTests.cs`

**Interfaces:**
- Produces, in `Academy.Application.Abstractions`:
  - `public interface IVideoLibrary { Task<VideoLibraryPageDto> ListAsync(string? search, int page, CancellationToken ct = default); }`
  - `public record VideoLibraryItemDto(string Id, string Title, int LengthSeconds, string Status, int EncodeProgress);`
  - `public record VideoLibraryPageDto(IReadOnlyList<VideoLibraryItemDto> Items, int Page, int TotalItems, string? Unavailable);` where `Unavailable` is null when the list is real and an Indonesian explanation otherwise.
- Produces `VideoOptions.ApiKey` (string, default `""`).

Bunny's list API, verified against its documentation:

```
GET https://video.bunnycdn.com/library/{libraryId}/videos?page=1&itemsPerPage=50&orderBy=title&search=…
Header: AccessKey: <library API key>
200 → { "totalItems": int, "currentPage": int, "itemsPerPage": int,
        "items": [ { "guid": string, "title": string, "length": int (seconds),
                     "status": int, "encodeProgress": int (0-100), … } ] }
status: 0 Created, 1 Uploaded, 2 Processing, 3 Transcoding, 4 Finished, 5 Error,
        6 UploadFailed, 7 JitSegmenting, 8 JitPlaylistsCreated
```

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/Integration.Tests/VideoLibraryTests.cs`:

```csharp
using System.Net;
using System.Text;
using Academy.Application.Abstractions;
using Academy.Infrastructure.Learning;

namespace Academy.Integration.Tests;

/// <summary>
/// The admin picks a session's video from the Bunny library instead of pasting a GUID. These pin
/// the mapping against a captured Bunny response — not a live call, which would need credentials
/// and network in CI — and the failure modes, each of which must leave the admin a way forward.
/// </summary>
public class VideoLibraryTests
{
    private const string LibraryId = "123456";

    /// <summary>The documented shape of GET /library/{id}/videos, with extra fields we ignore.</summary>
    private const string BunnyBody = """
        {"totalItems":2,"currentPage":1,"itemsPerPage":50,"items":[
          {"guid":"6f1d2c3b-0000-4000-8000-000000000001","title":"Sesi 1 — Format TOEFL","length":912,
           "status":4,"encodeProgress":100,"views":3,"dateUploaded":"2026-09-30T10:00:00","isPublic":false,"storageSize":104857600},
          {"guid":"6f1d2c3b-0000-4000-8000-000000000002","title":"Sesi 2 — Listening","length":0,
           "status":3,"encodeProgress":63,"views":0,"dateUploaded":"2026-10-01T10:00:00","isPublic":false,"storageSize":0}]}
        """;

    private sealed class StubHandler(HttpStatusCode code, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Last = request;
            return Task.FromResult(new HttpResponseMessage(code)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new HttpRequestException("connection refused");
    }

    private static BunnyVideoLibrary Library(HttpMessageHandler handler) => new(
        new HttpClient(handler),
        new VideoOptions { Provider = "bunny", LibraryId = LibraryId, ApiKey = "library-api-key" });

    [Fact]
    public async Task Bunny_videos_are_mapped_with_their_length_and_status()
    {
        var page = await Library(new StubHandler(HttpStatusCode.OK, BunnyBody)).ListAsync(null, 1);

        Assert.Null(page.Unavailable);
        Assert.Equal(2, page.TotalItems);
        var ready = page.Items[0];
        Assert.Equal("6f1d2c3b-0000-4000-8000-000000000001", ready.Id);
        Assert.Equal("Sesi 1 — Format TOEFL", ready.Title);
        Assert.Equal(912, ready.LengthSeconds);
        Assert.Equal("Finished", ready.Status);
        Assert.Equal("Transcoding", page.Items[1].Status);
        Assert.Equal(63, page.Items[1].EncodeProgress);
    }

    [Theory]
    [InlineData(0, "Created")]
    [InlineData(2, "Processing")]
    [InlineData(4, "Finished")]
    [InlineData(5, "Error")]
    [InlineData(6, "UploadFailed")]
    [InlineData(99, "Unknown")]
    public void Every_bunny_status_has_a_name(int status, string name)
    {
        // The frontend keys off these names, so Bunny's numbering never leaks past this class.
        Assert.Equal(name, BunnyVideoLibrary.StatusName(status));
    }

    [Fact]
    public async Task The_request_is_authenticated_scoped_to_the_library_and_carries_the_search()
    {
        var handler = new StubHandler(HttpStatusCode.OK, BunnyBody);
        await Library(handler).ListAsync("sesi 1", 2);

        var req = handler.Last!;
        Assert.Equal("library-api-key", req.Headers.GetValues("AccessKey").Single());
        Assert.Equal($"/library/{LibraryId}/videos", req.RequestUri!.AbsolutePath);
        Assert.Contains("search=sesi%201", req.RequestUri.Query);
        Assert.Contains("page=2", req.RequestUri.Query);
    }

    [Fact]
    public async Task A_rejected_api_key_says_which_key_to_use()
    {
        // The usual mistake is pasting the ACCOUNT key or the token key. Name the right one.
        var page = await Library(new StubHandler(HttpStatusCode.Unauthorized, "")).ListAsync(null, 1);

        Assert.Empty(page.Items);
        Assert.Contains("kunci API", page.Unavailable);
        Assert.Contains("pustaka", page.Unavailable);
    }

    [Fact]
    public async Task An_unreachable_bunny_falls_back_to_manual_entry_instead_of_failing()
    {
        var page = await Library(new ThrowingHandler()).ListAsync(null, 1);

        Assert.Empty(page.Items);
        Assert.Contains("manual", page.Unavailable);
    }

    [Fact]
    public async Task Without_a_library_the_reason_is_reported_not_thrown()
    {
        var page = await new UnavailableVideoLibrary("alasan").ListAsync("x", 1);

        Assert.Empty(page.Items);
        Assert.Equal("alasan", page.Unavailable);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Integration.Tests --filter "FullyQualifiedName~VideoLibraryTests"`

Expected: build errors, `BunnyVideoLibrary`, `UnavailableVideoLibrary` and `VideoOptions.ApiKey` not found.

- [ ] **Step 3: Add the port and DTOs**

Append to `backend/src/Application/Abstractions/Ports.cs`, after the `PlaybackTicket` record:

```csharp
/// <summary>
/// The video library an admin attaches sessions to. READ-ONLY on purpose: uploads, renames and
/// deletes happen in Bunny's own dashboard, which is the source of truth for the files.
/// </summary>
public interface IVideoLibrary
{
    Task<VideoLibraryPageDto> ListAsync(string? search, int page, CancellationToken ct = default);
}

/// <summary>One library video. <c>Status</c> is a name ("Finished", "Processing", …), never
/// Bunny's integer, so the frontend does not depend on Bunny's numbering.</summary>
public record VideoLibraryItemDto(string Id, string Title, int LengthSeconds, string Status, int EncodeProgress);

/// <summary><c>Unavailable</c> is null when <c>Items</c> is the real library, and otherwise says in
/// Indonesian why there is no list — the picker shows it and offers manual entry instead.</summary>
public record VideoLibraryPageDto(
    IReadOnlyList<VideoLibraryItemDto> Items, int Page, int TotalItems, string? Unavailable);
```

- [ ] **Step 4: Add the API key setting**

In `backend/src/Infrastructure/Learning/VideoOptions.cs`, add after the `LibraryId` property:

```csharp
    /// <summary>
    /// The video LIBRARY's API key (Stream → library → API), used only to list videos for the
    /// admin picker. Not the account API key, and not the token authentication key.
    ///
    /// A full-control credential — it can delete videos — so it stays in server config (GR-9) and
    /// never reaches a DTO or the browser. Optional: playback works without it, and the picker falls
    /// back to manual entry. Making it required would take the whole API down for a convenience.
    /// </summary>
    public string ApiKey { get; set; } = "";
```

Do not add it to `Validate`.

- [ ] **Step 5: Implement the two libraries**

Create `backend/src/Infrastructure/Learning/UnavailableVideoLibrary.cs`:

```csharp
using Academy.Application.Abstractions;

namespace Academy.Infrastructure.Learning;

/// <summary>Stands in when there is no library to list: the dev provider, or Bunny without an API
/// key. Reports why, so the admin picker can say so and offer manual entry.</summary>
public class UnavailableVideoLibrary(string reason) : IVideoLibrary
{
    public Task<VideoLibraryPageDto> ListAsync(string? search, int page, CancellationToken ct = default)
        => Task.FromResult(new VideoLibraryPageDto([], 1, 0, reason));
}
```

Create `backend/src/Infrastructure/Learning/BunnyVideoLibrary.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Academy.Application.Abstractions;

namespace Academy.Infrastructure.Learning;

/// <summary>
/// Lists the Bunny Stream library for the admin video picker.
///
/// Every failure becomes an <c>Unavailable</c> message rather than an exception. The picker is a
/// convenience over manual entry, and an admin who cannot reach the list must still be able to type
/// an id and save; a 500 here would take the whole session form down with it.
/// </summary>
public class BunnyVideoLibrary(HttpClient http, VideoOptions options) : IVideoLibrary
{
    public const int PageSize = 50;

    public async Task<VideoLibraryPageDto> ListAsync(string? search, int page, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var url = $"https://video.bunnycdn.com/library/{Uri.EscapeDataString(options.LibraryId)}/videos" +
                  $"?page={page}&itemsPerPage={PageSize}&orderBy=title" +
                  (string.IsNullOrWhiteSpace(search) ? "" : $"&search={Uri.EscapeDataString(search.Trim())}");

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("AccessKey", options.ApiKey);

        try
        {
            using var res = await http.SendAsync(req, ct);

            if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return Unavailable(
                    "Bunny menolak kunci API. Gunakan kunci API milik pustaka video (Stream → pustaka → API), " +
                    "bukan kunci akun atau kunci token. Sementara itu, isi ID video secara manual.");

            if (!res.IsSuccessStatusCode)
                return Unavailable($"Bunny membalas {(int)res.StatusCode}. Coba lagi, atau isi ID video secara manual.");

            var body = await res.Content.ReadFromJsonAsync<BunnyPage>(Json, ct) ?? new BunnyPage();
            return new VideoLibraryPageDto(
                body.Items.Select(v => new VideoLibraryItemDto(
                    v.Guid, v.Title ?? "", v.Length, StatusName(v.Status), v.EncodeProgress)).ToList(),
                body.CurrentPage, body.TotalItems, null);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return Unavailable("Bunny tidak dapat dihubungi. Coba lagi, atau isi ID video secara manual.");
        }
    }

    /// <summary>Bunny's documented video statuses. Unknown values stay visible as "Unknown"
    /// rather than being guessed at.</summary>
    public static string StatusName(int status) => status switch
    {
        0 => "Created",
        1 => "Uploaded",
        2 => "Processing",
        3 => "Transcoding",
        4 => "Finished",
        5 => "Error",
        6 => "UploadFailed",
        7 => "JitSegmenting",
        8 => "JitPlaylistsCreated",
        _ => "Unknown",
    };

    private static VideoLibraryPageDto Unavailable(string reason) => new([], 1, 0, reason);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed class BunnyPage
    {
        public int TotalItems { get; set; }
        public int CurrentPage { get; set; } = 1;
        public List<BunnyVideo> Items { get; set; } = [];
    }

    private sealed class BunnyVideo
    {
        public string Guid { get; set; } = "";
        public string? Title { get; set; }
        public int Length { get; set; }
        public int Status { get; set; }
        public int EncodeProgress { get; set; }
    }
}
```

Check the `catch` filter parses as intended. C# binds `is A or B && c` as `(is A or B) && c`. If the build warns or a reviewer finds it ambiguous, write it with explicit parentheses: `when ((e is HttpRequestException or TaskCanceledException) && !ct.IsCancellationRequested)`.

- [ ] **Step 6: Register it**

In `backend/src/Infrastructure/DependencyInjection.cs`, find the block added for the video provider:

```csharp
        if (VideoOptionsFactory.Build(configuration).IsBunny)
            services.AddScoped<IVideoProvider, BunnyVideoProvider>();
        else
            services.AddScoped<IVideoProvider, DevVideoProvider>();
```

Replace it with:

```csharp
        var videoOptions = VideoOptionsFactory.Build(configuration);
        if (videoOptions.IsBunny)
            services.AddScoped<IVideoProvider, BunnyVideoProvider>();
        else
            services.AddScoped<IVideoProvider, DevVideoProvider>();

        // The admin video picker. Real only under Bunny WITH a library API key; otherwise a stand-in
        // that says why, so the picker falls back to manual entry instead of erroring.
        if (videoOptions.IsBunny && !string.IsNullOrWhiteSpace(videoOptions.ApiKey))
            services.AddHttpClient<IVideoLibrary, BunnyVideoLibrary>();
        else
            services.AddSingleton<IVideoLibrary>(new UnavailableVideoLibrary(videoOptions.IsBunny
                ? "Kunci API Bunny (BUNNY_API_KEY) belum diatur. Isi ID video secara manual."
                : "Pustaka video hanya tersedia saat penyedia video adalah Bunny. Isi ID video secara manual."));
```

`BunnyVideoLibrary` takes `VideoOptions` in its constructor, which resolves from the existing singleton registration.

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Integration.Tests --filter "FullyQualifiedName~VideoLibraryTests"`

Expected: `Passed! - Failed: 0, Passed: 11`.

- [ ] **Step 8: Wire the config through deployment**

In `docker-compose.tunnel.yml`, under the `api` service's `environment`, after `Video__CaptionsLanguage`, add:

```yaml
      # The video LIBRARY's API key, for the admin video picker only. Optional: without it the
      # picker offers manual entry and playback is unaffected.
      Video__ApiKey: "${BUNNY_API_KEY:-}"
```

In `.env.example`, after `BUNNY_TOKEN_KEY=`, add:

```
# The video LIBRARY's API key (Stream → library → API), used only to list videos in the admin
# picker. NOT the account API key and NOT the token key. Optional; it can delete videos, so keep it
# out of the repo.
BUNNY_API_KEY=
```

In `DEPLOY.md`, in the variables table after the `BUNNY_CAPTIONS_LANGUAGE` row, add:

```
| `BUNNY_API_KEY` | The video **library's** API key, for the admin video picker. Optional. |
```

In `DEPLOY.md` under "Playing real videos", replace step 7 ("In Admin → Program, edit each video session…") with:

```
7. Optional but recommended: copy the **library's API key** (Stream → your library → API) into
   `.env` as `BUNNY_API_KEY`. The admin session form then lists the library and fills in each
   video's id and length when you pick it. Without it, paste each video's GUID by hand.
8. In Admin → Program → Sesi, use **Ubah** on each video session and choose its video.
```

Run: `docker compose -f docker-compose.tunnel.yml config >/dev/null && echo ok`

Expected: `ok`.

- [ ] **Step 9: Commit**

```bash
git add backend/src/Application/Abstractions/Ports.cs backend/src/Infrastructure/Learning/BunnyVideoLibrary.cs \
  backend/src/Infrastructure/Learning/UnavailableVideoLibrary.cs backend/src/Infrastructure/Learning/VideoOptions.cs \
  backend/src/Infrastructure/DependencyInjection.cs backend/tests/Integration.Tests/VideoLibraryTests.cs \
  docker-compose.tunnel.yml .env.example DEPLOY.md
git commit -m "feat: list the Bunny video library server-side for the admin picker

A read-only IVideoLibrary over Bunny's list API, using the library API key,
which stays in server config — it can delete videos. Every failure becomes a
stated reason rather than an exception, so the picker can fall back to manual
entry instead of taking the session form down with it.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Expose the library to admins

**Files:**
- Modify: `backend/src/Api/Endpoints/ProgramAdminEndpoints.cs`
- Modify: `backend/tests/Integration.Tests/VideoLibraryTests.cs`
- Modify: `frontend/api-client/schema.ts` (regenerated, never hand-edited)
- Modify: `frontend/lib/programs.ts`

**Interfaces:**
- Consumes: `IVideoLibrary`, `VideoLibraryPageDto` from Task 3.
- Produces: `GET /api/admin/video-library?search=&page=` returning `VideoLibraryPageDto`, admin-only. Frontend `listVideoLibrary(token: string, search: string, page?: number): Promise<VideoLibraryPage>` and types `VideoLibraryPage`, `VideoLibraryItem` in `frontend/lib/programs.ts`.

- [ ] **Step 1: Write the failing endpoint tests**

Append this class to `backend/tests/Integration.Tests/VideoLibraryTests.cs`, below the existing class, and add the usings shown to the top of the file:

```csharp
// add to the usings at the top of the file:
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Academy.Application.Auth;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
```

```csharp
/// <summary>
/// The endpoint proxies a call made with a credential that can delete videos, so who may reach it
/// matters more than what it returns. The suite runs under the dev provider, so the body is the
/// stated "unavailable" reason — which is also exactly what the picker must cope with.
/// </summary>
public class VideoLibraryEndpointTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    [Fact]
    public async Task Anonymous_requests_are_refused()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _client.GetAsync("/api/admin/video-library")).StatusCode);
    }

    [Fact]
    public async Task Learners_are_refused()
    {
        var token = await Token(UserRole.User);   // the learner role
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Get("/api/admin/video-library", token)).StatusCode);
    }

    [Fact]
    public async Task Admins_get_a_page_that_explains_itself_when_there_is_no_library()
    {
        var res = await Get("/api/admin/video-library?search=sesi", await Token(UserRole.Admin));
        res.EnsureSuccessStatusCode();

        var page = (await res.Content.ReadFromJsonAsync<VideoLibraryPageDto>(Json))!;
        Assert.Empty(page.Items);
        Assert.False(string.IsNullOrWhiteSpace(page.Unavailable));
    }

    private Task<HttpResponseMessage> Get(string url, string token)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url)
        { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };
        return _client.SendAsync(req);
    }

    private async Task<string> Token(UserRole role)
    {
        var email = $"vl{Guid.NewGuid():N}@test.local";
        (await _client.PostAsJsonAsync("/api/auth/register", new { name = "VL", email, password = Pw }))
            .EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Users.FirstAsync(x => x.Email == email);
            u.Role = role;
            await db.SaveChangesAsync();
        }
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Pw });
        return (await login.Content.ReadFromJsonAsync<AuthTokens>())!.AccessToken;
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/Integration.Tests --filter "FullyQualifiedName~VideoLibraryEndpointTests"`

Expected: `Learners_are_refused` and `Admins_get_a_page…` fail with 404 because the route doesn't exist. `Anonymous_requests_are_refused` may also see 404.

- [ ] **Step 3: Add the endpoint**

In `backend/src/Api/Endpoints/ProgramAdminEndpoints.cs`, inside the method that defines `var g = app.MapGroup("/api/admin")…`, after the `sessions/reorder` mapping, add:

```csharp
        // ---- video library (Bunny), for the session form's picker ----
        // Rate-limited on the "media" policy (per IP, generous) because the picker searches as the
        // admin types, and every call spends the library's API quota.
        g.MapGet("/video-library", async Task<Ok<VideoLibraryPageDto>> (
                string? search, int? page, IVideoLibrary library, CancellationToken ct) =>
            TypedResults.Ok(await library.ListAsync(search, page ?? 1, ct)))
            .RequireRateLimiting("media");
```

Add `using Academy.Application.Abstractions;` to the file's usings if it isn't there.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/tests/Integration.Tests --filter "FullyQualifiedName~VideoLibrary"`

Expected: all `VideoLibraryTests` and `VideoLibraryEndpointTests` pass.

- [ ] **Step 5: Regenerate the frontend API client**

The client is generated from the API's OpenAPI document, which is only served in Development. Run the API locally on a spare port, fetch the document, generate, and stop the API:

```bash
dotnet build backend/Academy.slnx
cd backend/src/Api
ASPNETCORE_ENVIRONMENT=Development RunMigrations=false SeedSampleData=false \
  dotnet run --no-build --urls http://localhost:8091 > /tmp/api-openapi.log 2>&1 &
until curl -sf http://localhost:8091/openapi/v1.json -o /tmp/openapi.json; do sleep 2; done
pkill -f "urls http://localhost:8091"
cd ../../../frontend
npx openapi-typescript /tmp/openapi.json -o api-client/schema.ts
grep -c "VideoLibraryPageDto" api-client/schema.ts
```

Expected: the final count is at least 1.

- [ ] **Step 6: Add the client function**

In `frontend/lib/programs.ts`, add next to the other type aliases near the top:

```ts
export type VideoLibraryPage = components["schemas"]["VideoLibraryPageDto"];
export type VideoLibraryItem = components["schemas"]["VideoLibraryItemDto"];
```

And after `reorderSessions`:

```ts
/** The Bunny library, for the session form's picker. Admin-only; the API key never leaves the
 *  server. `unavailable` is set — and `items` empty — when there is no list to show. */
export const listVideoLibrary = (t: string, search: string, page = 1) =>
  api<VideoLibraryPage>(
    "GET",
    `/api/admin/video-library?search=${encodeURIComponent(search)}&page=${page}`,
    t,
  );
```

Run: `cd frontend && npx tsc --noEmit`

Expected: no output.

- [ ] **Step 7: Commit**

```bash
git add backend/src/Api/Endpoints/ProgramAdminEndpoints.cs backend/tests/Integration.Tests/VideoLibraryTests.cs \
  frontend/api-client/schema.ts frontend/lib/programs.ts
git commit -m "feat: admin endpoint for the Bunny video library

Admin-only, since it proxies a call made with a key that can delete videos.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Edit sessions and pick videos in the admin screen

**Files:**
- Create: `frontend/components/admin/VideoPicker.tsx`
- Modify: `frontend/components/admin/SessionForm.tsx` (full replacement below)
- Modify: `frontend/components/admin/SessionManager.tsx`

**Interfaces:**
- Consumes: `listVideoLibrary`, `VideoLibraryItem`, `updateSession`, `createSession`, `AdminSession`, `UpsertSession`, `num`, `minutesLabel` from `@/lib/programs`.
- Produces: `VideoPicker({ token, value, onPick })` where `onPick(video: { id: string; lengthSeconds: number })`. `SessionForm` gains an optional `session?: AdminSession` prop; present means edit mode.

There's no frontend test runner. Verification is the typecheck, lint, and the manual pass in Step 6.

- [ ] **Step 1: Create the picker**

Create `frontend/components/admin/VideoPicker.tsx`:

```tsx
"use client";

import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Spinner } from "@/components/ui";
import { inputBlockCls } from "@/components/admin/fields";
import { listVideoLibrary, minutesLabel, num, type VideoLibraryItem } from "@/lib/programs";

/**
 * Picks a session's video from the Bunny library, by title. Choosing one hands back its id AND its
 * length, so the admin never pastes a GUID or types a duration.
 *
 * Only "Finished" videos are pickable. A video still encoding has no playlist yet, so attaching it
 * gives learners a broken player; it is shown, with its progress, so the admin knows to wait.
 *
 * When there is no library to list (no API key, dev provider, Bunny unreachable) the server says
 * why, and that reason is shown instead. The form's manual id field stays usable either way.
 */
export function VideoPicker({
  token, value, onPick,
}: {
  token: string;
  value: string;
  onPick: (video: { id: string; lengthSeconds: number }) => void;
}) {
  const [input, setInput] = useState("");
  const [search, setSearch] = useState("");

  // The list is searched as the admin types; wait for a pause rather than calling Bunny per key.
  useEffect(() => {
    const t = setTimeout(() => setSearch(input.trim()), 400);
    return () => clearTimeout(t);
  }, [input]);

  const q = useQuery({
    queryKey: ["video-library", search],
    queryFn: () => listVideoLibrary(token, search),
    staleTime: 30_000,
  });

  if (q.data?.unavailable) {
    return (
      <p className="rounded-base bg-surface-2 px-3 py-2 text-[12.5px] leading-snug text-ink-muted">
        {q.data.unavailable}
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-2">
      <input
        value={input}
        onChange={(e) => setInput(e.target.value)}
        placeholder="Cari judul video…"
        className={inputBlockCls}
      />
      <div className="max-h-56 overflow-y-auto rounded-base border border-border">
        {q.isPending ? (
          <div className="flex min-h-[80px] items-center justify-center"><Spinner size={18} /></div>
        ) : q.isError ? (
          <p className="px-3 py-3 text-[12.5px] text-danger">Daftar video gagal dimuat.</p>
        ) : (q.data?.items.length ?? 0) === 0 ? (
          <p className="px-3 py-3 text-[12.5px] text-ink-muted">Tidak ada video yang cocok.</p>
        ) : (
          <ul>
            {q.data!.items.map((v) => (
              <VideoRow key={v.id} video={v} selected={v.id === value} onPick={onPick} />
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

function VideoRow({
  video, selected, onPick,
}: {
  video: VideoLibraryItem;
  selected: boolean;
  onPick: (video: { id: string; lengthSeconds: number }) => void;
}) {
  const ready = video.status === "Finished";
  const note = ready
    ? minutesLabel(video.lengthSeconds)
    : video.status === "Error" || video.status === "UploadFailed"
      ? "Gagal diproses"
      : `Masih diproses (${num(video.encodeProgress)}%)`;

  return (
    <li>
      <button
        type="button"
        disabled={!ready}
        onClick={() => onPick({ id: video.id, lengthSeconds: num(video.lengthSeconds) })}
        className={
          "flex w-full items-center justify-between gap-3 border-b border-border px-3 py-2 text-left text-[13px] last:border-b-0 " +
          (selected ? "bg-primary-soft/60 font-semibold" : ready ? "hover:bg-surface-2" : "cursor-not-allowed opacity-50")
        }
      >
        <span className="min-w-0 truncate">{video.title || video.id}</span>
        <span className="shrink-0 text-[11.5px] text-ink-subtle">{selected ? "Terpilih ✓" : note}</span>
      </button>
    </li>
  );
}
```

Check `minutesLabel` accepts a number. Its signature is `minutesLabel(seconds: number | string | null | undefined)`, so passing `video.lengthSeconds` directly is fine.

- [ ] **Step 2: Replace the session form**

Replace the whole contents of `frontend/components/admin/SessionForm.tsx` with:

```tsx
"use client";

import { useState } from "react";
import { Button, Modal } from "@/components/ui";
import { Field, inputBlockCls } from "@/components/admin/fields";
import { VideoPicker } from "@/components/admin/VideoPicker";
import {
  createSession, updateSession, num,
  type AdminSession, type UpsertSession,
} from "@/lib/programs";

export type SessionKind = "Video" | "Live" | "FinalAssessment";

const TYPE_LABEL: Record<SessionKind, string> = {
  Video: "Video + tes",
  Live: "Sesi Live",
  FinalAssessment: "Tes Akhir",
};

/** ISO instant → the local "YYYY-MM-DDTHH:mm" a datetime-local input expects. */
function toLocalInput(iso: string | null | undefined): string {
  if (!iso) return "";
  const d = new Date(iso);
  const p = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
}

/**
 * Creates a session, or — given `session` — edits one.
 *
 * In edit mode the type is fixed: the server keeps the stored type whatever is sent, because a
 * video that learners have watched must not turn into a live session. The quiz and the order are
 * likewise owned by their own controls, and the server ignores them here.
 *
 * Fields this form does not show (description, location) are sent back as they were, because the
 * update replaces the session's content wholesale and would otherwise blank them.
 */
export function SessionForm({
  token, programId, nextOrder, session, onClose,
}: {
  token: string;
  programId: string;
  nextOrder: number;
  session?: AdminSession;
  onClose: () => void;
}) {
  const editing = session !== undefined;
  const [type, setType] = useState<SessionKind>((session?.type as SessionKind) ?? "Video");
  const [title, setTitle] = useState(session?.title ?? "");
  const [seconds, setSeconds] = useState<number>(session?.durationSeconds != null ? num(session.durationSeconds) : 900);
  // Empty, not "sample": under Bunny the server refuses anything that is not a real video id.
  const [assetId, setAssetId] = useState(session?.providerAssetId ?? "");
  const [scheduledAt, setScheduledAt] = useState(toLocalInput(session?.scheduledAt));
  const [joinUrl, setJoinUrl] = useState(session?.joinUrl ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function save() {
    setBusy(true); setError(null);
    try {
      const body: UpsertSession = {
        type,
        title: title.trim(),
        description: session?.description ?? null,
        orderIndex: session ? num(session.orderIndex) : nextOrder,
        providerAssetId: type === "Video" ? assetId.trim() || null : null,
        durationSeconds: type === "Video" ? seconds : null,
        scheduledAt: type === "Live" && scheduledAt ? new Date(scheduledAt).toISOString() : null,
        liveMode: type === "Live" ? (session?.liveMode ?? "Zoom") : null,
        joinUrl: type === "Live" ? joinUrl.trim() || null : null,
        location: session?.location ?? null,
        assessmentId: session?.assessmentId ?? null,
      };
      if (session) await updateSession(token, session.id, body);
      else await createSession(token, programId, body);
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal menyimpan sesi.");
      setBusy(false);
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={editing ? "Ubah sesi" : "Sesi baru"}
      className="max-w-lg"
      footer={
        <div className="flex w-full justify-end gap-2">
          <Button variant="neutral" size="sm" onClick={onClose}>Batal</Button>
          <Button size="sm" onClick={save} loading={busy} disabled={!title.trim()}>Simpan</Button>
        </div>
      }
    >
      <div className="flex flex-col gap-3">
        {error && <div className="rounded-base bg-danger-soft px-3 py-2 text-[13px] font-semibold text-danger">{error}</div>}

        <Field label="Tipe sesi">
          {editing ? (
            <p className="rounded-sm bg-surface-2 px-3 py-2 text-[13.5px] text-ink-muted">
              {TYPE_LABEL[type]} — tipe tidak dapat diubah setelah sesi dibuat.
            </p>
          ) : (
            <select value={type} onChange={(e) => setType(e.target.value as SessionKind)} className={inputBlockCls}>
              <option value="Video">{TYPE_LABEL.Video}</option>
              <option value="Live">{TYPE_LABEL.Live}</option>
              <option value="FinalAssessment">{TYPE_LABEL.FinalAssessment}</option>
            </select>
          )}
        </Field>

        <Field label="Judul">
          <input value={title} onChange={(e) => setTitle(e.target.value)} className={inputBlockCls} />
        </Field>

        {type === "Video" && (
          <>
            <div className="flex flex-col gap-1">
              <span className="text-[12px] font-bold text-ink-muted">Video</span>
              <VideoPicker
                token={token}
                value={assetId}
                onPick={(v) => { setAssetId(v.id); setSeconds(v.lengthSeconds); }}
              />
            </div>
            <Field label="ID video Bunny">
              <input
                value={assetId}
                onChange={(e) => setAssetId(e.target.value)}
                placeholder="Terisi otomatis saat memilih video, atau tempel manual"
                className={inputBlockCls}
              />
            </Field>
            <Field label="Durasi (menit)">
              <input
                type="number"
                min={1}
                value={Math.max(1, Math.round(seconds / 60))}
                onChange={(e) => setSeconds((Number(e.target.value) || 0) * 60)}
                className={inputBlockCls}
              />
            </Field>
          </>
        )}

        {type === "Live" && (
          <>
            <Field label="Jadwal">
              <input type="datetime-local" value={scheduledAt} onChange={(e) => setScheduledAt(e.target.value)} className={inputBlockCls} />
            </Field>
            <Field label="Tautan Zoom">
              <input value={joinUrl} onChange={(e) => setJoinUrl(e.target.value)} className={inputBlockCls} />
            </Field>
          </>
        )}

        {type === "FinalAssessment" && (
          <p className="rounded-base bg-surface-2 px-3 py-2 text-[12.5px] text-ink-muted">
            Setelah sesi tersimpan, gunakan tombol “Buat tes akhir” pada baris sesi untuk
            menyusun bagian dan soalnya.
          </p>
        )}
      </div>
    </Modal>
  );
}
```

Duration is held in **seconds**. A picked video keeps its exact length, for example 912. Typing in the minutes field writes whole minutes. The stored duration only labels the session; progress comes from the video file itself.

- [ ] **Step 3: Add "Ubah" to the session list**

In `frontend/components/admin/SessionManager.tsx`:

1. After `const [adding, setAdding] = useState(false);` add:

```tsx
  const [editing, setEditing] = useState<AdminSession | null>(null);
```

2. In each row's button group, immediately before the `↑` button, add:

```tsx
                <button
                  type="button"
                  onClick={() => setEditing(s)}
                  className="rounded px-2 py-1 text-[11.5px] font-bold text-primary hover:bg-primary-soft"
                >
                  Ubah
                </button>
```

3. After the existing `{adding && ( <SessionForm … /> )}` block, add:

```tsx
      {editing && (
        <SessionForm
          token={token}
          programId={program.id}
          nextOrder={sessions.length + 1}
          session={editing}
          onClose={async () => { setEditing(null); await qc.invalidateQueries({ queryKey: key }); }}
        />
      )}
```

`AdminSession` is already imported in this file.

- [ ] **Step 4: Typecheck and lint**

Run: `cd frontend && npx tsc --noEmit && npx next lint`

Expected: no type errors; `✔ No ESLint warnings or errors`.

If `tsc` complains that a field like `session.durationSeconds` is `number | string`, wrap it in `num(...)` as already done for `orderIndex`. The generated types render some integers as `number | string`.

- [ ] **Step 5: Commit**

```bash
git add frontend/components/admin/VideoPicker.tsx frontend/components/admin/SessionForm.tsx frontend/components/admin/SessionManager.tsx
git commit -m "feat: edit sessions and pick their video from the Bunny library

An Ubah button on each session opens the form prefilled. Video sessions choose
their video from the library by title, which fills in the id and length; videos
still encoding are shown but not pickable. Type is fixed once created, and the
fields the form does not show are sent back unchanged so an edit cannot blank
them. New video sessions no longer default to the \"sample\" placeholder.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 6: Manual pass (needs an admin login and the deployed stack)**

```bash
docker compose -f docker-compose.tunnel.yml up -d --build
```

Then in Admin → Program → Sesi:

1. Click **Ubah** on "Sesi 1". The form opens prefilled, and the type shows as fixed.
2. Without `BUNNY_API_KEY` set, the picker shows the "belum diatur" reason and the manual field works.
3. With `BUNNY_API_KEY` set and the API restarted, the picker lists the library. Pick a Finished video; the id and duration fill in.
4. Save. The row's duration label updates. Click **Tes ✓**: the quiz is still attached.
5. Try saving with the id field set to `sample`. It's refused with "Sesi video memerlukan ID video Bunny yang valid".
6. As a test learner, open that session. The real video plays.

---

## Self-review

**Spec coverage**

| Spec section | Task |
|---|---|
| §4 update keeps quiz, order, type | 1 |
| §7 reminder re-armed on reschedule | 1 |
| §7 no `"sample"` default; refuse non-ids under Bunny | 2 (server), 5 (form) |
| §5 backend picker endpoint, key server-side, status names, dev returns empty | 3, 4 |
| §5 `Video:ApiKey` optional, compose `BUNNY_API_KEY` | 3 |
| §5 picker UI: search, length and status, Finished-only, manual fallback | 5 |
| §6 "Ubah" button, edit mode, type read-only | 5 |
| §9 testing list | 1, 2, 3, 4; frontend manual in 5 |
| §8 no save-time existence check, no caching | deliberately absent |

**Deviations from the spec, on purpose**

- The spec's manual entry was a toggle. The plan always shows the id field under the picker instead. It's simpler, and the picker filling it makes the relationship obvious.
- The spec said the dev endpoint returns an empty list. It also returns an `unavailable` reason, so the picker can say why rather than showing "no videos".
- "Under Bunny, the asset id must exist" became "must be a GUID". That catches `sample` and pasted titles without a network call, consistent with §8.
- The wiring of `ValidateVideoAsset` into create and update is only covered by review, not by an integration test. The suite runs under the dev provider, where the rule is a no-op, and a second Bunny-configured test factory would add a Postgres container for two assertions.
