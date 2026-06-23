using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Academy.Application.Admin;
using Academy.Application.Auth;
using Academy.Application.Catalog;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Billing;
using Academy.Infrastructure.Catalog;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResourceDto = Academy.Application.Admin.AdminResourceDto;

namespace Academy.Integration.Tests;

public class AdminCrudTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    [Fact]
    public async Task Curriculum_full_crud_cycle()
    {
        var admin = await TokenWithRole(UserRole.SuperAdmin);

        var cat = await Post<CategoryDto>("/api/admin/categories", admin, new { name = "Tes Kategori", slug = (string?)null });
        var level = await Post<LevelDto>("/api/admin/levels", admin, new { name = "Tes Level", slug = (string?)null, requiredPlanTier = 1, orderIndex = 99, published = true });
        var track = await Post<TrackDto>("/api/admin/tracks", admin, new { levelId = level.Id, name = "Tes Track", slug = (string?)null, orderIndex = 0 });
        var mod = await Post<AdminModuleDetailDto>("/api/admin/modules", admin, new
        {
            trackId = track.Id, categoryId = cat.Id, title = "Tes Modul", slug = (string?)null, description = "deskripsi",
            summary = (string?)null, durationSeconds = 300, providerAssetId = (string?)null, thumbnailUrl = (string?)null,
            orderIndex = 0, isPreview = false, requiredPlanTier = 1, published = true, tagIds = Array.Empty<Guid>(),
        });

        // Published module appears in the public catalog.
        Assert.Contains(await PublicSlugs(), s => s == mod.Slug);

        // Update + read back.
        (await Authed(HttpMethod.Put, $"/api/admin/modules/{mod.Id}", admin, new
        {
            trackId = track.Id, categoryId = cat.Id, title = "Tes Modul Edit", slug = mod.Slug, description = "deskripsi",
            summary = (string?)null, durationSeconds = 360, providerAssetId = (string?)null, thumbnailUrl = (string?)null,
            orderIndex = 0, isPreview = false, requiredPlanTier = 1, published = true, tagIds = Array.Empty<Guid>(),
        })).EnsureSuccessStatusCode();
        var fetched = await Get<AdminModuleDetailDto>($"/api/admin/modules/{mod.Id}", admin);
        Assert.Equal("Tes Modul Edit", fetched.Title);

        // Resource CRUD.
        var res = await Post<ResourceDto>($"/api/admin/modules/{mod.Id}/resources", admin, new { type = "Link", @ref = "https://example.com", title = "Materi" });
        Assert.Contains(await Get<List<ResourceDto>>($"/api/admin/modules/{mod.Id}/resources", admin), r => r.Id == res.Id);

        // Tear down (no retained data references these) — all succeed.
        foreach (var url in new[] { $"/api/admin/resources/{res.Id}", $"/api/admin/modules/{mod.Id}", $"/api/admin/tracks/{track.Id}", $"/api/admin/levels/{level.Id}", $"/api/admin/categories/{cat.Id}" })
            (await Authed(HttpMethod.Delete, url, admin)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Delete_module_with_progress_is_blocked()
    {
        await Seed();
        var admin = await TokenWithRole(UserRole.SuperAdmin);
        var (_, userId) = await UserToken();

        var moduleId = await WithDbResult(db => db.Modules.Where(m => m.Status == ModuleStatus.Published).Select(m => m.Id).FirstAsync());
        await WithDb(async db =>
        {
            db.WatchProgress.Add(new WatchProgress { Id = Guid.CreateVersion7(), UserId = userId, ModuleId = moduleId, PercentComplete = 30m, LastWatchedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.Conflict, (await Authed(HttpMethod.Delete, $"/api/admin/modules/{moduleId}", admin)).StatusCode);
    }

    [Fact]
    public async Task User_management_grant_and_revoke()
    {
        await Seed();
        var admin = await TokenWithRole(UserRole.SuperAdmin);
        var (_, userId) = await UserToken();
        var beginner = (await Get<List<PlanDtoLite>>("/api/plans", null)).First(p => p.TierLevel == 1);

        // listed + detail
        Assert.Contains((await Get<AdminUserListDto>($"/api/admin/users?search={userId}", admin)).Users.Concat(
            (await Get<AdminUserListDto>("/api/admin/users?take=100", admin)).Users), u => u.Id == userId);

        // suspend
        (await Authed(HttpMethod.Put, $"/api/admin/users/{userId}/status", admin, new { status = "Suspended" })).EnsureSuccessStatusCode();
        Assert.Equal("Suspended", (await Get<AdminUserDetailDto>($"/api/admin/users/{userId}", admin)).Status);

        // grant comp plan → entitlement
        (await Authed(HttpMethod.Post, $"/api/admin/users/{userId}/grant", admin, new { planId = beginner.Id, days = 30 })).EnsureSuccessStatusCode();
        Assert.Equal(1, await ActiveTier(userId));

        // revoke → entitlement gone
        (await Authed(HttpMethod.Post, $"/api/admin/users/{userId}/revoke", admin)).EnsureSuccessStatusCode();
        Assert.Null(await ActiveTier(userId));
    }

    [Fact]
    public async Task Role_change_requires_superadmin()
    {
        var adminOnly = await TokenWithRole(UserRole.Admin);
        var super = await TokenWithRole(UserRole.SuperAdmin);
        var (_, target) = await UserToken();

        Assert.Equal(HttpStatusCode.Forbidden,
            (await Authed(HttpMethod.Put, $"/api/admin/users/{target}/role", adminOnly, new { role = "Admin" })).StatusCode);

        (await Authed(HttpMethod.Put, $"/api/admin/users/{target}/role", super, new { role = "Admin" })).EnsureSuccessStatusCode();
        Assert.Equal("Admin", (await Get<AdminUserDetailDto>($"/api/admin/users/{target}", super)).Role);
    }

    [Fact]
    public async Task Analytics_returns_shape()
    {
        await Seed();
        var admin = await TokenWithRole(UserRole.SuperAdmin);
        var a = await Get<AdminAnalyticsDto>("/api/admin/analytics", admin);
        Assert.True(a.TotalUsers >= 1);
        Assert.NotNull(a.ActiveByTier);
        Assert.True(a.CertificatesIssued >= 0);
    }

    // ---- helpers ----

    private record PlanDtoLite(Guid Id, int TierLevel);

    private async Task Seed()
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PlansSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync();
    }

    private static string Unique() => $"u{Guid.NewGuid():N}@test.local";

    private async Task<(string token, Guid userId)> UserToken()
    {
        var email = Unique();
        var res = await _client.PostAsJsonAsync("/api/auth/register", new { name = "T", email, password = Pw });
        res.EnsureSuccessStatusCode();
        var tokens = (await res.Content.ReadFromJsonAsync<AuthTokens>())!;
        return (tokens.AccessToken, tokens.User.Id);
    }

    private async Task<string> TokenWithRole(UserRole role)
    {
        var email = Unique();
        (await _client.PostAsJsonAsync("/api/auth/register", new { name = "Adm", email, password = Pw })).EnsureSuccessStatusCode();
        await WithDb(async db =>
        {
            var u = await db.Users.FirstAsync(x => x.Email == email);
            u.Role = role;
            await db.SaveChangesAsync();
        });
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Pw });
        return (await login.Content.ReadFromJsonAsync<AuthTokens>())!.AccessToken;
    }

    private async Task<int?> ActiveTier(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IEntitlementService>().GetActiveTierAsync(userId);
    }

    private async Task<List<string>> PublicSlugs()
    {
        var page = await _client.GetFromJsonAsync<CatalogPageDto>("/api/catalog?take=100", Json);
        return page!.Modules.Select(m => m.Slug).ToList();
    }

    private Task<HttpResponseMessage> Authed(HttpMethod method, string url, string token, object? body = null)
    {
        var req = new HttpRequestMessage(method, url) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };
        if (body is not null) req.Content = JsonContent.Create(body);
        return _client.SendAsync(req);
    }

    private async Task<T> Post<T>(string url, string token, object body)
    {
        var res = await Authed(HttpMethod.Post, url, token, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<T> Get<T>(string url, string? token)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _client.SendAsync(req);
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
}
