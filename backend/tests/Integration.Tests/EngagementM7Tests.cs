using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Academy.Application.Admin;
using Academy.Application.Auth;
using Academy.Application.Billing;
using Academy.Application.Catalog;
using Academy.Application.Engagement;
using Academy.Application.Learning;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Billing;
using Academy.Infrastructure.Catalog;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>M7 engagement: quiz completion gating (non-retroactive), notes/bookmarks,
/// module ratings, and notification dispatch honoring preferences.</summary>
public class EngagementM7Tests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    // ---- quizzes ----

    [Fact]
    public async Task Quiz_gates_completion_and_passing_unlocks_it()
    {
        await Seed();
        var (token, _) = await VerifiedUser();
        await SubscribeBeginner(token);
        var admin = await AdminToken();
        var moduleId = await CreateStandaloneModule(tier: 1);

        // Author a 2-question quiz that requires both correct (threshold 2).
        await AuthorQuiz(admin, moduleId, passThreshold: 2,
            (prompt: "Q1", choices: ["a", "b"], correct: 0),
            (prompt: "Q2", choices: ["x", "y"], correct: 1));

        // Learner sees the quiz, but WITHOUT the answer key.
        var quiz = await AuthedGet<QuizDto>($"/api/modules/{moduleId}/quiz", token);
        Assert.Equal(2, quiz.QuestionCount);
        Assert.False(quiz.Passed);

        // Watching to 100% does NOT complete the module while the quiz is unsatisfied.
        var watched = await SaveProgress(token, moduleId, 600, 100m);
        Assert.False(watched.Completed);

        // Wrong answers: not passed, still not completed.
        var failed = await SubmitQuiz(token, moduleId, [1, 0]);
        Assert.False(failed.Passed);
        Assert.False(failed.ModuleCompleted);

        // Correct answers: passes the gate → module completes.
        var passed = await SubmitQuiz(token, moduleId, [0, 1]);
        Assert.True(passed.Passed);
        Assert.Equal(2, passed.Score);
        Assert.True(passed.ModuleCompleted);

        // Completion is now persisted (re-saving progress reflects it).
        var after = await SaveProgress(token, moduleId, 600, 100m);
        Assert.True(after.Completed);
    }

    [Fact]
    public async Task Quiz_added_after_completion_does_not_uncomplete_module()
    {
        await Seed();
        var (token, _) = await VerifiedUser();
        await SubscribeBeginner(token);
        var admin = await AdminToken();
        var moduleId = await CreateStandaloneModule(tier: 1);

        // Complete by watching (no quiz yet).
        Assert.True((await SaveProgress(token, moduleId, 600, 100m)).Completed);

        // Admin now attaches a quiz — must NOT retroactively un-complete the module.
        await AuthorQuiz(admin, moduleId, passThreshold: 1, (prompt: "Q1", choices: ["a", "b"], correct: 0));

        var after = await SaveProgress(token, moduleId, 600, 100m);
        Assert.True(after.Completed);
    }

    [Fact]
    public async Task Inactive_or_empty_quiz_does_not_gate_completion()
    {
        await Seed();
        var (token, _) = await VerifiedUser();
        await SubscribeBeginner(token);
        var admin = await AdminToken();
        var moduleId = await CreateStandaloneModule(tier: 1);

        // Quiz exists but is inactive → no gate.
        await AuthorQuiz(admin, moduleId, passThreshold: 1, isActive: false,
            (prompt: "Q1", choices: ["a", "b"], correct: 0));

        Assert.True((await SaveProgress(token, moduleId, 600, 100m)).Completed);
    }

    // ---- notes & bookmarks ----

    [Fact]
    public async Task Notes_and_bookmarks_create_list_ordered_and_delete()
    {
        await Seed();
        var (token, _) = await VerifiedUser();
        await SubscribeBeginner(token);
        var moduleId = await CreateStandaloneModule(tier: 1);

        await CreateNote(token, moduleId, 120, "Note", "kedua");
        await CreateNote(token, moduleId, 30, "Bookmark", null);

        var notes = await AuthedGet<List<VideoNoteDto>>($"/api/modules/{moduleId}/notes", token);
        Assert.Equal(2, notes.Count);
        Assert.Equal(30, notes[0].TimestampSeconds);          // ordered by timestamp
        Assert.Equal("Bookmark", notes[0].Type);
        Assert.Equal(120, notes[1].TimestampSeconds);
        Assert.Equal("kedua", notes[1].Text);

        var del = await Authed(HttpMethod.Delete, $"/api/notes/{notes[0].Id}", token);
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
        Assert.Single(await AuthedGet<List<VideoNoteDto>>($"/api/modules/{moduleId}/notes", token));
    }

    // ---- module ratings ----

    [Fact]
    public async Task Module_rating_upserts_and_aggregates()
    {
        await Seed();
        var (a, _) = await VerifiedUser();
        var (b, _) = await VerifiedUser();
        await SubscribeBeginner(a);
        var moduleId = await CreateStandaloneModule(tier: 1);

        await RateModule(a, moduleId, 5, "mantap");
        await RateModule(b, moduleId, 3, null);

        var summary = await AuthedGet<ModuleFeedbackDto>($"/api/modules/{moduleId}/feedback", a);
        Assert.Equal(5, summary.MyRating);
        Assert.Equal("mantap", summary.MyComment);
        Assert.Equal(2, summary.Count);
        Assert.Equal(4.0, summary.AverageRating);

        // Re-rating the same module updates in place (no duplicate row).
        await RateModule(a, moduleId, 1, "berubah pikiran");
        var updated = await AuthedGet<ModuleFeedbackDto>($"/api/modules/{moduleId}/feedback", a);
        Assert.Equal(1, updated.MyRating);
        Assert.Equal(2, updated.Count);
        Assert.Equal(2.0, updated.AverageRating);
    }

    // ---- notifications ----

    [Fact]
    public async Task Default_preferences_returned_with_full_matrix()
    {
        await Seed();
        var (token, _) = await VerifiedUser();

        var prefs = await AuthedGet<List<NotificationPrefDto>>("/api/notifications/preferences", token);
        Assert.Equal(8, prefs.Count); // 4 categories × 2 channels

        Assert.True(Pref(prefs, "billing", "InApp"));   // billing on by default
        Assert.True(Pref(prefs, "progress", "Email"));
        Assert.False(Pref(prefs, "promo", "InApp"));     // promo off by default
        Assert.False(Pref(prefs, "content", "Email"));   // content email off by default
    }

    [Fact]
    public async Task Subscribing_dispatches_in_app_and_email_notification_by_default()
    {
        await Seed();
        var (token, _) = await VerifiedUser();
        var before = factory.Email.NotificationEmailCount;

        await SubscribeBeginner(token);

        var list = await AuthedGet<NotificationListDto>("/api/notifications", token);
        Assert.Contains(list.Items, n => n.Type == "subscription_activated");
        Assert.True(list.UnreadCount >= 1);
        Assert.True(factory.Email.NotificationEmailCount > before); // billing email default on

        // Marking all read clears the unread count.
        (await Authed(HttpMethod.Post, "/api/notifications/read-all", token)).EnsureSuccessStatusCode();
        Assert.Equal(0, (await AuthedGet<NotificationListDto>("/api/notifications", token)).UnreadCount);
    }

    [Fact]
    public async Task Disabled_preference_suppresses_both_channels()
    {
        await Seed();
        var (token, _) = await VerifiedUser();

        // Turn billing OFF on both channels before the event fires.
        var update = new UpdatePreferencesRequest(
        [
            new NotificationPrefDto("billing", "InApp", false),
            new NotificationPrefDto("billing", "Email", false),
        ]);
        (await Authed(HttpMethod.Put, "/api/notifications/preferences", token, update)).EnsureSuccessStatusCode();

        var before = factory.Email.NotificationEmailCount;
        await SubscribeBeginner(token);

        var list = await AuthedGet<NotificationListDto>("/api/notifications", token);
        Assert.DoesNotContain(list.Items, n => n.Type == "subscription_activated");
        Assert.Equal(before, factory.Email.NotificationEmailCount); // no notification email sent
    }

    [Fact]
    public async Task Certificate_issuance_dispatches_progress_notification()
    {
        await Seed();
        var (token, _) = await VerifiedUser();
        await SubscribeBeginner(token);

        foreach (var id in await BasicModuleIds())
            await SaveProgress(token, id, 600, 100m);

        var list = await AuthedGet<NotificationListDto>("/api/notifications", token);
        Assert.Contains(list.Items, n => n.Type == "certificate_issued");
    }

    // ---- helpers ----

    private async Task Seed()
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PlansSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync();
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
        (await _client.PostAsJsonAsync("/api/auth/register", new { name = "Adm", email, password = Pw })).EnsureSuccessStatusCode();
        await WithDb(async db =>
        {
            var u = await db.Users.FirstAsync(x => x.Email == email);
            u.Role = UserRole.Admin;
            await db.SaveChangesAsync();
        });
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Pw });
        return (await login.Content.ReadFromJsonAsync<AuthTokens>())!.AccessToken;
    }

    private async Task SubscribeBeginner(string token)
    {
        var plans = (await _client.GetFromJsonAsync<List<PlanDto>>("/api/plans", Json))!;
        var beginner = plans.First(p => p.TierLevel == 1);
        var res = await Authed(HttpMethod.Post, "/api/subscriptions/checkout", token,
            new { planId = beginner.Id, billingCycle = "Monthly" });
        var session = (await res.Content.ReadFromJsonAsync<CheckoutSession>(Json))!;
        (await _client.PostAsync($"/api/dev/payments/{Uri.EscapeDataString(session.ProviderRef)}/succeed", null))
            .EnsureSuccessStatusCode();
    }

    /// <summary>Creates an isolated published level/track/module at the given tier so quiz tests
    /// don't contaminate the shared seed (which the certificate tests complete).</summary>
    private async Task<Guid> CreateStandaloneModule(int tier)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var level = new Level { Name = $"QT {suffix}", Slug = $"qt-{suffix}", RequiredPlanTier = tier, OrderIndex = 50, Status = ModuleStatus.Published };
        var track = new Track { Level = level, Name = "T", Slug = $"qt-t-{suffix}", OrderIndex = 0 };
        var module = new Module
        {
            Track = track, Title = "Quiz module", Slug = $"qt-m-{suffix}", Description = "Q",
            DurationSeconds = 600, OrderIndex = 0, Status = ModuleStatus.Published,
            IsPreview = false, RequiredPlanTier = tier, PublishedAt = DateTimeOffset.UtcNow,
        };
        db.Modules.Add(module);
        await db.SaveChangesAsync();
        return module.Id;
    }

    private async Task AuthorQuiz(string adminToken, Guid moduleId, int passThreshold, bool isActive,
        params (string prompt, string[] choices, int correct)[] questions)
    {
        var body = new UpsertQuizRequest(passThreshold, isActive,
            questions.Select(q => new QuizQuestionInput(q.prompt, q.choices, q.correct)).ToList());
        (await Authed(HttpMethod.Put, $"/api/admin/modules/{moduleId}/quiz", adminToken, body)).EnsureSuccessStatusCode();
    }

    private Task AuthorQuiz(string adminToken, Guid moduleId, int passThreshold,
        params (string prompt, string[] choices, int correct)[] questions)
        => AuthorQuiz(adminToken, moduleId, passThreshold, true, questions);

    private async Task<QuizResultDto> SubmitQuiz(string token, Guid moduleId, int[] answers)
    {
        var res = await Authed(HttpMethod.Post, $"/api/modules/{moduleId}/quiz/attempt", token, new { answers });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<QuizResultDto>(Json))!;
    }

    private async Task CreateNote(string token, Guid moduleId, int ts, string type, string? text)
        => (await Authed(HttpMethod.Post, $"/api/modules/{moduleId}/notes", token,
            new { timestampSeconds = ts, type, text })).EnsureSuccessStatusCode();

    private async Task RateModule(string token, Guid moduleId, int rating, string? comment)
        => (await Authed(HttpMethod.Put, $"/api/modules/{moduleId}/feedback", token,
            new { rating, comment })).EnsureSuccessStatusCode();

    private async Task<ModuleProgressDto> SaveProgress(string token, Guid moduleId, int pos, decimal pct)
    {
        var res = await Authed(HttpMethod.Put, $"/api/modules/{moduleId}/progress", token,
            new { positionSeconds = pos, percent = pct });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ModuleProgressDto>(Json))!;
    }

    private async Task<List<Guid>> BasicModuleIds()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Modules
            .Where(m => m.Status == ModuleStatus.Published && m.Track.Level.Slug == "basic")
            .Select(m => m.Id).ToListAsync();
    }

    private static bool Pref(List<NotificationPrefDto> prefs, string cat, string ch)
        => prefs.First(p => p.Category == cat && p.Channel == ch).Enabled;

    private Task<HttpResponseMessage> Authed(HttpMethod method, string url, string token, object? body = null)
    {
        var req = new HttpRequestMessage(method, url) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };
        if (body is not null) req.Content = JsonContent.Create(body);
        return _client.SendAsync(req);
    }

    private async Task<T> AuthedGet<T>(string url, string token)
    {
        var res = await Authed(HttpMethod.Get, url, token);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task WithDb(Func<AppDbContext, Task> action)
    {
        using var scope = factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static string TokenFrom(string? url)
    {
        Assert.NotNull(url);
        var i = url!.IndexOf("token=", StringComparison.Ordinal);
        Assert.True(i >= 0, "verify URL has no token");
        return url[(i + "token=".Length)..];
    }
}
