using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Academy.Application.Assessments;
using Academy.Application.Auth;
using Academy.Application.Billing;
using Academy.Application.Programs;
using Academy.Domain;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>Task 4 of session parts (2026-10-05): learner routes are per part, and a video session
/// completes only when every part is done.</summary>
public class SessionPartLearnerTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    private record Parts(Guid Lesson, Guid Test, Guid Discussion, Guid Assessment,
                         Dictionary<string, int> Correct, Dictionary<string, int> Wrong, Guid FirstQuestion);

    [Fact]
    public async Task Context_lists_parts_with_status_and_no_asset_id()
    {
        var c = await EnrolledLearner();
        await AddParts(c.Session1);

        var res = await Authed(HttpMethod.Get, $"/api/sessions/{c.Session1}", c.Token);
        res.EnsureSuccessStatusCode();
        var raw = await res.Content.ReadAsStringAsync();
        var ctx = JsonSerializer.Deserialize<SessionContextDto>(raw, Json)!;

        Assert.Equal(["Open", "Locked", "Locked"], ctx.Parts.Select(p => p.Status));
        Assert.DoesNotContain("\"sample\"", raw);
        Assert.DoesNotContain("providerAssetId", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Locked_part_routes_are_refused()
    {
        var c = await EnrolledLearner();
        var p = await AddParts(c.Session1);
        var part = $"/api/sessions/{c.Session1}/parts";

        Assert.Equal(HttpStatusCode.Forbidden,
            (await Authed(HttpMethod.Post, $"{part}/{p.Discussion}/playback", c.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Authed(HttpMethod.Get, $"{part}/{p.Discussion}/progress", c.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Authed(HttpMethod.Put, $"{part}/{p.Discussion}/progress", c.Token,
                new { positionSeconds = 10, percent = 50m })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Authed(HttpMethod.Get, $"{part}/{p.Test}/assessment", c.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Authed(HttpMethod.Get, $"{part}/{p.Test}/audio/{p.FirstQuestion}", c.Token)).StatusCode);
    }

    [Fact]
    public async Task Lesson_progress_is_saved_per_part()
    {
        var c = await EnrolledLearner();
        var p = await AddParts(c.Session1);

        var progress = await SaveProgress(c.Token, c.Session1, p.Lesson, 100m);

        Assert.Equal(p.Lesson, progress.PartId);
        Assert.True(progress.Done);
        Assert.False(progress.SessionCompleted);
        var ctx = await AuthedGet<SessionContextDto>($"/api/sessions/{c.Session1}", c.Token);
        Assert.Equal("Open", ctx.Parts.Single(x => x.Id == p.Test).Status);
    }

    [Fact]
    public async Task Discussion_opens_after_N_failures_and_session_completes_after_pass_and_discussion()
    {
        var c = await EnrolledLearner();
        var p = await AddParts(c.Session1);
        await SaveProgress(c.Token, c.Session1, p.Lesson, 100m);

        await Take(c.Token, p.Assessment, p.Wrong);
        await Take(c.Token, p.Assessment, p.Wrong);
        var ctx = await AuthedGet<SessionContextDto>($"/api/sessions/{c.Session1}", c.Token);
        Assert.Equal("Open", ctx.Parts.Single(x => x.Id == p.Discussion).Status);
        Assert.Equal(2, ctx.Parts.Single(x => x.Id == p.Test).FailedAttempts);

        var discussion = await SaveProgress(c.Token, c.Session1, p.Discussion, 100m);
        Assert.True(discussion.Done);
        Assert.False(discussion.SessionCompleted);   // the test is not passed yet
        // Two progress rows in one session must not break the program view.
        (await Authed(HttpMethod.Get, $"/api/me/programs/{c.ProgramId}", c.Token)).EnsureSuccessStatusCode();

        var result = await Take(c.Token, p.Assessment, p.Correct);
        Assert.True(result.Passed);
        Assert.True(result.SessionCompleted);
        Assert.True(await CanAccess(c.UserId, c.Session2));
    }

    [Fact]
    public async Task Starting_an_attempt_on_a_locked_test_part_is_refused()
    {
        var c = await EnrolledLearner();
        var p = await AddParts(c.Session1);

        var res = await Authed(HttpMethod.Post, $"/api/assessments/{p.Assessment}/attempts", c.Token);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Session_with_no_parts_never_completes()
    {
        // Parts are deleted AFTER enrolment: the readiness check blocks publishing an empty video session.
        var c = await EnrolledLearner();
        await WithDb(async db =>
        {
            db.SessionParts.RemoveRange(db.SessionParts.Where(x => x.SessionId == c.Session1));
            await db.SaveChangesAsync();
        });

        var ctx = await AuthedGet<SessionContextDto>($"/api/sessions/{c.Session1}", c.Token);
        Assert.Empty(ctx.Parts);
        Assert.False(ctx.Completed);

        using (var scope = factory.Services.CreateScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<ISessionCompletionService>()
                .TryCompleteAsync(c.UserId, c.Session1));
        Assert.False(await CanAccess(c.UserId, c.Session2));
    }

    [Fact]
    public async Task Answer_key_is_not_in_the_part_assessment()
    {
        var c = await EnrolledLearner();
        var p = await AddParts(c.Session1);
        await SaveProgress(c.Token, c.Session1, p.Lesson, 100m);

        var res = await Authed(HttpMethod.Get, $"/api/sessions/{c.Session1}/parts/{p.Test}/assessment", c.Token);
        res.EnsureSuccessStatusCode();

        Assert.DoesNotContain("correct", await res.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    // ---- facts ----

    /// <summary>Session's own lesson part ("sample") + Test (2 questions, pass 2, discussion after 2
    /// failures) + Discussion ("sample").</summary>
    private async Task<Parts> AddParts(Guid sessionId)
    {
        var lesson = await WithDbResult(db => db.SessionParts
            .Where(x => x.SessionId == sessionId).Select(x => x.Id).SingleAsync());
        var a = new Assessment
        {
            Id = Guid.CreateVersion7(), Kind = AssessmentKind.Gating, Title = "Tes bagian",
            Config = """{"passThreshold":2,"proctoringEnabled":false,"sections":[],"discussionAfterFailures":2}""",
        };
        var test = Guid.CreateVersion7();
        var discussion = Guid.CreateVersion7();
        var correct = new Dictionary<string, int>();
        var wrong = new Dictionary<string, int>();
        await WithDb(async db =>
        {
            db.Assessments.Add(a);
            for (var i = 0; i < 2; i++)
            {
                var q = new Question
                {
                    Id = Guid.CreateVersion7(), Section = QuestionSection.Reading, Prompt = $"Q{i} {Guid.NewGuid():N}",
                    Choices = """["a","b"]""", Correct = "[0]",
                };
                db.Questions.Add(q);
                db.AssessmentQuestions.Add(new AssessmentQuestion
                { Id = Guid.CreateVersion7(), AssessmentId = a.Id, QuestionId = q.Id, OrderIndex = i + 1 });
                correct[q.Id.ToString()] = 0;
                wrong[q.Id.ToString()] = 1;
            }
            db.SessionParts.Add(new SessionPart
            { Id = test, SessionId = sessionId, OrderIndex = 2, Kind = SessionPartKind.Test, Title = "Tes", AssessmentId = a.Id });
            db.SessionParts.Add(new SessionPart
            {
                Id = discussion, SessionId = sessionId, OrderIndex = 3, Kind = SessionPartKind.Discussion,
                Title = "Pembahasan", ProviderAssetId = "sample", DurationSeconds = 60,
            });
            await db.SaveChangesAsync();
        });
        return new Parts(lesson, test, discussion, a.Id, correct, wrong, Guid.Parse(correct.Keys.First()));
    }

    private async Task<PartProgressDto> SaveProgress(string token, Guid sessionId, Guid partId, decimal pct)
    {
        var res = await Authed(HttpMethod.Put, $"/api/sessions/{sessionId}/parts/{partId}/progress", token,
            new { positionSeconds = 60, percent = pct });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<PartProgressDto>(Json))!;
    }

    private async Task<AttemptResultDto> Take(string token, Guid assessmentId, Dictionary<string, int> answers)
    {
        var start = await Authed(HttpMethod.Post, $"/api/assessments/{assessmentId}/attempts", token);
        start.EnsureSuccessStatusCode();
        var attempt = (await start.Content.ReadFromJsonAsync<AttemptDto>(Json))!;
        var res = await Authed(HttpMethod.Post, $"/api/attempts/{attempt.Id}/submit", token, new { answers });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AttemptResultDto>(Json))!;
    }

    private async Task<bool> CanAccess(Guid userId, Guid sessionId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISessionAccessService>()
            .CanAccessAsync(userId, sessionId);
    }

    // ---- helpers (copied from SessionGatingTests) ----

    private record Ctx(string Token, Guid UserId, string Admin, Guid ProgramId,
                       Guid Session1, Guid Session2, Guid Session3);

    private async Task<Ctx> EnrolledLearner()
    {
        var admin = await AdminToken();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var program = await PostJson<AdminProgramDto>("/api/admin/programs", admin, new
        {
            name = $"Program {suffix}", slug = $"prog-{suffix}", description = "Uji M3",
            summary = (string?)null, priceIdr = 100000m, published = false,
        });

        var sessionIds = new List<Guid>();
        for (var i = 1; i <= 3; i++)
        {
            var s = await PostJson<AdminSessionDto>($"/api/admin/programs/{program.Id}/sessions", admin, new
            {
                type = "Video", title = $"Sesi {i}", description = (string?)null, orderIndex = i,
                providerAssetId = "sample", durationSeconds = 900,
                scheduledAt = (DateTimeOffset?)null, liveMode = (string?)null,
                joinUrl = (string?)null, location = (string?)null, assessmentId = (Guid?)null,
            });
            sessionIds.Add(s.Id);
        }

        var finalAssessment = await ItpFinal.SeedAsync(factory, "Tes akhir");
        await PostJson<AdminSessionDto>($"/api/admin/programs/{program.Id}/sessions", admin, new
        {
            type = "FinalAssessment", title = "Sesi Akhir", description = (string?)null, orderIndex = 4,
            providerAssetId = (string?)null, durationSeconds = (int?)null,
            scheduledAt = (DateTimeOffset?)null, liveMode = (string?)null,
            joinUrl = (string?)null, location = (string?)null, assessmentId = finalAssessment.AssessmentId,
        });

        var bands = new List<object>();
        foreach (var (section, maxRaw, scaledMax) in new[]
        {
            ("Listening", ToeflScoring.ListeningQuestions, ToeflScoring.ListeningScaledMax),
            ("Structure", ToeflScoring.StructureQuestions, ToeflScoring.StructureScaledMax),
            ("Reading",   ToeflScoring.ReadingQuestions,   ToeflScoring.ReadingScaledMax),
        })
            for (var raw = 0; raw <= maxRaw; raw++)
            {
                var scaled = ToeflScoring.ScaledMin
                    + (int)Math.Round((double)raw / maxRaw * (scaledMax - ToeflScoring.ScaledMin));
                bands.Add(new
                {
                    id = Guid.Empty, section, minRaw = raw, maxRaw = raw,
                    scaledScore = scaled, predictedBand = (string?)null,
                });
            }
        (await Authed(HttpMethod.Put, $"/api/admin/programs/{program.Id}/score-bands", admin,
            new { bands })).EnsureSuccessStatusCode();

        (await Authed(HttpMethod.Put, $"/api/admin/programs/{program.Id}", admin, new
        {
            name = program.Name, slug = program.Slug, description = program.Description,
            summary = program.Summary, priceIdr = program.PriceIdr, published = true,
        })).EnsureSuccessStatusCode();

        var (token, userId) = await VerifiedUser();
        var checkout = await Authed(HttpMethod.Post, $"/api/programs/{program.Id}/enroll", token, new { });
        checkout.EnsureSuccessStatusCode();
        var session = (await checkout.Content.ReadFromJsonAsync<CheckoutSession>(Json))!;
        (await _client.PostAsync($"/api/dev/payments/{Uri.EscapeDataString(session.ProviderRef)}/succeed", null))
            .EnsureSuccessStatusCode();

        return new Ctx(token, userId, admin, program.Id, sessionIds[0], sessionIds[1], sessionIds[2]);
    }

    private async Task<(string token, Guid userId)> VerifiedUser()
    {
        var email = $"u{Guid.NewGuid():N}@test.local";
        (await _client.PostAsJsonAsync("/api/auth/register", new { name = "Budi", email, password = Pw }))
            .EnsureSuccessStatusCode();
        var token = TokenFrom(factory.Email.LastVerifyUrl);
        (await _client.PostAsJsonAsync("/api/auth/verify-email", new { token })).EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Pw });
        var tokens = (await login.Content.ReadFromJsonAsync<AuthTokens>())!;
        return (tokens.AccessToken, tokens.User.Id);
    }

    private async Task<string> AdminToken()
    {
        var email = $"adm{Guid.NewGuid():N}@test.local";
        (await _client.PostAsJsonAsync("/api/auth/register", new { name = "Adm", email, password = Pw }))
            .EnsureSuccessStatusCode();
        await WithDb(async db =>
        {
            var u = await db.Users.FirstAsync(x => x.Email == email);
            u.Role = UserRole.Admin;
            await db.SaveChangesAsync();
        });
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

    private async Task<T> PostJson<T>(string url, string token, object body)
    {
        var res = await Authed(HttpMethod.Post, url, token, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task WithDb(Func<AppDbContext, Task> action)
    {
        using var scope = factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<T> WithDbResult<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static string TokenFrom(string? url)
    {
        Assert.NotNull(url);
        var i = url!.IndexOf("token=", StringComparison.Ordinal);
        Assert.True(i >= 0, "verify URL has no token");
        return url[(i + "token=".Length)..];
    }
}
