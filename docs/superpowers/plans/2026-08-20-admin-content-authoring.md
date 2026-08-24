# Admin Content Authoring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give operations a browser interface to author gating tests, compose the final assessment, enter the score→band table, and see why a program can't be published yet.

**Architecture:** Four new admin surfaces on top of APIs that already exist and are already tested. One new backend capability — a readiness check — folded into the existing `ProgramAdminService` and enforced at publish. No new database tables, no migrations, no new dependencies.

**Tech Stack:** .NET 10 minimal APIs · EF Core 10 / PostgreSQL · xUnit + Testcontainers · Next.js App Router · TanStack Query · Tailwind

**Source spec:** [`docs/superpowers/specs/2026-08-20-admin-content-authoring-design.md`](../specs/2026-08-20-admin-content-authoring-design.md)

## Deviation from the spec — read this first

Spec §5.1 proposes a new `IProgramReadinessService`. **This plan folds readiness into the existing `IProgramAdminService` instead.** It is one method; the publish gate needs it in-process anyway; and a separate interface with a single implementation buys nothing but an extra file and a DI registration. Everything else follows the spec as approved.

## Global Constraints

- **Backend:** `Nullable` enabled, `TreatWarningsAsErrors` on. Build must end 0 warnings, 0 errors.
- **Layering:** Domain depends on nothing; Application on Domain; Infrastructure/Api inward. Never the reverse.
- **Minimal APIs use `TypedResults`** so OpenAPI carries response schemas.
- **The frontend API client is generated** — after any backend contract change run `npm run generate:api`. Never hand-edit `frontend/api-client/schema.ts`.
- **All UI strings are Bahasa Indonesia.**
- **Errors are RFC-7807**; surface `title` through the existing `problem()` helpers. No stack traces to clients.
- **No new npm or NuGet dependencies.**
- **Never delete** `watch_progress`, `attempts`, `proctor_events`, or `certificates` rows; never mutate an issued certificate.
- **Answer keys never reach a student DTO.** Admin DTOs may carry them; student DTOs may not.
- Run backend tests with `dotnet test backend/Academy.slnx`.

---

## File Structure

**Backend — modify only, no new files**

| File | Responsibility |
|---|---|
| `backend/src/Application/Programs/ProgramContracts.cs` | Add `ReadinessCheckDto`, `ProgramReadinessDto`, and `GetReadinessAsync` to `IProgramAdminService` |
| `backend/src/Infrastructure/Programs/ProgramAdminService.cs` | Implement readiness; gate publish in `CreateAsync`/`UpdateAsync` |
| `backend/src/Api/Endpoints/ProgramAdminEndpoints.cs` | `GET /api/admin/programs/{id}/readiness` |
| `backend/tests/Integration.Tests/ProgramReadinessTests.cs` | **New.** Readiness + publish-gating acceptance |

**Frontend**

| File | Responsibility |
|---|---|
| `frontend/lib/programs.ts` | Add readiness types + `getProgramReadiness` |
| `frontend/components/admin/ProgramForm.tsx` | **New (extracted).** Program create/edit modal |
| `frontend/components/admin/SessionForm.tsx` | **New (extracted).** Add-session modal |
| `frontend/components/admin/SessionManager.tsx` | **New (extracted).** Session list, reorder, delete, attendance, test button |
| `frontend/components/admin/QuestionPicker.tsx` | **New.** Reusable bank picker (filter, search, multi-select) |
| `frontend/components/admin/GatingTestEditor.tsx` | **New.** Create/edit/attach/detach a gating test |
| `frontend/components/admin/SectionComposer.tsx` | **New.** One section panel: selected vs required |
| `frontend/components/admin/ScoreBandPaste.tsx` | **New.** Parse, validate, preview pasted rows |
| `frontend/components/admin/ReadinessPanel.tsx` | **New.** Checklist modal |
| `frontend/app/admin/assessments/[id]/page.tsx` | **New.** Final-assessment composer page |
| `frontend/app/admin/programs/[id]/score-bands/page.tsx` | **New.** Score→band editor page |
| `frontend/app/admin/programs/page.tsx` | **Modify.** Shrinks to list + wiring after extraction |

---

## Task 1: Readiness checks (backend)

**Files:**
- Modify: `backend/src/Application/Programs/ProgramContracts.cs`
- Modify: `backend/src/Infrastructure/Programs/ProgramAdminService.cs`
- Modify: `backend/src/Api/Endpoints/ProgramAdminEndpoints.cs`
- Test: `backend/tests/Integration.Tests/ProgramReadinessTests.cs`

**Interfaces:**
- Consumes: existing `IProgramAdminService`, `AppDbContext`, `ToeflScoring`, `ProgramException`.
- Produces: `ProgramReadinessDto(bool Ready, IReadOnlyList<ReadinessCheckDto> Checks)`; `ReadinessCheckDto(string Key, string Title, bool Passed, bool Blocking, string? Detail)`; `Task<ProgramReadinessDto> GetReadinessAsync(Guid programId, CancellationToken ct = default)` on `IProgramAdminService`; `GET /api/admin/programs/{id}/readiness`.

- [ ] **Step 1: Write the failing test**

