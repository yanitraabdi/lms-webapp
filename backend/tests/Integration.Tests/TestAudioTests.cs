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

/// <summary>Task 5 of session parts (2026-10-05): one shared recording per session test, limited plays per attempt.</summary>
public class TestAudioTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    private record Test(Guid Part, Guid Assessment, Dictionary<string, int> Wrong);

    [Fact]
    public async Task Audio_before_starting_an_attempt_is_refused()
    {
        var c = await EnrolledLearner();
        var t = await AddTest(c.Session1, withLesson: false);

        Assert.Equal(HttpStatusCode.Conflict, (await Audio(c, t, false)).StatusCode);
    }

    [Fact]
    public async Task First_start_stamps_and_a_reload_resumes_the_same_play()
    {
        var c = await EnrolledLearner();
        var t = await AddTest(c.Session1, withLesson: false);
        await Start(c, t);

        var first = await PlayOk(c, t, false);
        var again = await PlayOk(c, t, false);

        Assert.Equal(first.StartedAt, again.StartedAt);
        Assert.Equal(1, again.PlaysUsed);
        Assert.Equal(1, again.PlayLimit);
    }

    [Fact]
    public async Task Replay_beyond_the_limit_is_refused()
    {
        var c = await EnrolledLearner();
        var t = await AddTest(c.Session1, withLesson: false);
        await Start(c, t);
        await PlayOk(c, t, false);

        var res = await Audio(c, t, true);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("Audio sudah diputar sebanyak batas yang diizinkan.", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Limit_of_two_allows_one_replay()
    {
        var c = await EnrolledLearner();
        var t = await AddTest(c.Session1, withLesson: false, playLimit: 2);
        await Start(c, t);
        var first = await PlayOk(c, t, false);
        await Task.Delay(20);

        var second = await PlayOk(c, t, true);

        Assert.Equal(2, second.PlaysUsed);
        Assert.NotEqual(first.StartedAt, second.StartedAt);
        Assert.Equal(HttpStatusCode.Conflict, (await Audio(c, t, true)).StatusCode);
    }

    [Fact]
    public async Task A_retake_gets_fresh_plays()
    {
        var c = await EnrolledLearner();
        var t = await AddTest(c.Session1, withLesson: false);
        var attempt = await Start(c, t);
        await PlayOk(c, t, false);
        (await Authed(HttpMethod.Post, $"/api/attempts/{attempt.Id}/submit", c.Token, new { answers = t.Wrong }))
            .EnsureSuccessStatusCode();
        await Start(c, t);

        var play = await PlayOk(c, t, false);

        Assert.Equal(1, play.PlaysUsed);
    }

    [Fact]
    public async Task Student_view_reports_shared_audio()
    {
        var c = await EnrolledLearner();
        var t = await AddTest(c.Session1, withLesson: false);

        var res = await Authed(HttpMethod.Get, $"/api/sessions/{c.Session1}/parts/{t.Part}/assessment", c.Token);
        res.EnsureSuccessStatusCode();
        var raw = await res.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<StudentAssessmentDto>(raw, Json)!;

        Assert.True(dto.HasTestAudio);
        Assert.Equal(1, dto.TestAudioPlayLimit);
        Assert.DoesNotContain("audio/test.mp3", raw);
    }

    [Fact]
    public async Task Locked_test_part_audio_is_refused()
    {
        var c = await EnrolledLearner();
        var t = await AddTest(c.Session1, withLesson: true);

        Assert.Equal(HttpStatusCode.Forbidden, (await Audio(c, t, false)).StatusCode);
    }

    [Fact]
    public async Task Discussion_threshold_below_one_is_rejected()
    {
        var c = await EnrolledLearner();
        var t = await AddTest(c.Session1, withLesson: false);

        var res = await Authed(HttpMethod.Put, $"/api/admin/assessments/{t.Assessment}", c.Admin,
            new { title = "Tes bagian", kind = "Gating", config = new { discussionAfterFailures = 0 } });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---- facts ----

    /// <summary>Replaces the session's parts with [Test] (or [Lesson, Test] when withLesson).</summary>
    private async Task<Test> AddTest(Guid sessionId, bool withLesson, int? playLimit = null)
    {
        var limit = playLimit is int n ? $",\"audioPlayLimit\":{n}" : "";
        var a = new Assessment
        {
            Id = Guid.CreateVersion7(), Kind = AssessmentKind.Gating, Title = "Tes bagian",
            Config = $$"""{"audioRef":"audio/test.mp3","passThreshold":2,"proctoringEnabled":false,"sections":[]{{limit}}}""",
        };
        var part = Guid.CreateVersion7();
        var wrong = new Dictionary<string, int>();
        await WithDb(async db =>
        {
            if (!withLesson)
                db.SessionParts.RemoveRange(db.SessionParts.Where(x => x.SessionId == sessionId));
            db.Assessments.Add(a);
            for (var i = 0; i < 2; i++)
            {
                var q = new Question
                {
                    Id = Guid.CreateVersion7(), Section = QuestionSection.Listening, Prompt = $"Q{i} {Guid.NewGuid():N}",
                    Choices = """["a","b"]""", Correct = "[0]",
                };
                db.Questions.Add(q);
                db.AssessmentQuestions.Add(new AssessmentQuestion
                { Id = Guid.CreateVersion7(), AssessmentId = a.Id, QuestionId = q.Id, OrderIndex = i + 1 });
                wrong[q.Id.ToString()] = 1;
            }
            db.SessionParts.Add(new SessionPart
            { Id = part, SessionId = sessionId, OrderIndex = withLesson ? 2 : 1, Kind = SessionPartKind.Test, Title = "Tes", AssessmentId = a.Id });
            await db.SaveChangesAsync();
        });
        return new Test(part, a.Id, wrong);
    }

    private Task<HttpResponseMessage> Audio(Ctx c, Test t, bool replay) =>
        Authed(HttpMethod.Post, $"/api/sessions/{c.Session1}/parts/{t.Part}/audio", c.Token, new { replay });

    private async Task<TestAudioDto> PlayOk(Ctx c, Test t, bool replay)
    {
        var res = await Audio(c, t, replay);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<TestAudioDto>(Json))!;
    }

    private async Task<AttemptDto> Start(Ctx c, Test t)
    {
        var res = await Authed(HttpMethod.Post, $"/api/assessments/{t.Assessment}/attempts", c.Token);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AttemptDto>(Json))!;
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
