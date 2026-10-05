using System.Security.Claims;
using Academy.Application.Assessments;
using Academy.Application.Programs;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Academy.Api.Endpoints;

/// <summary>
/// INVERTA M3 — learner session surface: the session context, then per PART (2026-10-05)
/// playback, progress, and the test.
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

        var p = g.MapGroup("/{id:guid}/parts/{partId:guid}");

        p.MapPost("/playback", async Task<Ok<PartPlaybackDto>> (
                Guid id, Guid partId, ClaimsPrincipal u, ISessionLearningService s, CancellationToken ct) =>
            TypedResults.Ok(await s.GetPartPlaybackAsync(u.UserId(), id, partId, ct)))
            .RequireRateLimiting("playback");

        p.MapGet("/progress", async Task<Ok<PartProgressDto>> (
                Guid id, Guid partId, ClaimsPrincipal u, ISessionLearningService s, CancellationToken ct) =>
            TypedResults.Ok(await s.GetPartProgressAsync(u.UserId(), id, partId, ct)));

        p.MapPut("/progress", async Task<Ok<PartProgressDto>> (
                Guid id, Guid partId, SaveProgressRequest r, ClaimsPrincipal u, ISessionLearningService s, CancellationToken ct) =>
            TypedResults.Ok(await s.SavePartProgressAsync(u.UserId(), id, partId, r.PositionSeconds, r.Percent, ct)));

        // The test of a Test part — WITHOUT the answer key (GR-11).
        p.MapGet("/assessment", async Task<Ok<StudentAssessmentDto>> (
                Guid id, Guid partId, ClaimsPrincipal u, IAssessmentService s, CancellationToken ct) =>
            TypedResults.Ok(await s.GetForPartAsync(u.UserId(), id, partId, ct)));

        // A question's own clip. Rate-limited with playback: it mints a signed URL per call.
        p.MapGet("/audio/{questionId:guid}", async Task<Ok<GatingAudioResponse>> (
                Guid id, Guid partId, Guid questionId, ClaimsPrincipal u, IAssessmentService s, CancellationToken ct) =>
            TypedResults.Ok(new GatingAudioResponse(await s.GetGatingAudioUrlAsync(u.UserId(), id, partId, questionId, ct))))
            .RequireRateLimiting("playback");

        // One recording for the whole test: stamped by the server, resumed on reload, replayed only
        // within the per-attempt limit.
        p.MapPost("/audio", async Task<Ok<TestAudioDto>> (
                Guid id, Guid partId, StartTestAudioRequest r, ClaimsPrincipal u, IAssessmentService s, CancellationToken ct) =>
            TypedResults.Ok(await s.StartTestAudioAsync(u.UserId(), id, partId, r.Replay, ct)))
            .RequireRateLimiting("playback");

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

public record GatingAudioResponse(string Url);