Create `backend/tests/Integration.Tests/ProgramReadinessTests.cs`:

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Academy.Application.Auth;
using Academy.Application.Programs;
using Academy.Domain;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>
/// A3 acceptance: a program is only publishable once its content is complete, so an incomplete
/// score->band table fails BEFORE a learner sits a 115-minute test rather than after.
/// </summary>
public class ProgramReadinessTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    [Fact]
    public async Task An_empty_program_is_not_ready_and_says_why()
    {
        var admin = await AdminToken();
        var program = await NewProgram(admin);

        var r = await AuthedGet<ProgramReadinessDto>($"/api/admin/programs/{program}/readiness", admin);

        Assert.False(r.Ready);
        Assert.False(Check(r, "has_sessions").Passed);
        Assert.False(Check(r, "final_assessment_present").Passed);
        Assert.False(Check(r, "score_bands_complete").Passed);
        // Every blocking failure explains itself — the operator must know what to fix.
        Assert.All(r.Checks.Where(c => c.Blocking && !c.Passed), c => Assert.False(string.IsNullOrWhiteSpace(c.Detail)));
    }

    [Fact]
    public async Task Price_is_reported_but_never_blocks()
    {
        var admin = await AdminToken();
        var program = await NewProgram(admin, priceIdr: 0m);

        var r = await AuthedGet<ProgramReadinessDto>($"/api/admin/programs/{program}/readiness", admin);

        var price = Check(r, "price_set");
        Assert.False(price.Passed);
        Assert.False(price.Blocking);
    }

    [Fact]
    public async Task A_gating_test_with_no_questions_blocks_readiness()
    {
        var admin = await AdminToken();
        var program = await NewProgram(admin);
        var assessment = await NewAssessment(admin, "Gating");
        var session = await NewSession(admin, program, "Video", 1, assessment);

        var r = await AuthedGet<ProgramReadinessDto>($"/api/admin/programs/{program}/readiness", admin);

        var check = Check(r, "gating_tests_populated");
        Assert.False(check.Passed);
        Assert.True(check.Blocking);
        Assert.Contains("Sesi 1", check.Detail);
        _ = session;
    }

    [Fact]
    public async Task Score_bands_with_a_gap_are_incomplete()
    {
        var admin = await AdminToken();
        var program = await NewProgram(admin);

        // Cover everything except Listening raw score 7.
        var bands = FullBands().Where(b => !(b.Section == "Listening" && b.MinRaw == 7)).ToList();
        await PutScoreBands(admin, program, bands);

        var r = await AuthedGet<ProgramReadinessDto>($"/api/admin/programs/{program}/readiness", admin);

        var check = Check(r, "score_bands_complete");
        Assert.False(check.Passed);
        Assert.Contains("Listening", check.Detail);
    }

    [Fact]
    public async Task A_fully_configured_program_is_ready()
    {
        var admin = await AdminToken();
        var program = await BuildReadyProgram(admin);

        var r = await AuthedGet<ProgramReadinessDto>($"/api/admin/programs/{program}/readiness", admin);

        Assert.True(r.Ready);
        Assert.All(r.Checks.Where(c => c.Blocking), c => Assert.True(c.Passed));
    }

    // ---- helpers ----

    private static ReadinessCheckDto Check(ProgramReadinessDto r, string key) =>
        r.Checks.Single(c => c.Key == key);

    /// <summary>A program that passes every blocking check: one video session with a populated
    /// gating test, a final assessment with its configured sections filled, and complete bands.</summary>
    private async Task<Guid> BuildReadyProgram(string admin)
    {
        var program = await NewProgram(admin);

        var gating = await NewAssessment(admin, "Gating");
        var q1 = await NewQuestion(admin, "Reading");
        await SetQuestions(admin, gating, [q1]);
        await NewSession(admin, program, "Video", 1, gating);

        var final = await NewAssessment(admin, "Final", sections: true);
        var l = await NewQuestion(admin, "Listening");
        var s = await NewQuestion(admin, "Structure");
        var rd = await NewQuestion(admin, "Reading");
        await SetQuestions(admin, final, [l, s, rd]);
        await NewSession(admin, program, "FinalAssessment", 2, final);

        await PutScoreBands(admin, program, FullBands());
        return program;
    }

    /// <summary>Every raw score in every ITP section mapped exactly once, scaled in range.</summary>
    private static List<ScoreBandDto> FullBands()
    {
        var bands = new List<ScoreBandDto>();
        foreach (var (section, maxRaw, scaledMax) in new[]
        {
            ("Listening", ToeflScoring.ListeningQuestions, ToeflScoring.ListeningScaledMax),
            ("Structure", ToeflScoring.StructureQuestions, ToeflScoring.StructureScaledMax),
            ("Reading",   ToeflScoring.ReadingQuestions,   ToeflScoring.ReadingScaledMax),
        })
        {
            for (var raw = 0; raw <= maxRaw; raw++)
            {
                var scaled = ToeflScoring.ScaledMin
                    + (int)Math.Round((double)raw / maxRaw * (scaledMax - ToeflScoring.ScaledMin));
                bands.Add(new ScoreBandDto(Guid.Empty, section, raw, raw, scaled, null));
            }
        }
        return bands;
    }

    private async Task PutScoreBands(string admin, Guid program, List<ScoreBandDto> bands) =>
        (await Authed(HttpMethod.Put, $"/api/admin/programs/{program}/score-bands", admin, new { bands }))
            .EnsureSuccessStatusCode();

    private async Task<Guid> NewProgram(string admin, decimal priceIdr = 100000m)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var res = await Authed(HttpMethod.Post, "/api/admin/programs", admin, new
        {
            name = $"Ready {suffix}", slug = $"ready-{suffix}", description = "A3",
            summary = (string?)null, priceIdr, published = false,
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AdminProgramDto>(Json))!.Id;
    }

    private async Task<Guid> NewAssessment(string admin, string kind, bool sections = false)
    {
        object config = sections
            ? new
            {
                passThreshold = 0, retakeCap = 1, proctoringEnabled = true,
                sections = new[]
                {
                    new { section = "Listening", questions = 1, minutes = 35 },
                    new { section = "Structure", questions = 1, minutes = 25 },
                    new { section = "Reading",   questions = 1, minutes = 55 },
                },
            }
            : new { passThreshold = 1, retakeCap = (int?)null, proctoringEnabled = false, sections = Array.Empty<object>() };

        var res = await Authed(HttpMethod.Post, "/api/admin/assessments", admin,
            new { kind, title = $"Tes {Guid.NewGuid():N}"[..12], config });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AssessmentIdDto>(Json))!.Id;
    }

    private async Task<Guid> NewQuestion(string admin, string section)
    {
        var res = await Authed(HttpMethod.Post, "/api/admin/questions", admin, new
        {
            section, prompt = $"{section} {Guid.NewGuid():N}", choices = new[] { "a", "b" },
            correct = new[] { 0 }, audioRef = (string?)null, passageRef = (string?)null, tags = (string[]?)null,
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<QuestionIdDto>(Json))!.Id;
    }

    private async Task SetQuestions(string admin, Guid assessment, Guid[] ids) =>
        (await Authed(HttpMethod.Put, $"/api/admin/assessments/{assessment}/questions", admin,
            new { questionIdsInOrder = ids })).EnsureSuccessStatusCode();

    private async Task<Guid> NewSession(string admin, Guid program, string type, int order, Guid? assessment)
    {
        var res = await Authed(HttpMethod.Post, $"/api/admin/programs/{program}/sessions", admin, new
        {
            type, title = $"Sesi {order}", description = (string?)null, orderIndex = order,
            providerAssetId = type == "Video" ? "sample" : null,
            durationSeconds = type == "Video" ? 600 : (int?)null,
            scheduledAt = (DateTimeOffset?)null, liveMode = (string?)null,
            joinUrl = (string?)null, location = (string?)null, assessmentId = assessment,
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AdminSessionDto>(Json))!.Id;
    }

    private record AssessmentIdDto(Guid Id);
    private record QuestionIdDto(Guid Id);

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

    private async Task<T> AuthedGet<T>(string url, string token)
    {
        var res = await Authed(HttpMethod.Get, url, token);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd backend && dotnet test tests/Integration.Tests/Academy.Integration.Tests.csproj --filter "FullyQualifiedName~ProgramReadinessTests"`

Expected: compile error — `ProgramReadinessDto` and `ReadinessCheckDto` do not exist.

- [ ] **Step 3: Add the contracts**

In `backend/src/Application/Programs/ProgramContracts.cs`, immediately above `public interface IProgramAdminService`:

```csharp
/// <summary>One publish-readiness condition. Blocking checks gate publishing; others inform.</summary>
public record ReadinessCheckDto(string Key, string Title, bool Passed, bool Blocking, string? Detail);

/// <summary>Whether a program may be published, and what is still missing if not.</summary>
public record ProgramReadinessDto(bool Ready, IReadOnlyList<ReadinessCheckDto> Checks);
```

Then add to the `IProgramAdminService` interface, just above `RevokeEnrollmentAsync`:

```csharp
    /// <summary>
    /// Content-completeness of a program. Publishing is gated on this so an incomplete score->band
    /// table fails here rather than after a learner has already sat the final assessment.
    /// </summary>
    Task<ProgramReadinessDto> GetReadinessAsync(Guid programId, CancellationToken ct = default);
```

- [ ] **Step 4: Implement readiness**

In `backend/src/Infrastructure/Programs/ProgramAdminService.cs`, add these usings if missing:

```csharp
using Academy.Application.Assessments;
using Academy.Domain;
```

Add this method just above `RevokeEnrollmentAsync`:

```csharp
    // ---------------------------------------------------------------- readiness

    public async Task<ProgramReadinessDto> GetReadinessAsync(Guid programId, CancellationToken ct = default)
    {
        var program = await db.Programs.FirstOrDefaultAsync(p => p.Id == programId, ct)
            ?? throw new ProgramException("Program tidak ditemukan.", 404);

        var sessions = await db.ProgramSessions
            .Where(s => s.ProgramId == programId)
            .Select(s => new { s.Id, s.Type, s.Title, s.AssessmentId })
            .ToListAsync(ct);

        var checks = new List<ReadinessCheckDto>
        {
            new("has_sessions", "Program memiliki sesi", sessions.Count > 0, true,
                sessions.Count == 0 ? "Belum ada sesi pada program ini." : null),
        };

        // Every attached assessment must actually have questions.
        var empty = new List<string>();
        foreach (var s in sessions.Where(s => s.AssessmentId != null))
            if (!await db.AssessmentQuestions.AnyAsync(q => q.AssessmentId == s.AssessmentId, ct))
                empty.Add(s.Title);
        checks.Add(new("gating_tests_populated", "Semua tes memiliki soal", empty.Count == 0, true,
            empty.Count > 0 ? $"Tes tanpa soal: {string.Join(", ", empty)}." : null));

        var final = sessions.FirstOrDefault(s => s.Type == SessionType.FinalAssessment);
        var finalAssessmentId = final?.AssessmentId;
        checks.Add(new("final_assessment_present", "Tes akhir terpasang", finalAssessmentId is not null, true,
            final is null ? "Belum ada sesi tes akhir."
            : finalAssessmentId is null ? "Sesi tes akhir belum memiliki tes." : null));

        checks.Add(await FinalSectionsCheckAsync(finalAssessmentId, ct));
        checks.Add(await ScoreBandsCheckAsync(programId, ct));

        checks.Add(new("price_set", "Harga sudah diisi", program.PriceIdr > 0, false,
            program.PriceIdr > 0 ? null : "Harga masih 0."));

        return new ProgramReadinessDto(checks.All(c => !c.Blocking || c.Passed), checks);
    }

    /// <summary>Each configured section must hold exactly the number of questions it declares.</summary>
    private async Task<ReadinessCheckDto> FinalSectionsCheckAsync(Guid? assessmentId, CancellationToken ct)
    {
        const string key = "final_sections_populated";
        const string title = "Bagian tes akhir lengkap";

        if (assessmentId is not Guid id)
            return new(key, title, false, true, "Tes akhir belum terpasang.");

        var config = AssessmentService.ParseConfig(
            await db.Assessments.Where(a => a.Id == id).Select(a => a.Config).FirstAsync(ct));

        if (config.Sections.Count == 0)
            return new(key, title, false, true, "Tes akhir belum memiliki konfigurasi bagian.");

        var problems = new List<string>();
        foreach (var section in config.Sections)
        {
            var actual = await db.AssessmentQuestions
                .CountAsync(q => q.AssessmentId == id && q.Question.Section == section.Section, ct);
            if (actual != section.Questions)
                problems.Add($"{section.Section} {actual}/{section.Questions}");
        }

        return new(key, title, problems.Count == 0, true,
            problems.Count > 0 ? $"Jumlah soal belum sesuai: {string.Join(", ", problems)}." : null);
    }

    /// <summary>Every raw score in every ITP section must map exactly once, within its scaled band.</summary>
    private async Task<ReadinessCheckDto> ScoreBandsCheckAsync(Guid programId, CancellationToken ct)
    {
        const string key = "score_bands_complete";
        const string title = "Tabel konversi skor lengkap";

        var bands = await db.ScoreBandMappings
            .Where(b => b.ProgramId == programId)
            .Select(b => new { b.Section, b.MinRaw, b.MaxRaw, b.ScaledScore })
            .ToListAsync(ct);

        if (bands.Count == 0)
            return new(key, title, false, true, "Tabel konversi skor belum diisi.");

        var problems = new List<string>();
        foreach (var (section, maxRaw, scaledMax) in new[]
        {
            (QuestionSection.Listening, ToeflScoring.ListeningQuestions, ToeflScoring.ListeningScaledMax),
            (QuestionSection.Structure, ToeflScoring.StructureQuestions, ToeflScoring.StructureScaledMax),
            (QuestionSection.Reading,   ToeflScoring.ReadingQuestions,   ToeflScoring.ReadingScaledMax),
        })
        {
            var rows = bands.Where(b => b.Section == section).ToList();

            var missing = Enumerable.Range(0, maxRaw + 1)
                .Where(raw => !rows.Any(r => raw >= r.MinRaw && raw <= r.MaxRaw))
                .ToList();
            if (missing.Count > 0)
                problems.Add($"{section}: skor {Describe(missing)} belum dipetakan");

            var outOfRange = rows.Count(r => !ToeflScoring.IsValidScaled(r.ScaledScore, scaledMax));
            if (outOfRange > 0)
                problems.Add($"{section}: {outOfRange} nilai skala di luar {ToeflScoring.ScaledMin}-{scaledMax}");
        }

        return new(key, title, problems.Count == 0, true,
            problems.Count > 0 ? string.Join("; ", problems) + "." : null);
    }

    private static string Describe(List<int> missing) =>
        missing.Count <= 5 ? string.Join(", ", missing) : $"{missing[0]}-{missing[^1]} ({missing.Count} nilai)";
```

- [ ] **Step 5: Expose the endpoint**

In `backend/src/Api/Endpoints/ProgramAdminEndpoints.cs`, add directly after the `GET /programs/{id:guid}` mapping:

```csharp
        g.MapGet("/programs/{id:guid}/readiness", async Task<Ok<ProgramReadinessDto>> (
                Guid id, IProgramAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.GetReadinessAsync(id, ct)));
```

- [ ] **Step 6: Run the tests**

Run: `cd backend && dotnet test tests/Integration.Tests/Academy.Integration.Tests.csproj --filter "FullyQualifiedName~ProgramReadinessTests"`

Expected: 5 passed.

- [ ] **Step 7: Run the whole suite and commit**

Run: `cd backend && dotnet build Academy.slnx && dotnet test Academy.slnx`
Expected: 0 warnings, 0 errors; 198 passed (193 existing + 5 new).

```bash
git add backend/src/Application/Programs/ProgramContracts.cs \
        backend/src/Infrastructure/Programs/ProgramAdminService.cs \
        backend/src/Api/Endpoints/ProgramAdminEndpoints.cs \
        backend/tests/Integration.Tests/ProgramReadinessTests.cs
git commit -m "feat: program readiness checks for publish gating"
```

---

## Task 2: Gate publish on readiness

**Files:**
- Modify: `backend/src/Infrastructure/Programs/ProgramAdminService.cs` (`CreateAsync`, `UpdateAsync`)
- Test: `backend/tests/Integration.Tests/ProgramReadinessTests.cs`

**Interfaces:**
- Consumes: `GetReadinessAsync` from Task 1.
- Produces: `HTTP 409` from `POST /api/admin/programs` and `PUT /api/admin/programs/{id}` when publishing an unready program. No signature changes.

- [ ] **Step 1: Write the failing tests**

Append these to `ProgramReadinessTests.cs`, inside the class above the `// ---- helpers ----` line:

```csharp
    [Fact]
    public async Task A_new_program_cannot_be_created_already_published()
    {
        var admin = await AdminToken();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var res = await Authed(HttpMethod.Post, "/api/admin/programs", admin, new
        {
            name = $"Langsung {suffix}", slug = $"langsung-{suffix}", description = "A3",
            summary = (string?)null, priceIdr = 100000m, published = true,
        });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Publishing_an_unready_program_is_refused_and_names_what_is_missing()
    {
        var admin = await AdminToken();
        var program = await NewProgram(admin);

        var res = await Publish(admin, program, published: true);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("Belum ada sesi", body);
    }

    [Fact]
    public async Task Publishing_a_ready_program_succeeds()
    {
        var admin = await AdminToken();
        var program = await BuildReadyProgram(admin);

        (await Publish(admin, program, published: true)).EnsureSuccessStatusCode();

        var listed = await AuthedGet<List<AdminProgramDto>>("/api/admin/programs", admin);
        Assert.Equal("Published", listed.Single(p => p.Id == program).Status);
    }

    [Fact]
    public async Task Saving_a_draft_and_unpublishing_are_never_gated()
    {
        var admin = await AdminToken();
        var program = await NewProgram(admin);          // empty, therefore unready

        // Draft edits stay legal so operators can stage incomplete content.
        (await Publish(admin, program, published: false)).EnsureSuccessStatusCode();

        var ready = await BuildReadyProgram(admin);
        (await Publish(admin, ready, published: true)).EnsureSuccessStatusCode();
        // Unpublishing is never blocked, even though the check would still run.
        (await Publish(admin, ready, published: false)).EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> Publish(string admin, Guid program, bool published)
    {
        var p = (await AuthedGet<List<AdminProgramDto>>("/api/admin/programs", admin))
            .Single(x => x.Id == program);
        return await Authed(HttpMethod.Put, $"/api/admin/programs/{program}", admin, new
        {
            name = p.Name, slug = p.Slug, description = p.Description,
            summary = p.Summary, priceIdr = p.PriceIdr, published,
        });
    }
```

Add `using System.Net;` to the top of the file.

- [ ] **Step 2: Run to verify they fail**

Run: `cd backend && dotnet test tests/Integration.Tests/Academy.Integration.Tests.csproj --filter "FullyQualifiedName~ProgramReadinessTests"`

Expected: the four new tests fail — publishing currently succeeds unconditionally.

- [ ] **Step 3: Gate `CreateAsync`**

In `ProgramAdminService.CreateAsync`, insert as the first statement of the method body:

```csharp
        // A brand-new program has no sessions, so it can never be ready. Refuse directly rather
        // than running the full check against a program that does not exist yet.
        if (req.Published)
            throw new ProgramException(
                "Program baru harus dibuat sebagai draf. Terbitkan setelah kontennya lengkap.", 409);
```

- [ ] **Step 4: Gate `UpdateAsync`**

In `ProgramAdminService.UpdateAsync`, insert immediately after the `program` null-check and before `var wasPublished`:

```csharp
        // Publishing is the gate; drafts and unpublishing stay free so work can be staged.
        if (req.Published)
        {
            var readiness = await GetReadinessAsync(id, ct);
            if (!readiness.Ready)
                throw new ProgramException(
                    "Program belum siap diterbitkan. " + string.Join(" ",
                        readiness.Checks.Where(c => c.Blocking && !c.Passed)
                                        .Select(c => c.Detail ?? c.Title)), 409);
        }
```

- [ ] **Step 5: Run the tests**

Run: `cd backend && dotnet test tests/Integration.Tests/Academy.Integration.Tests.csproj --filter "FullyQualifiedName~ProgramReadinessTests"`
Expected: 9 passed.

- [ ] **Step 6: Run the whole suite**

Run: `cd backend && dotnet test Academy.slnx`

Expected: all pass. **If `ProgramEnrollmentTests` or `SessionGatingTests` now fail**, it is because their fixtures create programs with `published = true`. Fix those fixtures to create a draft and leave them unpublished — the public-landing test is the only one that needs a published program, and it should build a ready one via the same steps as `BuildReadyProgram`.

- [ ] **Step 7: Commit**

```bash
git add backend/src/Infrastructure/Programs/ProgramAdminService.cs \
        backend/tests/Integration.Tests/
git commit -m "feat: refuse to publish a program that is not content-complete"
```

---

## Frontend verification note

This repository has **no frontend test harness** — every milestone verified the UI with
`tsc --noEmit`, `next build`, and a live walkthrough. These tasks keep that. Where a task contains
non-trivial pure logic (the score-band parser), its verification step lists the exact malformed
inputs to try, because that is the runnable check available here.

Frontend commands, run from `frontend/`:

```bash
npx tsc --noEmit
NEXT_TELEMETRY_DISABLED=1 NEXT_PUBLIC_API_BASE_URL="" API_INTERNAL_URL="http://api:8080" npm run build
```

To see changes in the running deployment, from the repo root:

```bash
docker compose -f docker-compose.tunnel.yml up -d --build frontend
```

Confirm the build actually succeeded — `| tail` hides a failed exit code:

```bash
docker compose -f docker-compose.tunnel.yml up -d --build frontend > /tmp/b.log 2>&1; echo "exit=$?"; grep -c "Image .* Built" /tmp/b.log
```

---

## Task 3: Extract the program page components

Pure move, no behaviour change. Does this first so later tasks add to small files instead of a 500-line one.

**Files:**
- Create: `frontend/components/admin/ProgramForm.tsx`
- Create: `frontend/components/admin/SessionForm.tsx`
- Create: `frontend/components/admin/SessionManager.tsx`
- Modify: `frontend/app/admin/programs/page.tsx`

**Interfaces:**
- Consumes: existing `lib/programs.ts` functions.
- Produces, for later tasks to import:
  - `ProgramForm({ token, program, onClose }: { token: string; program: AdminProgram | null; onClose: () => void })`
  - `SessionForm({ token, programId, nextOrder, onClose }: { token: string; programId: string; nextOrder: number; onClose: () => void })`
  - `SessionManager({ token, program, onClose }: { token: string; program: AdminProgram; onClose: () => void })`

- [ ] **Step 1: Move `ProgramForm`**

Cut the entire `function ProgramForm(...)` from `app/admin/programs/page.tsx` into `components/admin/ProgramForm.tsx`. Add `"use client";` at the top, import what it uses (`useState` from react; `Button`, `Modal` from `@/components/ui`; `createProgram`, `updateProgram`, `num`, `type AdminProgram` from `@/lib/programs`), and `export` the function. Move the `inputCls` and `Field` helpers with it.

- [ ] **Step 2: Move `SessionForm` and `SessionManager`**

Same treatment into `components/admin/SessionForm.tsx` and `components/admin/SessionManager.tsx`. `SessionManager` keeps its `AttendanceModal` usage and imports `SessionForm` from its new location. Move the `SessionKind` type with `SessionForm`.

- [ ] **Step 3: Import them back**

In `app/admin/programs/page.tsx` delete the moved code and add:

```tsx
import { ProgramForm } from "@/components/admin/ProgramForm";
import { SessionManager } from "@/components/admin/SessionManager";
```

Remove any now-unused imports — `next build` fails on unused variables (`@typescript-eslint/no-unused-vars`).

- [ ] **Step 4: Verify nothing changed**

Run: `cd frontend && npx tsc --noEmit && NEXT_TELEMETRY_DISABLED=1 NEXT_PUBLIC_API_BASE_URL="" API_INTERNAL_URL="http://api:8080" npm run build`
Expected: compiles clean. `app/admin/programs/page.tsx` is now under ~120 lines.

Then confirm behaviour is unchanged in the browser: `/admin/programs` still lists programs, "+ Program baru" opens the form, "Sesi" opens the session manager, reorder arrows still work.

- [ ] **Step 5: Commit**

```bash
git add frontend/components/admin/ frontend/app/admin/programs/page.tsx
git commit -m "refactor: extract program form and session manager components"
```

---

## Task 4: Gating test authoring

**Files:**
- Create: `frontend/components/admin/QuestionPicker.tsx`
- Create: `frontend/components/admin/GatingTestEditor.tsx`
- Modify: `frontend/components/admin/SessionManager.tsx`

**Interfaces:**
- Consumes: `listQuestions`, `createAssessment`, `updateAssessment`, `getAssessment`, `setAssessmentQuestions`, `attachAssessment`, `num` from `@/lib/sessions`; `SessionManager` from Task 3.
- Produces:
  - `QuestionPicker({ token, section, selected, onChange }: { token: string; section?: string; selected: string[]; onChange: (ids: string[]) => void })` — reused by Task 5.
  - `GatingTestEditor({ token, sessionId, assessmentId, onClose }: { token: string; sessionId: string; assessmentId: string | null; onClose: () => void })`

- [ ] **Step 1: Build the question picker**

Create `frontend/components/admin/QuestionPicker.tsx`:

```tsx
"use client";

import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Spinner, SearchIcon } from "@/components/ui";
import { listQuestions, QUESTION_SECTIONS, num } from "@/lib/sessions";

/** Bank picker. Selection order is preserved — it becomes the question order in the test. */
export function QuestionPicker({
  token, section, selected, onChange,
}: {
  token: string;
  section?: string;
  selected: string[];
  onChange: (ids: string[]) => void;
}) {
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState(section ?? "");

  useEffect(() => {
    const t = setTimeout(() => setSearch(searchInput.trim()), 300);
    return () => clearTimeout(t);
  }, [searchInput]);

  const q = useQuery({
    queryKey: ["picker-questions", filter, search],
    queryFn: () => listQuestions(token, { section: filter || undefined, search: search || undefined }),
  });

  function toggle(id: string) {
    onChange(selected.includes(id) ? selected.filter((x) => x !== id) : [...selected, id]);
  }

  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-center gap-2">
        {!section && (
          <select
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
            className="rounded-sm border border-border bg-surface px-2.5 py-2 text-[13px] outline-none focus:border-primary"
          >
            <option value="">Semua bagian</option>
            {QUESTION_SECTIONS.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
        )}
        <div className="relative flex-1">
          <SearchIcon size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-ink-subtle" />
          <input
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            placeholder="Cari soal…"
            className="w-full rounded-sm border border-border bg-surface py-2 pl-8 pr-3 text-[13px] outline-none focus:border-primary"
          />
        </div>
        <span className="shrink-0 text-[12px] font-bold text-ink-muted">{selected.length} dipilih</span>
      </div>

      {q.isPending ? (
        <div className="flex min-h-[120px] items-center justify-center"><Spinner size={20} /></div>
      ) : q.data.length === 0 ? (
        <p className="py-6 text-center text-sm text-ink-muted">
          Tidak ada soal. Tambahkan di halaman Bank Soal terlebih dahulu.
        </p>
      ) : (
        <ul className="flex max-h-[320px] flex-col gap-1 overflow-y-auto">
          {q.data.map((item) => {
            const on = selected.includes(item.id);
            const order = selected.indexOf(item.id) + 1;
            return (
              <li key={item.id}>
                <label
                  className={
                    "flex cursor-pointer items-start gap-2.5 rounded-base border px-3 py-2 text-[13px] " +
                    (on ? "border-primary bg-primary-soft/40" : "border-border hover:bg-surface-2")
                  }
                >
                  <input type="checkbox" checked={on} onChange={() => toggle(item.id)} className="mt-0.5 accent-primary" />
                  <span className="min-w-0 flex-1">
                    <span className="block truncate font-semibold text-ink">{item.prompt}</span>
                    <span className="block text-[11px] text-ink-subtle">
                      {item.section} · {item.choices.length} pilihan
                      {on && ` · urutan ${order}`}
                    </span>
                  </span>
                </label>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Build the gating test editor**

Create `frontend/components/admin/GatingTestEditor.tsx`:

```tsx
"use client";

import { useEffect, useState } from "react";
import { Button, Modal, Spinner } from "@/components/ui";
import { QuestionPicker } from "@/components/admin/QuestionPicker";
import {
  getAssessment, createAssessment, updateAssessment,
  setAssessmentQuestions, attachAssessment, num,
} from "@/lib/sessions";

/** Create, edit, attach or detach the short test that gates a video session. */
export function GatingTestEditor({
  token, sessionId, assessmentId, onClose,
}: { token: string; sessionId: string; assessmentId: string | null; onClose: () => void }) {
  const [loading, setLoading] = useState(!!assessmentId);
  const [title, setTitle] = useState("Tes sesi");
  const [passThreshold, setPassThreshold] = useState(1);
  const [retakeCap, setRetakeCap] = useState("");        // blank = unlimited
  const [selected, setSelected] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!assessmentId) return;
    let cancelled = false;
    getAssessment(token, assessmentId)
      .then((a) => {
        if (cancelled) return;
        setTitle(a.title);
        setPassThreshold(num(a.config.passThreshold ?? 1));
        setRetakeCap(a.config.retakeCap == null ? "" : String(num(a.config.retakeCap)));
        setSelected(a.questions.map((q) => q.id));
      })
      .catch((e) => !cancelled && setError(e instanceof Error ? e.message : "Gagal memuat tes."))
      .finally(() => !cancelled && setLoading(false));
    return () => { cancelled = true; };
  }, [token, assessmentId]);

  const valid = title.trim() && selected.length > 0 && passThreshold >= 1 && passThreshold <= selected.length;

  async function save() {
    if (!valid) return;
    setBusy(true); setError(null);
    try {
      const config = {
        passThreshold,
        retakeCap: retakeCap.trim() === "" ? null : Number(retakeCap),
        proctoringEnabled: false,
        audioPlayLimit: null,
        sections: [],
        timeLimitMinutes: null,
      };
      let id = assessmentId;
      if (id) {
        await updateAssessment(token, id, { kind: "Gating", title: title.trim(), config });
      } else {
        id = (await createAssessment(token, { kind: "Gating", title: title.trim(), config })).id;
      }
      await setAssessmentQuestions(token, id, selected);
      if (!assessmentId) await attachAssessment(token, sessionId, id);
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal menyimpan tes.");
      setBusy(false);
    }
  }

  async function detach() {
    if (!confirm("Lepas tes dari sesi ini?\n\nTes dan percobaan peserta tidak dihapus.")) return;
    setBusy(true);
    try {
      await attachAssessment(token, sessionId, null);
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal melepas tes.");
      setBusy(false);
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Tes sesi"
      className="max-w-2xl"
      footer={
        <div className="flex w-full items-center justify-between gap-2">
          {assessmentId
            ? <Button variant="neutral" size="sm" onClick={detach} loading={busy}>Lepas dari sesi</Button>
            : <span />}
          <div className="flex gap-2">
            <Button variant="neutral" size="sm" onClick={onClose}>Batal</Button>
            <Button size="sm" onClick={save} loading={busy} disabled={!valid}>Simpan</Button>
          </div>
        </div>
      }
    >
      {loading ? (
        <div className="flex min-h-[200px] items-center justify-center"><Spinner size={22} /></div>
      ) : (
        <div className="flex flex-col gap-3">
          {error && <div className="rounded-base bg-danger-soft px-3 py-2 text-[13px] font-semibold text-danger">{error}</div>}

          <label className="flex flex-col gap-1">
            <span className="text-[12px] font-bold text-ink-muted">Judul</span>
            <input value={title} onChange={(e) => setTitle(e.target.value)} className={inputCls} />
          </label>

          <div className="flex flex-wrap gap-4">
            <label className="flex flex-col gap-1">
              <span className="text-[12px] font-bold text-ink-muted">Skor lulus</span>
              <input
                type="number" min={1} max={Math.max(1, selected.length)} value={passThreshold}
                onChange={(e) => setPassThreshold(Number(e.target.value) || 1)}
                className={inputCls + " w-24"}
              />
            </label>
            <label className="flex flex-col gap-1">
              <span className="text-[12px] font-bold text-ink-muted">Batas percobaan</span>
              <input
                type="number" min={1} value={retakeCap} placeholder="tanpa batas"
                onChange={(e) => setRetakeCap(e.target.value)}
                className={inputCls + " w-36"}
              />
            </label>
          </div>

          {selected.length > 0 && passThreshold > selected.length && (
            <p className="text-[12.5px] font-semibold text-danger">
              Skor lulus tidak boleh melebihi jumlah soal ({selected.length}).
            </p>
          )}

          <div>
            <span className="text-[12px] font-bold text-ink-muted">Soal</span>
            <div className="mt-1">
              <QuestionPicker token={token} selected={selected} onChange={setSelected} />
            </div>
          </div>
        </div>
      )}
    </Modal>
  );
}

const inputCls =
  "rounded-sm border border-border bg-surface px-3 py-2 text-[13.5px] outline-none focus:border-primary";
```

- [ ] **Step 3: Wire it into the session row**

In `components/admin/SessionManager.tsx`, add the import and a state hook:

```tsx
import { GatingTestEditor } from "@/components/admin/GatingTestEditor";
```

```tsx
  const [testFor, setTestFor] = useState<{ sessionId: string; assessmentId: string | null } | null>(null);
```

In the session row's button group, directly before the up/down arrows, add:

```tsx
                {s.type === "Video" && (
                  <button
                    type="button"
                    onClick={() => setTestFor({ sessionId: s.id, assessmentId: s.assessmentId ?? null })}
                    className="rounded px-2 py-1 text-[11.5px] font-bold text-primary hover:bg-primary-soft"
                  >
                    {s.assessmentId ? "Tes ✓" : "Tes"}
                  </button>
                )}
```

And beside the existing `AttendanceModal` render:

```tsx
      {testFor && (
        <GatingTestEditor
          token={token}
          sessionId={testFor.sessionId}
          assessmentId={testFor.assessmentId}
          onClose={async () => { setTestFor(null); await qc.invalidateQueries({ queryKey: key }); }}
        />
      )}
```

- [ ] **Step 4: Verify**

Run: `cd frontend && npx tsc --noEmit && NEXT_TELEMETRY_DISABLED=1 NEXT_PUBLIC_API_BASE_URL="" API_INTERNAL_URL="http://api:8080" npm run build`

Then rebuild the container and check in a browser at `/admin/programs`:
- A Video session shows a "Tes" button; a Live or FinalAssessment session does not.
- Creating a test with 2 questions and pass score 1 saves, and the button becomes "Tes ✓".
- Reopening shows the saved title, threshold and selected questions.
- Setting pass score above the question count disables Save and shows the warning.
- "Lepas dari sesi" returns the button to "Tes".

- [ ] **Step 5: Commit**

```bash
git add frontend/components/admin/
git commit -m "feat: author gating tests from the session row"
```

---

## Task 5: Final assessment composer

**Files:**
- Create: `frontend/components/admin/SectionComposer.tsx`
- Create: `frontend/app/admin/assessments/[id]/page.tsx`
- Modify: `frontend/components/admin/SessionManager.tsx`

**Interfaces:**
- Consumes: `QuestionPicker` (Task 4); `getAssessment`, `setAssessmentQuestions`, `num` from `@/lib/sessions`.
- Produces: `SectionComposer({ token, section, required, selected, onChange }: { token: string; section: string; required: number; selected: string[]; onChange: (ids: string[]) => void })`; route `/admin/assessments/[id]`.

- [ ] **Step 1: Build the section panel**

Create `frontend/components/admin/SectionComposer.tsx`:

```tsx
"use client";

import { QuestionPicker } from "@/components/admin/QuestionPicker";

/** One section of the final assessment: how many questions it needs, and which are chosen. */
export function SectionComposer({
  token, section, required, selected, onChange,
}: {
  token: string;
  section: string;
  required: number;
  selected: string[];
  onChange: (ids: string[]) => void;
}) {
  const complete = selected.length === required;

  return (
    <section className="rounded-lg border border-border bg-surface p-5 shadow-sm">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-base font-extrabold">{section}</h2>
        <span
          className={
            "rounded-full px-2.5 py-1 text-[12px] font-bold " +
            (complete ? "bg-success-soft text-success" : "bg-warning-soft text-warning")
          }
        >
          {selected.length} / {required} soal
        </span>
      </div>
      <QuestionPicker token={token} section={section} selected={selected} onChange={onChange} />
    </section>
  );
}
```

- [ ] **Step 2: Build the composer page**

Create `frontend/app/admin/assessments/[id]/page.tsx`:

```tsx
"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Button, Spinner, ErrorState, ChevronRightIcon } from "@/components/ui";
import { SectionComposer } from "@/components/admin/SectionComposer";
import { getAssessment, setAssessmentQuestions, num } from "@/lib/sessions";

export default function AssessmentComposerPage() {
  const token = useAuth().accessToken;
  const id = useParams<{ id: string }>().id;

  const q = useQuery({
    queryKey: ["admin-assessment", id],
    queryFn: () => getAssessment(token!, id),
    enabled: !!token,
    retry: false,
  });

  // sectionName -> selected question ids, seeded from what is already composed.
  const [bySection, setBySection] = useState<Record<string, string[]>>({});
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!q.data) return;
    const seeded: Record<string, string[]> = {};
    for (const s of q.data.config.sections ?? []) seeded[String(s.section)] = [];
    for (const question of q.data.questions) {
      (seeded[question.section] ??= []).push(question.id);
    }
    setBySection(seeded);
  }, [q.data]);

  if (!token || q.isPending) {
    return <div className="flex min-h-[300px] items-center justify-center"><Spinner size={24} /></div>;
  }
  if (q.isError) {
    return <ErrorState title="Tes tidak ditemukan" message="Periksa kembali tautannya." />;
  }

  const sections = q.data.config.sections ?? [];
  const totalSelected = Object.values(bySection).reduce((n, ids) => n + ids.length, 0);
  const totalRequired = sections.reduce((n, s) => n + num(s.questions), 0);

  async function save() {
    // Replacing the set on a test people have already sat is destructive enough to confirm.
    if (num(q.data!.attemptCount) > 0 &&
        !confirm("Tes ini sudah dikerjakan peserta. Ganti susunan soal?")) return;

    setBusy(true); setError(null); setSaved(false);
    try {
      // The API replaces the whole set, so flatten in section order — the same order the
      // runtime uses when it filters questions per section.
      const ordered = sections.flatMap((s) => bySection[String(s.section)] ?? []);
      await setAssessmentQuestions(token!, id, ordered);
      await q.refetch();
      setSaved(true);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal menyimpan susunan soal.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex flex-col gap-1">
          <Link href="/admin/programs" className="inline-flex items-center gap-1.5 text-[12.5px] font-bold text-ink-muted hover:text-ink">
            <ChevronRightIcon size={15} className="rotate-180" /> Program
          </Link>
          <h1 className="text-xl font-extrabold tracking-tight">{q.data.title}</h1>
          <p className="text-[12.5px] text-ink-subtle">
            {totalSelected} dari {totalRequired} soal tersusun
            {num(q.data.attemptCount) > 0 && ` · ${num(q.data.attemptCount)} percobaan tercatat`}
          </p>
        </div>
        <div className="flex items-center gap-3">
          {saved && <span className="text-[13px] font-bold text-success">Tersimpan ✓</span>}
          <Button onClick={save} loading={busy}>Simpan susunan</Button>
        </div>
      </div>

      {num(q.data.attemptCount) > 0 && (
        <div className="rounded-base border border-[#F5D9A8] bg-warning-soft px-4 py-3 text-[13px] text-ink">
          Tes ini sudah dikerjakan peserta. Mengubah susunan soal tidak mengubah hasil yang sudah
          tercatat, tetapi akan berlaku untuk percobaan berikutnya.
        </div>
      )}

      {error && <div className="rounded-base bg-danger-soft px-4 py-3 text-sm font-semibold text-danger">{error}</div>}

      {sections.length === 0 ? (
        <p className="rounded-lg border border-border bg-surface px-5 py-8 text-center text-sm text-ink-muted">
          Tes ini belum memiliki konfigurasi bagian.
        </p>
      ) : (
        sections.map((s) => {
          const name = String(s.section);
          return (
            <SectionComposer
              key={name}
              token={token}
              section={name}
              required={num(s.questions)}
              selected={bySection[name] ?? []}
              onChange={(ids) => { setSaved(false); setBySection((b) => ({ ...b, [name]: ids })); }}
            />
          );
        })
      )}
    </div>
  );
}
```

- [ ] **Step 3: Link to it from the final-assessment session row**

In `components/admin/SessionManager.tsx`, add `import Link from "next/link";` and, in the row button group beside the "Tes" button from Task 4:

```tsx
                {s.type === "FinalAssessment" && s.assessmentId && (
                  <Link
                    href={`/admin/assessments/${s.assessmentId}`}
                    className="rounded px-2 py-1 text-[11.5px] font-bold text-primary hover:bg-primary-soft"
                  >
                    Susun soal
                  </Link>
                )}
