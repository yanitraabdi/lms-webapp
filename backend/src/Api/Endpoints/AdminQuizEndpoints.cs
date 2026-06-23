using System.Security.Claims;
using Academy.Application.Admin;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Academy.Api.Endpoints;

public static class AdminQuizEndpoints
{
    public static IEndpointRouteBuilder MapAdminQuizEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin/modules").RequireAuthorization("Admin").WithTags("AdminQuiz");

        g.MapGet("/{moduleId:guid}/quiz", async Task<Results<Ok<AdminQuizDto>, NoContent>> (Guid moduleId, IQuizAdminService s, CancellationToken ct) =>
        {
            var q = await s.GetAsync(moduleId, ct);
            return q is null ? TypedResults.NoContent() : TypedResults.Ok(q);
        });

        g.MapPut("/{moduleId:guid}/quiz", async Task<NoContent> (Guid moduleId, UpsertQuizRequest r, ClaimsPrincipal u, IQuizAdminService s, CancellationToken ct) =>
        { await s.UpsertAsync(u.UserId(), moduleId, r, ct); return TypedResults.NoContent(); });

        g.MapDelete("/{moduleId:guid}/quiz", async Task<NoContent> (Guid moduleId, ClaimsPrincipal u, IQuizAdminService s, CancellationToken ct) =>
        { await s.DeleteAsync(u.UserId(), moduleId, ct); return TypedResults.NoContent(); });

        return app;
    }
}
