using System.Security.Claims;
using Academy.Application.Admin;
using Academy.Domain.Enums;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Academy.Api.Endpoints;

public static class UserAdminEndpoints
{
    public static IEndpointRouteBuilder MapUserAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin").RequireAuthorization("Admin").WithTags("AdminUsers");

        g.MapGet("/analytics", async Task<Ok<AdminAnalyticsDto>> (IAdminAnalyticsService s, CancellationToken ct) =>
            TypedResults.Ok(await s.GetAsync(ct)));

        g.MapGet("/users", async Task<Ok<AdminUserListDto>> (
            string? search, string? status, int? tier, int? skip, int? take, IUserAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.ListAsync(search, status, tier, skip ?? 0, take ?? 25, ct)));

        g.MapGet("/users/{id:guid}", async Task<Ok<AdminUserDetailDto>> (Guid id, IUserAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.GetAsync(id, ct)));

        g.MapPut("/users/{id:guid}/status", async Task<Results<NoContent, BadRequest<string>>> (
            Guid id, SetUserStatusRequest r, ClaimsPrincipal u, IUserAdminService s, CancellationToken ct) =>
        {
            if (!Enum.TryParse<UserStatus>(r.Status, true, out var st) || st == UserStatus.Deleted)
                return TypedResults.BadRequest("Status tidak valid.");
            await s.SetStatusAsync(u.UserId(), id, st, ct);
            return TypedResults.NoContent();
        });

        // Role changes are SuperAdmin-only.
        g.MapPut("/users/{id:guid}/role", async Task<Results<NoContent, BadRequest<string>>> (
            Guid id, SetUserRoleRequest r, ClaimsPrincipal u, IUserAdminService s, CancellationToken ct) =>
        {
            if (!Enum.TryParse<UserRole>(r.Role, true, out var role))
                return TypedResults.BadRequest("Peran tidak valid.");
            await s.SetRoleAsync(u.UserId(), id, role, ct);
            return TypedResults.NoContent();
        }).RequireAuthorization("SuperAdmin");

        g.MapPost("/users/{id:guid}/grant", async Task<NoContent> (
            Guid id, GrantPlanRequest r, ClaimsPrincipal u, IUserAdminService s, CancellationToken ct) =>
        {
            await s.GrantPlanAsync(u.UserId(), id, r.PlanId, r.Days, ct);
            return TypedResults.NoContent();
        });

        g.MapPost("/users/{id:guid}/revoke", async Task<NoContent> (
            Guid id, ClaimsPrincipal u, IUserAdminService s, CancellationToken ct) =>
        {
            await s.RevokeAsync(u.UserId(), id, ct);
            return TypedResults.NoContent();
        });

        return app;
    }
}
