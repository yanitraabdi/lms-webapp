using System.Security.Claims;
using Academy.Application.Admin;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Academy.Api.Endpoints;

public static class CurriculumAdminEndpoints
{
    public static IEndpointRouteBuilder MapCurriculumAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin").RequireAuthorization("Admin").WithTags("AdminCurriculum");
        static Guid Actor(ClaimsPrincipal u) => u.UserId();

        // ---- levels ----
        g.MapGet("/levels", async Task<Ok<IReadOnlyList<LevelDto>>> (ICurriculumAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.GetLevelsAsync(ct)));
        g.MapPost("/levels", async Task<Ok<LevelDto>> (UpsertLevelRequest r, ClaimsPrincipal u, ICurriculumAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.CreateLevelAsync(Actor(u), r, ct)));
        g.MapPut("/levels/{id:guid}", async Task<NoContent> (Guid id, UpsertLevelRequest r, ClaimsPrincipal u, ICurriculumAdminService s, CancellationToken ct) =>
        { await s.UpdateLevelAsync(Actor(u), id, r, ct); return TypedResults.NoContent(); });
        g.MapDelete("/levels/{id:guid}", async Task<NoContent> (Guid id, ClaimsPrincipal u, ICurriculumAdminService s, CancellationToken ct) =>
        { await s.DeleteLevelAsync(Actor(u), id, ct); return TypedResults.NoContent(); });

        // ---- tracks ----
        g.MapGet("/tracks", async Task<Ok<IReadOnlyList<TrackDto>>> (Guid? levelId, ICurriculumAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.GetTracksAsync(levelId, ct)));
        g.MapPost("/tracks", async Task<Ok<TrackDto>> (UpsertTrackRequest r, ClaimsPrincipal u, ICurriculumAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.CreateTrackAsync(Actor(u), r, ct)));
        g.MapPut("/tracks/{id:guid}", async Task<NoContent> (Guid id, UpsertTrackRequest r, ClaimsPrincipal u, ICurriculumAdminService s, CancellationToken ct) =>
        { await s.UpdateTrackAsync(Actor(u), id, r, ct); return TypedResults.NoContent(); });
        g.MapDelete("/tracks/{id:guid}", async Task<NoContent> (Guid id, ClaimsPrincipal u, ICurriculumAdminService s, CancellationToken ct) =>
        { await s.DeleteTrackAsync(Actor(u), id, ct); return TypedResults.NoContent(); });

        // ---- modules (list + publish toggle live in AdminEndpoints) ----
        g.MapGet("/modules/{id:guid}", async Task<Ok<AdminModuleDetailDto>> (Guid id, ICurriculumAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.GetModuleAsync(id, ct)));
        g.MapPost("/modules", async Task<Ok<AdminModuleDetailDto>> (UpsertModuleRequest r, ClaimsPrincipal u, ICurriculumAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.CreateModuleAsync(Actor(u), r, ct)));
        g.MapPut("/modules/{id:guid}", async Task<NoContent> (Guid id, UpsertModuleRequest r, ClaimsPrincipal u, ICurriculumAdminService s, CancellationToken ct) =>
        { await s.UpdateModuleAsync(Actor(u), id, r, ct); return TypedResults.NoContent(); });
        g.MapDelete("/modules/{id:guid}", async Task<NoContent> (Guid id, ClaimsPrincipal u, ICurriculumAdminService s, CancellationToken ct) =>
        { await s.DeleteModuleAsync(Actor(u), id, ct); return TypedResults.NoContent(); });

        // ---- categories ----
        g.MapGet("/categories", async Task<Ok<IReadOnlyList<CategoryDto>>> (ICurriculumAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.GetCategoriesAsync(ct)));
        g.MapPost("/categories", async Task<Ok<CategoryDto>> (UpsertCategoryRequest r, ClaimsPrincipal u, ICurriculumAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.CreateCategoryAsync(Actor(u), r, ct)));
        g.MapPut("/categories/{id:guid}", async Task<NoContent> (Guid id, UpsertCategoryRequest r, ClaimsPrincipal u, ICurriculumAdminService s, CancellationToken ct) =>
        { await s.UpdateCategoryAsync(Actor(u), id, r, ct); return TypedResults.NoContent(); });
        g.MapDelete("/categories/{id:guid}", async Task<NoContent> (Guid id, ClaimsPrincipal u, ICurriculumAdminService s, CancellationToken ct) =>
        { await s.DeleteCategoryAsync(Actor(u), id, ct); return TypedResults.NoContent(); });

        // ---- tags ----
        g.MapGet("/tags", async Task<Ok<IReadOnlyList<TagDto>>> (ICurriculumAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.GetTagsAsync(ct)));
        g.MapPost("/tags", async Task<Ok<TagDto>> (UpsertTagRequest r, ClaimsPrincipal u, ICurriculumAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.CreateTagAsync(Actor(u), r, ct)));
        g.MapPut("/tags/{id:guid}", async Task<NoContent> (Guid id, UpsertTagRequest r, ClaimsPrincipal u, ICurriculumAdminService s, CancellationToken ct) =>
        { await s.UpdateTagAsync(Actor(u), id, r, ct); return TypedResults.NoContent(); });
        g.MapDelete("/tags/{id:guid}", async Task<NoContent> (Guid id, ClaimsPrincipal u, ICurriculumAdminService s, CancellationToken ct) =>
        { await s.DeleteTagAsync(Actor(u), id, ct); return TypedResults.NoContent(); });

        // ---- resources (per module) ----
        g.MapGet("/modules/{moduleId:guid}/resources", async Task<Ok<IReadOnlyList<AdminResourceDto>>> (Guid moduleId, ICurriculumAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.GetResourcesAsync(moduleId, ct)));
        g.MapPost("/modules/{moduleId:guid}/resources", async Task<Ok<AdminResourceDto>> (Guid moduleId, UpsertResourceRequest r, ClaimsPrincipal u, ICurriculumAdminService s, CancellationToken ct) =>
            TypedResults.Ok(await s.CreateResourceAsync(Actor(u), moduleId, r, ct)));
        g.MapPut("/resources/{id:guid}", async Task<NoContent> (Guid id, UpsertResourceRequest r, ClaimsPrincipal u, ICurriculumAdminService s, CancellationToken ct) =>
        { await s.UpdateResourceAsync(Actor(u), id, r, ct); return TypedResults.NoContent(); });
        g.MapDelete("/resources/{id:guid}", async Task<NoContent> (Guid id, ClaimsPrincipal u, ICurriculumAdminService s, CancellationToken ct) =>
        { await s.DeleteResourceAsync(Actor(u), id, ct); return TypedResults.NoContent(); });

        return app;
    }
}