```

- [ ] **Step 4: Verify**

Run: `cd frontend && npx tsc --noEmit && NEXT_TELEMETRY_DISABLED=1 NEXT_PUBLIC_API_BASE_URL="" API_INTERNAL_URL="http://api:8080" npm run build`

In the browser, on a program whose final-assessment session has an assessment attached:
- "Susun soal" opens `/admin/assessments/<id>`.
- Three section panels appear with the counts from the config (e.g. `0 / 50`).
- Selecting questions updates the counter; the badge turns green at exactly the required count.
- Save, reload the page, and the selections persist in the same order.
- The Listening panel only offers Listening questions.

- [ ] **Step 5: Commit**

```bash
git add frontend/components/admin/SectionComposer.tsx \
        frontend/app/admin/assessments/ frontend/components/admin/SessionManager.tsx
git commit -m "feat: compose the final assessment section by section"
```

---

## Task 6: Score band editor

**Files:**
- Create: `frontend/components/admin/ScoreBandPaste.tsx`
- Create: `frontend/app/admin/programs/[id]/score-bands/page.tsx`
- Modify: `frontend/components/admin/SessionManager.tsx`

**Interfaces:**
- Consumes: `listScoreBands`, `replaceScoreBands`, `num`, `type ScoreBand` from `@/lib/sessions`.
- Produces: `parseBands(text: string): { rows: ParsedBand[]; errors: string[] }`; `coverage(rows: ParsedBand[]): CoverageRow[]`; `ScoreBandPaste({ onParsed }: { onParsed: (rows: ParsedBand[]) => void })`; route `/admin/programs/[id]/score-bands`.

- [ ] **Step 1: Write the parser and coverage check**

Create `frontend/components/admin/ScoreBandPaste.tsx`:

```tsx
"use client";

