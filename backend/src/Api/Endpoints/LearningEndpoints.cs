using System.Security.Claims;
using Academy.Application.Learning;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Academy.Api.Endpoints;

public static class LearningEndpoints
{
    public static IEndpointRouteBuilder MapLearningEndpoints(this IEndpointRouteBuilder app)
    {
        var modules = app.MapGroup("/api/modules").RequireAuthorization().WithTags("Learning");

        // Per-session signed playback — entitlement-checked, rate-limited (GR-3).
        modules.MapPost("/{id:guid}/playback", async Task<Ok<PlaybackTicketDto>> (
            Guid id, ClaimsPrincipal user, ILearningService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetPlaybackAsync(user.UserId(), id, ct)))
            .RequireRateLimiting("playback");

        modules.MapGet("/{id:guid}/player", async Task<Ok<PlayerContextDto>> (
            Guid id, ClaimsPrincipal user, ILearningService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetPlayerContextAsync(user.UserId(), id, ct)));

        modules.MapGet("/{id:guid}/progress", async Task<Results<Ok<ModuleProgressDto>, NoContent>> (
            Guid id, ClaimsPrincipal user, ILearningService svc, CancellationToken ct) =>
        {
            var p = await svc.GetProgressAsync(user.UserId(), id, ct);
            return p is null ? TypedResults.NoContent() : TypedResults.Ok(p);
        });

        modules.MapPut("/{id:guid}/progress", async Task<Ok<ModuleProgressDto>> (
            Guid id, SaveProgressRequest req, ClaimsPrincipal user, ILearningService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.SaveProgressAsync(user.UserId(), id, req.PositionSeconds, req.Percent, ct)));

        // Learner dashboard + own certificates.
        app.MapGet("/api/me/dashboard", async Task<Ok<DashboardDto>> (
            ClaimsPrincipal user, ILearningService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetDashboardAsync(user.UserId(), ct)))
            .RequireAuthorization().WithTags("Learning");

        app.MapGet("/api/me/certificates", async Task<Ok<IReadOnlyList<CertificateDto>>> (
            ClaimsPrincipal user, ICertificateService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetMineAsync(user.UserId(), ct)))
            .RequireAuthorization().WithTags("Certificates");

        // Certificate PDF — owner only, generated on demand.
        app.MapGet("/api/certificates/{id:guid}/pdf", async Task<Results<FileContentHttpResult, NotFound>> (
            Guid id, ClaimsPrincipal user, ICertificateService svc, CancellationToken ct) =>
        {
            var result = await svc.GetPdfAsync(user.UserId(), id, ct);
            return result is null
                ? TypedResults.NotFound()
                : TypedResults.File(result.Value.Pdf, "application/pdf", result.Value.FileName);
        }).RequireAuthorization().WithTags("Certificates");

        // Public certificate verification — no auth.
        app.MapGet("/api/certificates/verify/{code}", async Task<Ok<CertificateVerifyDto>> (
            string code, ICertificateService svc, CancellationToken ct) =>
            TypedResults.Ok((await svc.VerifyAsync(code, ct))!)).WithTags("Certificates");

        return app;
    }
}
