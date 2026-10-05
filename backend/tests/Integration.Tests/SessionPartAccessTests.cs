using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Academy.Application.Auth;
using Academy.Application.Billing;
using Academy.Application.Programs;
using Academy.Domain;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Academy.Infrastructure.Programs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>Task 3 of session parts (2026-10-05): one reader turns progress and attempts into part
/// statuses, and the part gate sits on top of the session gate.</summary>
public class SessionPartAccessTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    private record Parts(Guid Lesson, Guid Test, Guid Discussion, Guid Assessment);

    [Fact]
    public async Task Only_the_first_part_is_open_at_the_start()
    {
        var c = await EnrolledLearner();
        var p = await AddParts(c.Session1);

        Assert.Equal([PartStatus.Open, PartStatus.Locked, PartStatus.Locked],
            (await States(c.UserId, c.Session1)).Select(s => s.Status));
        Assert.True(await CanAccessPart(c.UserId, c.Session1, p.Lesson));
        Assert.False(await CanAccessPart(c.UserId, c.Session1, p.Test));
    }

    [Fact]
    public async Task Watching_the_lesson_opens_the_test()
    {
        var c = await EnrolledLearner();
        var p = await AddParts(c.Session1);
        await Watch(c.UserId, c.Session1, p.Lesson);

        Assert.True(await CanAccessPart(c.UserId, c.Session1, p.Test));
        Assert.False(await CanAccessPart(c.UserId, c.Session1, p.Discussion));
    }

    [Fact]
    public async Task Two_failures_open_the_discussion()
    {
        var c = await EnrolledLearner();
        var p = await AddParts(c.Session1);
        await Watch(c.UserId, c.Session1, p.Lesson);
        await Attempt(c.UserId, p.Assessment, passed: false);
        Assert.False(await CanAccessPart(c.UserId, c.Session1, p.Discussion));
        await Attempt(c.UserId, p.Assessment, passed: false);

        Assert.Equal([PartStatus.Done, PartStatus.Open, PartStatus.Open],
            (await States(c.UserId, c.Session1)).Select(s => s.Status));
        Assert.True(await CanAccessPart(c.UserId, c.Session1, p.Discussion));
    }

    [Fact]
    public async Task A_part_of_another_session_is_not_found()
    {
        var c = await EnrolledLearner();
        var p = await AddParts(c.Session1);

        using var scope = factory.Services.CreateScope();
        var gate = scope.ServiceProvider.GetRequiredService<ISessionAccessService>();
        var ex = await Assert.ThrowsAsync<ProgramException>(
            () => gate.EnsurePartAccessAsync(c.UserId, c.Session2, p.Lesson));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task A_locked_session_locks_all_its_parts()
    {
        var c = await EnrolledLearner();
        var lesson2 = await WithDbResult(db => db.SessionParts
            .Where(x => x.SessionId == c.Session2).Select(x => x.Id).SingleAsync());

        Assert.False(await CanAccessPart(c.UserId, c.Session2, lesson2));
    }

    [Fact]
    public async Task Unenrolled_user_cannot_access_a_part()
    {
        var c = await EnrolledLearner();
        var p = await AddParts(c.Session1);
        var (_, stranger) = await VerifiedUser();

        Assert.False(await CanAccessPart(stranger, c.Session1, p.Lesson));
    }

    // ---- facts ----

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
        return new Parts(lesson, test, discussion, a.Id);
    }

    private Task Watch(Guid userId, Guid sessionId, Guid partId) => WithDb(async db =>
    {
        db.WatchProgress.Add(new WatchProgress
        {
            Id = Guid.CreateVersion7(), UserId = userId, SessionId = sessionId, PartId = partId,
            PercentComplete = 100, LastWatchedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    });

    private Task Attempt(Guid userId, Guid assessmentId, bool passed) => WithDb(async db =>
    {
        db.Attempts.Add(new Attempt
        {
            Id = Guid.CreateVersion7(), UserId = userId, AssessmentId = assessmentId,
            StartedAt = DateTimeOffset.UtcNow, SubmittedAt = DateTimeOffset.UtcNow, Passed = passed,
        });
        await db.SaveChangesAsync();
    });

    private async Task<IReadOnlyList<PartState>> States(Guid userId, Guid sessionId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SessionPartStates>()
            .LoadAsync(userId, sessionId, CancellationToken.None);
    }

    private async Task<bool> CanAccessPart(Guid userId, Guid sessionId, Guid partId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISessionAccessService>()
            .CanAccessPartAsync(userId, sessionId, partId);
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

        // These three sessions are what each test drives; content-completeness (needed to
        // publish, needed to enroll) is satisfied with a throwaway final-assessment session
        // and a full score-band table, same as ProgramReadinessTests.BuildReadyProgram.
        var finalAssessment = await ItpFinal.SeedAsync(factory, "Tes akhir");
        await PostJson<AdminSessionDto>($"/api/admin/programs/{program.Id}/sessions", admin, new
        {
            type = "FinalAssessment", title = "Sesi Akhir", description = (string?)null, orderIndex = 4,
            providerAssetId = (string?)null, durationSeconds = (int?)null,
            scheduledAt = (DateTimeOffset?)null, liveMode = (string?)null,
            joinUrl = (string?)null, location = (string?)null, assessmentId = finalAssessment.AssessmentId,
        });

        // Score bands must cover the FULL ITP raw-score range per section (not just the toy
        // question count above) — the readiness check tests against the fixed ITP format sizes.
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
