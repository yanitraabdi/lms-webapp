using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Academy.Application.Auth;
using Academy.Application.Billing;
using Academy.Application.Programs;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Academy.Infrastructure.Programs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>
/// INVERTA M2 acceptance (docs/M2_Program_Enrollment_Spec.md §5–§6):
/// enrollment only via verified webhook, linear lock, idempotency, retention on revoke.
/// </summary>
public class ProgramEnrollmentTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    // ---- public landing ----

    [Fact]
    public async Task Public_program_is_visible_without_auth_and_hides_internals()
    {
        await Seed();
        var p = await _client.GetFromJsonAsync<PublicProgramDto>($"/api/programs/{ProgramSeeder.Slug}", Json);

        Assert.NotNull(p);
        Assert.True(p!.SessionCount >= 3);
        Assert.True(p.PriceIdr > 0);
        Assert.Equal(p.SessionCount, p.Sessions.Count);
        // Syllabus is public, but the ordered lock is the product — sessions are ordered.
        Assert.Equal(p.Sessions.OrderBy(s => s.OrderIndex).Select(s => s.Id), p.Sessions.Select(s => s.Id));
    }

    [Fact]
    public async Task Unknown_or_unpublished_program_is_404()
        => Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync("/api/programs/tidak-ada")).StatusCode);

    // ---- the M2 backbone: payment must not grant, the webhook must ----

    [Fact]
    public async Task Checkout_alone_grants_nothing_and_webhook_activates_enrollment()
    {
        await Seed();
        var (token, userId) = await VerifiedUser();
        var programId = await ProgramId();

        // Before enrolling at all: the student view is forbidden.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Authed(HttpMethod.Get, $"/api/me/programs/{programId}", token)).StatusCode);

        // Checkout creates a PendingPayment enrollment — and grants NOTHING (GR-2).
        var session = await Checkout(token, programId);
        Assert.False(string.IsNullOrWhiteSpace(session.CheckoutUrl));
        await WithDb(async db => Assert.Equal(EnrollmentStatus.PendingPayment,
            await db.Enrollments.Where(e => e.UserId == userId && e.ProgramId == programId)
                .Select(e => e.Status).FirstAsync()));

        Assert.Equal(HttpStatusCode.Forbidden,
            (await Authed(HttpMethod.Get, $"/api/me/programs/{programId}", token)).StatusCode);

        // The verified webhook is the only thing that grants access.
        await PayDev(session.ProviderRef);

        var view = await AuthedGet<StudentProgramDto>($"/api/me/programs/{programId}", token);
        Assert.Equal(nameof(EnrollmentStatus.Active), view.EnrollmentStatus);
        Assert.NotEmpty(view.Sessions);
        Assert.True(factory.Email.EnrollmentReceiptCount >= 1);   // receipt sent
    }

    [Fact]
    public async Task Unverified_user_cannot_purchase()
    {
        await Seed();
        var email = $"u{Guid.NewGuid():N}@test.local";
        var res = await _client.PostAsJsonAsync("/api/auth/register",
            new { name = "Belum", email, password = Pw });
        res.EnsureSuccessStatusCode();
        var token = (await res.Content.ReadFromJsonAsync<AuthTokens>())!.AccessToken;

        var enroll = await Authed(HttpMethod.Post, $"/api/programs/{await ProgramId()}/enroll", token, new { });
        Assert.Equal(HttpStatusCode.Forbidden, enroll.StatusCode);
    }

    [Fact]
    public async Task Webhook_replay_is_idempotent()
    {
        await Seed();
        var (token, userId) = await VerifiedUser();
        var programId = await ProgramId();
        var before = factory.Email.EnrollmentReceiptCount;

        var session = await Checkout(token, programId);
        await PayDev(session.ProviderRef);
        await PayDev(session.ProviderRef);   // replay
        await PayDev(session.ProviderRef);   // replay again

        await WithDb(async db =>
        {
            var enrollments = await db.Enrollments
                .Where(e => e.UserId == userId && e.ProgramId == programId).ToListAsync();
            Assert.Single(enrollments);                                  // exactly one enrollment
            Assert.Equal(EnrollmentStatus.Active, enrollments[0].Status);
        });
        Assert.Equal(before + 1, factory.Email.EnrollmentReceiptCount);  // exactly one receipt
    }

    // ---- linear lock ----

    [Fact]
    public async Task First_session_is_available_and_the_rest_are_locked()
    {
        await Seed();
        var (token, _) = await VerifiedUser();
        var programId = await ProgramId();
        await Enroll(token, programId);

        var view = await AuthedGet<StudentProgramDto>($"/api/me/programs/{programId}", token);
        var ordered = view.Sessions.OrderBy(s => s.OrderIndex).ToList();

        Assert.Equal(SessionState.Available, ordered[0].State);
        Assert.All(ordered.Skip(1), s => Assert.Equal(SessionState.Locked, s.State));
        Assert.Equal(ordered[0].Id, view.NextSessionId);
        Assert.Equal(0, view.CompletedCount);
    }

    [Fact]
    public async Task Completing_a_session_unlocks_exactly_the_next_one()
    {
        await Seed();
        var (token, userId) = await VerifiedUser();
        var programId = await ProgramId();
        await Enroll(token, programId);

        var ordered = (await AuthedGet<StudentProgramDto>($"/api/me/programs/{programId}", token))
            .Sessions.OrderBy(s => s.OrderIndex).ToList();

        // The gate refuses session 2 while session 1 is incomplete.
        Assert.False(await CanAccess(userId, ordered[1].Id));

        await CompleteSession(userId, ordered[0].Id);

        Assert.True(await CanAccess(userId, ordered[1].Id));
        Assert.False(await CanAccess(userId, ordered[2].Id));   // and only the next one

        var after = await AuthedGet<StudentProgramDto>($"/api/me/programs/{programId}", token);
        var afterOrdered = after.Sessions.OrderBy(s => s.OrderIndex).ToList();
        Assert.Equal(SessionState.Completed, afterOrdered[0].State);
        Assert.Equal(SessionState.Available, afterOrdered[1].State);
        Assert.Equal(SessionState.Locked, afterOrdered[2].State);
        Assert.Equal(1, after.CompletedCount);
        Assert.Equal(afterOrdered[1].Id, after.NextSessionId);
    }

    [Fact]
    public async Task Access_gate_refuses_a_user_who_is_not_enrolled()
    {
        await Seed();
        var (_, userId) = await VerifiedUser();
        var programId = await ProgramId();

        var firstSessionId = await WithDbResult(db => db.ProgramSessions
            .Where(s => s.ProgramId == programId).OrderBy(s => s.OrderIndex)
            .Select(s => s.Id).FirstAsync());

        // Not enrolled → even the first session is closed (GR-1: both halves must hold).
        Assert.False(await CanAccess(userId, firstSessionId));
    }

    [Fact]
    public async Task Locked_session_hides_live_join_details()
    {
        await Seed();
        var (token, _) = await VerifiedUser();
        var programId = await ProgramId();
        await Enroll(token, programId);

        var view = await AuthedGet<StudentProgramDto>($"/api/me/programs/{programId}", token);
        var live = view.Sessions.First(s => s.Type == nameof(SessionType.Live));
        Assert.Equal(SessionState.Locked, live.State);
        Assert.Null(live.JoinUrl);      // withheld while locked
        Assert.NotNull(live.Title);     // but still visible (KAK §9.3 R4)
    }

    // ---- retention ----

    [Fact]
    public async Task Revoking_an_enrollment_removes_access_but_deletes_nothing()
    {
        await Seed();
        var (token, userId) = await VerifiedUser();
        var programId = await ProgramId();
        await Enroll(token, programId);

        var ordered = (await AuthedGet<StudentProgramDto>($"/api/me/programs/{programId}", token))
            .Sessions.OrderBy(s => s.OrderIndex).ToList();
        await CompleteSession(userId, ordered[0].Id);

        var enrollmentId = await WithDbResult(db => db.Enrollments
            .Where(e => e.UserId == userId && e.ProgramId == programId).Select(e => e.Id).FirstAsync());

        var admin = await AdminToken();
        (await Authed(HttpMethod.Post, $"/api/admin/enrollments/{enrollmentId}/revoke", admin))
            .EnsureSuccessStatusCode();

        // Access is gone …
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Authed(HttpMethod.Get, $"/api/me/programs/{programId}", token)).StatusCode);
        Assert.False(await CanAccess(userId, ordered[0].Id));

        // … but the learner's record is intact (GR-7).
        await WithDb(async db =>
        {
            Assert.True(await db.SessionCompletions.AnyAsync(c => c.UserId == userId));
            Assert.True(await db.Enrollments.AnyAsync(e => e.Id == enrollmentId));
        });
    }

    [Fact]
    public async Task An_admin_can_restore_access_after_revoking_it()
    {
        // Revoking has to be reversible: an admin can revoke the wrong learner, and a payment
        // dispute can be resolved in the learner's favour. Restoring returns the SAME enrollment
        // to Active — it never creates a second one — and the progress that survived the revoke
        // is still there afterwards.
        await Seed();
        var (token, userId) = await VerifiedUser();
        var programId = await ProgramId();
        await Enroll(token, programId);

        var ordered = (await AuthedGet<StudentProgramDto>($"/api/me/programs/{programId}", token))
            .Sessions.OrderBy(s => s.OrderIndex).ToList();
        await CompleteSession(userId, ordered[0].Id);

        var enrollmentId = await WithDbResult(db => db.Enrollments
            .Where(e => e.UserId == userId && e.ProgramId == programId).Select(e => e.Id).FirstAsync());
        var email = await WithDbResult(db => db.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstAsync());

        var admin = await AdminToken();
        (await Authed(HttpMethod.Post, $"/api/admin/enrollments/{enrollmentId}/revoke", admin))
            .EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Authed(HttpMethod.Get, $"/api/me/programs/{programId}", token)).StatusCode);

        // Restore.
        (await Authed(HttpMethod.Post, "/api/admin/enrollments/grant", admin, new { email, programId }))
            .EnsureSuccessStatusCode();

        (await Authed(HttpMethod.Get, $"/api/me/programs/{programId}", token)).EnsureSuccessStatusCode();
        Assert.True(await CanAccess(userId, ordered[0].Id));

        await WithDb(async db =>
        {
            var enrollments = await db.Enrollments
                .Where(e => e.UserId == userId && e.ProgramId == programId).ToListAsync();
            Assert.Single(enrollments);                                  // restored, not duplicated
            Assert.Equal(enrollmentId, enrollments[0].Id);
            Assert.Equal(EnrollmentStatus.Active, enrollments[0].Status);
            Assert.True(await db.SessionCompletions.AnyAsync(c => c.UserId == userId));
        });
    }

    [Fact]
    public async Task A_replayed_payment_webhook_cannot_resurrect_a_revoked_enrollment()
    {
        // The reason reinstatement is a separate rule from the webhook's transition table. If the
        // two were merged, a late or replayed paid event for a refunded learner would hand access
        // straight back, with no human involved — exactly what GR-2 exists to prevent.
        await Seed();
        var (token, userId) = await VerifiedUser();
        var programId = await ProgramId();

        var session = await Checkout(token, programId);
        await PayDev(session.ProviderRef);

        var enrollmentId = await WithDbResult(db => db.Enrollments
            .Where(e => e.UserId == userId && e.ProgramId == programId).Select(e => e.Id).FirstAsync());

        var admin = await AdminToken();
        (await Authed(HttpMethod.Post, $"/api/admin/enrollments/{enrollmentId}/revoke", admin))
            .EnsureSuccessStatusCode();

        await PayDev(session.ProviderRef);   // the replay

        await WithDb(async db => Assert.Equal(EnrollmentStatus.Revoked,
            await db.Enrollments.Where(e => e.Id == enrollmentId).Select(e => e.Status).FirstAsync()));
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Authed(HttpMethod.Get, $"/api/me/programs/{programId}", token)).StatusCode);
    }

    // ---- admin ----

    [Fact]
    public async Task Admin_can_author_and_reorder_sessions()
    {
        await Seed();
        var admin = await AdminToken();
        var created = await PostJson<AdminProgramDto>("/api/admin/programs", admin, new
        {
            name = "Program Uji", slug = (string?)null, description = "Uji", summary = (string?)null,
            priceIdr = 250000m, published = false,
        });

        async Task<AdminSessionDto> AddSession(string title, int order) => await PostJson<AdminSessionDto>(
            $"/api/admin/programs/{created.Id}/sessions", admin,
            new { type = "Video", title, description = (string?)null, orderIndex = order,
                  providerAssetId = "sample", durationSeconds = 600,
                  scheduledAt = (DateTimeOffset?)null, liveMode = (string?)null,
                  joinUrl = (string?)null, location = (string?)null, assessmentId = (Guid?)null });

        var s1 = await AddSession("Satu", 1);
        var s2 = await AddSession("Dua", 2);
        var s3 = await AddSession("Tiga", 3);

        // Reverse the order — the two-phase write must survive UNIQUE(program_id, order_index).
        (await Authed(HttpMethod.Post, $"/api/admin/programs/{created.Id}/sessions/reorder", admin,
            new { sessionIdsInOrder = new[] { s3.Id, s2.Id, s1.Id } })).EnsureSuccessStatusCode();

        var after = await AuthedGet<List<AdminSessionDto>>($"/api/admin/programs/{created.Id}/sessions", admin);
        Assert.Equal([s3.Id, s2.Id, s1.Id], after.OrderBy(s => s.OrderIndex).Select(s => s.Id));
    }

    [Fact]
    public async Task Program_with_enrollments_cannot_be_deleted()
    {
        await Seed();
        var (token, _) = await VerifiedUser();
        var programId = await ProgramId();
        await Enroll(token, programId);

        var admin = await AdminToken();
        var res = await Authed(HttpMethod.Delete, $"/api/admin/programs/{programId}", admin);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Admin_endpoints_reject_a_normal_user()
    {
        await Seed();
        var (token, _) = await VerifiedUser();
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Authed(HttpMethod.Get, "/api/admin/programs", token)).StatusCode);
    }

    // ---- helpers ----

    private async Task Seed()
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ProgramSeeder>().SeedAsync();
    }

    private Task<Guid> ProgramId() =>
        WithDbResult(db => db.Programs.Where(p => p.Slug == ProgramSeeder.Slug).Select(p => p.Id).FirstAsync());

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

    private async Task<CheckoutSession> Checkout(string token, Guid programId)
    {
        var res = await Authed(HttpMethod.Post, $"/api/programs/{programId}/enroll", token, new { });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<CheckoutSession>(Json))!;
    }

    private Task PayDev(string providerRef) =>
        _client.PostAsync($"/api/dev/payments/{Uri.EscapeDataString(providerRef)}/succeed", null);

    /// <summary>Full purchase: checkout then the verified webhook that actually grants access.</summary>
    private async Task Enroll(string token, Guid programId)
    {
        var session = await Checkout(token, programId);
        (await PayDevChecked(session.ProviderRef)).EnsureSuccessStatusCode();
    }

    private Task<HttpResponseMessage> PayDevChecked(string providerRef) =>
        _client.PostAsync($"/api/dev/payments/{Uri.EscapeDataString(providerRef)}/succeed", null);

    /// <summary>Drives the real completion service, exactly as M3/M4 will.</summary>
    private async Task CompleteSession(Guid userId, Guid sessionId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // A video session with no gating test completes at the watch threshold.
        db.WatchProgress.Add(new WatchProgress
        {
            Id = Guid.CreateVersion7(), UserId = userId, SessionId = sessionId,
            PercentComplete = 100m, ResumePositionSeconds = 600, LastWatchedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var completion = scope.ServiceProvider.GetRequiredService<ISessionCompletionService>();
        Assert.True(await completion.TryCompleteAsync(userId, sessionId));
    }

    private async Task<bool> CanAccess(Guid userId, Guid sessionId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISessionAccessService>()
            .CanAccessAsync(userId, sessionId);
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
