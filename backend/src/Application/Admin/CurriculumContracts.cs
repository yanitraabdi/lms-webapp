namespace Academy.Application.Admin;

// ---- read DTOs ----

public record LevelDto(Guid Id, string Name, string Slug, int RequiredPlanTier, int OrderIndex, bool Published, int TrackCount);
public record TrackDto(Guid Id, Guid LevelId, string Name, string Slug, int OrderIndex, int ModuleCount);
public record CategoryDto(Guid Id, string Name, string Slug, int ModuleCount);
public record TagDto(Guid Id, string Name, string Slug);
public record AdminResourceDto(Guid Id, Guid ModuleId, string Type, string Ref, string Title);

public record AdminModuleDetailDto(
    Guid Id, Guid TrackId, Guid? CategoryId, string Title, string Slug, string Description, string? Summary,
    int DurationSeconds, string? ProviderAssetId, string? ThumbnailUrl, int OrderIndex, bool Published,
    bool IsPreview, int RequiredPlanTier, IReadOnlyList<Guid> TagIds);

// ---- write requests ----

public record UpsertLevelRequest(string Name, string? Slug, int RequiredPlanTier, int OrderIndex, bool Published);
public record UpsertTrackRequest(Guid LevelId, string Name, string? Slug, int OrderIndex);
public record UpsertCategoryRequest(string Name, string? Slug);
public record UpsertTagRequest(string Name, string? Slug);
public record UpsertResourceRequest(string Type, string Ref, string Title);

public record UpsertModuleRequest(
    Guid TrackId, Guid? CategoryId, string Title, string? Slug, string Description, string? Summary,
    int DurationSeconds, string? ProviderAssetId, string? ThumbnailUrl, int OrderIndex,
    bool IsPreview, int RequiredPlanTier, bool Published, IReadOnlyList<Guid>? TagIds);

// ---- service ----

/// <summary>Full curriculum content-ops CRUD (admin). Mutations are audit-logged and trigger
/// ISR revalidation. Deletes are blocked (409) when retained data (progress/certificates) references the row.</summary>
public interface ICurriculumAdminService
{
    Task<IReadOnlyList<LevelDto>> GetLevelsAsync(CancellationToken ct = default);
    Task<LevelDto> CreateLevelAsync(Guid actor, UpsertLevelRequest req, CancellationToken ct = default);
    Task UpdateLevelAsync(Guid actor, Guid id, UpsertLevelRequest req, CancellationToken ct = default);
    Task DeleteLevelAsync(Guid actor, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<TrackDto>> GetTracksAsync(Guid? levelId, CancellationToken ct = default);
    Task<TrackDto> CreateTrackAsync(Guid actor, UpsertTrackRequest req, CancellationToken ct = default);
    Task UpdateTrackAsync(Guid actor, Guid id, UpsertTrackRequest req, CancellationToken ct = default);
    Task DeleteTrackAsync(Guid actor, Guid id, CancellationToken ct = default);

    Task<AdminModuleDetailDto> GetModuleAsync(Guid id, CancellationToken ct = default);
    Task<AdminModuleDetailDto> CreateModuleAsync(Guid actor, UpsertModuleRequest req, CancellationToken ct = default);
    Task UpdateModuleAsync(Guid actor, Guid id, UpsertModuleRequest req, CancellationToken ct = default);
    Task DeleteModuleAsync(Guid actor, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default);
    Task<CategoryDto> CreateCategoryAsync(Guid actor, UpsertCategoryRequest req, CancellationToken ct = default);
    Task UpdateCategoryAsync(Guid actor, Guid id, UpsertCategoryRequest req, CancellationToken ct = default);
    Task DeleteCategoryAsync(Guid actor, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<TagDto>> GetTagsAsync(CancellationToken ct = default);
    Task<TagDto> CreateTagAsync(Guid actor, UpsertTagRequest req, CancellationToken ct = default);
    Task UpdateTagAsync(Guid actor, Guid id, UpsertTagRequest req, CancellationToken ct = default);
    Task DeleteTagAsync(Guid actor, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<AdminResourceDto>> GetResourcesAsync(Guid moduleId, CancellationToken ct = default);
    Task<AdminResourceDto> CreateResourceAsync(Guid actor, Guid moduleId, UpsertResourceRequest req, CancellationToken ct = default);
    Task UpdateResourceAsync(Guid actor, Guid id, UpsertResourceRequest req, CancellationToken ct = default);
    Task DeleteResourceAsync(Guid actor, Guid id, CancellationToken ct = default);
}
