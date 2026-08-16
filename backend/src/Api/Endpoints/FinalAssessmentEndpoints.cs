using System.Security.Claims;
using Academy.Application.Assessments;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Academy.Api.Endpoints;

/// <summary>INVERTA M4 — the timed sectional sitting, proctoring, and certificates.</summary>
public static class FinalAssessmentEndpoints
{
    public static IEndpointRouteBuilder MapFinalAssessmentEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/attempts").RequireAuthorization().WithTags("FinalAssessment");

        // Server-authoritative state: deadlines are enforced on every read (GR-12).
        g.MapGet("/{id:guid}/state", async Task<Ok<AttemptStateDto>> (
                Guid id, ClaimsPrincipal u, IFinalAssessmentService s, CancellationToken ct) =>
            TypedResults.Ok(await s.GetStateAsync(u.UserId(), id, ct)));

        g.MapPost("/{id:guid}/section-answers", async Task<Ok<AttemptStateDto>> (
                Guid id, SaveAnswersRequest r, ClaimsPrincipal u, IFinalAssessmentService s, CancellationToken ct) =>
            TypedResults.Ok(await s.SaveAnswersAsync(u.UserId(), id, r.Answers, ct)));

        g.MapPost("/{id:guid}/advance", async Task<Ok<AttemptStateDto>> (
                Guid id, AdvanceSectionRequest? r, ClaimsPrincipal u, IFinalAssessmentService s, CancellationToken ct) =>
            TypedResults.Ok(await s.AdvanceSectionAsync(u.UserId(), id, r?.Answers, ct)));

        g.MapPost("/{id:guid}/finish", async Task<Ok<AttemptResultDto>> (
                Guid id, ClaimsPrincipal u, IFinalAssessmentService s, CancellationToken ct) =>
            TypedResults.Ok(await s.SubmitAsync(u.UserId(), id, ct)))
            .RequireRateLimiting("playback");

        // The client REPORTS; the server decides warn / auto-submit (GR-13).
        g.MapPost("/{id:guid}/proctor-events", async Task<Ok<ProctorStateDto>> (
                Guid id, ProctorEventRequest r, ClaimsPrincipal u, IProctorService s, CancellationToken ct) =>
            TypedResults.Ok(await s.ReportAsync(u.UserId(), id, r.Kind, r.DurationMs, ct)));

        g.MapGet("/{id:guid}/audio/{questionId:guid}", async Task<Ok<AudioUrlResponse>> (
                Guid id, Guid questionId, ClaimsPrincipal u, IFinalAssessmentService s, CancellationToken ct) =>
            TypedResults.Ok(new AudioUrlResponse(await s.GetAudioUrlAsync(u.UserId(), id, questionId, ct))))
            .RequireRateLimiting("playback");

        // ---- certificates ----
        app.MapGet("/api/me/program-certificates", async Task<Ok<IReadOnlyList<ProgramCertificateDto>>> (
                ClaimsPrincipal u, IProgramCertificateService s, CancellationToken ct) =>
            TypedResults.Ok(await s.GetMineAsync(u.UserId(), ct)))
            .RequireAuthorization().WithTags("Certificates");

        app.MapGet("/api/program-certificates/{id:guid}/pdf", async Task<Results<FileContentHttpResult, NotFound>> (
            Guid id, ClaimsPrincipal u, IProgramCertificateService s, CancellationToken ct) =>
        {
            var file = await s.GetPdfAsync(u.UserId(), id, ct);
            return file is null
                ? TypedResults.NotFound()
                : TypedResults.File(file.Value.Pdf, "application/pdf", file.Value.FileName);
        }).RequireAuthorization().WithTags("Certificates");

        // Public verification — an unknown code returns valid:false, never an error (KAK §9.10 R6).
        app.MapGet("/api/program-certificates/verify/{code}", async Task<Ok<CertificateVerificationDto>> (
                string code, IProgramCertificateService s, CancellationToken ct) =>
            TypedResults.Ok(await s.VerifyAsync(code, ct)))
            .WithTags("Certificates");

        // ---- admin ----
        var adm = app.MapGroup("/api/admin").RequireAuthorization("Admin").WithTags("AdminAssessments");

        adm.MapPost("/attempts/{id:guid}/reinstate", async Task<NoContent> (
            Guid id, ClaimsPrincipal u, IProctorService s, CancellationToken ct) =>
        { await s.ReinstateAsync(u.UserId(), id, ct); return TypedResults.NoContent(); });

        adm.MapGet("/programs/{programId:guid}/score-bands", async Task<Ok<IReadOnlyList<ScoreBandDto>>> (
                Guid programId, IScoreBandAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.ListAsync(programId, ct)));

        adm.MapPut("/programs/{programId:guid}/score-bands", async Task<NoContent> (
            Guid programId, UpsertScoreBandsRequest r, ClaimsPrincipal u, IScoreBandAdminService s, CancellationToken ct) =>
        { await s.ReplaceAsync(u.UserId(), programId, r, ct); return TypedResults.NoContent(); });

        return app;
    }
}

public record AudioUrlResponse(string Url);
