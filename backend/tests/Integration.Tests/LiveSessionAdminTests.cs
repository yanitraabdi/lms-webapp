using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Academy.Application.Auth;
using Academy.Application.Billing;
using Academy.Application.Programs;
using Academy.Domain;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>
/// INVERTA M5 acceptance: admin-marked attendance completes a live session and advances the lock,
/// H-1 reminders are idempotent, and the operational dashboards work (including the support-grant
/// path and the proctor trail used for disputes).
/// </summary>
public class LiveSessionAdminTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    // ---- attendance drives the linear lock ----

    [Fact]
    public async Task Roster_lists_every_actively_enrolled_learner()
    {
        var c = await SetUp(learners: 3);
        var roster = await AuthedGet<AttendanceRosterDto>(
            $"/api/admin/sessions/{c.LiveSessionId}/attendance", c.Admin);

        Assert.Equal(3, roster.EnrolledCount);
        Assert.Equal(0, roster.AttendedCount);
        Assert.All(roster.Rows, r => Assert.False(r.Attended));
        Assert.All(roster.Rows, r => Assert.False(r.SessionCompleted));
    }

    [Fact]
    public async Task Marking_attendance_completes_the_live_session_and_unlocks_the_next()
    {
        var c = await SetUp(learners: 2);
        var learner = c.Learners[0];

        // The live session is session 1, so it is available; session 2 is locked until attendance.
        Assert.False(await CanAccess(learner.UserId, c.NextSessionId));

        (await Authed(HttpMethod.Post, $"/api/admin/sessions/{c.LiveSessionId}/attendance", c.Admin,
            new { userIds = new[] { learner.UserId }, attended = true })).EnsureSuccessStatusCode();

        Assert.True(await CanAccess(learner.UserId, c.NextSessionId));

        var roster = await AuthedGet<AttendanceRosterDto>(
            $"/api/admin/sessions/{c.LiveSessionId}/attendance", c.Admin);
        var row = roster.Rows.Single(r => r.UserId == learner.UserId);
        Assert.True(row.Attended);
        Assert.True(row.SessionCompleted);

        // The other learner is untouched.
        Assert.False(await CanAccess(c.Learners[1].UserId, c.NextSessionId));
    }

    [Fact]
    public async Task Mark_all_completes_the_whole_cohort()
    {
        var c = await SetUp(learners: 3);

        (await Authed(HttpMethod.Post, $"/api/admin/sessions/{c.LiveSessionId}/attendance/all", c.Admin))
            .EnsureSuccessStatusCode();

        var roster = await AuthedGet<AttendanceRosterDto>(
            $"/api/admin/sessions/{c.LiveSessionId}/attendance", c.Admin);
        Assert.Equal(3, roster.AttendedCount);
        foreach (var l in c.Learners) Assert.True(await CanAccess(l.UserId, c.NextSessionId));
    }

    [Fact]
    public async Task Unmarking_attendance_does_not_un_complete_the_session()
    {
        var c = await SetUp(learners: 1);
        var learner = c.Learners[0];

        await Authed(HttpMethod.Post, $"/api/admin/sessions/{c.LiveSessionId}/attendance", c.Admin,
            new { userIds = new[] { learner.UserId }, attended = true });
        Assert.True(await CanAccess(learner.UserId, c.NextSessionId));

        // Correcting an admin mistake clears the flag but must NOT revoke earned progress (GR-8).
        (await Authed(HttpMethod.Post, $"/api/admin/sessions/{c.LiveSessionId}/attendance", c.Admin,
            new { userIds = new[] { learner.UserId }, attended = false })).EnsureSuccessStatusCode();

        var roster = await AuthedGet<AttendanceRosterDto>(
            $"/api/admin/sessions/{c.LiveSessionId}/attendance", c.Admin);
        Assert.False(roster.Rows.Single(r => r.UserId == learner.UserId).Attended);
        Assert.True(roster.Rows.Single(r => r.UserId == learner.UserId).SessionCompleted);
        Assert.True(await CanAccess(learner.UserId, c.NextSessionId));
    }

    [Fact]
    public async Task Attendance_is_rejected_for_a_non_live_session()
    {
        var c = await SetUp(learners: 1);
        var res = await Authed(HttpMethod.Get, $"/api/admin/sessions/{c.NextSessionId}/attendance", c.Admin);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---- H-1 reminder ----

    [Fact]
    public async Task Reminder_is_sent_once_per_session_even_across_repeated_sweeps()
    {
        var c = await SetUp(learners: 2, liveInHours: 12);   // inside the 24h window

        // The sweep is global, so assert on THIS session rather than the run total.
        await PostJson<ReminderRunResultDto>("/api/admin/live-reminders/run", c.Admin, new { });
        Assert.Equal(2, factory.Email.LiveRemindersFor(c.LiveSessionTitle));   // one per enrolled learner

        var stamped = await WithDbResult(db => db.ProgramSessions
            .Where(s => s.Id == c.LiveSessionId).Select(s => s.ReminderSentAt).FirstAsync());
        Assert.NotNull(stamped);

        // Re-running must not re-send — reminder_sent_at is claimed before the first send.
        await PostJson<ReminderRunResultDto>("/api/admin/live-reminders/run", c.Admin, new { });
        Assert.Equal(2, factory.Email.LiveRemindersFor(c.LiveSessionTitle));   // still 2, not 4

        var stampedAgain = await WithDbResult(db => db.ProgramSessions
            .Where(s => s.Id == c.LiveSessionId).Select(s => s.ReminderSentAt).FirstAsync());
        Assert.Equal(stamped, stampedAgain);
    }

    [Fact]
    public async Task A_session_outside_the_window_gets_no_reminder()
    {
        var c = await SetUp(learners: 1, liveInHours: 96);    // far beyond H-1
        await PostJson<ReminderRunResultDto>("/api/admin/live-reminders/run", c.Admin, new { });

        Assert.Equal(0, factory.Email.LiveRemindersFor(c.LiveSessionTitle));
        await WithDb(async db => Assert.Null(
            await db.ProgramSessions.Where(s => s.Id == c.LiveSessionId)
                .Select(s => s.ReminderSentAt).FirstAsync()));
    }

    // ---- operational dashboards ----

    [Fact]
    public async Task Enrollment_dashboard_lists_filters_and_shows_progress()
    {
        var c = await SetUp(learners: 2);
        var learner = c.Learners[0];
        await Authed(HttpMethod.Post, $"/api/admin/sessions/{c.LiveSessionId}/attendance", c.Admin,
            new { userIds = new[] { learner.UserId }, attended = true });

        var all = await AuthedGet<AdminEnrollmentListDto>(
            $"/api/admin/enrollments?programId={c.ProgramId}", c.Admin);
        Assert.Equal(2, all.Total);

        var mine = all.Items.Single(i => i.UserId == learner.UserId);
        Assert.Equal(nameof(EnrollmentStatus.Active), mine.Status);
        Assert.Equal(1, mine.CompletedSessions);
        Assert.Equal(2, mine.TotalSessions);

        // Search narrows by name/email.
        var searched = await AuthedGet<AdminEnrollmentListDto>(
            $"/api/admin/enrollments?programId={c.ProgramId}&search={Uri.EscapeDataString(learner.Email)}", c.Admin);
        Assert.Equal(1, searched.Total);
    }

    [Fact]
    public async Task Admin_can_grant_an_enrollment_without_a_payment()
    {
        var c = await SetUp(learners: 0);
        var (token, userId) = await VerifiedUser();

        // No payment: the program view is closed.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Authed(HttpMethod.Get, $"/api/me/programs/{c.ProgramId}", token)).StatusCode);

        (await Authed(HttpMethod.Post, "/api/admin/enrollments/grant", c.Admin,
            new { email = await EmailOf(userId), programId = c.ProgramId })).EnsureSuccessStatusCode();

        var view = await AuthedGet<StudentProgramDto>($"/api/me/programs/{c.ProgramId}", token);
        Assert.Equal(nameof(EnrollmentStatus.Active), view.EnrollmentStatus);

        // The exception to "webhook only" is explicitly audited.
        await WithDb(async db => Assert.True(
            await db.AuditLogs.AnyAsync(a => a.Action == "enrollment_granted_manually")));
    }

    [Fact]
    public async Task Granting_twice_is_refused()
    {
        var c = await SetUp(learners: 1);
        var email = await EmailOf(c.Learners[0].UserId);

        var res = await Authed(HttpMethod.Post, "/api/admin/enrollments/grant", c.Admin,
            new { email, programId = c.ProgramId });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Attempt_dashboard_filters_flagged_and_exposes_the_proctor_trail()
    {
        var c = await SetUp(learners: 1);
        var learner = c.Learners[0];

        // Attendance unlocks the final-assessment session so the learner can sit it.
        await Authed(HttpMethod.Post, $"/api/admin/sessions/{c.LiveSessionId}/attendance", c.Admin,
            new { userIds = new[] { learner.UserId }, attended = true });

        var start = await Authed(HttpMethod.Post, $"/api/assessments/{c.AssessmentId}/attempts", learner.Token);
        start.EnsureSuccessStatusCode();
        var attempt = (await start.Content.ReadFromJsonAsync<AttemptIdDto>(Json))!;

        // Two qualifying strikes flag it.
        await Authed(HttpMethod.Post, $"/api/attempts/{attempt.Id}/proctor-events", learner.Token,
            new { kind = "WindowBlur", durationMs = 6000 });
        await Authed(HttpMethod.Post, $"/api/attempts/{attempt.Id}/proctor-events", learner.Token,
            new { kind = "WindowBlur", durationMs = 6000 });

        var flagged = await AuthedGet<AdminAttemptListDto>("/api/admin/attempts?flaggedOnly=true", c.Admin);
        var row = Assert.Single(flagged.Items, i => i.Id == attempt.Id);
        Assert.True(row.ProctorFlagged);
        Assert.Equal(2, row.StrikeCount);

        var detail = await AuthedGet<AdminAttemptDetailDto>($"/api/admin/attempts/{attempt.Id}", c.Admin);
        Assert.True(detail.Events.Count >= 2);
        Assert.Contains(detail.Events, e => e.Kind == nameof(ProctorEventKind.AutoSubmitted));

        // Reinstating from the dashboard clears the flag but keeps every event.
        (await Authed(HttpMethod.Post, $"/api/admin/attempts/{attempt.Id}/reinstate", c.Admin))
            .EnsureSuccessStatusCode();

        var after = await AuthedGet<AdminAttemptDetailDto>($"/api/admin/attempts/{attempt.Id}", c.Admin);
        Assert.False(after.Attempt.ProctorFlagged);
        Assert.True(after.Attempt.Reinstated);
        Assert.Equal(detail.Events.Count, after.Events.Count);
    }

    [Fact]
    public async Task Operational_routes_reject_a_normal_user()
    {
        var c = await SetUp(learners: 1);
        var token = c.Learners[0].Token;

        foreach (var url in new[] { "/api/admin/enrollments", "/api/admin/attempts" })
            Assert.Equal(HttpStatusCode.Forbidden,
                (await Authed(HttpMethod.Get, url, token)).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await Authed(HttpMethod.Get, $"/api/admin/sessions/{c.LiveSessionId}/attendance", token)).StatusCode);
    }

    // ================================================================ helpers

    private record Learner(string Token, Guid UserId, string Email);
    private record Ctx(string Admin, Guid ProgramId, Guid LiveSessionId, string LiveSessionTitle,
                       Guid NextSessionId, Guid AssessmentId, IReadOnlyList<Learner> Learners);
    private record ReminderRunResultDto(int EmailsSent);
    private record AttemptIdDto(Guid Id);

    /// <summary>Program with a live session (order 1) followed by a final assessment (order 2).</summary>
    private async Task<Ctx> SetUp(int learners, int liveInHours = 12)
    {
        var admin = await AdminToken();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var program = await PostJson<AdminProgramDto>("/api/admin/programs", admin, new
        {
            name = $"M5 {suffix}", slug = $"m5-{suffix}", description = "Uji M5",
            summary = (string?)null, priceIdr = 100000m, published = false,
        });

        var live = await PostJson<AdminSessionDto>($"/api/admin/programs/{program.Id}/sessions", admin, new
        {
            type = "Live", title = $"Kelas Live {suffix}", description = (string?)null, orderIndex = 1,
            providerAssetId = (string?)null, durationSeconds = (int?)null,
            scheduledAt = DateTimeOffset.UtcNow.AddHours(liveInHours),
            liveMode = "Zoom", joinUrl = "https://example.com/live", location = (string?)null,
            assessmentId = (Guid?)null,
        });

        // A content-complete final assessment (all three sections populated, full score bands)
        // is needed only because publishing (and therefore enrolling) now requires readiness —
        // the tests in this file don't otherwise exercise the final assessment itself.
        var assessment = await ItpFinal.SeedAsync(factory, "Tes Akhir");

        var next = await PostJson<AdminSessionDto>($"/api/admin/programs/{program.Id}/sessions", admin, new
        {
            type = "FinalAssessment", title = "Tes Akhir", description = (string?)null, orderIndex = 2,
            providerAssetId = (string?)null, durationSeconds = (int?)null,
            scheduledAt = (DateTimeOffset?)null, liveMode = (string?)null,
            joinUrl = (string?)null, location = (string?)null, assessmentId = assessment.AssessmentId,
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

        var list = new List<Learner>();
        for (var i = 0; i < learners; i++)
        {
            var (token, userId) = await VerifiedUser();
            var checkout = await Authed(HttpMethod.Post, $"/api/programs/{program.Id}/enroll", token, new { });
            checkout.EnsureSuccessStatusCode();
            var pay = (await checkout.Content.ReadFromJsonAsync<CheckoutSession>(Json))!;
            (await _client.PostAsync($"/api/dev/payments/{Uri.EscapeDataString(pay.ProviderRef)}/succeed", null))
                .EnsureSuccessStatusCode();
            list.Add(new Learner(token, userId, await EmailOf(userId)));
        }

        return new Ctx(admin, program.Id, live.Id, live.Title, next.Id, assessment.AssessmentId, list);
    }

    private record QuestionIdDto(Guid Id);
    private record AssessmentIdDto(Guid Id);

    private Task<string> EmailOf(Guid userId) =>
        WithDbResult(db => db.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstAsync());

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
