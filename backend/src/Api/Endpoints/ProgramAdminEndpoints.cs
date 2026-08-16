using System.Security.Claims;
using Academy.Application.Programs;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Academy.Api.Endpoints;

/// <summary>INVERTA M2 — program/session/batch authoring and enrollment support (KAK §9.12).</summary>
public static class ProgramAdminEndpoints
{
    public static IEndpointRouteBuilder MapProgramAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin").RequireAuthorization("Admin").WithTags("AdminPrograms");

        // ---- programs ----
        g.MapGet("/programs", async Task<Ok<IReadOnlyList<AdminProgramDto>>> (
                IProgramAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.ListAsync(ct)));

        g.MapGet("/programs/{id:guid}", async Task<Results<Ok<AdminProgramDto>, NotFound>> (
            Guid id, IProgramAdminService s, CancellationToken ct) =>
        {
            var p = await s.GetAsync(id, ct);
            return p is null ? TypedResults.NotFound() : TypedResults.Ok(p);
        });

        g.MapPost("/programs", async Task<Ok<AdminProgramDto>> (
                UpsertProgramRequest r, ClaimsPrincipal u, IProgramAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.CreateAsync(u.UserId(), r, ct)));

        g.MapPut("/programs/{id:guid}", async Task<NoContent> (
            Guid id, UpsertProgramRequest r, ClaimsPrincipal u, IProgramAdminService s, CancellationToken ct) =>
        { await s.UpdateAsync(u.UserId(), id, r, ct); return TypedResults.NoContent(); });

        g.MapDelete("/programs/{id:guid}", async Task<NoContent> (
            Guid id, ClaimsPrincipal u, IProgramAdminService s, CancellationToken ct) =>
        { await s.DeleteAsync(u.UserId(), id, ct); return TypedResults.NoContent(); });

        // ---- sessions ----
        g.MapGet("/programs/{programId:guid}/sessions", async Task<Ok<IReadOnlyList<AdminSessionDto>>> (
                Guid programId, IProgramAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.ListSessionsAsync(programId, ct)));

        g.MapPost("/programs/{programId:guid}/sessions", async Task<Ok<AdminSessionDto>> (
                Guid programId, UpsertSessionRequest r, ClaimsPrincipal u, IProgramAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.CreateSessionAsync(u.UserId(), programId, r, ct)));

        g.MapPut("/sessions/{id:guid}", async Task<NoContent> (
            Guid id, UpsertSessionRequest r, ClaimsPrincipal u, IProgramAdminService s, CancellationToken ct) =>
        { await s.UpdateSessionAsync(u.UserId(), id, r, ct); return TypedResults.NoContent(); });

        g.MapDelete("/sessions/{id:guid}", async Task<NoContent> (
            Guid id, ClaimsPrincipal u, IProgramAdminService s, CancellationToken ct) =>
        { await s.DeleteSessionAsync(u.UserId(), id, ct); return TypedResults.NoContent(); });

        g.MapPost("/programs/{programId:guid}/sessions/reorder", async Task<NoContent> (
            Guid programId, ReorderSessionsRequest r, ClaimsPrincipal u, IProgramAdminService s, CancellationToken ct) =>
        { await s.ReorderSessionsAsync(u.UserId(), programId, r, ct); return TypedResults.NoContent(); });

        // ---- batches ----
        g.MapGet("/programs/{programId:guid}/batches", async Task<Ok<IReadOnlyList<AdminBatchDto>>> (
                Guid programId, IProgramAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.ListBatchesAsync(programId, ct)));

        g.MapPost("/programs/{programId:guid}/batches", async Task<Ok<AdminBatchDto>> (
                Guid programId, UpsertBatchRequest r, ClaimsPrincipal u, IProgramAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.CreateBatchAsync(u.UserId(), programId, r, ct)));

        g.MapPut("/batches/{id:guid}", async Task<NoContent> (
            Guid id, UpsertBatchRequest r, ClaimsPrincipal u, IProgramAdminService s, CancellationToken ct) =>
        { await s.UpdateBatchAsync(u.UserId(), id, r, ct); return TypedResults.NoContent(); });

        g.MapDelete("/batches/{id:guid}", async Task<NoContent> (
            Guid id, ClaimsPrincipal u, IProgramAdminService s, CancellationToken ct) =>
        { await s.DeleteBatchAsync(u.UserId(), id, ct); return TypedResults.NoContent(); });

        // ---- enrollment support ----
        g.MapPost("/enrollments/{id:guid}/revoke", async Task<NoContent> (
            Guid id, ClaimsPrincipal u, IProgramAdminService s, CancellationToken ct) =>
        { await s.RevokeEnrollmentAsync(u.UserId(), id, ct); return TypedResults.NoContent(); });

        return app;
    }
}