import { useState } from "react";
import { Button } from "@/components/ui";

export type ParsedBand = {
  section: string;
  minRaw: number;
  maxRaw: number;
  scaledScore: number;
  predictedBand: string | null;
};

const SECTIONS = ["Listening", "Structure", "Reading", "Vocabulary", "General"];

/** ITP limits: max raw score, and the highest legal scaled score. Reading tops out one lower. */
const LIMITS: Record<string, [number, number]> = {
  Listening: [50, 68],
  Structure: [40, 68],
  Reading: [50, 67],
};
const SCALED_MIN = 31;

/** Parses `section, minRaw, maxRaw, scaled[, band]` rows. Tab- or comma-separated. */
export function parseBands(text: string): { rows: ParsedBand[]; errors: string[] } {
  const rows: ParsedBand[] = [];
  const errors: string[] = [];

  text.split(/\r?\n/).forEach((line, i) => {
    const trimmed = line.trim();
    if (!trimmed) return;

    const cells = trimmed.split(/\t|,/).map((c) => c.trim());
    if (i === 0 && /^section$/i.test(cells[0])) return;          // spreadsheet header row

    const lineNo = i + 1;
    if (cells.length < 4) {
      errors.push(`Baris ${lineNo}: butuh 4 kolom (bagian, min, max, skala).`);
      return;
    }

    const [rawSection, min, max, scaled, band] = cells;
    const section = SECTIONS.find((s) => s.toLowerCase() === rawSection.toLowerCase());
    if (!section) {
      errors.push(`Baris ${lineNo}: bagian "${rawSection}" tidak dikenal.`);
      return;
    }

    const numbers = [min, max, scaled].map(Number);
    if (numbers.some((n) => !Number.isInteger(n))) {
      errors.push(`Baris ${lineNo}: min, max dan skala harus bilangan bulat.`);
      return;
    }

    const [minRaw, maxRaw, scaledScore] = numbers;
    if (minRaw > maxRaw) {
      errors.push(`Baris ${lineNo}: min (${minRaw}) lebih besar dari max (${maxRaw}).`);
      return;
    }

    rows.push({ section, minRaw, maxRaw, scaledScore, predictedBand: band?.trim() || null });
  });

  return { rows, errors };
}

