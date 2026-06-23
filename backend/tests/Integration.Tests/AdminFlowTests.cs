using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Academy.Application.Admin;
using Academy.Application.Auth;
using Academy.Application.Catalog;
using Academy.Domain.Enums;
using Academy.Infrastructure.Billing;
using Academy.Infrastructure.Catalog;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

public class AdminFlowTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    [Fact]
    public async Task Admin_endpoints_require_admin_role()
    {
        await Seed();
        var normal = (await Register(Unique())).AccessToken;
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Authed(HttpMethod.Get, "/api/admin/modules", normal)).StatusCode);

        var admin = await AdminToken();
        Assert.Equal(HttpStatusCode.OK,
            (await Authed(HttpMethod.Get, "/api/admin/modules", admin)).StatusCode);

        // Anonymous → 401.
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/admin/modules")).StatusCode);
    }

    [Fact]
    public async Task Unpublish_hides_module_from_public_catalog_and_republish_restores()
    {
        await Seed();
        var admin = await AdminToken();

        var mods = await AuthedGet<List<AdminModuleDto>>("/api/admin/modules", admin);
        var target = mods.First(m => m.Published);

        Assert.Contains(await PublicSlugs(), s => s == target.Slug);

        (await Authed(HttpMethod.Put, $"/api/admin/modules/{target.Id}/published", admin, new { published = false }))
            .EnsureSuccessStatusCode();
        Assert.DoesNotContain(await PublicSlugs(), s => s == target.Slug);

        (await Authed(HttpMethod.Put, $"/api/admin/modules/{target.Id}/published", admin, new { published = true }))
            .EnsureSuccessStatusCode();
        Assert.Contains(await PublicSlugs(), s => s == target.Slug);

        // Audit trail recorded.
        await WithDb(async db =>
            Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "module_unpublished" && a.Target == target.Id.ToString())));
    }

    [Fact]
    public async Task Admin_updates_plan_prices_and_logs_it()
    {
        await Seed();
        var admin = await AdminToken();

        var plans = await AuthedGet<List<AdminPlanDto>>("/api/admin/plans", admin);
        var beginner = plans.First(p => p.TierLevel == 1);
        var newMonthly = beginner.PriceMonthly + 25_000m;

        (await Authed(HttpMethod.Put, "/api/admin/plans/prices", admin, new
        {
            items = new[] { new { planId = beginner.Id, priceMonthly = newMonthly, priceAnnual = beginner.PriceAnnual } },
        })).EnsureSuccessStatusCode();

        var after = await AuthedGet<List<AdminPlanDto>>("/api/admin/plans", admin);
        Assert.Equal(newMonthly, after.First(p => p.Id == beginner.Id).PriceMonthly);

        await WithDb(async db =>
            Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "plan_prices_updated" && a.Target == beginner.Id.ToString())));
    }

    // ---- helpers ----

    private static string Unique() => $"u{Guid.NewGuid():N}@test.local";

    private async Task Seed()
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PlansSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync();
    }

    private async Task<AuthTokens> Register(string email)
    {
        var res = await _client.PostAsJsonAsync("/api/auth/register", new { name = "T", email, password = Pw });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AuthTokens>())!;
    }

    private async Task<string> AdminToken()
    {
        var email = Unique();
        await Register(email);
        await WithDb(async db =>
        {
            var u = await db.Users.FirstAsync(x => x.Email == email);
            u.Role = UserRole.SuperAdmin;
            await db.SaveChangesAsync();
        });
        // Re-login so the fresh JWT carries the elevated role claim.
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Pw });
        return (await login.Content.ReadFromJsonAsync<AuthTokens>())!.AccessToken;
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
}
