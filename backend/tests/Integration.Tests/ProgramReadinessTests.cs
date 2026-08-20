using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Academy.Application.Assessments;
using Academy.Application.Auth;
using Academy.Application.Programs;
using Academy.Domain;
using Academy.Domain.Entities;
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
    public async Task Score_bands_with_an_overlap_are_incomplete()
    {
        var admin = await AdminToken();
        var program = await NewProgram(admin);
        await PutScoreBands(admin, program, FullBands());

        // The score-bands endpoint itself rejects overlapping ranges, so the only way to get an
        // overlapping row into the table is to write it directly via the DbContext.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ScoreBandMappings.Add(new ScoreBandMapping
            {
                Id = Guid.CreateVersion7(), ProgramId = program, Section = QuestionSection.Listening,
                MinRaw = 0, MaxRaw = 3, ScaledScore = ToeflScoring.ScaledMin, PredictedBand = null,
            });
            await db.SaveChangesAsync();
        }

        var r = await AuthedGet<ProgramReadinessDto>($"/api/admin/programs/{program}/readiness", admin);

        var check = Check(r, "score_bands_complete");
        Assert.False(check.Passed);
        Assert.Contains("Listening", check.Detail);
    }

    [Fact]
    public async Task A_final_assessment_that_is_not_the_ITP_format_blocks_readiness()
    {
        var admin = await AdminToken();
        var program = await NewProgram(admin);

        // The 5/4/5 typo: internally consistent (each section holds exactly what it declares), and
        // raw scores 0-5 still resolve against the 0-50 band table — so nothing else catches it and
        // every certificate would carry a bogus ~310 prediction.
        var final = await NewAssessment(admin, "Final", new
        {
            passThreshold = 0, retakeCap = 1, proctoringEnabled = true,
            sections = new[]
            {
                new { section = "Listening", questions = 5, minutes = 35 },
                new { section = "Structure", questions = 4, minutes = 25 },
                new { section = "Reading",   questions = 5, minutes = 55 },
            },
        });
        var ids = new List<Guid>();
        foreach (var (section, count) in new[] { ("Listening", 5), ("Structure", 4), ("Reading", 5) })
            for (var i = 0; i < count; i++) ids.Add(await NewQuestion(admin, section));
        await SetQuestions(admin, final, [.. ids]);
        await NewSession(admin, program, "FinalAssessment", 1, final);
        await PutScoreBands(admin, program, FullBands());

        var r = await AuthedGet<ProgramReadinessDto>($"/api/admin/programs/{program}/readiness", admin);

        // The populated check is happy — 5 questions for a declared 5 — which is exactly why the
        // format has to be checked against ToeflScoring instead.
        Assert.True(Check(r, "final_sections_populated").Passed);

        var format = Check(r, "final_sections_match_itp");
        Assert.False(format.Passed);
        Assert.True(format.Blocking);
        Assert.False(r.Ready);
        // The detail must let an operator find the problem: what was declared, what is required.
        Assert.Contains("Listening tertulis 5 soal, wajib 50", format.Detail);
        Assert.Contains("Structure tertulis 4 soal, wajib 40", format.Detail);
        Assert.Contains("Reading tertulis 5 soal, wajib 50", format.Detail);

        // …and publishing is refused, naming the same thing.
        var publish = await Publish(admin, program, published: true);
        Assert.Equal(HttpStatusCode.Conflict, publish.StatusCode);
        Assert.Contains("wajib 50", await publish.Content.ReadAsStringAsync());
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

    [Fact]
    public async Task Editing_an_already_published_program_is_not_re_gated()
    {
        var admin = await AdminToken();
        var program = await BuildReadyProgram(admin);
        (await Publish(admin, program, published: true)).EnsureSuccessStatusCode();

        // Make the program unready without ever un-publishing it — deleting the final-assessment
        // session (an un-gated endpoint) is the cheapest way to break `final_assessment_present`.
        var sessions = await AuthedGet<List<AdminSessionDto>>($"/api/admin/programs/{program}/sessions", admin);
        var finalSession = sessions.Single(s => s.Type == "FinalAssessment");
        (await Authed(HttpMethod.Delete, $"/api/admin/sessions/{finalSession.Id}", admin)).EnsureSuccessStatusCode();

        var readiness = await AuthedGet<ProgramReadinessDto>($"/api/admin/programs/{program}/readiness", admin);
        Assert.False(readiness.Ready);

        // The gate only fires on the draft->published TRANSITION. A program that is ALREADY
        // published must stay editable even once it has drifted unready — otherwise an admin
        // fixing a typo would be forced to un-publish first, taking the live page down.
        var current = (await AuthedGet<List<AdminProgramDto>>("/api/admin/programs", admin)).Single(p => p.Id == program);
        var res = await Authed(HttpMethod.Put, $"/api/admin/programs/{program}", admin, new
        {
            name = current.Name, slug = current.Slug, description = "Deskripsi diperbarui",
            summary = current.Summary, priceIdr = current.PriceIdr, published = true,
        });

        res.EnsureSuccessStatusCode();
        var after = (await AuthedGet<List<AdminProgramDto>>("/api/admin/programs", admin)).Single(p => p.Id == program);
        Assert.Equal("Published", after.Status);
        Assert.Equal("Deskripsi diperbarui", after.Description);
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

        // The ITP format is 50/40/50 and readiness checks it against Academy.Domain.ToeflScoring,
        // so a "ready" program needs a real 140-question final assessment.
        var final = await ItpFinal.SeedAsync(factory);
        await NewSession(admin, program, "FinalAssessment", 2, final.AssessmentId);

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

    private async Task<Guid> NewAssessment(string admin, string kind, object? config = null)
    {
        config ??= new { passThreshold = 1, retakeCap = (int?)null, proctoringEnabled = false, sections = Array.Empty<object>() };

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
