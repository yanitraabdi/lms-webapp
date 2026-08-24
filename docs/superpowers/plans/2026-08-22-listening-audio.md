# Listening Audio Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Listening audio real — stored, signed, uploadable from the admin UI, and required by the readiness check — so a final assessment composed entirely in a browser can actually be sat.

**Architecture:** `IObjectStorage` gets two methods and a local-disk implementation behind a docker volume, registered by config so R2 is a later swap. Audio reaches the browser through a new anonymous `GET /api/media/{**key}` route guarded by an HMAC signature and expiry — the same scheme `DevVideoProvider` already uses — because an `<audio>` element cannot send an Authorization header. Audio may hang on a question or on a section, resolved server-side with the question winning; the existing per-attempt play counter keys by question id or section name accordingly.

**Tech Stack:** .NET 10 minimal APIs, EF Core 10 + Npgsql, xUnit + Testcontainers PostgreSQL, Next.js 15 App Router, TanStack Query, Tailwind.

**Spec:** `docs/superpowers/specs/2026-08-22-listening-audio-design.md`

## Global Constraints

- Backend: `Nullable` enabled, `TreatWarningsAsErrors` on — builds must end **0 warnings, 0 errors**.
- Layer boundaries: Domain depends on nothing; Application depends on Domain; Infrastructure and Api depend inward. Application must stay free of EF and HTTP types.
- Errors surface as RFC-7807 problem details. No stack traces to clients.
- All user-facing strings are in **Bahasa Indonesia**.
- Frontend: TypeScript strict; `npx tsc --noEmit` and `npm run build` must pass clean.
- `frontend/api-client/schema.ts` is generated from the .NET OpenAPI spec — regenerate it, never hand-edit.
- **No new npm or NuGet dependency.**
- No business logic in Next.js: no DB access, no secrets, no access or scoring decisions in the client.
- **GR-10: the answer key never reaches a student.** `StudentQuestionDto` must never gain a correct-answer field.
- **GR-3: media URLs are minted server-side, short-TTL, after an access check.** No public or persisted media URL.
- `watch_progress`, `attempts`, `proctor_events`, `certificates` are never hard-deleted; an issued certificate is never mutated.
- Never commit secrets. `Media:SigningKey` belongs in server-side config only.
- **Known EF trap in this codebase:** EF Core cannot translate C# helper methods or `enum.ToString()` inside `Where`/`Count`/`Any` on an `IQueryable`. Materialize first, or inline the comparison.
- Backend suite is currently **206 tests**, all passing. Run `dotnet test backend/Academy.slnx`.

---

## File Structure

| File | Responsibility |
|---|---|
| `backend/src/Application/Abstractions/Ports.cs` | `IObjectStorage` gains `PutAsync` / `OpenReadAsync` (modify) |
| `backend/src/Application/Assessments/AudioResolution.cs` | **new** — pure resolution of which audio a question uses, and under which play-counter key |
| `backend/src/Application/Assessments/MediaUpload.cs` | **new** — pure validation of an upload's content type and size |
| `backend/src/Infrastructure/Media/MediaOptions.cs` | **new** — `Storage:*` and `Media:*` config, mirroring `VideoOptions` |
| `backend/src/Infrastructure/Media/LocalObjectStorage.cs` | **new** — local-disk `IObjectStorage`, with key validation |
| `backend/src/Infrastructure/Media/MediaSigner.cs` | **new** — HMAC sign/verify for media URLs |
| `backend/src/Api/Endpoints/MediaEndpoints.cs` | **new** — anonymous signed serve route + admin upload route |
| `backend/src/Infrastructure/Assessments/FinalAssessmentService.cs` | audio URL minting, play counting, `HasAudio` (modify) |
| `backend/src/Infrastructure/Programs/ProgramAdminService.cs` | `listening_audio_present` readiness check (modify) |
| `frontend/components/admin/AudioUpload.tsx` | **new** — shared upload control used by both admin surfaces |
| `frontend/app/admin/questions/page.tsx` | audio upload for a Listening question (modify) |
| `frontend/app/admin/assessments/[id]/page.tsx` | audio upload for the Listening section (modify) |

---

## Task 1: Storage port and local implementation

**Files:**
- Modify: `backend/src/Application/Abstractions/Ports.cs` (the `IObjectStorage` stub, ~line 48)
- Create: `backend/src/Infrastructure/Media/MediaOptions.cs`
- Create: `backend/src/Infrastructure/Media/LocalObjectStorage.cs`
- Modify: `backend/src/Infrastructure/DependencyInjection.cs`
- Modify: `docker-compose.yml`, `docker-compose.tunnel.yml`
- Test: `backend/tests/Integration.Tests/LocalObjectStorageTests.cs`

**Interfaces:**
- Produces, for later tasks:
  - `IObjectStorage.PutAsync(string key, Stream content, string contentType, CancellationToken ct = default) : Task`
  - `IObjectStorage.OpenReadAsync(string key, CancellationToken ct = default) : Task<Stream?>`
  - `MediaOptions` with `Root`, `SigningKey`, `UrlTtlMinutes`, `MaxUploadBytes`, and `MediaOptionsFactory.Build(IConfiguration)`
  - `LocalObjectStorage.IsValidKey(string key) : bool` — public static, so the serve route can reject a bad key before touching storage

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/Integration.Tests/LocalObjectStorageTests.cs`:

```csharp
using System.Text;
using Academy.Infrastructure.Media;

namespace Academy.Integration.Tests;

