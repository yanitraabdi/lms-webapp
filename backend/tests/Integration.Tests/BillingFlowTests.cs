using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Academy.Application.Auth;
using Academy.Application.Billing;
using Academy.Application.Catalog;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Billing;
using Academy.Infrastructure.Catalog;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

public class BillingFlowTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    // ---- the critical paths (TSD §14) ----

    [Fact]
    public async Task Checkout_grants_entitlement_only_after_webhook()
    {
        var plans = await SeedAndPlans();
        var (token, userId) = await VerifiedUser();
        var beginner = plans.First(p => p.TierLevel == 1);

        var session = await Checkout(token, beginner.Id, BillingCycle.Monthly);

        // Before the webhook: checkout alone grants NOTHING (GR-2).
        Assert.Null(await ActiveTier(userId));
        Assert.Equal(HttpStatusCode.NoContent, (await Authed(HttpMethod.Get, "/api/subscriptions/me", token)).StatusCode);

        await SimulateSucceed(session.ProviderRef);

        // After the verified webhook: subscription active + tier granted.
        Assert.Equal(1, await ActiveTier(userId));
        var me = await AuthedGet<MySubscriptionDto>("/api/subscriptions/me", token);
        Assert.Equal(SubscriptionStatus.Active, me.Status);
        Assert.Equal(beginner.Id, me.PlanId);

        // End-to-end: a Basic (non-preview) module now reads as Entitled in the catalog.
        var page = await AuthedGet<CatalogPageDto>("/api/catalog?level=basic&take=100", token);
        Assert.Contains(page.Modules, m => m.Access == ModuleAccess.Entitled);
    }

    [Fact]
    public async Task Webhook_is_idempotent_on_external_id()
    {
        var plans = await SeedAndPlans();
        var (token, userId) = await VerifiedUser();
        var session = await Checkout(token, plans.First(p => p.TierLevel == 1).Id, BillingCycle.Monthly);

        var body = WebhookBody($"evt_{Guid.NewGuid():N}", session.ProviderRef, "succeeded");
        var sig = DevPaymentGateway.Sign(body, CallbackToken);

        Assert.Equal("Processed", await OutcomeOf(await PostWebhook(body, sig)));
        Assert.Equal("Duplicate", await OutcomeOf(await PostWebhook(body, sig)));

        // Exactly one subscription was created despite two deliveries.
        await WithDb(async db => Assert.Equal(1, await db.Subscriptions.CountAsync(s => s.UserId == userId)));
    }

    [Fact]
    public async Task Webhook_rejects_invalid_or_missing_signature()
    {
        var plans = await SeedAndPlans();
        var (token, userId) = await VerifiedUser();
        var session = await Checkout(token, plans.First(p => p.TierLevel == 1).Id, BillingCycle.Monthly);
        var body = WebhookBody($"evt_{Guid.NewGuid():N}", session.ProviderRef, "succeeded");

        Assert.Equal(HttpStatusCode.Forbidden, (await PostWebhook(body, "wrong-token")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostWebhook(body, null)).StatusCode);
        // The rejected events never created a subscription.
        await WithDb(async db => Assert.Equal(0, await db.Subscriptions.CountAsync(s => s.UserId == userId)));
    }

    [Fact]
    public async Task Upgrade_charges_prorated_delta_and_relocks_price()
    {
        var plans = await SeedAndPlans();
        var (token, userId) = await VerifiedUser();
        var beginner = plans.First(p => p.TierLevel == 1);
        var intermediate = plans.First(p => p.TierLevel == 2);

        await SimulateSucceed((await Checkout(token, beginner.Id, BillingCycle.Monthly)).ProviderRef);

        // Just subscribed (≈full period remaining) → delta ≈ full monthly difference.
        var preview = await AuthedGet<UpgradePreviewDto>($"/api/subscriptions/upgrade-preview?newPlanId={intermediate.Id}", token);
        Assert.True(preview.DeltaIdr > 0);
        Assert.True(preview.DeltaIdr <= intermediate.PriceMonthly - beginner.PriceMonthly);

        var up = await AuthedPost("/api/subscriptions/upgrade", token, new { newPlanId = intermediate.Id });
        var session = (await up.Content.ReadFromJsonAsync<CheckoutSession>(Json))!;
        await SimulateSucceed(session.ProviderRef);

        Assert.Equal(2, await ActiveTier(userId));
        var me = await AuthedGet<MySubscriptionDto>("/api/subscriptions/me", token);
        Assert.Equal(intermediate.Id, me.PlanId);
        Assert.Equal(intermediate.PriceMonthly, me.PriceLockedIdr); // next renewal bills full new price
    }

    [Fact]
    public async Task Downgrade_takes_effect_at_renewal()
    {
        var plans = await SeedAndPlans();
        var (token, userId) = await VerifiedUser();
        var advanced = plans.First(p => p.TierLevel == 3);
        var intermediate = plans.First(p => p.TierLevel == 2);

        await SimulateSucceed((await Checkout(token, advanced.Id, BillingCycle.Monthly)).ProviderRef);

        (await AuthedPost("/api/subscriptions/downgrade", token, new { newPlanId = intermediate.Id }))
            .EnsureSuccessStatusCode();

        // Still on Advanced until the period ends.
        var before = await AuthedGet<MySubscriptionDto>("/api/subscriptions/me", token);
        Assert.Equal(advanced.Id, before.PlanId);
        Assert.Equal(intermediate.Id, before.PlannedPlanId);
        Assert.Equal(3, await ActiveTier(userId));

        await EndPeriodNow(userId);
        await Reconcile();

        // Renewal applied the downgrade + re-locked to the now-current Intermediate price.
        var after = await AuthedGet<MySubscriptionDto>("/api/subscriptions/me", token);
        Assert.Equal(intermediate.Id, after.PlanId);
        Assert.Null(after.PlannedPlanId);
        Assert.Equal(intermediate.PriceMonthly, after.PriceLockedIdr);
        Assert.Equal(2, await ActiveTier(userId));
    }

    [Fact]
    public async Task Cancel_keeps_access_until_period_end_then_expires()
    {
        var plans = await SeedAndPlans();
        var (token, userId) = await VerifiedUser();
        await SimulateSucceed((await Checkout(token, plans.First(p => p.TierLevel == 1).Id, BillingCycle.Monthly)).ProviderRef);

        (await AuthedPost("/api/subscriptions/cancel", token)).EnsureSuccessStatusCode();

        var me = await AuthedGet<MySubscriptionDto>("/api/subscriptions/me", token);
        Assert.Equal(SubscriptionStatus.Canceled, me.Status);
        Assert.Equal(1, await ActiveTier(userId)); // still entitled until period end

        await EndPeriodNow(userId);
        await Reconcile();

        Assert.Null(await ActiveTier(userId)); // expired → back to Free
        Assert.Equal(HttpStatusCode.NoContent, (await Authed(HttpMethod.Get, "/api/subscriptions/me", token)).StatusCode);
    }

    [Fact]
    public async Task Grandfathering_locks_price_for_existing_subscribers_only()
    {
        var plans = await SeedAndPlans();
        var beginner = plans.First(p => p.TierLevel == 1);

        var (tokenA, _) = await VerifiedUser();
        await SimulateSucceed((await Checkout(tokenA, beginner.Id, BillingCycle.Monthly)).ProviderRef);
        var locked = (await AuthedGet<MySubscriptionDto>("/api/subscriptions/me", tokenA)).PriceLockedIdr;

        // Admin raises the plan price.
        var newPrice = locked + 51_000m;
        await WithDb(async db =>
        {
            var plan = await db.Plans.FirstAsync(p => p.Id == beginner.Id);
            plan.PriceMonthly = newPrice;
            await db.SaveChangesAsync();
        });

        // Existing subscriber is unaffected...
        Assert.Equal(locked, (await AuthedGet<MySubscriptionDto>("/api/subscriptions/me", tokenA)).PriceLockedIdr);

        // ...a brand-new subscriber pays the new price immediately.
        var (tokenB, _) = await VerifiedUser();
        await SimulateSucceed((await Checkout(tokenB, beginner.Id, BillingCycle.Monthly)).ProviderRef);
        Assert.Equal(newPrice, (await AuthedGet<MySubscriptionDto>("/api/subscriptions/me", tokenB)).PriceLockedIdr);
    }

    [Fact]
    public async Task Progress_is_retained_across_expiry()
    {
        var plans = await SeedAndPlans();
        var (token, userId) = await VerifiedUser();
        await SimulateSucceed((await Checkout(token, plans.First(p => p.TierLevel == 1).Id, BillingCycle.Monthly)).ProviderRef);
        Assert.Equal(1, await ActiveTier(userId));

        // Record progress on some module, then let the subscription lapse.
        Guid moduleId = default;
        await WithDb(async db =>
        {
            moduleId = await db.Modules.Select(m => m.Id).FirstAsync();
            db.WatchProgress.Add(new WatchProgress
            {
                Id = Guid.CreateVersion7(), UserId = userId, ModuleId = moduleId,
                ResumePositionSeconds = 120, PercentComplete = 50m, Completed = false,
                LastWatchedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        });

        await EndPeriodNow(userId);
        await Reconcile();
        Assert.Null(await ActiveTier(userId)); // access revoked

        // Progress survives — never hard-deleted (GR-7).
        await WithDb(async db =>
        {
            var wp = await db.WatchProgress.SingleAsync(w => w.UserId == userId && w.ModuleId == moduleId);
            Assert.Equal(50m, wp.PercentComplete);
        });
    }

    // ---- helpers ----

    private string CallbackToken => factory.Services.GetRequiredService<BillingOptions>().CallbackToken;

    private async Task<IReadOnlyList<PlanDto>> SeedAndPlans()
    {
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<PlansSeeder>().SeedAsync();
            await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync();
        }
        return (await _client.GetFromJsonAsync<List<PlanDto>>("/api/plans", Json))!;
    }

    private async Task<(string token, Guid userId)> VerifiedUser()
    {
        var email = $"u{Guid.NewGuid():N}@test.local";
        var reg = await _client.PostAsJsonAsync("/api/auth/register", new { name = "T", email, password = Pw });
        reg.EnsureSuccessStatusCode();
        var token = TokenFrom(factory.Email.LastVerifyUrl);
        (await _client.PostAsJsonAsync("/api/auth/verify-email", new { token })).EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Pw });
        var tokens = (await login.Content.ReadFromJsonAsync<AuthTokens>())!;
        return (tokens.AccessToken, tokens.User.Id);
    }

    private async Task<CheckoutSession> Checkout(string token, Guid planId, BillingCycle cycle)
    {
        var res = await AuthedPost("/api/subscriptions/checkout", token, new { planId, billingCycle = cycle.ToString() });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<CheckoutSession>(Json))!;
    }

    private async Task SimulateSucceed(string providerRef)
        => (await _client.PostAsync($"/api/dev/payments/{Uri.EscapeDataString(providerRef)}/succeed", null))
            .EnsureSuccessStatusCode();

    private async Task<int?> ActiveTier(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IEntitlementService>().GetActiveTierAsync(userId);
    }

    private async Task Reconcile()
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IBillingReconciler>().ReconcileAsync();
    }

    private Task EndPeriodNow(Guid userId) => WithDb(async db =>
    {
        var sub = await db.Subscriptions.Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CurrentPeriodEnd).FirstAsync();
        sub.CurrentPeriodEnd = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
    });

    private static string WebhookBody(string externalId, string providerRef, string type) =>
        JsonSerializer.Serialize(new { externalId, providerRef, type, method = "dev" });

    private Task<HttpResponseMessage> PostWebhook(string body, string? token)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/xendit")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (token is not null) req.Headers.Add("x-callback-token", token);
        return _client.SendAsync(req);
    }

    private static async Task<string> OutcomeOf(HttpResponseMessage res)
    {
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("outcome").GetString()!;
    }

    private Task<HttpResponseMessage> Authed(HttpMethod method, string url, string token, object? body = null)
    {
        var req = new HttpRequestMessage(method, url) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };
        if (body is not null) req.Content = JsonContent.Create(body);
        return _client.SendAsync(req);
    }

    private Task<HttpResponseMessage> AuthedPost(string url, string token, object? body = null)
        => Authed(HttpMethod.Post, url, token, body);

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