export type CoverageRow = {
  section: string;
  maxRaw: number;
  missing: number[];
  duplicated: number[];
  outOfRange: number;
  complete: boolean;
};

/** Every raw score 0..max must be covered exactly once, with a scaled value in range. */
export function coverage(rows: ParsedBand[]): CoverageRow[] {
  return Object.entries(LIMITS).map(([section, [maxRaw, scaledMax]]) => {
    const mine = rows.filter((r) => r.section === section);
    const missing: number[] = [];
    const duplicated: number[] = [];

    for (let raw = 0; raw <= maxRaw; raw++) {
      const hits = mine.filter((r) => raw >= r.minRaw && raw <= r.maxRaw).length;
      if (hits === 0) missing.push(raw);
      else if (hits > 1) duplicated.push(raw);
    }

    const outOfRange = mine.filter((r) => r.scaledScore < SCALED_MIN || r.scaledScore > scaledMax).length;
    return {
      section, maxRaw, missing, duplicated, outOfRange,
      complete: missing.length === 0 && duplicated.length === 0 && outOfRange === 0,
    };
  });
}

function list(values: number[]): string {
  return values.length <= 6 ? values.join(", ") : `${values[0]}–${values[values.length - 1]} (${values.length} nilai)`;
}

/** Paste box plus per-section coverage summary. Nothing leaves here until the input parses. */
export function ScoreBandPaste({ onParsed }: { onParsed: (rows: ParsedBand[]) => void }) {
  const [text, setText] = useState("");
  const parsed = text.trim() ? parseBands(text) : null;
  const cover = parsed && parsed.errors.length === 0 ? coverage(parsed.rows) : null;

  return (
    <div className="flex flex-col gap-3">
      <label className="flex flex-col gap-1">
        <span className="text-[12px] font-bold text-ink-muted">
          Tempel tabel dari spreadsheet — kolom: bagian, min, max, skala, label (opsional)
        </span>
        <textarea
          value={text}
          onChange={(e) => setText(e.target.value)}
          rows={8}
          placeholder={"Listening\t0\t0\t31\nListening\t1\t1\t32"}
          className="w-full rounded-sm border border-border bg-surface px-3 py-2 font-mono text-[12.5px] outline-none focus:border-primary"
        />
      </label>

      {parsed && parsed.errors.length > 0 && (
        <div className="rounded-base bg-danger-soft px-4 py-3 text-[13px] text-danger">
          <p className="font-bold">{parsed.errors.length} baris bermasalah — tidak ada yang disimpan:</p>
          <ul className="mt-1 list-inside list-disc">
            {parsed.errors.slice(0, 8).map((e) => <li key={e}>{e}</li>)}
          </ul>
          {parsed.errors.length > 8 && <p className="mt-1">…dan {parsed.errors.length - 8} lainnya.</p>}
        </div>
      )}

      {cover && (
        <div className="rounded-base border border-border bg-surface-2 px-4 py-3 text-[12.5px]">
          <p className="font-bold text-ink">{parsed!.rows.length} baris terbaca</p>
          <ul className="mt-1.5 flex flex-col gap-1">
            {cover.map((c) => (
              <li key={c.section} className={c.complete ? "text-success" : "text-warning"}>
                <strong>{c.section}</strong>{" "}
                {c.complete
                  ? `lengkap (0–${c.maxRaw})`
                  : [
                      c.missing.length ? `belum dipetakan: ${list(c.missing)}` : null,
                      c.duplicated.length ? `tumpang tindih: ${list(c.duplicated)}` : null,
                      c.outOfRange ? `${c.outOfRange} skala di luar rentang` : null,
                    ].filter(Boolean).join(" · ")}
              </li>
            ))}
          </ul>
        </div>
      )}

      <Button
        size="sm"
        className="self-start"
        disabled={!parsed || parsed.errors.length > 0 || parsed.rows.length === 0}
        onClick={() => parsed && onParsed(parsed.rows)}
      >
        Gunakan tabel ini
      </Button>
    </div>
  );
}
```

- [ ] **Step 2: Build the page**

Create `frontend/app/admin/programs/[id]/score-bands/page.tsx`:

```tsx
"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Button, Spinner, ErrorState, ChevronRightIcon } from "@/components/ui";
import { ScoreBandPaste, type ParsedBand } from "@/components/admin/ScoreBandPaste";
import { listScoreBands, replaceScoreBands, num } from "@/lib/sessions";

