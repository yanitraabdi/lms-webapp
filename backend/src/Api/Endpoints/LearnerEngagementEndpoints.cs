using System.Security.Claims;
using Academy.Application.Engagement;
using Academy.Application.Learning;
using Academy.Domain.Enums;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Academy.Api.Endpoints;

public static class LearnerEngagementEndpoints
{
    public static IEndpointRouteBuilder MapLearnerEngagementEndpoints(this IEndpointRouteBuilder app)
    {
        var m = app.MapGroup("/api/modules").RequireAuthorization().WithTags("Engagement");

        // ---- notes & bookmarks ----
        m.MapGet("/{id:guid}/notes", async Task<Ok<IReadOnlyList<VideoNoteDto>>> (Guid id, ClaimsPrincipal u, INotesService s, CancellationToken ct) =>
            TypedResults.Ok(await s.ListAsync(u.UserId(), id, ct)));

        m.MapPost("/{id:guid}/notes", async Task<Ok<VideoNoteDto>> (Guid id, CreateNoteRequest r, ClaimsPrincipal u, INotesService s, CancellationToken ct) =>
        {
            var type = r.Type.Equals("Bookmark", StringComparison.OrdinalIgnoreCase) ? NoteType.Bookmark : NoteType.Note;
            return TypedResults.Ok(await s.CreateAsync(u.UserId(), id, r.TimestampSeconds, type, r.Text, ct));
        });

        // ---- quiz (learner) ----
        m.MapGet("/{id:guid}/quiz", async Task<Results<Ok<QuizDto>, NoContent>> (Guid id, ClaimsPrincipal u, IQuizService s, CancellationToken ct) =>
        {
            var q = await s.GetForModuleAsync(u.UserId(), id, ct);
            return q is null ? TypedResults.NoContent() : TypedResults.Ok(q);
        });

        m.MapPost("/{id:guid}/quiz/attempt", async Task<Ok<QuizResultDto>> (Guid id, SubmitQuizRequest r, ClaimsPrincipal u, IQuizService s, CancellationToken ct) =>
            TypedResults.Ok(await s.SubmitAsync(u.UserId(), id, r.Answers, ct)));

        // ---- module rating ----
        m.MapPut("/{id:guid}/feedback", async Task<NoContent> (Guid id, UpsertFeedbackRequest r, ClaimsPrincipal u, IModuleFeedbackService s, CancellationToken ct) =>
        { await s.UpsertAsync(u.UserId(), id, r.Rating, r.Comment, ct); return TypedResults.NoContent(); });

        // delete note (top-level, auth)
        app.MapDelete("/api/notes/{id:guid}", async Task<NoContent> (Guid id, ClaimsPrincipal u, INotesService s, CancellationToken ct) =>
        { await s.DeleteAsync(u.UserId(), id, ct); return TypedResults.NoContent(); }).RequireAuthorization().WithTags("Engagement");

        // module rating summary — public, personalised when a bearer is present
        app.MapGet("/api/modules/{id:guid}/feedback", async Task<Ok<ModuleFeedbackDto>> (Guid id, ClaimsPrincipal u, IModuleFeedbackService s, CancellationToken ct) =>
        {
            var uid = Guid.TryParse(u.FindFirstValue("sub"), out var g) ? g : (Guid?)null;
            return TypedResults.Ok(await s.GetAsync(uid, id, ct));
        }).WithTags("Engagement");

        return app;
    }
}