/// <summary>
/// Local object storage is the dev-sim behind IObjectStorage. The key comes from a URL on the
/// serve route, so key validation is a trust boundary, not a tidiness rule.
/// </summary>
public class LocalObjectStorageTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "inverta-storage-" + Guid.CreateVersion7().ToString("N"));

    private LocalObjectStorage Storage() =>
        new(new MediaOptions { Root = _root });

    [Fact]
    public async Task Put_then_open_round_trips_the_bytes()
    {
        var s = Storage();
        await s.PutAsync("audio/clip.mp3", new MemoryStream("hello"u8.ToArray()), "audio/mpeg");

        await using var read = await s.OpenReadAsync("audio/clip.mp3");
        Assert.NotNull(read);
        using var r = new StreamReader(read!, Encoding.UTF8);
        Assert.Equal("hello", await r.ReadToEndAsync());
    }

    [Fact]
    public async Task Put_overwrites_an_existing_object()
    {
        var s = Storage();
        await s.PutAsync("audio/clip.mp3", new MemoryStream("first"u8.ToArray()), "audio/mpeg");
        await s.PutAsync("audio/clip.mp3", new MemoryStream("second"u8.ToArray()), "audio/mpeg");

        await using var read = await s.OpenReadAsync("audio/clip.mp3");
        using var r = new StreamReader(read!, Encoding.UTF8);
        Assert.Equal("second", await r.ReadToEndAsync());
    }

    [Fact]
    public async Task Open_returns_null_for_an_unknown_key()
        => Assert.Null(await Storage().OpenReadAsync("audio/nope.mp3"));

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("audio/../../etc/passwd")]
    [InlineData("/etc/passwd")]
    [InlineData("audio\\..\\..\\secret")]
    [InlineData("audio/")]
    [InlineData("nofolder.mp3")]
    [InlineData("")]
    public async Task Traversal_and_malformed_keys_are_refused(string key)
    {
        var s = Storage();
        Assert.False(LocalObjectStorage.IsValidKey(key));
        await Assert.ThrowsAsync<ArgumentException>(
            () => s.PutAsync(key, new MemoryStream([1, 2, 3]), "audio/mpeg"));
        Assert.Null(await s.OpenReadAsync(key));
    }

    [Fact]
    public async Task Nothing_is_written_outside_the_root()
    {
        var s = Storage();
        await s.PutAsync("audio/clip.mp3", new MemoryStream([1, 2, 3]), "audio/mpeg");

        Assert.True(File.Exists(Path.Combine(_root, "audio", "clip.mp3")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:
```bash
cd backend && dotnet test tests/Integration.Tests/Academy.Integration.Tests.csproj --filter "FullyQualifiedName~LocalObjectStorageTests"
```
Expected: compile failure — `LocalObjectStorage` and `MediaOptions` do not exist.

- [ ] **Step 3: Give `IObjectStorage` real methods**

In `backend/src/Application/Abstractions/Ports.cs`, replace the empty stub:

```csharp
/// <summary>Object storage (local-disk dev-sim; Cloudflare R2 adapter later).
/// Deliberately minimal: no Delete and no Exists. Replacing an upload orphans the previous
/// object, which costs disk and nothing else, whereas a delete could remove a file another
/// question still references — keys are free-form strings with no reference counting.
/// OpenReadAsync returning null answers the existence question at the only point that asks it.</summary>
public interface IObjectStorage
{
    /// <summary>Stores the stream under <paramref name="key"/>, overwriting any existing object.</summary>
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Opens the object for reading, or null when the key does not exist.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default);
}
```

- [ ] **Step 4: Add the options**

Create `backend/src/Infrastructure/Media/MediaOptions.cs`:

```csharp
using Microsoft.Extensions.Configuration;

namespace Academy.Infrastructure.Media;

/// <summary>
/// Media storage and signed-delivery settings. Mirrors <see cref="Learning.VideoOptions"/>.
/// SigningKey MUST be overridden in deployed config — see the note on the default below.
/// </summary>
public class MediaOptions
{
    public const string StorageSection = "Storage";
    public const string MediaSection = "Media";

    /// <summary>Active object-storage provider: "local" (disk dev-sim) or "r2" (later).</summary>
    public string Provider { get; set; } = "local";

    /// <summary>Filesystem root for the local provider. Mounted as a docker volume.</summary>
    public string Root { get; set; } = "/data/media";

    /// <summary>Key used to sign media URLs. Separate from Video:SigningKey so rotating one
    /// does not invalidate the other. NEVER ship this default.</summary>
    public string SigningKey { get; set; } = "dev-media-signing-key-change-me";

    /// <summary>How long a signed media URL stays valid. Deliberately long: a section recording
    /// can run 35 minutes and every seek re-requests the URL, so a short TTL breaks playback
    /// partway through. Two hours exceeds the longest ITP section (Reading, 55 minutes).</summary>
    public int UrlTtlMinutes { get; set; } = 120;

    /// <summary>Largest accepted upload. A 35-minute MP3 is roughly 30 MB.</summary>
    public long MaxUploadBytes { get; set; } = 104_857_600;
}

public static class MediaOptionsFactory
{
    public static MediaOptions Build(IConfiguration config)
    {
        var o = new MediaOptions();
        config.GetSection(MediaOptions.StorageSection).Bind(o);
        config.GetSection(MediaOptions.MediaSection).Bind(o);
        return o;
    }
}
```

- [ ] **Step 5: Implement local storage**

Create `backend/src/Infrastructure/Media/LocalObjectStorage.cs`:

```csharp
using System.Text.RegularExpressions;
using Academy.Application.Abstractions;

namespace Academy.Infrastructure.Media;

/// <summary>
/// Stores objects as files beneath <see cref="MediaOptions.Root"/>. Swapped for an R2 adapter
/// when Storage:Provider = "r2".
/// </summary>
public partial class LocalObjectStorage(MediaOptions options) : IObjectStorage
{
    /// <summary>
    /// A key is exactly one lowercase folder segment and one filename. The serve route takes a
    /// key straight from a URL, so this is a trust boundary: anything with "..", a rooted path,
    /// or a backslash is refused before it reaches the filesystem.
    /// </summary>
    public static bool IsValidKey(string key) =>
        !string.IsNullOrEmpty(key) && key.Length <= 200 && KeyPattern().IsMatch(key);

    [GeneratedRegex(@"^[a-z0-9]+/[A-Za-z0-9._-]+$")]
    private static partial Regex KeyPattern();

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        if (!IsValidKey(key)) throw new ArgumentException($"Invalid object key: {key}", nameof(key));

        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
    {
        if (!IsValidKey(key)) return Task.FromResult<Stream?>(null);

        var path = Resolve(key);
        if (!File.Exists(path)) return Task.FromResult<Stream?>(null);

        // Seekable, so the serve route can satisfy range requests.
        return Task.FromResult<Stream?>(File.OpenRead(path));
    }

    private string Resolve(string key) => Path.Combine(options.Root, key.Replace('/', Path.DirectorySeparatorChar));
}
```

Note the `partial` keyword — `[GeneratedRegex]` requires it.

- [ ] **Step 6: Register it**

In `backend/src/Infrastructure/DependencyInjection.cs`, immediately after the
`services.AddSingleton(VideoOptionsFactory.Build(configuration));` line, add:

```csharp
        // Media storage (audio). LocalObjectStorage is the dev-sim; swap to an R2 adapter
        // when Storage:Provider = "r2".
        services.AddSingleton(MediaOptionsFactory.Build(configuration));
        services.AddSingleton<IObjectStorage, LocalObjectStorage>();
```

Add `using Academy.Infrastructure.Media;` to the file's usings if it is not already there.

- [ ] **Step 7: Mount the volume**

In `docker-compose.yml`, under the `api` service, add a `volumes:` entry mounting the named
volume at `/data/media`, and declare `media:` alongside the existing `pgdata:` in the
top-level `volumes:` block. Do exactly the same in `docker-compose.tunnel.yml` — **that is
the deployment that matters; uploads vanish on redeploy without it.**

- [ ] **Step 8: Run the tests to verify they pass**

Run:
```bash
cd backend && dotnet test tests/Integration.Tests/Academy.Integration.Tests.csproj --filter "FullyQualifiedName~LocalObjectStorageTests"
```
Expected: 11 passed (5 facts + 7 theory cases, minus overlap — accept whatever the runner reports as long as 0 fail).

- [ ] **Step 9: Verify the whole suite and the build**

Run:
```bash
cd backend && dotnet build Academy.slnx && dotnet test Academy.slnx
```
Expected: 0 warnings, 0 errors; 206 existing tests plus the new ones, 0 failures.

- [ ] **Step 10: Commit**

```bash
git add backend/src/Application/Abstractions/Ports.cs backend/src/Infrastructure/Media \
        backend/src/Infrastructure/DependencyInjection.cs \
        backend/tests/Integration.Tests/LocalObjectStorageTests.cs \
        docker-compose.yml docker-compose.tunnel.yml
git commit -m "feat: real IObjectStorage with a local-disk implementation"
```

---

## Task 2: Signed media delivery

**Files:**
- Create: `backend/src/Infrastructure/Media/MediaSigner.cs`
- Create: `backend/src/Api/Endpoints/MediaEndpoints.cs`
- Modify: `backend/src/Api/Program.cs`
- Modify: `backend/src/Infrastructure/DependencyInjection.cs`
- Test: `backend/tests/Integration.Tests/MediaDeliveryTests.cs`

**Interfaces:**
- Consumes: `IObjectStorage.OpenReadAsync`, `LocalObjectStorage.IsValidKey`, `MediaOptions` (Task 1).
- Produces, for later tasks:
  - `MediaSigner.Sign(string key) : string` — returns a relative URL `"/api/media/{key}?exp=…&sig=…"`
  - `MediaSigner.Verify(string key, long exp, string sig) : bool`
  - Route `GET /api/media/{**key}` and its `ContentTypeFor(string key)` mapping

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/Integration.Tests/MediaDeliveryTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using Academy.Application.Abstractions;
using Academy.Infrastructure.Media;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>
/// GR-3: media URLs are minted server-side, short-TTL, after an access check. The signature IS
/// the credential here — an &lt;audio&gt; element cannot send an Authorization header.
/// </summary>
public class MediaDeliveryTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> SeedAsync(string key, byte[] bytes)
    {
        using var scope = factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        await storage.PutAsync(key, new MemoryStream(bytes), "audio/mpeg");
        return key;
    }

    private MediaSigner Signer()
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<MediaSigner>();
    }

    [Fact]
    public async Task A_valid_signature_streams_the_object()
    {
        var key = await SeedAsync("audio/ok.mp3", "hello-audio"u8.ToArray());

        var res = await _client.GetAsync(Signer().Sign(key));

        res.EnsureSuccessStatusCode();
        Assert.Equal("audio/mpeg", res.Content.Headers.ContentType?.MediaType);
        Assert.Equal("hello-audio", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_range_request_returns_a_partial_slice()
    {
        var key = await SeedAsync("audio/range.mp3", "0123456789"u8.ToArray());

        var req = new HttpRequestMessage(HttpMethod.Get, Signer().Sign(key));
        req.Headers.Range = new RangeHeaderValue(2, 5);
        var res = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.PartialContent, res.StatusCode);
        Assert.Equal("2345", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_expired_url_is_refused()
    {
        var key = await SeedAsync("audio/expired.mp3", [1, 2, 3]);
        var signer = Signer();

        // Sign for a moment already past.
        var exp = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds();
        var sig = signer.SignatureFor(key, exp);

        var res = await _client.GetAsync($"/api/media/{key}?exp={exp}&sig={sig}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task A_tampered_signature_is_refused()
    {
        var key = await SeedAsync("audio/tampered.mp3", [1, 2, 3]);
        var url = Signer().Sign(key);

        var res = await _client.GetAsync(url.Replace("sig=", "sig=00"));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task A_signature_minted_for_another_key_does_not_work_here()
    {
        await SeedAsync("audio/mine.mp3", [1, 2, 3]);
        var other = await SeedAsync("audio/yours.mp3", [4, 5, 6]);
        var signer = Signer();

        // Take the query from a URL signed for "yours" and paste it onto "mine".
        var query = signer.Sign(other).Split('?')[1];
        var res = await _client.GetAsync($"/api/media/audio/mine.mp3?{query}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task An_unknown_key_is_indistinguishable_from_a_bad_signature()
    {
        var missing = await _client.GetAsync(Signer().Sign("audio/absent.mp3"));
        var badSig = await _client.GetAsync("/api/media/audio/absent.mp3?exp=99999999999&sig=deadbeef");

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, badSig.StatusCode);
    }

    [Fact]
    public async Task A_traversal_key_is_refused()
    {
        var res = await _client.GetAsync("/api/media/..%2F..%2Fetc%2Fpasswd?exp=99999999999&sig=deadbeef");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:
```bash
cd backend && dotnet test tests/Integration.Tests/Academy.Integration.Tests.csproj --filter "FullyQualifiedName~MediaDeliveryTests"
```
Expected: compile failure — `MediaSigner` does not exist.

- [ ] **Step 3: Write the signer**

Create `backend/src/Infrastructure/Media/MediaSigner.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace Academy.Infrastructure.Media;

/// <summary>
/// Mints and verifies signed media URLs. Same HMAC scheme as DevVideoProvider, with its own
/// key so rotating one does not invalidate the other.
/// </summary>
public class MediaSigner(MediaOptions options)
{
    /// <summary>A relative, same-origin URL the browser can hand straight to an audio element.</summary>
    public string Sign(string key)
    {
        var exp = DateTimeOffset.UtcNow.AddMinutes(options.UrlTtlMinutes).ToUnixTimeSeconds();
        return $"/api/media/{key}?exp={exp}&sig={SignatureFor(key, exp)}";
    }

    public string SignatureFor(string key, long exp)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(options.SigningKey));
        return Convert.ToHexStringLower(h.ComputeHash(Encoding.UTF8.GetBytes($"{key}.{exp}")));
    }

    public bool Verify(string key, long exp, string sig)
    {
        if (DateTimeOffset.FromUnixTimeSeconds(exp) < DateTimeOffset.UtcNow) return false;

        // Fixed-time comparison: a byte-by-byte early exit leaks the signature one nibble at a time.
        var expected = Encoding.UTF8.GetBytes(SignatureFor(key, exp));
        var actual = Encoding.UTF8.GetBytes(sig);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
```

- [ ] **Step 4: Write the serve route**

Create `backend/src/Api/Endpoints/MediaEndpoints.cs`:

```csharp
using Academy.Application.Abstractions;
using Academy.Infrastructure.Media;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Academy.Api.Endpoints;

/// <summary>Signed media delivery (GR-3). The signature is the credential — an audio element
/// cannot send an Authorization header, so this route is deliberately anonymous.</summary>
public static class MediaEndpoints
{
    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/media/{**key}", async Task<Results<FileStreamHttpResult, NotFound>> (
                string key, long? exp, string? sig,
                IObjectStorage storage, MediaSigner signer, CancellationToken ct) =>
            {
                // Every rejection is a 404: a 403 would confirm the key exists.
                if (exp is not long e || string.IsNullOrEmpty(sig) || !LocalObjectStorage.IsValidKey(key))
                    return TypedResults.NotFound();
                if (!signer.Verify(key, e, sig)) return TypedResults.NotFound();

                var stream = await storage.OpenReadAsync(key, ct);
                if (stream is null) return TypedResults.NotFound();

                return TypedResults.Stream(stream, ContentTypeFor(key), enableRangeProcessing: true);
            })
            .WithTags("Media")
            .RequireRateLimiting("playback");

        return app;
    }

    /// <summary>Derived from the stored key's extension — never from a client-supplied filename.</summary>
    internal static string ContentTypeFor(string key) =>
        Path.GetExtension(key).ToLowerInvariant() switch
        {
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            _ => "application/octet-stream",
        };
}
```

- [ ] **Step 5: Register the signer and map the route**

In `backend/src/Infrastructure/DependencyInjection.cs`, beside the Task 1 registrations, add:

```csharp
        services.AddSingleton<MediaSigner>();
```

In `backend/src/Api/Program.cs`, add `app.MapMediaEndpoints();` immediately after
`app.MapFinalAssessmentEndpoints();`.

- [ ] **Step 6: Run the tests to verify they pass**

Run:
```bash
cd backend && dotnet test tests/Integration.Tests/Academy.Integration.Tests.csproj --filter "FullyQualifiedName~MediaDeliveryTests"
```
Expected: 7 passed.

If the range test fails with 200 instead of 206, the stream is not seekable — confirm
`LocalObjectStorage.OpenReadAsync` returns `File.OpenRead(...)` and not a copied
`MemoryStream`.

- [ ] **Step 7: Verify the whole suite and the build**

Run:
```bash
cd backend && dotnet build Academy.slnx && dotnet test Academy.slnx
```
Expected: 0 warnings, 0 errors, 0 failures.

- [ ] **Step 8: Commit**

```bash
git add backend/src/Infrastructure/Media/MediaSigner.cs \
        backend/src/Api/Endpoints/MediaEndpoints.cs \
        backend/src/Api/Program.cs backend/src/Infrastructure/DependencyInjection.cs \
        backend/tests/Integration.Tests/MediaDeliveryTests.cs
git commit -m "feat: HMAC-signed, short-TTL media delivery with range support"
```

---

## Task 3: Resolve audio per question or per section

**Files:**
- Create: `backend/src/Application/Assessments/AudioResolution.cs`
- Modify: `backend/src/Application/Assessments/AssessmentContracts.cs` (`AssessmentSectionConfig`, ~line 34)
- Modify: `backend/src/Infrastructure/Assessments/FinalAssessmentService.cs` (`GetAudioUrlAsync` ~line 81, and the `GetStateAsync` question projection ~line 246)
- Test: `backend/tests/Application.Tests/AudioResolutionTests.cs`
- Test: `backend/tests/Integration.Tests/ListeningAudioTests.cs`

**Interfaces:**
- Consumes: `MediaSigner.Sign(string key)` (Task 2).
- Produces, for later tasks:
  - `AssessmentSectionConfig.AudioRef : string?` — the section-level recording
  - `AudioResolution.StorageKey(string? questionAudioRef, QuestionSection section, AssessmentConfig config) : string?`
  - `AudioResolution.PlayKey(Guid questionId, string? questionAudioRef, QuestionSection section, AssessmentConfig config) : string?`

- [ ] **Step 1: Add the section field**

In `backend/src/Application/Assessments/AssessmentContracts.cs`, extend `AssessmentSectionConfig`:

```csharp
public class AssessmentSectionConfig
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public QuestionSection Section { get; set; }
    public int Questions { get; set; }
    public int Minutes { get; set; }

    /// <summary>Optional whole-section recording (Listening). A question's own AudioRef wins.
    /// Lives in the config jsonb, so adding it needs no migration.</summary>
    public string? AudioRef { get; set; }
}
```

- [ ] **Step 2: Write the failing resolution tests**

Create `backend/tests/Application.Tests/AudioResolutionTests.cs`:

```csharp
using Academy.Application.Assessments;
using Academy.Domain.Enums;

namespace Academy.Application.Tests;

/// <summary>
/// Audio may arrive as one clip per question or as one recording per section. A question's own
/// clip wins, so a single item can be re-recorded without replacing the whole section file.
/// </summary>
public class AudioResolutionTests
{
    private static AssessmentConfig Config(string? sectionAudio) => new()
    {
        Sections =
        [
            new() { Section = QuestionSection.Listening, Questions = 50, Minutes = 35, AudioRef = sectionAudio },
            new() { Section = QuestionSection.Structure, Questions = 40, Minutes = 25 },
        ],
    };

    private static readonly Guid Q = Guid.CreateVersion7();

    [Fact]
    public void A_questions_own_clip_wins_over_the_section_recording()
    {
        var c = Config("audio/section.mp3");
        Assert.Equal("audio/mine.mp3",
            AudioResolution.StorageKey("audio/mine.mp3", QuestionSection.Listening, c));
        Assert.Equal(Q.ToString(),
            AudioResolution.PlayKey(Q, "audio/mine.mp3", QuestionSection.Listening, c));
    }

    [Fact]
    public void Without_its_own_clip_a_question_falls_back_to_the_section_recording()
    {
        var c = Config("audio/section.mp3");
        Assert.Equal("audio/section.mp3",
            AudioResolution.StorageKey(null, QuestionSection.Listening, c));
    }

    [Fact]
    public void Section_audio_is_counted_under_one_shared_key()
    {
        var c = Config("audio/section.mp3");
        var other = Guid.CreateVersion7();

        // Two different questions, one allowance — there is only one recording.
        Assert.Equal("Listening", AudioResolution.PlayKey(Q, null, QuestionSection.Listening, c));
        Assert.Equal("Listening", AudioResolution.PlayKey(other, null, QuestionSection.Listening, c));
    }

    [Fact]
    public void With_neither_there_is_no_audio()
    {
        var c = Config(null);
        Assert.Null(AudioResolution.StorageKey(null, QuestionSection.Listening, c));
        Assert.Null(AudioResolution.PlayKey(Q, null, QuestionSection.Listening, c));
    }

    [Fact]
    public void A_section_absent_from_the_config_has_no_audio()
    {
        var c = Config("audio/section.mp3");
        Assert.Null(AudioResolution.StorageKey(null, QuestionSection.Reading, c));
    }

    [Fact]
    public void Blank_refs_count_as_absent()
    {
        var c = Config("   ");
        Assert.Null(AudioResolution.StorageKey("  ", QuestionSection.Listening, c));
        Assert.Null(AudioResolution.PlayKey(Q, "", QuestionSection.Listening, c));
    }
}
```

- [ ] **Step 3: Run them to verify they fail**

Run:
```bash
cd backend && dotnet test tests/Application.Tests/Academy.Application.Tests.csproj
```
Expected: compile failure — `AudioResolution` does not exist.

- [ ] **Step 4: Write the resolution helper**

Create `backend/src/Application/Assessments/AudioResolution.cs`:

```csharp
using Academy.Domain.Enums;

namespace Academy.Application.Assessments;

/// <summary>
/// Which audio object a question plays, and which counter its plays are charged to.
/// Pure — no EF, no HTTP — so both the URL-minting path and the state projection can share it.
/// </summary>
public static class AudioResolution
{
    /// <summary>The storage key to serve, or null when the question has no audio at all.</summary>
    public static string? StorageKey(string? questionAudioRef, QuestionSection section, AssessmentConfig config)
    {
        if (!string.IsNullOrWhiteSpace(questionAudioRef)) return questionAudioRef;

        var sectionRef = config.Sections.FirstOrDefault(s => s.Section == section)?.AudioRef;
        return string.IsNullOrWhiteSpace(sectionRef) ? null : sectionRef;
    }

    /// <summary>
    /// The AttemptState.AudioPlays key, or null when there is no audio. A per-question clip is
    /// charged to the question; a section recording is charged to the section, so replaying it
    /// from three different questions consumes ONE shared allowance — there is only one audio.
    /// </summary>
    public static string? PlayKey(Guid questionId, string? questionAudioRef, QuestionSection section, AssessmentConfig config)
    {
        if (!string.IsNullOrWhiteSpace(questionAudioRef)) return questionId.ToString();

        var sectionRef = config.Sections.FirstOrDefault(s => s.Section == section)?.AudioRef;
        return string.IsNullOrWhiteSpace(sectionRef) ? null : section.ToString();
    }
}
```

- [ ] **Step 5: Run them to verify they pass**

Run:
```bash
cd backend && dotnet test tests/Application.Tests/Academy.Application.Tests.csproj
```
Expected: 7 passed (1 existing + 6 new).

- [ ] **Step 6: Rewrite `GetAudioUrlAsync`**

In `backend/src/Infrastructure/Assessments/FinalAssessmentService.cs`, replace the body of
`GetAudioUrlAsync` from the question lookup onward. Inject `MediaSigner signer` into the
service's primary constructor parameter list first.

```csharp
        var question = await db.Questions
            .Where(q => q.Id == questionId)
            .Select(q => new { q.Id, q.AudioRef, q.Section })
            .FirstOrDefaultAsync(ct)
            ?? throw new AssessmentException("Soal tidak ditemukan.", 404);

        // The question must belong to this assessment AND the active section.
        var state = AttemptState.Parse(attempt.State);
        var current = state.Current ?? throw new AssessmentException("Tidak ada bagian yang aktif.", 409);
        var allowed = await SectionQuestionIdsAsync(attempt.AssessmentId, current.Section, ct);
        if (!allowed.Contains(questionId.ToString()))
            throw new AssessmentException("Soal ini bukan bagian dari sesi yang sedang berjalan.", 403);

        var config = AssessmentService.ParseConfig(
            await db.Assessments.Where(a => a.Id == attempt.AssessmentId).Select(a => a.Config).FirstAsync(ct));

        // A question's own clip wins; otherwise the section recording, if there is one.
        var storageKey = AudioResolution.StorageKey(question.AudioRef, question.Section, config)
            ?? throw new AssessmentException("Soal ini tidak memiliki audio.", 400);

        // Play limit is counted SERVER-side; the client cannot grant itself another play.
        if (config.AudioPlayLimit is int limit)
        {
            var key = AudioResolution.PlayKey(questionId, question.AudioRef, question.Section, config)!;
            state.AudioPlays.TryGetValue(key, out var used);
            if (used >= limit)
                throw new AssessmentException("Batas pemutaran audio untuk soal ini sudah tercapai.", 409);
            state.AudioPlays[key] = used + 1;
            attempt.State = state.Serialize();
            await db.SaveChangesAsync(ct);
        }

        // Signed, short-TTL, minted per play (GR-3).
        return signer.Sign(storageKey);
```

Note the null check moved: the old code rejected a null `AudioRef` before the
section/ownership checks. It must now come **after** resolution, because a question with a
null `AudioRef` may still have section audio.

- [ ] **Step 7: Fix `HasAudio` and `audioPlaysLeft` in `GetStateAsync`**

Around line 246 the projection sets `HasAudio` from `q.Question.AudioRef != null`. Section
audio would be invisible to the learner — the player renders its button on that flag alone
(`frontend/app/app/assessment/[id]/page.tsx:275`), so the audio would exist, be served, and
never be offered.

Change the projection to carry the raw ref, then compute both `HasAudio` and the play-counter
key in memory (EF cannot translate `AudioResolution`):

```csharp
            var rows = await db.AssessmentQuestions
                .Where(q => q.AssessmentId == attempt.AssessmentId
                            && q.Question.Section == currentSection)
                .OrderBy(q => q.OrderIndex)
                .Select(q => new
                {
                    q.QuestionId,
                    q.Question.Section,
                    q.Question.Prompt,
                    q.Question.Choices,
                    q.Question.PassageRef,
                    q.Question.AudioRef,
                })
                .ToListAsync(ct);

            questions = rows
                .Select(r => new StudentQuestionDto(
                    r.QuestionId, r.Section.ToString(), r.Prompt, ParseStrings(r.Choices), r.PassageRef,
                    AudioResolution.StorageKey(r.AudioRef, r.Section, config) is not null))
                .ToList();

            var saved = ParseInts(attempt.Answers);
            foreach (var r in rows)
            {
                var id = r.QuestionId.ToString();
                if (saved.TryGetValue(id, out var v)) answers[id] = v;

                if (config.AudioPlayLimit is int limit
                    && AudioResolution.PlayKey(r.QuestionId, r.AudioRef, r.Section, config) is string playKey)
                {
                    state.AudioPlays.TryGetValue(playKey, out var used);
                    // Keyed by question id for the client, even when the allowance is shared,
                    // so every question in the section shows the same remaining count.
                    audioLeft[id] = Math.Max(0, limit - used);
                }
            }
```

Add `using Academy.Application.Assessments;` if it is not already present.

- [ ] **Step 8: Make the fixture's audio key valid**

`ItpFinal.cs:83` sets `AudioRef = "clip.mp3"`. That is not a valid storage key — keys are
`folder/filename`, so it would never serve. Change it to:

```csharp
                    AudioRef = section == QuestionSection.Listening ? "audio/clip.mp3" : null,
```

Do not loosen `LocalObjectStorage.IsValidKey` to accommodate the old value.

- [ ] **Step 9: Write the integration tests**

These go **into the existing `backend/tests/Integration.Tests/FinalAssessmentTests.cs`**,
beneath the current `Listening_audio_play_limit_is_enforced_server_side` fact. That class
already owns the fixture (`SetUp`, `Start`, `Authed`, `AuthedGet`, the `Ctx` record) — a new
class would have to duplicate all of it.

`SetUp` has the signature `SetUp(int? retakeCap = 1, int? audioPlayLimit = null, bool
bandsCoverOnlyZero = false)` and returns `Ctx(Token, UserId, Admin, ProgramId, SessionId,
AssessmentId, Key)`. Add one more optional parameter to it, `string? sectionAudioRef = null`,
threaded into `ItpFinal.SeedAsync` so the Listening section config can carry an `audioRef`;
add the matching optional parameter to `ItpFinal.SeedAsync` and emit it in `SectionConfig()`.

```csharp
    // ---- audio resolution: question clip, or one section recording ----

    [Fact]
    public async Task A_questions_own_clip_is_served_through_a_signed_url()
    {
        var c = await SetUp();
        await SeedObjectAsync("audio/clip.mp3", "per-question"u8.ToArray());
        var state = await Start(c);
        var audioQ = state.Questions.First(q => q.HasAudio).Id;

        var res = await Authed(HttpMethod.Get, $"/api/attempts/{state.AttemptId}/audio/{audioQ}", c.Token);
        res.EnsureSuccessStatusCode();
        var url = (await res.Content.ReadFromJsonAsync<AudioUrlResponse>(Json))!.Url;

        // The minted URL must actually serve — the previous implementation returned a dead path.
        var played = await _client.GetAsync(url);
        played.EnsureSuccessStatusCode();
        Assert.Equal("per-question", await played.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_question_without_its_own_clip_falls_back_to_the_section_recording()
    {
        var c = await SetUp(sectionAudioRef: "audio/section.mp3");
        await SeedObjectAsync("audio/section.mp3", "whole-section"u8.ToArray());
        await ClearQuestionAudioAsync(c.AssessmentId);

        var state = await Start(c);
        var q = state.Questions.First().Id;

        var res = await Authed(HttpMethod.Get, $"/api/attempts/{state.AttemptId}/audio/{q}", c.Token);
        res.EnsureSuccessStatusCode();
        var url = (await res.Content.ReadFromJsonAsync<AudioUrlResponse>(Json))!.Url;

        var played = await _client.GetAsync(url);
        Assert.Equal("whole-section", await played.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_question_with_neither_still_reports_no_audio()
    {
        var c = await SetUp();
        await ClearQuestionAudioAsync(c.AssessmentId);

        var state = await Start(c);
        var q = state.Questions.First().Id;

        var res = await Authed(HttpMethod.Get, $"/api/attempts/{state.AttemptId}/audio/{q}", c.Token);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Section_audio_shares_one_allowance_across_its_questions()
    {
        var c = await SetUp(audioPlayLimit: 1, sectionAudioRef: "audio/section.mp3");
        await SeedObjectAsync("audio/section.mp3", [1, 2, 3]);
        await ClearQuestionAudioAsync(c.AssessmentId);

        var state = await Start(c);
        var a = state.Questions[0].Id;
        var b = state.Questions[1].Id;

        (await Authed(HttpMethod.Get, $"/api/attempts/{state.AttemptId}/audio/{a}", c.Token))
            .EnsureSuccessStatusCode();

        // There is only ONE recording, so the second question has no allowance left.
        var second = await Authed(HttpMethod.Get, $"/api/attempts/{state.AttemptId}/audio/{b}", c.Token);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Per_question_clips_have_separate_allowances()
    {
        var c = await SetUp(audioPlayLimit: 1);
        var state = await Start(c);
        var a = state.Questions[0].Id;
        var b = state.Questions[1].Id;

        (await Authed(HttpMethod.Get, $"/api/attempts/{state.AttemptId}/audio/{a}", c.Token))
            .EnsureSuccessStatusCode();
        (await Authed(HttpMethod.Get, $"/api/attempts/{state.AttemptId}/audio/{b}", c.Token))
            .EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Section_audio_is_visible_to_the_player()
    {
        var c = await SetUp(sectionAudioRef: "audio/section.mp3");
        await ClearQuestionAudioAsync(c.AssessmentId);

        var state = await Start(c);

        // hasAudio drives whether the play button renders at all — section audio that the
        // client cannot see is audio that is never offered.
        Assert.All(state.Questions, q => Assert.True(q.HasAudio));
    }

    // ---- helpers for the audio facts ----

    private async Task SeedObjectAsync(string key, byte[] bytes)
    {
        using var scope = factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        await storage.PutAsync(key, new MemoryStream(bytes), "audio/mpeg");
    }

    /// <summary>Strips per-question audio so only the section recording (if any) can resolve.</summary>
    private async Task ClearQuestionAudioAsync(Guid assessmentId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ids = await db.AssessmentQuestions
            .Where(aq => aq.AssessmentId == assessmentId)
            .Select(aq => aq.QuestionId)
            .ToListAsync();
        var questions = await db.Questions.Where(q => ids.Contains(q.Id)).ToListAsync();
        foreach (var q in questions) q.AudioRef = null;
        await db.SaveChangesAsync();
    }
```

Add `using Academy.Application.Abstractions;` for `IObjectStorage`. `AudioUrlResponse` is the
existing response record on the audio endpoint — reuse it rather than declaring another.

- [ ] **Step 10: Run the tests**

Run:
```bash
cd backend && dotnet test tests/Integration.Tests/Academy.Integration.Tests.csproj --filter "FullyQualifiedName~FinalAssessmentTests"
```
Expected: the existing facts plus 6 new ones, 0 failures.

- [ ] **Step 11: Verify the whole suite and the build**

Run:
```bash
cd backend && dotnet build Academy.slnx && dotnet test Academy.slnx
```
Expected: 0 warnings, 0 errors, 0 failures.

- [ ] **Step 12: Commit**

```bash
git add backend/src/Application/Assessments backend/src/Infrastructure/Assessments/FinalAssessmentService.cs \
        backend/tests/Application.Tests/AudioResolutionTests.cs \
        backend/tests/Integration.Tests/ListeningAudioTests.cs
git commit -m "feat: serve listening audio from a question clip or a section recording"
```

---

## Task 4: Admin upload

**Files:**
- Create: `backend/src/Application/Assessments/MediaUpload.cs`
- Modify: `backend/src/Api/Endpoints/MediaEndpoints.cs`
- Create: `frontend/components/admin/AudioUpload.tsx`
- Modify: `frontend/app/admin/questions/page.tsx`
- Modify: `frontend/app/admin/assessments/[id]/page.tsx`
- Modify: `frontend/lib/sessions.ts`
- Test: `backend/tests/Application.Tests/MediaUploadTests.cs`
- Test: `backend/tests/Integration.Tests/MediaUploadEndpointTests.cs`

**Interfaces:**
- Consumes: `IObjectStorage.PutAsync`, `MediaOptions.MaxUploadBytes` (Task 1); `AssessmentSectionConfig.AudioRef` (Task 3).
- Produces:
  - `MediaUpload.Validate(string? contentType, long length, long maxBytes) : string?` — a Bahasa Indonesia error, or null when acceptable
  - `MediaUpload.ExtensionFor(string contentType) : string`
  - `POST /api/admin/media/audio` → `{ "key": "audio/…" }`
  - `uploadAudio(t: string, file: File): Promise<{ key: string }>` in `frontend/lib/sessions.ts`
  - `AudioUpload({ token, value, onChange }: { token: string; value: string | null; onChange: (key: string | null) => void })`

- [ ] **Step 1: Write the failing validator tests**

Create `backend/tests/Application.Tests/MediaUploadTests.cs`:

```csharp
using Academy.Application.Assessments;

namespace Academy.Application.Tests;

/// <summary>
/// Upload validation is pure so it can be tested without pushing 100 MB through HTTP.
/// </summary>
public class MediaUploadTests
{
    private const long Max = 1024;

    [Theory]
    [InlineData("audio/mpeg", ".mp3")]
    [InlineData("audio/mp4", ".m4a")]
    [InlineData("audio/x-m4a", ".m4a")]
    [InlineData("audio/wav", ".wav")]
    [InlineData("audio/ogg", ".ogg")]
    public void Accepted_types_pass_and_map_to_an_extension(string type, string ext)
    {
        Assert.Null(MediaUpload.Validate(type, 100, Max));
        Assert.Equal(ext, MediaUpload.ExtensionFor(type));
    }

    [Theory]
    [InlineData("video/mp4")]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    [InlineData("")]
    [InlineData(null)]
    public void Other_types_are_refused(string? type)
        => Assert.NotNull(MediaUpload.Validate(type, 100, Max));

    [Fact]
    public void An_oversize_file_is_refused_and_the_message_names_the_limit()
    {
        var error = MediaUpload.Validate("audio/mpeg", Max + 1, Max);
        Assert.NotNull(error);
        Assert.Contains("1", error);
    }

    [Fact]
    public void An_empty_file_is_refused()
        => Assert.NotNull(MediaUpload.Validate("audio/mpeg", 0, Max));

    [Fact]
    public void A_type_with_charset_parameters_still_matches()
        => Assert.Null(MediaUpload.Validate("audio/mpeg; charset=binary", 100, Max));
}
```

- [ ] **Step 2: Run them to verify they fail**

Run:
```bash
cd backend && dotnet test tests/Application.Tests/Academy.Application.Tests.csproj
```
Expected: compile failure — `MediaUpload` does not exist.

- [ ] **Step 3: Write the validator**

Create `backend/src/Application/Assessments/MediaUpload.cs`:

```csharp
namespace Academy.Application.Assessments;

/// <summary>Pure validation for an admin audio upload. Messages are shown to the admin.</summary>
public static class MediaUpload
{
    private static readonly Dictionary<string, string> Accepted = new(StringComparer.OrdinalIgnoreCase)
    {
        ["audio/mpeg"] = ".mp3",
        ["audio/mp4"] = ".m4a",
        ["audio/x-m4a"] = ".m4a",
        ["audio/wav"] = ".wav",
        ["audio/x-wav"] = ".wav",
        ["audio/ogg"] = ".ogg",
    };

    /// <summary>Null when acceptable; otherwise the reason, in Bahasa Indonesia.</summary>
    public static string? Validate(string? contentType, long length, long maxBytes)
    {
        var type = Normalize(contentType);
        if (type is null || !Accepted.ContainsKey(type))
            return "Format audio tidak didukung. Gunakan MP3, M4A, WAV, atau OGG.";
        if (length <= 0)
            return "Berkas audio kosong.";
        if (length > maxBytes)
            return $"Berkas audio terlalu besar. Maksimum {maxBytes / (1024 * 1024)} MB.";
        return null;
    }

    /// <summary>The extension for a stored object. Derived from the content type — never from
    /// a client-supplied filename.</summary>
    public static string ExtensionFor(string contentType)
        => Normalize(contentType) is string t && Accepted.TryGetValue(t, out var ext) ? ext : ".bin";

    private static string? Normalize(string? contentType)
        => string.IsNullOrWhiteSpace(contentType) ? null : contentType.Split(';')[0].Trim();
}
```

- [ ] **Step 4: Run them to verify they pass**

Run:
```bash
cd backend && dotnet test tests/Application.Tests/Academy.Application.Tests.csproj
```
Expected: 20 passed (1 existing + 6 from Task 3 + 13 here).

- [ ] **Step 5: Write the failing endpoint tests**

Create `backend/tests/Integration.Tests/MediaUploadEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Academy.Application.Auth;
using Academy.Domain.Enums;
using Academy.Infrastructure.Media;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>
/// Admin audio upload. Content type and size are validated server-side, and the stored
/// extension comes from the content type — never from a client-supplied filename.
/// </summary>
public class MediaUploadEndpointTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    private static MultipartFormDataContent Form(byte[] bytes, string contentType, string filename)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", filename } };
    }

    private async Task<HttpResponseMessage> Upload(string? token, MultipartFormDataContent form)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/admin/media/audio") { Content = form };
        if (token is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(req);
    }

    [Fact]
    public async Task An_admin_upload_is_stored_and_then_serves()
    {
        var res = await Upload(await AdminToken(), Form("real-audio"u8.ToArray(), "audio/mpeg", "clip.mp3"));
        res.EnsureSuccessStatusCode();

        var key = (await res.Content.ReadFromJsonAsync<KeyDto>(Json))!.Key;
        Assert.StartsWith("audio/", key);

        string url;
        using (var scope = factory.Services.CreateScope())
            url = scope.ServiceProvider.GetRequiredService<MediaSigner>().Sign(key);

        var played = await _client.GetAsync(url);
        played.EnsureSuccessStatusCode();
        Assert.Equal("real-audio", await played.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_extension_comes_from_the_content_type_not_the_filename()
    {
        var res = await Upload(await AdminToken(), Form([1, 2, 3], "audio/mpeg", "evil.exe"));
        res.EnsureSuccessStatusCode();

        var key = (await res.Content.ReadFromJsonAsync<KeyDto>(Json))!.Key;
        Assert.EndsWith(".mp3", key);
        Assert.DoesNotContain("evil", key);
    }

    [Fact]
    public async Task A_non_audio_content_type_is_refused()
    {
        var res = await Upload(await AdminToken(), Form([1, 2, 3], "video/mp4", "movie.mp4"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("Format audio", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_empty_file_is_refused()
    {
        var res = await Upload(await AdminToken(), Form([], "audio/mpeg", "empty.mp3"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task A_learner_cannot_upload()
    {
        var res = await Upload(await LearnerToken(), Form([1, 2, 3], "audio/mpeg", "clip.mp3"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task An_anonymous_request_cannot_upload()
    {
        var res = await Upload(null, Form([1, 2, 3], "audio/mpeg", "clip.mp3"));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    private record KeyDto(string Key);

    private async Task<string> LearnerToken()
    {
        var email = $"stu{Guid.NewGuid():N}@test.local";
        (await _client.PostAsJsonAsync("/api/auth/register", new { name = "Stu", email, password = Pw }))
            .EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Pw });
        return (await login.Content.ReadFromJsonAsync<AuthTokens>())!.AccessToken;
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
}
```

- [ ] **Step 6: Add the upload route**

In `backend/src/Api/Endpoints/MediaEndpoints.cs`, inside `MapMediaEndpoints`, add:

```csharp
        app.MapPost("/api/admin/media/audio", async Task<Results<Ok<MediaKeyResponse>, ProblemHttpResult>> (
                IFormFile file, IObjectStorage storage, MediaOptions options, CancellationToken ct) =>
            {
                if (MediaUpload.Validate(file.ContentType, file.Length, options.MaxUploadBytes) is string error)
                    return TypedResults.Problem(title: error, statusCode: 400);

                var key = $"audio/{Guid.CreateVersion7():N}{MediaUpload.ExtensionFor(file.ContentType)}";
                await using var stream = file.OpenReadStream();
                await storage.PutAsync(key, stream, file.ContentType, ct);

                return TypedResults.Ok(new MediaKeyResponse(key));
            })
            .RequireAuthorization("Admin")
            .WithTags("Media")
            .DisableAntiforgery()          // required for IFormFile binding in minimal APIs
            .DisableRequestSizeLimit();    // our own MaxUploadBytes is the limit; Kestrel's default is 30 MB
```

Add the response record at the bottom of the file:

```csharp
public record MediaKeyResponse(string Key);
```

and `using Academy.Application.Assessments;` to the usings.

**A note on `DisableRequestSizeLimit`:** Kestrel's 30 MB default would reject a legitimate
section recording before the handler ever runs, and minimal-API endpoint filters run after
model binding, so the limit cannot be raised inside the handler. Disabling it means a
malicious body is buffered before `file.Length` is checked. This endpoint is
admin-authenticated and rate-limited, so that trade is acceptable — but do not copy the
pattern onto an anonymous route.

- [ ] **Step 7: Run the endpoint tests**

Run:
```bash
cd backend && dotnet test tests/Integration.Tests/Academy.Integration.Tests.csproj --filter "FullyQualifiedName~MediaUploadEndpointTests"
```
Expected: 5 passed.

- [ ] **Step 8: Regenerate the API client**

The new endpoints must appear in the generated schema. From `backend/`:

```bash
dotnet build Academy.slnx
(ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:8087 \
 ConnectionStrings__Default="Host=localhost;Port=5999;Database=none;Username=x;Password=x" \
 RunMigrations=false SeedSampleData=false dotnet src/Api/bin/Debug/net10.0/Academy.Api.dll >/dev/null 2>&1 &)
curl -s --retry 25 --retry-delay 1 --retry-all-errors -o /dev/null http://localhost:8087/openapi/v1.json
cd ../frontend && npx openapi-typescript http://localhost:8087/openapi/v1.json -o api-client/schema.ts
pkill -f "Academy.Api.dll"
```

Confirm: `grep -c "MediaKeyResponse" api-client/schema.ts` returns at least 1.

- [ ] **Step 9: Add the frontend upload helper**

In `frontend/lib/sessions.ts`, add — note it cannot use the `api()` helper, which
JSON-stringifies its body and sets a JSON content-type:

```ts
export async function uploadAudio(t: string, file: File): Promise<{ key: string }> {
  const form = new FormData();
  form.append("file", file);
  const res = await fetch(`${API}/api/admin/media/audio`, {
    method: "POST",
    headers: { Authorization: `Bearer ${t}` },   // let the browser set the multipart boundary
    body: form,
    cache: "no-store",
  });
  if (!res.ok) throw await problem(res, "Unggah audio gagal.");
  return await res.json();
}
```

- [ ] **Step 10: Build the shared upload control**

Create `frontend/components/admin/AudioUpload.tsx`:

```tsx
"use client";

import { useRef, useState } from "react";
import { Button } from "@/components/ui";
import { uploadAudio } from "@/lib/sessions";

/** Uploads one audio file and reports its storage key. Uploading replaces whatever the
 *  owning question or section currently points at. */
export function AudioUpload({
  token, value, onChange,
}: { token: string; value: string | null; onChange: (key: string | null) => void }) {
  const input = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  async function pick(file: File) {
    setBusy(true);
    setErr(null);
    try {
      const { key } = await uploadAudio(token, file);
      onChange(key);
    } catch (e) {
      setErr(e instanceof Error ? e.message : "Unggah audio gagal.");
    } finally {
      setBusy(false);
      if (input.current) input.current.value = "";
    }
  }

  return (
    <div className="flex flex-col gap-1.5">
      <div className="flex items-center gap-2">
        <input
          ref={input}
          type="file"
          accept="audio/mpeg,audio/mp4,audio/x-m4a,audio/wav,audio/ogg"
          className="hidden"
          onChange={(e) => { const f = e.target.files?.[0]; if (f) void pick(f); }}
        />
        <Button size="sm" variant="secondary" disabled={busy} onClick={() => input.current?.click()}>
          {busy ? "Mengunggah…" : value ? "Ganti audio" : "Unggah audio"}
        </Button>
        {value && (
          <button
            type="button"
            onClick={() => onChange(null)}
            className="text-[12.5px] font-bold text-danger hover:underline"
          >
            Hapus
          </button>
        )}
      </div>
      {value && <p className="truncate text-[12px] text-ink-muted">{value}</p>}
      {!value && <p className="text-[12px] text-ink-subtle">Belum ada audio. MP3, M4A, WAV, atau OGG.</p>}
      {err && <p className="text-[12px] text-danger">{err}</p>}
    </div>
  );
}
```

- [ ] **Step 11: Wire it into the question form**

In `frontend/app/admin/questions/page.tsx`:

1. Add `const [audioRef, setAudioRef] = useState<string | null>(null);` beside the other
   form state, seeded from the edited question's `audioRef` when one is being edited.
2. Render `<AudioUpload token={token} value={audioRef} onChange={setAudioRef} />` in the form,
   under a label `Audio (Listening)`, shown only when `section === "Listening"`.
3. Replace the hardcoded `audioRef: null` in the save body with
   `audioRef: section === "Listening" ? audioRef : null`.

- [ ] **Step 12: Wire it into the composer**

In `frontend/app/admin/assessments/[id]/page.tsx`, on the Listening section panel, render an
`AudioUpload` bound to that section's `audioRef` in the config, and include the updated
config in the save. If the page currently saves only questions and not config, add the
config save — the section recording is stored in the assessment config, so it cannot persist
otherwise. Label it `Rekaman satu bagian (opsional)` with the helper text
`Dipakai untuk soal yang tidak punya audio sendiri.`

- [ ] **Step 13: Verify the frontend**

Run:
```bash
cd frontend && npx tsc --noEmit && NEXT_TELEMETRY_DISABLED=1 NEXT_PUBLIC_API_BASE_URL="" API_INTERNAL_URL="http://api:8080" npm run build
```
Expected: no type or lint errors; build compiles.

- [ ] **Step 14: Verify the whole backend suite**

Run:
```bash
cd backend && dotnet build Academy.slnx && dotnet test Academy.slnx
```
Expected: 0 warnings, 0 errors, 0 failures.

- [ ] **Step 15: Commit**

```bash
git add backend/src/Application/Assessments/MediaUpload.cs \
        backend/src/Api/Endpoints/MediaEndpoints.cs \
        backend/tests/Application.Tests/MediaUploadTests.cs \
        backend/tests/Integration.Tests/MediaUploadEndpointTests.cs \
        frontend/api-client/schema.ts frontend/lib/sessions.ts \
        frontend/components/admin/AudioUpload.tsx \
        frontend/app/admin/questions/page.tsx \
        "frontend/app/admin/assessments/[id]/page.tsx"
git commit -m "feat: upload listening audio from the question form and the composer"
```

---

## Task 5: Readiness check and end-to-end verification

**Files:**
- Modify: `backend/src/Infrastructure/Programs/ProgramAdminService.cs` (`GetReadinessAsync` ~line 258, and a new private check beside `FinalSectionsCheckAsync` ~line 329)
- Modify: `frontend/components/admin/ReadinessPanel.tsx` (the `FIX` map)
- Modify: `docs/KAK_INVERTA_TOEFL_v1.0.md`
- Test: `backend/tests/Integration.Tests/ProgramReadinessTests.cs`

**Interfaces:**
- Consumes: `AssessmentSectionConfig.AudioRef` (Task 3); the existing
  `ReadinessCheckDto(string Key, string Title, bool Passed, bool Blocking, string? Detail)`.
- Produces: readiness check key `listening_audio_present`.

- [ ] **Step 1: Write the failing tests**

Add to `backend/tests/Integration.Tests/ProgramReadinessTests.cs`, following the style of the
existing checks' tests:

```csharp
    [Fact]
    public async Task Listening_questions_without_audio_block_publishing()
    {
        var admin = await AdminToken();
        var program = await BuildReadyProgram(admin);

        // Strip the audio from this program's Listening questions ONLY. Scoping matters: the
        // integration suite shares one database, and a global
        // `db.Questions.Where(q => q.Section == Listening)` would silently break other tests.
        await ClearListeningAudioAsync(program);

        var r = await AuthedGet<ProgramReadinessDto>($"/api/admin/programs/{program}/readiness", admin);
        var check = r.Checks.Single(c => c.Key == "listening_audio_present");

        Assert.False(check.Passed);
        Assert.True(check.Blocking);
        Assert.False(r.Ready);
        Assert.Equal(HttpStatusCode.Conflict, (await Publish(admin, program, published: true)).StatusCode);
    }

    [Fact]
    public async Task Section_level_audio_alone_satisfies_the_check()
    {
        var admin = await AdminToken();
        var program = await BuildReadyProgram(admin);

        // No question has its own clip …
        await ClearListeningAudioAsync(program);

        // … but the section carries one recording, which covers all of them.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var final = await FinalOf(db, program);

            var config = JsonSerializer.Deserialize<AssessmentConfig>(final.Config, Json)!;
            config.Sections.First(s => s.Section == QuestionSection.Listening).AudioRef = "audio/section.mp3";
            final.Config = JsonSerializer.Serialize(config, Json);

            await db.SaveChangesAsync();
        }

        var r = await AuthedGet<ProgramReadinessDto>($"/api/admin/programs/{program}/readiness", admin);

        Assert.True(Check(r, "listening_audio_present").Passed);
        Assert.True(r.Ready);
        (await Publish(admin, program, published: true)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_final_without_a_listening_section_is_unaffected()
    {
        var admin = await AdminToken();
        var program = await NewProgram(admin);

        // A final assessment with no Listening section at all: nothing to play, nothing to check.
        var final = await NewAssessment(admin, "Final", new
        {
            passThreshold = 0, retakeCap = 1, proctoringEnabled = false,
            sections = new[] { new { section = "Reading", questions = 1, minutes = 55 } },
        });
        var q = await NewQuestion(admin, "Reading");
        await SetQuestions(admin, final, [q]);
        await NewSession(admin, program, "FinalAssessment", 1, final);

        var r = await AuthedGet<ProgramReadinessDto>($"/api/admin/programs/{program}/readiness", admin);

        Assert.True(Check(r, "listening_audio_present").Passed);
    }
```

Add these two helpers to the class:

```csharp
    /// <summary>The final assessment attached to one program's sessions.</summary>
    private static async Task<Assessment> FinalOf(AppDbContext db, Guid program)
    {
        var ids = await db.ProgramSessions
            .Where(s => s.ProgramId == program && s.AssessmentId != null)
            .Select(s => s.AssessmentId!.Value)
            .ToListAsync();
        return await db.Assessments.FirstAsync(a => ids.Contains(a.Id) && a.Kind == AssessmentKind.Final);
    }

    /// <summary>
    /// Clears AudioRef on the Listening questions of ONE program's final assessment.
    /// Scoped deliberately: the integration suite shares a database, so a query filtered only
    /// by section would strip audio from other tests' fixtures too.
    /// </summary>
    private async Task ClearListeningAudioAsync(Guid program)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var final = await FinalOf(db, program);

        var ids = await db.AssessmentQuestions
            .Where(aq => aq.AssessmentId == final.Id && aq.Question.Section == QuestionSection.Listening)
            .Select(aq => aq.QuestionId)
            .ToListAsync();

        var questions = await db.Questions.Where(q => ids.Contains(q.Id)).ToListAsync();
        foreach (var q in questions) q.AudioRef = null;
        await db.SaveChangesAsync();
    }
```

`Check`, `BuildReadyProgram`, `NewProgram`, `NewAssessment`, `NewQuestion`, `SetQuestions`,
`NewSession`, `Publish`, `AuthedGet` and `AdminToken` all already exist in that class — reuse
them, do not redefine. Add `using System.Text.Json;`,
`using Academy.Application.Assessments;` and `using Academy.Domain.Entities;` if they are not
already present.

Note the third test deliberately does **not** assert `r.Ready` — that program has no score
bands and no gating test, so other blocking checks legitimately fail. It asserts only the one
check under test.

- [ ] **Step 2: Run them to verify they fail**

Run:
```bash
cd backend && dotnet test tests/Integration.Tests/Academy.Integration.Tests.csproj --filter "FullyQualifiedName~ProgramReadinessTests"
```
Expected: the three new tests fail — there is no `listening_audio_present` check, so
`Checks.Single(...)` throws.

- [ ] **Step 3: Add the check**

In `backend/src/Infrastructure/Programs/ProgramAdminService.cs`, add the call in
`GetReadinessAsync` immediately after `checks.Add(await FinalSectionsCheckAsync(...));`:

```csharp
        checks.Add(await ListeningAudioCheckAsync(finalAssessmentId, finalConfig, ct));
```

and the method beside `FinalSectionsCheckAsync`:

```csharp
    /// <summary>
    /// A Listening section must be playable before the program can be published. Either the
    /// section carries one recording, or every Listening question carries its own clip —
    /// otherwise a learner hits a 400 mid-exam, which is exactly the failure readiness exists
    /// to move forward in time.
    /// </summary>
    private async Task<ReadinessCheckDto> ListeningAudioCheckAsync(
        Guid? assessmentId, AssessmentConfig? config, CancellationToken ct)
    {
        const string key = "listening_audio_present";
        const string title = "Audio listening tersedia";

        if (assessmentId is not Guid id || config is null)
            return new(key, title, false, true, "Tes akhir belum terpasang.");

        var listening = config.Sections.FirstOrDefault(s => s.Section == QuestionSection.Listening);
        if (listening is null)
            return new(key, title, true, true, null);          // no Listening section, nothing to play

        if (!string.IsNullOrWhiteSpace(listening.AudioRef))
            return new(key, title, true, true, null);          // one recording covers the whole section

        var missing = await db.AssessmentQuestions
            .CountAsync(q => q.AssessmentId == id
                             && q.Question.Section == QuestionSection.Listening
                             && q.Question.AudioRef == null, ct);

        return new(key, title, missing == 0, true,
            missing > 0
                ? $"{missing} soal listening belum memiliki audio. Unggah audio per soal, atau satu rekaman untuk seluruh bagian."
                : null);
    }
```

Note the `CountAsync` predicate inlines the enum comparison and the null test — do not
extract either into a helper method, which EF cannot translate.

- [ ] **Step 4: Run them to verify they pass**

Run:
```bash
cd backend && dotnet test tests/Integration.Tests/Academy.Integration.Tests.csproj --filter "FullyQualifiedName~ProgramReadinessTests"
```
Expected: all pass, 3 more than before.

- [ ] **Step 5: Point the readiness panel at the fix**

In `frontend/components/admin/ReadinessPanel.tsx`, add an entry to the `FIX` map so a failing
check deep-links to where an admin fixes it:

```tsx
  listening_audio_present: () => null,
```

Leave it `null` — the fix lives on two different screens depending on which route the admin
chooses, so a single link would be misleading. The detail text already names both options.

- [ ] **Step 6: Correct the KAK**

In `docs/KAK_INVERTA_TOEFL_v1.0.md` §9.12, the note added by A3 currently says that
attaching listening audio to a question is still API-only. That is no longer true. Edit that
sentence so it names only what genuinely remains API-only — the proctoring on/off flag and
the audio play limit — and drop the listening-audio clause.

- [ ] **Step 7: Verify everything**

Run:
```bash
cd backend && dotnet build Academy.slnx && dotnet test Academy.slnx
cd ../frontend && npx tsc --noEmit && NEXT_TELEMETRY_DISABLED=1 NEXT_PUBLIC_API_BASE_URL="" API_INTERNAL_URL="http://api:8080" npm run build
```
Expected: 0 warnings, 0 errors, 0 test failures, clean frontend build.

- [ ] **Step 8: Walk it in a browser**

Rebuild both containers and do this by hand — no `curl`. A dead audio path is precisely the
failure that unit tests did not catch last time.

```bash
docker compose up -d --build
```

Check the build actually succeeded (look for `Image ... Built`, or check the exit code) —
piping the output can mask a failure.

Then:

1. Create a program; add a video session and a final-assessment session with an assessment.
2. Compose the final assessment's sections.
3. In the question bank, create a Listening question and **upload a real MP3** to it.
4. Open "Periksa" — `listening_audio_present` should be red, naming the questions still
   missing audio.
5. Upload a section recording on the composer's Listening panel; the check turns green.
6. Publish; it succeeds.
7. Enroll a learner, reach the Listening section, and **play the audio** — including a seek
   partway through, which proves range requests work through the signed URL.
8. Confirm the play counter decrements, and that a second question in the same section shares
   the same remaining count when it is section audio.

- [ ] **Step 9: Commit**

```bash
git add backend/src/Infrastructure/Programs/ProgramAdminService.cs \
        backend/tests/Integration.Tests/ProgramReadinessTests.cs \
        frontend/components/admin/ReadinessPanel.tsx \
        docs/KAK_INVERTA_TOEFL_v1.0.md
git commit -m "feat: refuse to publish a program whose listening section has no audio"
```

---

## Definition of done

- `dotnet build backend/Academy.slnx` — 0 warnings, 0 errors.
- `dotnet test backend/Academy.slnx` — all pass (206 existing plus roughly 40 new).
- `npx tsc --noEmit` and `npm run build` clean.
- The Task 5 Step 8 browser walkthrough completes, including a seek during playback.
- `GetAudioUrlAsync` no longer returns an unsigned path; GR-3 is satisfied for audio.
- A program whose Listening section cannot be played cannot be published.

## Deliberately not built

**Video storage** — every session still resolves to the same public Mux test clip.
`DevVideoProvider` mixes `assetId` into the HMAC payload but never uses it to select a file,
and no `BunnyVideoProvider` exists. Video needs transcoding, HLS packaging and adaptive
bitrate; self-hosting it from a docker volume would be substantial work thrown away when
Bunny lands. **Stays in A1.**

**Proctoring hardening** — fullscreen enforcement, `beforeunload`, copy/paste blocking, and
the tab-close blind spot (the watcher only reports on the *return* side, so someone who never
comes back leaves no event). Its own spec.

**R2** — the local implementation is the dev-sim; R2 is a later config swap.

**Object deletion** — replacing an upload orphans the previous file. Accepted: disk is cheap,
and a delete is unsafe without reference counting.

**A media library page** — no picker, no listing, no delete-safety question. Add one if the
same clip turns out to be reused across many questions.
