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

/// <summary>Learner dashboard task 1: GET /api/me/session-results.</summary>
public class StudentResultsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";
    private const string Url = "/api/me/session-results";

    private record Tests(Guid LessonPart, Guid PartA, Guid AssessmentA, Guid PartB, Guid AssessmentB,
                         Dictionary<string, int> CorrectA, Dictionary<string, int> WrongA,
                         Dictionary<string, int> CorrectB);

    [Fact]
    public async Task Anonymous_is_refused()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(Url)).StatusCode);

    [Fact]
    public async Task A_learner_with_no_attempts_gets_an_empty_list()
    {
        var c = await EnrolledLearner();
        Assert.Empty(await AuthedGet<List<SessionAttemptDto>>(Url, c.Token));
    }

    [Fact]
    public async Task Each_learner_sees_only_their_own_attempts()
    {
        var x = await EnrolledLearner();
        var t = await AddTests(x.Session1);
        var (yToken, _) = await Enroll(x.ProgramId);
        await Watch(x.Token, x.Session1, t.LessonPart);
        await Watch(yToken, x.Session1, t.LessonPart);

        var xAttempt = await Take(x.Token, t.AssessmentA, t.CorrectA);
        var yAttempt = await Take(yToken, t.AssessmentA, t.CorrectA);

        var xRows = await AuthedGet<List<SessionAttemptDto>>(Url, x.Token);
        var yRows = await AuthedGet<List<SessionAttemptDto>>(Url, yToken);
        Assert.Equal(xAttempt, Assert.Single(xRows).AttemptId);
        Assert.Equal(yAttempt, Assert.Single(yRows).AttemptId);
    }

    [Fact]
    public async Task Rows_carry_the_test_part_and_are_ordered_by_submission()
    {
        var x = await EnrolledLearner();
        var t = await AddTests(x.Session1);
        await Watch(x.Token, x.Session1, t.LessonPart);
        await Take(x.Token, t.AssessmentA, t.WrongA);
        await Take(x.Token, t.AssessmentA, t.CorrectA);
        await Take(x.Token, t.AssessmentB, t.CorrectB);

        var rows = await AuthedGet<List<SessionAttemptDto>>(Url, x.Token);

        Assert.Equal(3, rows.Count);
        Assert.Equal([t.PartA, t.PartA, t.PartB], rows.Select(r => r.PartId));
        Assert.Equal(["Tes A", "Tes A", "Tes B"], rows.Select(r => r.PartTitle));
        Assert.All(rows, r =>
        {
            Assert.Equal(x.ProgramId, r.ProgramId);
            Assert.Equal(x.Session1, r.SessionId);
            Assert.Equal("Sesi 1", r.SessionTitle);
            Assert.Equal(1, r.SessionOrderIndex);
            Assert.Equal(2, r.MaxScore);
        });
        Assert.Equal([false, true, true], rows.Select(r => r.Passed));
        Assert.Equal([0, 2, 2], rows.Select(r => r.Score));
        Assert.Equal(rows.OrderBy(r => r.SubmittedAt).Select(r => r.AttemptId), rows.Select(r => r.AttemptId));
    }

    [Fact]
    public async Task No_answer_key_reaches_the_client()
    {
        var x = await EnrolledLearner();
        var t = await AddTests(x.Session1);
        await Watch(x.Token, x.Session1, t.LessonPart);
        await Take(x.Token, t.AssessmentA, t.CorrectA);

        var res = await Authed(HttpMethod.Get, Url, x.Token);
        res.EnsureSuccessStatusCode();
        var raw = await res.Content.ReadAsStringAsync();

        foreach (var banned in new[] { "correct", "answerKey", "isCorrect" })
            Assert.DoesNotContain(banned, raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_attempt_on_a_detached_test_is_omitted()
    {
        var x = await EnrolledLearner();
        var t = await AddTests(x.Session1);
        await Watch(x.Token, x.Session1, t.LessonPart);
        await Take(x.Token, t.AssessmentA, t.CorrectA);
        await Take(x.Token, t.AssessmentB, t.CorrectB);
        // The API refuses removing a part that has attempts, so detach through the db.
        await WithDb(async db =>
        {
            db.SessionParts.RemoveRange(db.SessionParts.Where(p => p.Id == t.PartB));
            await db.SaveChangesAsync();
        });

        var rows = await AuthedGet<List<SessionAttemptDto>>(Url, x.Token);

        Assert.Equal(t.PartA, Assert.Single(rows).PartId);
    }

    [Fact]
    public async Task The_final_assessment_is_never_listed()
    {
        var x = await EnrolledLearner();
        await WithDb(async db =>
        {
            var finalId = await db.ProgramSessions
                .Where(s => s.ProgramId == x.ProgramId && s.Type == SessionType.FinalAssessment)
                .Select(s => s.AssessmentId!.Value).SingleAsync();
            db.Attempts.Add(new Attempt
            {
                Id = Guid.CreateVersion7(), UserId = x.UserId, AssessmentId = finalId,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5), SubmittedAt = DateTimeOffset.UtcNow,
                TotalScore = 400, MaxScore = 677, Passed = true,
            });
            await db.SaveChangesAsync();
        });

        Assert.Empty(await AuthedGet<List<SessionAttemptDto>>(Url, x.Token));
    }

    // ---- facts ----

    /// <summary>Session's own lesson part + Test A + Test B (2 questions each, pass 2).</summary>
    private async Task<Tests> AddTests(Guid sessionId)
    {
        var lesson = await WithDbResult(db => db.SessionParts
            .Where(x => x.SessionId == sessionId).Select(x => x.Id).SingleAsync());
        var (partA, asmA, corA, wrongA) = await AddTest(sessionId, 2, "Tes A");
        var (partB, asmB, corB, _) = await AddTest(sessionId, 3, "Tes B");
        return new Tests(lesson, partA, asmA, partB, asmB, corA, wrongA, corB);
    }

    private async Task<(Guid part, Guid assessment, Dictionary<string, int> correct, Dictionary<string, int> wrong)>
        AddTest(Guid sessionId, int order, string title)
    {
        var a = new Assessment
        {
            Id = Guid.CreateVersion7(), Kind = AssessmentKind.Gating, Title = title,
            Config = """{"passThreshold":2,"proctoringEnabled":false,"sections":[]}""",
        };
        var part = Guid.CreateVersion7();
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
            { Id = part, SessionId = sessionId, OrderIndex = order, Kind = SessionPartKind.Test, Title = title, AssessmentId = a.Id });
            await db.SaveChangesAsync();
        });
        return (part, a.Id, correct, wrong);
    }

    private async Task Watch(string token, Guid sessionId, Guid partId)
        => (await Authed(HttpMethod.Put, $"/api/sessions/{sessionId}/parts/{partId}/progress", token,
            new { positionSeconds = 60, percent = 100m })).EnsureSuccessStatusCode();

    /// <summary>Starts and submits an attempt; returns the attempt id.</summary>
    private async Task<Guid> Take(string token, Guid assessmentId, Dictionary<string, int> answers)
    {
        var start = await Authed(HttpMethod.Post, $"/api/assessments/{assessmentId}/attempts", token);
        start.EnsureSuccessStatusCode();
        var attempt = (await start.Content.ReadFromJsonAsync<AttemptDto>(Json))!;
        (await Authed(HttpMethod.Post, $"/api/attempts/{attempt.Id}/submit", token, new { answers }))
            .EnsureSuccessStatusCode();
        return attempt.Id;
    }

    // ---- helpers (copied from SessionPartLearnerTests) ----

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

        var (token, userId) = await Enroll(program.Id);
        return new Ctx(token, userId, admin, program.Id, sessionIds[0], sessionIds[1], sessionIds[2]);
    }

    private async Task<(string token, Guid userId)> Enroll(Guid programId)
    {
        var (token, userId) = await VerifiedUser();
        var checkout = await Authed(HttpMethod.Post, $"/api/programs/{programId}/enroll", token, new { });
        checkout.EnsureSuccessStatusCode();
        var session = (await checkout.Content.ReadFromJsonAsync<CheckoutSession>(Json))!;
        (await _client.PostAsync($"/api/dev/payments/{Uri.EscapeDataString(session.ProviderRef)}/succeed", null))
            .EnsureSuccessStatusCode();
        return (token, userId);
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