export default function ScoreBandsPage() {
  const token = useAuth().accessToken;
  const programId = useParams<{ id: string }>().id;
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  const q = useQuery({
    queryKey: ["score-bands", programId],
    queryFn: () => listScoreBands(token!, programId),
    enabled: !!token,
  });

  async function apply(rows: ParsedBand[]) {
    const existing = q.data?.length ?? 0;
    if (!confirm(
      `Ganti tabel konversi dengan ${rows.length} baris?` +
      (existing ? `\n\n${existing} baris yang ada akan ditimpa.` : ""))) return;

    setBusy(true); setError(null); setSaved(false);
    try {
      await replaceScoreBands(token!, programId, rows.map((r) => ({
        id: "00000000-0000-0000-0000-000000000000",
        section: r.section,
        minRaw: r.minRaw,
        maxRaw: r.maxRaw,
        scaledScore: r.scaledScore,
        predictedBand: r.predictedBand,
      })));
      await q.refetch();
      setSaved(true);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal menyimpan tabel.");
    } finally {
      setBusy(false);
    }
  }

  /** Round-trip: export what is stored so it can be corrected in a spreadsheet and pasted back. */
  function download() {
    const tsv = (q.data ?? [])
      .map((b) => [b.section, num(b.minRaw), num(b.maxRaw), num(b.scaledScore), b.predictedBand ?? ""].join("\t"))
      .join("\n");
    const url = URL.createObjectURL(new Blob([tsv], { type: "text/tab-separated-values" }));
    const a = document.createElement("a");
    a.href = url;
    a.download = `score-bands-${programId}.tsv`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  }

  if (!token || q.isPending) {
    return <div className="flex min-h-[300px] items-center justify-center"><Spinner size={24} /></div>;
  }
  if (q.isError) {
    return <ErrorState title="Gagal memuat tabel konversi" />;
  }

  return (
    <div className="flex flex-col gap-4">
      <div>
        <Link href="/admin/programs" className="inline-flex items-center gap-1.5 text-[12.5px] font-bold text-ink-muted hover:text-ink">
          <ChevronRightIcon size={15} className="rotate-180" /> Program
        </Link>
        <h1 className="mt-1 text-xl font-extrabold tracking-tight">Tabel konversi skor</h1>
        <p className="mt-1 text-[13px] text-ink-muted">
          Setiap skor mentah harus dipetakan tepat satu kali. Tanpa tabel yang lengkap, sertifikat
          tidak dapat diterbitkan dan program tidak dapat diterbitkan.
        </p>
      </div>

      {error && <div className="rounded-base bg-danger-soft px-4 py-3 text-sm font-semibold text-danger">{error}</div>}
      {saved && <div className="rounded-base bg-success-soft px-4 py-3 text-sm font-semibold text-success">Tabel tersimpan ✓</div>}

      <div className="rounded-lg border border-border bg-surface p-5 shadow-sm">
        <ScoreBandPaste onParsed={apply} />
        {busy && <p className="mt-2 text-[12.5px] text-ink-muted">Menyimpan…</p>}
      </div>

      <div className="rounded-lg border border-border bg-surface p-5 shadow-sm">
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-base font-extrabold">Tersimpan saat ini</h2>
          <div className="flex items-center gap-3">
            <span className="text-[12.5px] text-ink-muted">{q.data.length} baris</span>
            <Button variant="neutral" size="sm" onClick={download} disabled={q.data.length === 0}>
              Unduh (.tsv)
            </Button>
          </div>
        </div>

        {q.data.length === 0 ? (
          <p className="py-6 text-center text-sm text-ink-muted">Belum ada tabel konversi.</p>
        ) : (
          <div className="max-h-[320px] overflow-y-auto">
            <table className="w-full border-collapse text-[13px]">
              <thead className="sticky top-0 bg-surface-2">
                <tr className="text-left text-xs font-bold text-ink-muted">
                  <th className="px-3 py-2">Bagian</th>
                  <th className="px-3 py-2">Min</th>
                  <th className="px-3 py-2">Max</th>
                  <th className="px-3 py-2">Skala</th>
                  <th className="px-3 py-2">Label</th>
                </tr>
              </thead>
              <tbody>
                {q.data.map((b) => (
                  <tr key={b.id} className="border-t border-border">
                    <td className="px-3 py-1.5">{b.section}</td>
                    <td className="px-3 py-1.5">{num(b.minRaw)}</td>
                    <td className="px-3 py-1.5">{num(b.maxRaw)}</td>
                    <td className="px-3 py-1.5 font-semibold">{num(b.scaledScore)}</td>
                    <td className="px-3 py-1.5 text-ink-muted">{b.predictedBand ?? "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
```

**On row-level correction.** Spec §3 asks for it. The correction path here is the round-trip —
**Unduh (.tsv) → fix in the spreadsheet → paste back** — rather than 143 inline-editable table rows.
For a non-technical operator holding the authoritative table in Excel, correcting it there and
re-pasting is both fewer steps and less error-prone than editing rows in a browser. If inline editing
is still wanted after real use, it is a small addition on top of the stored-table view.

- [ ] **Step 3: Link to it**

In `components/admin/SessionManager.tsx`, add to the modal footer (beside "+ Tambah sesi"):

```tsx
          <Link
            href={`/admin/programs/${program.id}/score-bands`}
            className="text-[12.5px] font-bold text-primary hover:underline"
          >
            Tabel konversi skor →
          </Link>
```

- [ ] **Step 4: Verify — including the malformed inputs**

Run: `cd frontend && npx tsc --noEmit && NEXT_TELEMETRY_DISABLED=1 NEXT_PUBLIC_API_BASE_URL="" API_INTERNAL_URL="http://api:8080" npm run build`

Then in the browser at `/admin/programs/<id>/score-bands`, paste each of these and confirm the stated result. This is the runnable check for the parser:

| Paste | Expected |
|---|---|
| `Listening\t0\t0\t31` | 1 row read; Listening incomplete, missing 1–50 |
| `Reading,0,50,31` | 1 row read; Reading complete (one row spans everything) |
| `Bogus\t0\t0\t31` | error: bagian "Bogus" tidak dikenal |
| `Listening\t0\t0` | error: butuh 4 kolom |
| `Listening\t5\t2\t40` | error: min (5) lebih besar dari max (2) |
| `Listening\tx\t0\t31` | error: harus bilangan bulat |
| `Listening\t0\t0\t99` | 1 row read; Listening shows "1 skala di luar rentang" |
| `Listening\t0\t5\t31` and `Listening\t3\t8\t32` | Listening shows "tumpang tindih: 3, 4, 5" |
| A header row `section,min,max,scaled` followed by valid rows | header skipped, rows read |

Then paste a full valid table, save, and confirm: the confirmation names the row counts, the stored table lists them, and "Unduh (.tsv)" round-trips a file that pastes back cleanly.

- [ ] **Step 5: Commit**

```bash
git add frontend/components/admin/ScoreBandPaste.tsx \
        frontend/app/admin/programs/ frontend/components/admin/SessionManager.tsx
git commit -m "feat: score band editor with paste, validation and preview"
```

---

## Task 7: Readiness panel and spec correction

**Files:**
- Modify: `frontend/lib/programs.ts`
- Create: `frontend/components/admin/ReadinessPanel.tsx`
- Modify: `frontend/app/admin/programs/page.tsx`
- Modify: `docs/KAK_INVERTA_TOEFL_v1.0.md`

**Interfaces:**
- Consumes: `GET /api/admin/programs/{id}/readiness` (Task 1).
- Produces: `getProgramReadiness(t: string, programId: string): Promise<ProgramReadiness>`; `ReadinessPanel({ token, program, onClose }: { token: string; program: AdminProgram; onClose: () => void })`.

- [ ] **Step 1: Regenerate the API client**

The readiness DTOs are new, so the generated schema must be refreshed. From `backend/`:

```bash
(ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:8087 \
 ConnectionStrings__Default="Host=localhost;Port=5999;Database=none;Username=x;Password=x" \
 RunMigrations=false SeedSampleData=false dotnet src/Api/bin/Debug/net10.0/Academy.Api.dll >/dev/null 2>&1 &)
curl -s --retry 25 --retry-delay 1 --retry-all-errors -o /dev/null http://localhost:8087/openapi/v1.json
cd ../frontend && npx openapi-typescript http://localhost:8087/openapi/v1.json -o api-client/schema.ts
pkill -f "Academy.Api.dll"
```

Confirm: `grep -c "ProgramReadinessDto" api-client/schema.ts` returns at least 1.

- [ ] **Step 2: Add the client function**

Append to `frontend/lib/programs.ts`:

```ts
export type ProgramReadiness = components["schemas"]["ProgramReadinessDto"];
export type ReadinessCheck = components["schemas"]["ReadinessCheckDto"];

export const getProgramReadiness = (t: string, programId: string) =>
  api<ProgramReadiness>("GET", `/api/admin/programs/${programId}/readiness`, t);
```

- [ ] **Step 3: Build the panel**

Create `frontend/components/admin/ReadinessPanel.tsx`:

```tsx
"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { Button, Modal, Spinner, CheckIcon, AlertTriangleIcon } from "@/components/ui";
import { getProgramReadiness, type AdminProgram } from "@/lib/programs";

/** Where to go to fix each failing check. */
const FIX: Record<string, (p: AdminProgram) => { href: string; label: string } | null> = {
  score_bands_complete: (p) => ({ href: `/admin/programs/${p.id}/score-bands`, label: "Buka tabel konversi" }),
  gating_tests_populated: () => null,
  final_sections_populated: () => null,
  has_sessions: () => null,
  final_assessment_present: () => null,
  price_set: () => null,
};

export function ReadinessPanel({
  token, program, onClose,
}: { token: string; program: AdminProgram; onClose: () => void }) {
  const q = useQuery({
    queryKey: ["readiness", program.id],
    queryFn: () => getProgramReadiness(token, program.id),
  });

  return (
    <Modal
      open
      onClose={onClose}
      title={`Kesiapan — ${program.name}`}
      className="max-w-lg"
      footer={<div className="flex w-full justify-end"><Button size="sm" onClick={onClose}>Tutup</Button></div>}
    >
      {q.isPending ? (
        <div className="flex min-h-[160px] items-center justify-center"><Spinner size={22} /></div>
      ) : q.isError ? (
        <p className="py-6 text-center text-sm text-ink-muted">Gagal memuat status kesiapan.</p>
      ) : (
        <div className="flex flex-col gap-3">
          <div
            className={
              "rounded-base px-4 py-3 text-[13px] font-semibold " +
              (q.data.ready ? "bg-success-soft text-success" : "bg-warning-soft text-warning")
            }
          >
            {q.data.ready
              ? "Program siap diterbitkan."
              : "Program belum bisa diterbitkan. Lengkapi item bertanda di bawah."}
          </div>

          <ul className="flex flex-col gap-2">
            {q.data.checks.map((c) => {
              const fix = !c.passed ? FIX[c.key]?.(program) : null;
              return (
                <li key={c.key} className="flex items-start gap-2.5 rounded-base border border-border px-3.5 py-2.5">
                  <span className={"mt-0.5 shrink-0 " + (c.passed ? "text-success" : c.blocking ? "text-danger" : "text-ink-subtle")}>
                    {c.passed ? <CheckIcon size={16} strokeWidth={3} /> : <AlertTriangleIcon size={16} />}
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block text-[13.5px] font-semibold text-ink">
                      {c.title}
                      {!c.blocking && <span className="ml-1.5 text-[11px] font-normal text-ink-subtle">(tidak wajib)</span>}
                    </span>
                    {c.detail && <span className="mt-0.5 block text-[12.5px] leading-snug text-ink-muted">{c.detail}</span>}
                    {fix && (
                      <Link href={fix.href} className="mt-1 inline-block text-[12.5px] font-bold text-primary hover:underline">
                        {fix.label} →
                      </Link>
                    )}
                  </span>
                </li>
              );
            })}
          </ul>
        </div>
      )}
    </Modal>
  );
}
```

- [ ] **Step 4: Add a readiness column to the program list**

In `app/admin/programs/page.tsx`: import `ReadinessPanel` and `getProgramReadiness`, add `const [readinessFor, setReadinessFor] = useState<AdminProgram | null>(null);`, add a `<th className="px-3 py-3">Kesiapan</th>` header before "Aksi", and in each row:

```tsx
                    <td className="px-3 py-3.5">
                      <button
                        type="button"
                        onClick={() => setReadinessFor(p)}
                        className="text-[12.5px] font-bold text-primary hover:underline"
                      >
                        Periksa
                      </button>
                    </td>
```

Render beside the other modals:

```tsx
      {readinessFor && token && (
        <ReadinessPanel token={token} program={readinessFor} onClose={() => setReadinessFor(null)} />
      )}
```

- [ ] **Step 5: Correct the KAK**

`docs/KAK_INVERTA_TOEFL_v1.0.md` §9.12 lists Assessments and Score conversion as delivered; until this plan lands they were not. Add this line immediately below the §9.12 capability table:

```markdown
> **Delivered 2026-08-20 (A3).** Assessment authoring and score-conversion editing originally
> shipped as API-only — the admin UI for them was added by the A3 project, together with a
> publish-blocking readiness check. See
> `docs/superpowers/specs/2026-08-20-admin-content-authoring-design.md`.
```

- [ ] **Step 6: Verify end to end**

Run backend and frontend checks:

```bash
cd backend && dotnet test Academy.slnx
cd ../frontend && npx tsc --noEmit && NEXT_TELEMETRY_DISABLED=1 NEXT_PUBLIC_API_BASE_URL="" API_INTERNAL_URL="http://api:8080" npm run build
```

Rebuild both containers, then walk the whole path in a browser — no `curl`, because a browser-only gap is exactly what this project exists to close:

1. Create a program (it is forced to draft).
2. Add a video session, author a gating test on it with 2 questions.
3. Add a final-assessment session with an assessment, compose its sections to full.
4. Paste a complete score→band table.
5. "Periksa" shows every blocking check green.
6. Publish succeeds.
7. Remove one score-band row via a re-paste with a gap; publishing is refused and the message names the gap.

- [ ] **Step 7: Commit**

```bash
git add frontend/lib/programs.ts frontend/api-client/schema.ts \
        frontend/components/admin/ReadinessPanel.tsx \
        frontend/app/admin/programs/page.tsx docs/KAK_INVERTA_TOEFL_v1.0.md
git commit -m "feat: readiness panel and KAK correction for admin authoring"
```

---

## Definition of done

- `dotnet build backend/Academy.slnx` — 0 warnings, 0 errors.
- `dotnet test backend/Academy.slnx` — all pass (193 existing + 9 new).
- `npx tsc --noEmit` and `npm run build` clean.
- The Task 7 Step 6 walkthrough completes in a browser with no `curl`.
- `docs/KAK_INVERTA_TOEFL_v1.0.md` §9.12 no longer overstates the admin surface.

## Deliberately not built

Question-bank bulk import · randomised assembly · editing question content from the composer ·
frontend test harness. Add bulk import when it is clear how content actually arrives; the score-band
paste in Task 6 covers the one table that cannot reasonably be typed.
