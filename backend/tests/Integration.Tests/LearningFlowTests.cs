using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Academy.Application.Auth;
using Academy.Application.Billing;
using Academy.Application.Catalog;
using Academy.Application.Learning;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Billing;
using Academy.Infrastructure.Catalog;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

public class LearningFlowTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    [Fact]
    public async Task Playback_gates_on_entitlement()
    {
        await Seed();
        var (token, _) = await VerifiedUser();

        var previewId = await ModuleId(m => m.IsPreview);
        var lockedId = await ModuleId(m => !m.IsPreview && m.RequiredPlanTier >= 1);

        // Free user: preview plays, paid module is forbidden.
        var preview = await Authed(HttpMethod.Post, $"/api/modules/{previewId}/playback", token);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var ticket = (await preview.Content.ReadFromJsonAsync<PlaybackTicketDto>(Json))!;
        Assert.False(string.IsNullOrWhiteSpace(ticket.Url));
        Assert.True(ticket.ExpiresAt > DateTimeOffset.UtcNow);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await Authed(HttpMethod.Post, $"/api/modules/{lockedId}/playback", token)).StatusCode);

        // After subscribing, the paid module plays.
        await SubscribeBeginner(token);
        Assert.Equal(HttpStatusCode.OK,
            (await Authed(HttpMethod.Post, $"/api/modules/{lockedId}/playback", token)).StatusCode);
    }

    [Fact]
    public async Task Progress_auto_completes_at_threshold()
    {
        await Seed();
        var (token, _) = await VerifiedUser();
        await SubscribeBeginner(token);
        var moduleId = await ModuleId(m => !m.IsPreview && m.RequiredPlanTier == 1);

        var half = await SaveProgress(token, moduleId, 300, 50m);
        Assert.False(half.Completed);

        var done = await SaveProgress(token, moduleId, 580, 95m);
        Assert.True(done.Completed);
        Assert.NotNull(done.CompletedAt);

        // Percent is monotonic — a later seek-back doesn't undo completion.
        var back = await SaveProgress(token, moduleId, 10, 5m);
        Assert.True(back.Completed);
        Assert.Equal(95m, back.PercentComplete);
    }

    [Fact]
    public async Task Completing_a_level_issues_an_immutable_certificate()
    {
        await Seed();
        var (token, userId) = await VerifiedUser();
        await SubscribeBeginner(token);

        var basicModuleIds = await BasicModuleIds();
        foreach (var id in basicModuleIds)
            await SaveProgress(token, id, 600, 100m);

        var certs = await AuthedGet<List<CertificateDto>>("/api/me/certificates", token);
        var basic = Assert.Single(certs, c => c.LevelName == "Basic");
        Assert.Equal(basicModuleIds.Count, basic.ModuleCount);

        // Add a NEW published module to Basic AFTER issuance.
        await AddBonusBasicModule();

        // Cert is unchanged (immutable) and the level still displays 100% (GR-6).
        var certsAfter = await AuthedGet<List<CertificateDto>>("/api/me/certificates", token);
        Assert.Single(certsAfter, c => c.LevelName == "Basic");
        Assert.Equal(basicModuleIds.Count, certsAfter.First(c => c.LevelName == "Basic").ModuleCount);

        var dash = await AuthedGet<DashboardDto>("/api/me/dashboard", token);
        var basicLevel = dash.Levels.First(l => l.Slug == "basic");
        Assert.True(basicLevel.Certified);
        Assert.Equal(100m, basicLevel.Percent);
        Assert.Equal(basicLevel.PublishedCount, basicLevel.CompletedCount); // displays complete despite new module
    }

    [Fact]
    public async Task Certificate_verifies_publicly_and_renders_pdf()
    {
        await Seed();
        var (token, _) = await VerifiedUser();
        await SubscribeBeginner(token);
        foreach (var id in await BasicModuleIds())
            await SaveProgress(token, id, 600, 100m);

        var cert = (await AuthedGet<List<CertificateDto>>("/api/me/certificates", token)).First();

        // Public verification — no auth.
        var verify = await _client.GetFromJsonAsync<CertificateVerifyDto>(
            $"/api/certificates/verify/{cert.VerificationCode}", Json);
        Assert.True(verify!.Valid);
        Assert.Equal("Basic", verify.LevelName);
        Assert.False(string.IsNullOrWhiteSpace(verify.RecipientName));

        var bogus = await _client.GetFromJsonAsync<CertificateVerifyDto>("/api/certificates/verify/AIPA-NOPE", Json);
        Assert.False(bogus!.Valid);

        // PDF renders (owner only).
        var pdf = await Authed(HttpMethod.Get, $"/api/certificates/{cert.Id}/pdf", token);
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 800);
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }

    [Fact]
    public async Task Certificate_and_progress_survive_expiry()
    {
        await Seed();
        var (token, userId) = await VerifiedUser();
        await SubscribeBeginner(token);
        foreach (var id in await BasicModuleIds())
            await SaveProgress(token, id, 600, 100m);
        Assert.Equal(1, await ActiveTier(userId));

        // Expire the subscription.
        await WithDb(async db =>
        {
            var sub = await db.Subscriptions.Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CurrentPeriodEnd).FirstAsync();
            sub.CurrentPeriodEnd = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IBillingReconciler>().ReconcileAsync();

        Assert.Null(await ActiveTier(userId)); // access revoked

        // Certificate + progress are retained and still verifiable.
        var certs = await AuthedGet<List<CertificateDto>>("/api/me/certificates", token);
        Assert.Single(certs, c => c.LevelName == "Basic");
        await WithDb(async db => Assert.True(await db.WatchProgress.AnyAsync(w => w.UserId == userId && w.Completed)));
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

    private async Task<ModuleProgressDto> SaveProgress(string token, Guid moduleId, int pos, decimal pct)
    {
        var res = await Authed(HttpMethod.Put, $"/api/modules/{moduleId}/progress", token,
            new { positionSeconds = pos, percent = pct });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ModuleProgressDto>(Json))!;
    }

    private async Task<Guid> ModuleId(System.Linq.Expressions.Expression<Func<Module, bool>> predicate)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Modules.Where(m => m.Status == ModuleStatus.Published).Where(predicate)
            .Select(m => m.Id).FirstAsync();
    }

    private async Task<List<Guid>> BasicModuleIds()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Modules
            .Where(m => m.Status == ModuleStatus.Published && m.Track.Level.Slug == "basic")
            .Select(m => m.Id).ToListAsync();
    }

    private async Task AddBonusBasicModule()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var trackId = await db.Tracks.Where(t => t.Level.Slug == "basic").Select(t => t.Id).FirstAsync();
        db.Modules.Add(new Module
        {
            Id = Guid.CreateVersion7(),
            TrackId = trackId,
            Title = "Bonus modul",
            Slug = $"bonus-{Guid.NewGuid():N}",
            Description = "Bonus.",
            DurationSeconds = 300,
            OrderIndex = 99,
            Status = ModuleStatus.Published,
            IsPreview = false,
            RequiredPlanTier = 1,
            PublishedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task<int?> ActiveTier(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IEntitlementService>().GetActiveTierAsync(userId);
    }

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
