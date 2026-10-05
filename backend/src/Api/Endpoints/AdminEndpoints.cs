using System.Security.Claims;
using Academy.Application.Admin;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Academy.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // Admin-only (role enforced server-side, GR — never client-inferred).
        var g = app.MapGroup("/api/admin").RequireAuthorization("Admin").WithTags("Admin");

        g.MapGet("/modules", async Task<Ok<IReadOnlyList<AdminModuleDto>>> (
            string? search, IAdminService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetModulesAsync(search, ct)));

        g.MapPut("/modules/{id:guid}/published", async Task<NoContent> (
            Guid id, SetPublishedRequest req, ClaimsPrincipal user, IAdminService svc, CancellationToken ct) =>
        {
            await svc.SetModulePublishedAsync(user.UserId(), id, req.Published, ct);
            return TypedResults.NoContent();
        });

        g.MapGet("/plans", async Task<Ok<IReadOnlyList<AdminPlanDto>>> (IAdminService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetPlansAsync(ct)));

        g.MapPut("/plans/prices", async Task<NoContent> (
            UpdatePlanPricesRequest req, ClaimsPrincipal user, IAdminService svc, CancellationToken ct) =>
        {
            await svc.UpdatePlanPricesAsync(user.UserId(), req.Items, ct);
            return TypedResults.NoContent();
        });

        // ---- email relay (spec 2026-10-06) ----
        g.MapGet("/email/status", Ok<EmailStatusDto> (IEmailDiagnosticsService s) => TypedResults.Ok(s.GetStatus()));

        // Only ever mails the caller themself; rate-limited like auth so it cannot be used to hammer the relay.
        g.MapPost("/email/test", async Task<Ok<EmailTestResultDto>> (
                ClaimsPrincipal u, IEmailDiagnosticsService s, CancellationToken ct) =>
            TypedResults.Ok(await s.SendTestAsync(u.UserId(), ct)))
            .RequireRateLimiting("auth");

        return app;
    }
}
