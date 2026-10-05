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

/// <summary>Task 6 of session parts (2026-10-05): the admin API that composes a video session's parts.</summary>
public class SessionPartAdminTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    private static object Lesson(Guid? id, string title = "Materi") =>
        new { id, kind = "LessonVideo", title, providerAssetId = "sample", durationSeconds = 60, assessmentId = (Guid?)null };
    private static object Disc(string title = "Pembahasan") =>
        new { id = (Guid?)null, kind = "Discussion", title, providerAssetId = "sample", durationSeconds = 60, assessmentId = (Guid?)null };
    private static object Test(Guid assessment, Guid? id = null, string title = "Tes") =>
        new { id, kind = "Test", title, providerAssetId = (string?)null, durationSeconds = (int?)null, assessmentId = (Guid?)assessment };

    private Task<HttpResponseMessage> PutParts(string admin, Guid session, params object[] parts)
        => Authed(HttpMethod.Put, $"/api/admin/sessions/{session}/parts", admin, new { parts });

    private async Task<IReadOnlyList<AdminSessionPartDto>> ListParts(string admin, Guid session)
        => await AuthedGet<IReadOnlyList<AdminSessionPartDto>>($"/api/admin/sessions/{session}/parts", admin);

    private static async Task AssertError(HttpResponseMessage res, HttpStatusCode code, string message)
    {
        Assert.Equal(code, res.StatusCode);
        Assert.Contains(message, await res.Content.ReadAsStringAsync());
    }

    private async Task<Guid> Lesson1(Guid session) => await WithDbResult(db => db.SessionParts
        .Where(p => p.SessionId == session).Select(p => p.Id).SingleAsync());

    private async Task<(Guid Id, Dictionary<string, int> Correct)> NewAssessment(AssessmentKind kind = AssessmentKind.Gating,
        string config = """{"passThreshold":2,"proctoringEnabled":false,"sections":[]}""")
    {
        var a = new Assessment { Id = Guid.CreateVersion7(), Kind = kind, Title = "Tes " + Guid.NewGuid().ToString("N")[..6], Config = config };
        var correct = new Dictionary<string, int>();
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
            }
            await db.SaveChangesAsync();
        });
        return (a.Id, correct);
    }

    [Fact]
    public async Task Saving_builds_the_ordered_list()
    {
        var c = await EnrolledLearner();
        var l = await Lesson1(c.Session1);
        var a1 = (await NewAssessment()).Id; var a2 = (await NewAssessment()).Id;

        var res = await PutParts(c.Admin, c.Session1, Lesson(l), Test(a1), Disc(), Lesson(null, "Materi 2"), Test(a2));
        res.EnsureSuccessStatusCode();
        var saved = (await res.Content.ReadFromJsonAsync<List<AdminSessionPartDto>>(Json))!;

        Assert.Equal([1, 2, 3, 4, 5], saved.Select(p => p.OrderIndex));
        Assert.Equal(["LessonVideo", "Test", "Discussion", "LessonVideo", "Test"], saved.Select(p => p.Kind));
        Assert.Equal(l, saved[0].Id);
        Assert.Equal(a1, saved[1].AssessmentId);
        Assert.Equal(saved.Select(p => p.Id), (await ListParts(c.Admin, c.Session1)).Select(p => p.Id));
    }

    [Fact]
    public async Task Reordering_keeps_ids()
    {
        var c = await EnrolledLearner();
        var l = await Lesson1(c.Session1);
        var a1 = (await NewAssessment()).Id;
        var first = (await (await PutParts(c.Admin, c.Session1, Lesson(l), Test(a1)))
            .Content.ReadFromJsonAsync<List<AdminSessionPartDto>>(Json))!;

        var res = await PutParts(c.Admin, c.Session1, Test(a1, first[1].Id), Lesson(first[0].Id));
        res.EnsureSuccessStatusCode();
        var again = (await res.Content.ReadFromJsonAsync<List<AdminSessionPartDto>>(Json))!;

        Assert.Equal([first[1].Id, first[0].Id], again.Select(p => p.Id));
        Assert.Equal([1, 2], again.Select(p => p.OrderIndex));
    }

    [Fact]
    public async Task Empty_list_is_refused()
    {
        var c = await EnrolledLearner();
        await AssertError(await PutParts(c.Admin, c.Session1), HttpStatusCode.BadRequest,
            "Sesi video harus memiliki minimal satu bagian.");
    }

    [Fact]
    public async Task Discussion_not_after_a_test_is_refused()
    {
        var c = await EnrolledLearner();
        var l = await Lesson1(c.Session1);
        await AssertError(await PutParts(c.Admin, c.Session1, Lesson(l), Disc()), HttpStatusCode.BadRequest,
            "Video pembahasan harus tepat setelah sebuah tes.");
    }

    [Fact]
    public async Task Two_discussions_for_one_test_are_refused()
    {
        var c = await EnrolledLearner();
        var a1 = (await NewAssessment()).Id;
        await AssertError(await PutParts(c.Admin, c.Session1, Test(a1), Disc(), Disc()), HttpStatusCode.BadRequest,
            "Video pembahasan harus tepat setelah sebuah tes.");
    }

    [Fact]
    public async Task Final_assessment_as_a_test_part_is_refused()
    {
        var c = await EnrolledLearner();
        var f = (await NewAssessment(AssessmentKind.Final)).Id;
        await AssertError(await PutParts(c.Admin, c.Session1, Test(f)), HttpStatusCode.BadRequest,
            "Bagian tes harus memakai tes sesi (bukan tes akhir).");
    }

    [Fact]
    public async Task Same_test_twice_is_refused()
    {
        var c = await EnrolledLearner();
        var a1 = (await NewAssessment()).Id;
        await AssertError(await PutParts(c.Admin, c.Session1, Test(a1), Test(a1)), HttpStatusCode.BadRequest,
            "Satu tes hanya boleh dipakai di satu bagian.");
    }

    [Fact]
    public async Task Test_used_by_another_session_is_refused()
    {
        var c = await EnrolledLearner();
        var a1 = (await NewAssessment()).Id;
        (await PutParts(c.Admin, c.Session1, Test(a1))).EnsureSuccessStatusCode();
        await AssertError(await PutParts(c.Admin, c.Session2, Test(a1)), HttpStatusCode.Conflict,
            "Tes ini sudah dipakai di sesi lain.");
    }

    [Fact]
    public async Task Removing_a_part_with_progress_is_refused()
    {
        var c = await EnrolledLearner();
        var l = await Lesson1(c.Session1);
        var a1 = (await NewAssessment()).Id;
        (await PutParts(c.Admin, c.Session1, Lesson(l), Test(a1))).EnsureSuccessStatusCode();
        await WithDb(async db =>
        {
            db.WatchProgress.Add(new WatchProgress
            {
                Id = Guid.CreateVersion7(), UserId = c.UserId, SessionId = c.Session1, PartId = l,
                PercentComplete = 50, LastWatchedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        });

        await AssertError(await PutParts(c.Admin, c.Session1, Test(a1)), HttpStatusCode.Conflict,
            "tidak bisa dihapus karena sudah memiliki progres peserta");

        var res = await PutParts(c.Admin, c.Session1, Lesson(l, "Judul baru"), Test(a1));
        res.EnsureSuccessStatusCode();
        Assert.Equal("Judul baru", (await ListParts(c.Admin, c.Session1))[0].Title);
    }

    [Fact]
    public async Task Parts_of_a_live_session_are_refused()
    {
        var c = await EnrolledLearner();
        var live = await PostJson<AdminSessionDto>($"/api/admin/programs/{c.ProgramId}/sessions", c.Admin, new
        {
            type = "Live", title = "Live", description = (string?)null, orderIndex = 9,
            providerAssetId = (string?)null, durationSeconds = (int?)null,
            scheduledAt = DateTimeOffset.UtcNow.AddDays(30), liveMode = "Zoom",
            joinUrl = (string?)null, location = (string?)null, assessmentId = (Guid?)null,
        });
        await AssertError(await PutParts(c.Admin, live.Id, Lesson(null)), HttpStatusCode.BadRequest,
            "Hanya sesi video yang memiliki bagian.");
    }

    [Fact]
    public async Task Learner_cannot_call_admin_parts()
    {
        var c = await EnrolledLearner();
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Authed(HttpMethod.Get, $"/api/admin/sessions/{c.Session1}/parts", c.Token)).StatusCode);
    }

    [Fact]
    public async Task Session_edit_no_longer_changes_the_video()
    {
        var c = await EnrolledLearner();
        var res = await Authed(HttpMethod.Put, $"/api/admin/sessions/{c.Session1}", c.Admin, new
        {
            type = "Video", title = "Sesi 1", description = (string?)null, orderIndex = 1,
            providerAssetId = "other-video", durationSeconds = 5,
            scheduledAt = (DateTimeOffset?)null, liveMode = (string?)null,
            joinUrl = (string?)null, location = (string?)null, assessmentId = (Guid?)null,
        });
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        var part = (await ListParts(c.Admin, c.Session1)).Single();
        Assert.Equal("sample", part.ProviderAssetId);
    }

    [Fact]
    public async Task Readiness_flags_a_video_session_with_no_parts()
    {
        var c = await EnrolledLearner();
        await PostJson<AdminSessionDto>($"/api/admin/programs/{c.ProgramId}/sessions", c.Admin, new
        {
            type = "Video", title = "Tanpa video", description = (string?)null, orderIndex = 20,
            providerAssetId = (string?)null, durationSeconds = (int?)null,
            scheduledAt = (DateTimeOffset?)null, liveMode = (string?)null,
            joinUrl = (string?)null, location = (string?)null, assessmentId = (Guid?)null,
        });

        var r = await AuthedGet<ProgramReadinessDto>($"/api/admin/programs/{c.ProgramId}/readiness", c.Admin);
        var check = r.Checks.Single(x => x.Key == "video_sessions_have_parts");
        Assert.False(check.Passed);
        Assert.True(check.Blocking);
    }

    [Fact]
    public async Task End_to_end_lesson_test_failures_discussion_pass_next_session()
    {
        var c = await EnrolledLearner();
        var l = await Lesson1(c.Session1);
        var a = await NewAssessment(config:
            """{"passThreshold":2,"proctoringEnabled":false,"sections":[],"discussionAfterFailures":2}""");
        var parts = (await (await PutParts(c.Admin, c.Session1, Lesson(l), Test(a.Id), Disc()))
            .Content.ReadFromJsonAsync<List<AdminSessionPartDto>>(Json))!;
        var wrong = a.Correct.ToDictionary(k => k.Key, _ => 1);

        await Save(c, parts[0].Id);
        Assert.False((await Take(c.Token, a.Id, wrong)).Passed);
        Assert.False((await Take(c.Token, a.Id, wrong)).Passed);

        var ctx = await AuthedGet<SessionContextDto>($"/api/sessions/{c.Session1}", c.Token);
        Assert.Equal("Open", ctx.Parts.Single(p => p.Id == parts[2].Id).Status);
        var disc = await Save(c, parts[2].Id);
        Assert.False(disc.SessionCompleted);

        var result = await Take(c.Token, a.Id, a.Correct);
        Assert.True(result.Passed);
        Assert.True(result.SessionCompleted);
        Assert.Equal(HttpStatusCode.OK,
            (await Authed(HttpMethod.Get, $"/api/sessions/{c.Session2}", c.Token)).StatusCode);
    }

    private async Task<PartProgressDto> Save(Ctx c, Guid partId)
    {
        var res = await Authed(HttpMethod.Put, $"/api/sessions/{c.Session1}/parts/{partId}/progress", c.Token,
            new { positionSeconds = 60, percent = 100m });
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
