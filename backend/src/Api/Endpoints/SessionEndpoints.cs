using System.Security.Claims;
using Academy.Application.Assessments;
using Academy.Application.Programs;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Academy.Api.Endpoints;

/// <summary>
/// INVERTA M3 — learner session surface: playback, progress, and the gating test.
/// Every route is gated by ISessionAccessService inside the services (GR-1).
/// </summary>
public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/sessions").RequireAuthorization().WithTags("Sessions");

        g.MapGet("/{id:guid}", async Task<Ok<SessionContextDto>> (
                Guid id, ClaimsPrincipal u, ISessionLearningService s, CancellationToken ct) =>
            TypedResults.Ok(await s.GetContextAsync(u.UserId(), id, ct)));

        g.MapPost("/{id:guid}/playback", async Task<Ok<SessionPlaybackDto>> (
                Guid id, ClaimsPrincipal u, ISessionLearningService s, CancellationToken ct) =>
            TypedResults.Ok(await s.GetPlaybackAsync(u.UserId(), id, ct)))
            .RequireRateLimiting("playback");

        g.MapGet("/{id:guid}/progress", async Task<Ok<SessionProgressDto>> (
                Guid id, ClaimsPrincipal u, ISessionLearningService s, CancellationToken ct) =>
            TypedResults.Ok(await s.GetProgressAsync(u.UserId(), id, ct)));

        g.MapPut("/{id:guid}/progress", async Task<Ok<SessionProgressDto>> (
                Guid id, SaveProgressRequest r, ClaimsPrincipal u, ISessionLearningService s, CancellationToken ct) =>
            TypedResults.Ok(await s.SaveProgressAsync(u.UserId(), id, r.PositionSeconds, r.Percent, ct)));

        // The gating test for this session — WITHOUT the answer key (GR-11).
        g.MapGet("/{id:guid}/assessment", async Task<Results<Ok<StudentAssessmentDto>, NoContent>> (
            Guid id, ClaimsPrincipal u, IAssessmentService s, CancellationToken ct) =>
        {
            var a = await s.GetForSessionAsync(u.UserId(), id, ct);
            return a is null ? TypedResults.NoContent() : TypedResults.Ok(a);
        });

        // ---- attempts ----
        var a = app.MapGroup("/api").RequireAuthorization().WithTags("Assessments");

        a.MapPost("/assessments/{id:guid}/attempts", async Task<Ok<AttemptDto>> (
                Guid id, ClaimsPrincipal u, IAssessmentService s, CancellationToken ct) =>
            TypedResults.Ok(await s.StartAttemptAsync(u.UserId(), id, ct)));

        a.MapPost("/attempts/{id:guid}/answers", async Task<NoContent> (
            Guid id, SaveAnswersRequest r, ClaimsPrincipal u, IAssessmentService s, CancellationToken ct) =>
        { await s.SaveAnswersAsync(u.UserId(), id, r.Answers, ct); return TypedResults.NoContent(); });

        a.MapPost("/attempts/{id:guid}/submit", async Task<Ok<AttemptResultDto>> (
                Guid id, SubmitAttemptRequest? r, ClaimsPrincipal u, IAssessmentService s, CancellationToken ct) =>
            TypedResults.Ok(await s.SubmitAsync(u.UserId(), id, r?.Answers, ct)))
            .RequireRateLimiting("playback");

        a.MapGet("/attempts/{id:guid}/result", async Task<Ok<AttemptResultDto>> (
                Guid id, ClaimsPrincipal u, IAssessmentService s, CancellationToken ct) =>
            TypedResults.Ok(await s.GetResultAsync(u.UserId(), id, ct)));

        return app;
    }
}
