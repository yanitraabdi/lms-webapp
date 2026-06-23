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

        return app;
    }
}
