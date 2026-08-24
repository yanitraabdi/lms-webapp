using System.Security.Claims;
using Academy.Application.Billing;
using Academy.Application.Programs;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Academy.Api.Endpoints;

/// <summary>INVERTA M2 — public program, enrollment checkout, and the student program view.</summary>
public static class ProgramEndpoints
{
    public static IEndpointRouteBuilder MapProgramEndpoints(this IEndpointRouteBuilder app)
    {
        // ---- public (landing / SSG) ----
        app.MapGet("/api/programs/{slug}", async Task<Results<Ok<PublicProgramDto>, NotFound>> (
            string slug, IProgramService s, CancellationToken ct) =>
        {
            var p = await s.GetPublicAsync(slug, ct);
            return p is null ? TypedResults.NotFound() : TypedResults.Ok(p);
        }).WithTags("Programs");

        // ---- enrollment ----
        // Email verification is required before purchase (inherited rule, KAK §9.4 R2).
        app.MapPost("/api/programs/{id:guid}/enroll", async Task<Ok<CheckoutSession>> (
                Guid id, EnrollRequest? body, ClaimsPrincipal u, IEnrollmentService s, CancellationToken ct) =>
            TypedResults.Ok(await s.CheckoutAsync(u.UserId(), id, body?.BatchId, ct)))
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("payment")
            .WithTags("Programs");

        app.MapGet("/api/me/enrollments", async Task<Ok<IReadOnlyList<EnrollmentDto>>> (
                ClaimsPrincipal u, IEnrollmentService s, CancellationToken ct) =>
            TypedResults.Ok(await s.ListMineAsync(u.UserId(), ct)))
            .RequireAuthorization()
            .WithTags("Programs");

        // ---- student program view (enrollment enforced inside the service) ----
        app.MapGet("/api/me/programs/{id:guid}", async Task<Ok<StudentProgramDto>> (
                Guid id, ClaimsPrincipal u, IProgramService s, CancellationToken ct) =>
            TypedResults.Ok(await s.GetForStudentAsync(u.UserId(), id, ct)))
            .RequireAuthorization()
            .WithTags("Programs");

        return app;
    }
}

public record EnrollRequest(Guid? BatchId);
