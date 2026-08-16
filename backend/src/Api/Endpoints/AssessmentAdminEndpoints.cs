using System.Security.Claims;
using Academy.Application.Assessments;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Academy.Api.Endpoints;

/// <summary>INVERTA M3 — question bank and assessment authoring (KAK §9.12).</summary>
public static class AssessmentAdminEndpoints
{
    public static IEndpointRouteBuilder MapAssessmentAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin").RequireAuthorization("Admin").WithTags("AdminAssessments");

        // ---- question bank ----
        g.MapGet("/questions", async Task<Ok<IReadOnlyList<AdminQuestionDto>>> (
                string? section, string? search, IQuestionBankService s, CancellationToken ct) =>
            TypedResults.Ok(await s.ListAsync(section, search, ct)));

        g.MapPost("/questions", async Task<Ok<AdminQuestionDto>> (
                UpsertQuestionRequest r, ClaimsPrincipal u, IQuestionBankService s, CancellationToken ct) =>
            TypedResults.Ok(await s.CreateAsync(u.UserId(), r, ct)));

        g.MapPut("/questions/{id:guid}", async Task<NoContent> (
            Guid id, UpsertQuestionRequest r, ClaimsPrincipal u, IQuestionBankService s, CancellationToken ct) =>
        { await s.UpdateAsync(u.UserId(), id, r, ct); return TypedResults.NoContent(); });

        g.MapDelete("/questions/{id:guid}", async Task<NoContent> (
            Guid id, ClaimsPrincipal u, IQuestionBankService s, CancellationToken ct) =>
        { await s.DeleteAsync(u.UserId(), id, ct); return TypedResults.NoContent(); });

        // ---- assessments ----
        g.MapGet("/assessments", async Task<Ok<IReadOnlyList<AdminAssessmentDto>>> (
                IAssessmentAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.ListAsync(ct)));

        g.MapGet("/assessments/{id:guid}", async Task<Results<Ok<AdminAssessmentDto>, NotFound>> (
            Guid id, IAssessmentAdminService s, CancellationToken ct) =>
        {
            var a = await s.GetAsync(id, ct);
            return a is null ? TypedResults.NotFound() : TypedResults.Ok(a);
        });

        g.MapPost("/assessments", async Task<Ok<AdminAssessmentDto>> (
                UpsertAssessmentRequest r, ClaimsPrincipal u, IAssessmentAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.CreateAsync(u.UserId(), r, ct)));

        g.MapPut("/assessments/{id:guid}", async Task<NoContent> (
            Guid id, UpsertAssessmentRequest r, ClaimsPrincipal u, IAssessmentAdminService s, CancellationToken ct) =>
        { await s.UpdateAsync(u.UserId(), id, r, ct); return TypedResults.NoContent(); });

        g.MapDelete("/assessments/{id:guid}", async Task<NoContent> (
            Guid id, ClaimsPrincipal u, IAssessmentAdminService s, CancellationToken ct) =>
        { await s.DeleteAsync(u.UserId(), id, ct); return TypedResults.NoContent(); });

        g.MapPut("/assessments/{id:guid}/questions", async Task<NoContent> (
            Guid id, SetAssessmentQuestionsRequest r, ClaimsPrincipal u, IAssessmentAdminService s, CancellationToken ct) =>
        { await s.SetQuestionsAsync(u.UserId(), id, r, ct); return TypedResults.NoContent(); });

        g.MapPut("/sessions/{sessionId:guid}/assessment", async Task<NoContent> (
            Guid sessionId, AttachAssessmentRequest r, ClaimsPrincipal u, IAssessmentAdminService s, CancellationToken ct) =>
        { await s.AttachToSessionAsync(u.UserId(), sessionId, r.AssessmentId, ct); return TypedResults.NoContent(); });

        return app;
    }
}

public record AttachAssessmentRequest(Guid? AssessmentId);
