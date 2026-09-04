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
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Academy.Infrastructure.Programs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>
/// INVERTA M3 acceptance (KAK §16.3): the answer key never leaves the server, watching alone
/// does not complete a gated session, passing unlocks the next one, retake caps are
/// server-authoritative, and completion is non-retroactive.
/// </summary>
public class SessionGatingTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    // ---- GR-11: the answer key never leaves the server ----

    [Fact]
    public async Task Student_assessment_never_exposes_the_answer_key()
    {
        var c = await EnrolledLearner();
        await AttachGatingTest(c, c.Session1, passThreshold: 2);

        var res = await Authed(HttpMethod.Get, $"/api/sessions/{c.Session1}/assessment", c.Token);
        res.EnsureSuccessStatusCode();
        var raw = await res.Content.ReadAsStringAsync();

        // Neither the field name nor the key values may appear anywhere in the payload.
        Assert.DoesNotContain("correct", raw, StringComparison.OrdinalIgnoreCase);

        var dto = JsonSerializer.Deserialize<StudentAssessmentDto>(raw, Json)!;
        Assert.Equal(2, dto.QuestionCount);
        Assert.All(dto.Questions, q => Assert.NotEmpty(q.Choices));
        Assert.False(dto.Passed);
        Assert.True(dto.CanAttempt);
    }

    // ---- the gate itself ----

    [Fact]
    public async Task Watching_to_100_percent_does_not_complete_a_gated_session()
    {
        var c = await EnrolledLearner();
        await AttachGatingTest(c, c.Session1, passThreshold: 2);

        var progress = await SaveProgress(c.Token, c.Session1, 900, 100m);

        Assert.True(progress.WatchThresholdMet);   // the test unlocks …
        Assert.False(progress.Completed);          // … but the session does NOT complete
        Assert.False(await CanAccess(c.UserId, c.Session2));
    }

    [Fact]
    public async Task Passing_the_gating_test_completes_the_session_and_unlocks_the_next()
    {
        var c = await EnrolledLearner();
        var key = await AttachGatingTest(c, c.Session1, passThreshold: 2);
        await SaveProgress(c.Token, c.Session1, 900, 100m);

        var result = await TakeTest(c.Token, c.Session1, key, correct: true);

        Assert.True(result.Passed);
        Assert.Equal(2, result.Score);
        Assert.Equal(2, result.MaxScore);
        Assert.True(result.SessionCompleted);
        Assert.Equal(c.Session2, result.NextSessionId);
        Assert.True(await CanAccess(c.UserId, c.Session2));
        Assert.False(await CanAccess(c.UserId, c.Session3));   // only the next one
    }

    [Fact]
    public async Task Failing_the_gating_test_leaves_the_session_locked()
    {
        var c = await EnrolledLearner();
        var key = await AttachGatingTest(c, c.Session1, passThreshold: 2);
        await SaveProgress(c.Token, c.Session1, 900, 100m);

        var result = await TakeTest(c.Token, c.Session1, key, correct: false);

        Assert.False(result.Passed);
        Assert.Equal(0, result.Score);
        Assert.False(result.SessionCompleted);
        Assert.False(await CanAccess(c.UserId, c.Session2));
    }

    [Fact]
    public async Task Passing_without_watching_does_not_complete_the_session()
    {
        var c = await EnrolledLearner();
        var key = await AttachGatingTest(c, c.Session1, passThreshold: 2);

        // Pass the test but never watch: the watch threshold is the other half of the rule.
        var result = await TakeTest(c.Token, c.Session1, key, correct: true);

        Assert.True(result.Passed);
        Assert.False(result.SessionCompleted);
        Assert.False(await CanAccess(c.UserId, c.Session2));

        // Watching then finishes it, without retaking the test.
        var progress = await SaveProgress(c.Token, c.Session1, 900, 100m);
        Assert.True(progress.Completed);
        Assert.True(await CanAccess(c.UserId, c.Session2));
    }

    // ---- a gating test is always retried until passed ----

    [Fact]
    public async Task A_stored_retake_cap_cannot_strand_a_learner_on_a_gating_test()
    {
        // A gating test has no cap (FSD §6.1) — but one WAS stored on the live sample test by an
        // admin edit, and a capped-out learner has no way past it: no retry in the UI, no admin
        // reset, and the linear lock holds the rest of a paid programme behind that one session.
        // So the cap is ignored at read, which disarms the bad data already out there rather than
        // only the next save.
        var c = await EnrolledLearner();
        var key = await AttachGatingTest(c, c.Session1, passThreshold: 2, retakeCap: 1);

        var first = await TakeTest(c.Token, c.Session1, key, correct: false);
        Assert.False(first.Passed);

        var second = await TakeTest(c.Token, c.Session1, key, correct: true);
        Assert.True(second.Passed);

        var view = await AuthedGet<StudentAssessmentDto>($"/api/sessions/{c.Session1}/assessment", c.Token);
        Assert.True(view.CanAttempt);
        Assert.Null(view.RetakeCap);        // never advertised to the client either
        Assert.Equal(2, view.AttemptsUsed);
    }

    [Fact]
    public async Task A_retake_cap_of_zero_is_refused_by_the_admin_api()
    {
        var admin = await AdminToken();

        // cap 0 would make `used >= cap` true before the first attempt: the gating test could
        // never be passed and the linear lock would jam for every learner on the program.
        var res = await Authed(HttpMethod.Post, "/api/admin/assessments", admin, new
        {
            kind = "Gating", title = "Tes sesi",
            config = new { passThreshold = 1, retakeCap = 0, proctoringEnabled = false, sections = Array.Empty<object>() },
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        // …and the same value cannot be smuggled in through an update either.
        var ok = await PostJson<AdminAssessmentDto>("/api/admin/assessments", admin, new
        {
            kind = "Gating", title = "Tes sesi",
            config = new { passThreshold = 1, retakeCap = (int?)null, proctoringEnabled = false, sections = Array.Empty<object>() },
        });
        var update = await Authed(HttpMethod.Put, $"/api/admin/assessments/{ok.Id}", admin, new
        {
            kind = "Gating", title = "Tes sesi",
            config = new { passThreshold = 1, retakeCap = 0, proctoringEnabled = false, sections = Array.Empty<object>() },
        });
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    [Fact]
    public async Task Unlimited_retakes_by_default()
    {
        var c = await EnrolledLearner();
        var key = await AttachGatingTest(c, c.Session1, passThreshold: 2);   // no cap

        await TakeTest(c.Token, c.Session1, key, correct: false);
        await TakeTest(c.Token, c.Session1, key, correct: false);
        var third = await TakeTest(c.Token, c.Session1, key, correct: true);

        Assert.True(third.Passed);
        var view = await AuthedGet<StudentAssessmentDto>($"/api/sessions/{c.Session1}/assessment", c.Token);
        Assert.Equal(3, view.AttemptsUsed);
        Assert.True(view.Passed);
    }

    // ---- non-retroactive (GR-8) ----

    [Fact]
    public async Task Attaching_a_test_after_completion_does_not_uncomplete_the_session()
    {
        var c = await EnrolledLearner();

        // Complete session 1 with no gating test attached.
        var done = await SaveProgress(c.Token, c.Session1, 900, 100m);
        Assert.True(done.Completed);

        // Admin now attaches a gating test.
        await AttachGatingTest(c, c.Session1, passThreshold: 2);

        // Still complete, and the next session stays unlocked.
        var after = await GetProgress(c.Token, c.Session1);
        Assert.True(after.Completed);
        Assert.True(await CanAccess(c.UserId, c.Session2));
    }

    // ---- the access gate covers every M3 resource (GR-1) ----

    [Fact]
    public async Task Locked_session_refuses_playback_progress_and_assessment()
    {
        var c = await EnrolledLearner();

        Assert.Equal(HttpStatusCode.Forbidden,
            (await Authed(HttpMethod.Post, $"/api/sessions/{c.Session2}/playback", c.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Authed(HttpMethod.Get, $"/api/sessions/{c.Session2}/assessment", c.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Authed(HttpMethod.Put, $"/api/sessions/{c.Session2}/progress", c.Token,
                new { positionSeconds = 10, percent = 50m })).StatusCode);
    }

    [Fact]
    public async Task Playback_is_signed_short_lived_and_hides_the_asset_id()
    {
        var c = await EnrolledLearner();
        var res = await Authed(HttpMethod.Post, $"/api/sessions/{c.Session1}/playback", c.Token);
        res.EnsureSuccessStatusCode();
        var raw = await res.Content.ReadAsStringAsync();
        var ticket = JsonSerializer.Deserialize<SessionPlaybackDto>(raw, Json)!;

        Assert.False(string.IsNullOrWhiteSpace(ticket.Url));
        Assert.True(ticket.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.True(ticket.ExpiresAt < DateTimeOffset.UtcNow.AddHours(2));   // short-TTL
        Assert.DoesNotContain("providerAssetId", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Progress_percent_is_monotonic()
    {
        var c = await EnrolledLearner();
        await SaveProgress(c.Token, c.Session1, 600, 80m);
        var back = await SaveProgress(c.Token, c.Session1, 10, 5m);   // seek backwards
        Assert.Equal(80m, back.PercentComplete);
        Assert.Equal(10, back.ResumePositionSeconds);                // resume position DOES move
    }

    // ---- attempts belong to their owner ----

    [Fact]
    public async Task An_attempt_cannot_be_submitted_by_another_user()
    {
        var c = await EnrolledLearner();
        var key = await AttachGatingTest(c, c.Session1, passThreshold: 2);
        var assessmentId = await AssessmentIdFor(c.Session1);

        var start = await Authed(HttpMethod.Post, $"/api/assessments/{assessmentId}/attempts", c.Token);
        var attempt = (await start.Content.ReadFromJsonAsync<AttemptDto>(Json))!;

        var other = await EnrolledLearner();
        var res = await Authed(HttpMethod.Post, $"/api/attempts/{attempt.Id}/submit", other.Token,
            new { answers = key.Correct });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ---- admin authoring ----

    [Fact]
    public async Task Question_used_by_an_assessment_cannot_be_deleted()
    {
        var c = await EnrolledLearner();
        var key = await AttachGatingTest(c, c.Session1, passThreshold: 2);

        var res = await Authed(HttpMethod.Delete, $"/api/admin/questions/{key.QuestionIds[0]}", c.Admin);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Assessment_with_attempts_cannot_be_deleted()
    {
        var c = await EnrolledLearner();
        var key = await AttachGatingTest(c, c.Session1, passThreshold: 2);
        await TakeTest(c.Token, c.Session1, key, correct: true);

        var assessmentId = await AssessmentIdFor(c.Session1);
        var res = await Authed(HttpMethod.Delete, $"/api/admin/assessments/{assessmentId}", c.Admin);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Exactly_the_pass_mark_passes()
    {
        // The boundary, in the direction that costs a learner their session. The rule is
        // `score >= passThreshold`, so scoring EXACTLY the mark must pass; an off-by-one here
        // fails people who earned it, on a test they paid to sit.
        var c = await EnrolledLearner();
        var key = await AttachGatingTest(c, c.Session1, passThreshold: 1);
        await SaveProgress(c.Token, c.Session1, 600, 95m);

        var result = await TakeTest(c.Token, c.Session1, key, correctCount: 1);

        Assert.Equal(1, result.Score);
        Assert.True(result.Passed);
    }

    [Fact]
    public async Task One_below_the_pass_mark_fails()
    {
        // The other side of the same boundary. Together these pin `>=` exactly: relaxing it to
        // `>` breaks the test above, and loosening it to `>= threshold - 1` breaks this one.
        var c = await EnrolledLearner();
        var key = await AttachGatingTest(c, c.Session1, passThreshold: 2);
        await SaveProgress(c.Token, c.Session1, 600, 95m);

        var result = await TakeTest(c.Token, c.Session1, key, correctCount: 1);

        Assert.Equal(1, result.Score);
        Assert.False(result.Passed);
    }

    [Fact]
    public async Task A_question_with_no_correct_answer_marked_is_refused()
    {
        // The admin form always has a radio selected, so this state is only reachable through the
        // API — which is exactly why the server has to refuse it rather than trust the screen.
        var admin = await AdminToken();
        var res = await Authed(HttpMethod.Post, "/api/admin/questions", admin, new
        {
            section = "Reading", prompt = "Q", choices = new[] { "a", "b" }, correct = Array.Empty<int>(),
            audioRef = (string?)null, passageRef = (string?)null, tags = (string[]?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("jawaban benar", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_question_with_fewer_than_two_choices_is_refused()
    {
        // Same reasoning: the form will not let an admin delete down to one choice, so the only
        // way in is the API. A single-choice question is unanswerable, not merely odd.
        var admin = await AdminToken();
        var res = await Authed(HttpMethod.Post, "/api/admin/questions", admin, new
        {
            section = "Reading", prompt = "Q", choices = new[] { "hanya satu" }, correct = new[] { 0 },
            audioRef = (string?)null, passageRef = (string?)null, tags = (string[]?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("2 pilihan", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Question_bank_rejects_an_out_of_range_correct_index()
    {
        var admin = await AdminToken();
        var res = await Authed(HttpMethod.Post, "/api/admin/questions", admin, new
        {
            section = "Reading", prompt = "Q", choices = new[] { "a", "b" }, correct = new[] { 5 },
            audioRef = (string?)null, passageRef = (string?)null, tags = (string[]?)null,
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Admin_routes_reject_a_normal_user()
    {
        var c = await EnrolledLearner();
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Authed(HttpMethod.Get, "/api/admin/questions", c.Token)).StatusCode);
    }

    // ================================================================ helpers

    private record Ctx(string Token, Guid UserId, string Admin, Guid ProgramId,
                       Guid Session1, Guid Session2, Guid Session3);

    private record TestKey(IReadOnlyList<Guid> QuestionIds, Dictionary<string, int> Correct,
                           Dictionary<string, int> Wrong);

    /// <summary>A fresh program (isolated per test) with an enrolled, verified learner.</summary>
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

    /// <summary>Authors a 2-question gating test and attaches it to the session.</summary>
    private async Task<TestKey> AttachGatingTest(Ctx c, Guid sessionId, int passThreshold, int? retakeCap = null)
    {
        var q1 = await PostJson<AdminQuestionDto>("/api/admin/questions", c.Admin, new
        {
            section = "Reading", prompt = $"Q1 {Guid.NewGuid():N}", choices = new[] { "benar", "salah" },
            correct = new[] { 0 }, audioRef = (string?)null, passageRef = (string?)null, tags = (string[]?)null,
        });
        var q2 = await PostJson<AdminQuestionDto>("/api/admin/questions", c.Admin, new
        {
            section = "Structure", prompt = $"Q2 {Guid.NewGuid():N}", choices = new[] { "salah", "benar" },
            correct = new[] { 1 }, audioRef = (string?)null, passageRef = (string?)null, tags = (string[]?)null,
        });

        var assessment = await PostJson<AdminAssessmentDto>("/api/admin/assessments", c.Admin, new
        {
            kind = "Gating",
            title = "Tes sesi",
            config = new { passThreshold, retakeCap, proctoringEnabled = false, sections = Array.Empty<object>() },
        });

        (await Authed(HttpMethod.Put, $"/api/admin/assessments/{assessment.Id}/questions", c.Admin,
            new { questionIdsInOrder = new[] { q1.Id, q2.Id } })).EnsureSuccessStatusCode();

        (await Authed(HttpMethod.Put, $"/api/admin/sessions/{sessionId}/assessment", c.Admin,
            new { assessmentId = assessment.Id })).EnsureSuccessStatusCode();

        // Answers are keyed by question id, so they're independent of the served order.
        return new TestKey(
            [q1.Id, q2.Id],
            new Dictionary<string, int> { [q1.Id.ToString()] = 0, [q2.Id.ToString()] = 1 },
            new Dictionary<string, int> { [q1.Id.ToString()] = 1, [q2.Id.ToString()] = 0 });
    }

    private Task<AttemptResultDto> TakeTest(string token, Guid sessionId, TestKey key, bool correct)
        => Submit(token, sessionId, correct ? key.Correct : key.Wrong);

    /// <summary>Answers exactly <paramref name="correctCount"/> of the questions correctly and the
    /// rest wrongly — the only way to land ON a pass mark rather than either side of it.</summary>
    private Task<AttemptResultDto> TakeTest(string token, Guid sessionId, TestKey key, int correctCount)
    {
        var answers = key.QuestionIds
            .Select((id, index) => (Key: id.ToString(), Value: index < correctCount
                ? key.Correct[id.ToString()]
                : key.Wrong[id.ToString()]))
            .ToDictionary(x => x.Key, x => x.Value);
        return Submit(token, sessionId, answers);
    }

    private async Task<AttemptResultDto> Submit(
        string token, Guid sessionId, IReadOnlyDictionary<string, int> answers)
    {
        var assessmentId = await AssessmentIdFor(sessionId);
        var start = await Authed(HttpMethod.Post, $"/api/assessments/{assessmentId}/attempts", token);
        start.EnsureSuccessStatusCode();
        var attempt = (await start.Content.ReadFromJsonAsync<AttemptDto>(Json))!;

        var res = await Authed(HttpMethod.Post, $"/api/attempts/{attempt.Id}/submit", token,
            new { answers });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AttemptResultDto>(Json))!;
    }

    private Task<Guid> AssessmentIdFor(Guid sessionId) => WithDbResult(db => db.ProgramSessions
        .Where(s => s.Id == sessionId).Select(s => s.AssessmentId!.Value).FirstAsync());

    private async Task<SessionProgressDto> SaveProgress(string token, Guid sessionId, int pos, decimal pct)
    {
        var res = await Authed(HttpMethod.Put, $"/api/sessions/{sessionId}/progress", token,
            new { positionSeconds = pos, percent = pct });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<SessionProgressDto>(Json))!;
    }

    private Task<SessionProgressDto> GetProgress(string token, Guid sessionId)
        => AuthedGet<SessionProgressDto>($"/api/sessions/{sessionId}/progress", token);

    private async Task<bool> CanAccess(Guid userId, Guid sessionId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISessionAccessService>()
            .CanAccessAsync(userId, sessionId);
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
