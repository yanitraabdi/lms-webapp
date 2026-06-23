using Academy.Application.Billing;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Academy.Api.Endpoints;

public static class SubscriptionEndpoints
{
    public static IEndpointRouteBuilder MapSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        // Public: plan catalog for the pricing page.
        app.MapGet("/api/plans", async Task<Ok<IReadOnlyList<PlanDto>>> (ISubscriptionService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetPlansAsync(ct))).WithTags("Billing");

        var group = app.MapGroup("/api/subscriptions").RequireAuthorization().WithTags("Billing");

        group.MapGet("/me", async Task<Results<Ok<MySubscriptionDto>, NoContent>> (
            System.Security.Claims.ClaimsPrincipal user, ISubscriptionService svc, CancellationToken ct) =>
        {
            var me = await svc.GetMyAsync(user.UserId(), ct);
            return me is null ? TypedResults.NoContent() : TypedResults.Ok(me);
        });

        group.MapGet("/billing-history", async Task<Ok<IReadOnlyList<BillingHistoryItemDto>>> (
            System.Security.Claims.ClaimsPrincipal user, ISubscriptionService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetBillingHistoryAsync(user.UserId(), ct)));

        // Checkout / upgrade go through the gateway → return a hosted-checkout URL.
        group.MapPost("/checkout", async Task<Ok<CheckoutSession>> (
            CheckoutRequest req, System.Security.Claims.ClaimsPrincipal user, ISubscriptionService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.CheckoutAsync(user.UserId(), req.PlanId, req.BillingCycle, ct)))
            .RequireRateLimiting("payment");

        group.MapGet("/upgrade-preview", async Task<Ok<UpgradePreviewDto>> (
            Guid newPlanId, System.Security.Claims.ClaimsPrincipal user, ISubscriptionService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.PreviewUpgradeAsync(user.UserId(), newPlanId, ct)));

        group.MapPost("/upgrade", async Task<Ok<CheckoutSession>> (
            PlanChangeRequest req, System.Security.Claims.ClaimsPrincipal user, ISubscriptionService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.UpgradeAsync(user.UserId(), req.NewPlanId, ct)))
            .RequireRateLimiting("payment");

        group.MapPost("/downgrade", async Task<NoContent> (
            PlanChangeRequest req, System.Security.Claims.ClaimsPrincipal user, ISubscriptionService svc, CancellationToken ct) =>
        {
            await svc.DowngradeAsync(user.UserId(), req.NewPlanId, ct);
            return TypedResults.NoContent();
        });

        group.MapPost("/cancel", async Task<NoContent> (
            System.Security.Claims.ClaimsPrincipal user, ISubscriptionService svc, CancellationToken ct) =>
        {
            await svc.CancelAsync(user.UserId(), ct);
            return TypedResults.NoContent();
        });

        return app;
    }
}
