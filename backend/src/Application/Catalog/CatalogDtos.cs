namespace Academy.Application.Catalog;

/// <summary>Per-user access state for a module in the catalog/detail views.</summary>
public enum ModuleAccess { Entitled, Preview, Locked }

public record CatalogFilters(
    IReadOnlyList<string>? Levels = null,     // level slugs (multi)
    IReadOnlyList<string>? Categories = null, // category slugs (multi)
    IReadOnlyList<string>? Tags = null,       // tag slugs (multi)
    string? Search = null,                // free-text over title + description
    string Sort = "curriculum",           // curriculum | newest | duration
    int Skip = 0,
    int Take = 24);

public record ModuleSummaryDto(
    Guid Id,
    string Slug,
    string Title,
    string? Summary,
    string LevelName,
    string LevelSlug,
    int RequiredPlanTier,
    string TrackName,
    string? CategoryName,
    int DurationSeconds,
    string? ThumbnailUrl,
    bool IsPreview,
    IReadOnlyList<string> Tags,
    ModuleAccess Access,
    DateTimeOffset? PublishedAt);

public record ResourceDto(string Type, string Title);

public record ModuleDetailDto(
    Guid Id,
    string Slug,
    string Title,
    string Description,
    string? Summary,
    string LevelName,
    string LevelSlug,
    int RequiredPlanTier,
    string TrackName,
    string? CategoryName,
    int DurationSeconds,
    string? ThumbnailUrl,
    bool IsPreview,
    IReadOnlyList<string> Tags,
    ModuleAccess Access,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<ResourceDto> Resources);

public record CatalogPageDto(IReadOnlyList<ModuleSummaryDto> Modules, int Total);

// ---- facets for the filter sidebar ----
public record FacetLevel(string Slug, string Name, int RequiredPlanTier, int Count);
public record FacetCategory(string Slug, string Name, int Count);
public record FacetTag(string Slug, string Name);
public record CatalogFacetsDto(
    IReadOnlyList<FacetLevel> Levels,
    IReadOnlyList<FacetCategory> Categories,
    IReadOnlyList<FacetTag> Tags);
