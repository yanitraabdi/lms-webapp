using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Academy.Application.Assessments;
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
