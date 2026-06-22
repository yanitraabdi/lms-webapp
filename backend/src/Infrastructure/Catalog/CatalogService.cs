using Academy.Application.Catalog;
using Academy.Domain;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Catalog;

public class CatalogService(AppDbContext db, IEntitlementService entitlement) : ICatalogService
{
    public async Task<CatalogFacetsDto> GetFacetsAsync(CancellationToken ct = default)
    {
        var levels = await db.Levels
            .Where(l => l.Status == ModuleStatus.Published)
            .OrderBy(l => l.OrderIndex)
            .Select(l => new FacetLevel(
                l.Slug, l.Name, l.RequiredPlanTier,
                db.Modules.Count(m => m.Track.LevelId == l.Id && m.Status == ModuleStatus.Published)))
            .ToListAsync(ct);

        var categories = await db.Categories
            .Where(c => db.Modules.Any(m => m.CategoryId == c.Id && m.Status == ModuleStatus.Published))
            .OrderBy(c => c.Name)
            .Select(c => new FacetCategory(
                c.Slug, c.Name,
                db.Modules.Count(m => m.CategoryId == c.Id && m.Status == ModuleStatus.Published)))
            .ToListAsync(ct);

        var tags = await db.Tags.OrderBy(t => t.Name)
            .Select(t => new FacetTag(t.Slug, t.Name))
            .ToListAsync(ct);

        return new CatalogFacetsDto(levels, categories, tags);
    }

    public async Task<CatalogPageDto> GetCatalogAsync(CatalogFilters filters, Guid? userId, CancellationToken ct = default)
    {
        var activeTier = userId is Guid uid ? await entitlement.GetActiveTierAsync(uid, ct) : null;

        var query = db.Modules.Where(m => m.Status == ModuleStatus.Published);

        if (filters.Levels is { Count: > 0 })
            query = query.Where(m => filters.Levels.Contains(m.Track.Level.Slug));
        if (filters.Categories is { Count: > 0 })
            query = query.Where(m => m.Category != null && filters.Categories.Contains(m.Category.Slug));
        if (filters.Tags is { Count: > 0 })
            query = query.Where(m => m.ModuleTags.Any(mt => filters.Tags.Contains(mt.Tag.Slug)));
        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var pattern = $"%{filters.Search.Trim()}%";
            query = query.Where(m => EF.Functions.ILike(m.Title, pattern) || EF.Functions.ILike(m.Description, pattern));
        }

        query = filters.Sort switch
        {
            "newest" => query.OrderByDescending(m => m.PublishedAt),
            "duration" => query.OrderBy(m => m.DurationSeconds),
            _ => query.OrderBy(m => m.Track.Level.OrderIndex).ThenBy(m => m.Track.OrderIndex).ThenBy(m => m.OrderIndex),
        };

        var total = await query.CountAsync(ct);

        var rows = await query
            .Skip(Math.Max(0, filters.Skip))
            .Take(Math.Clamp(filters.Take, 1, 100))
            .Select(m => new Row(
                m.Id, m.Slug, m.Title, m.Summary, m.Track.Level.Name, m.Track.Level.Slug,
                m.RequiredPlanTier, m.Track.Name, m.Category != null ? m.Category.Name : null,
                m.DurationSeconds, m.ThumbnailUrl, m.IsPreview,
                m.ModuleTags.Select(mt => mt.Tag.Slug).ToList(), m.PublishedAt))
            .ToListAsync(ct);

        var modules = rows.Select(r => new ModuleSummaryDto(
            r.Id, r.Slug, r.Title, r.Summary, r.LevelName, r.LevelSlug, r.RequiredPlanTier,
            r.TrackName, r.CategoryName, r.DurationSeconds, r.ThumbnailUrl, r.IsPreview,
            r.Tags, Access(activeTier, r.IsPreview, r.RequiredPlanTier), r.PublishedAt)).ToList();

        return new CatalogPageDto(modules, total);
    }

    public async Task<ModuleDetailDto?> GetModuleBySlugAsync(string slug, Guid? userId, CancellationToken ct = default)
    {
        var activeTier = userId is Guid uid ? await entitlement.GetActiveTierAsync(uid, ct) : null;

        var m = await db.Modules
            .Where(x => x.Slug == slug && x.Status == ModuleStatus.Published)
            .Select(x => new
            {
                x.Id, x.Slug, x.Title, x.Description, x.Summary,
                LevelName = x.Track.Level.Name, LevelSlug = x.Track.Level.Slug,
                x.RequiredPlanTier, TrackName = x.Track.Name,
                CategoryName = x.Category != null ? x.Category.Name : null,
                x.DurationSeconds, x.ThumbnailUrl, x.IsPreview, x.PublishedAt,
                Tags = x.ModuleTags.Select(mt => mt.Tag.Slug).ToList(),
                Resources = x.Resources.Select(r => new ResourceDto(r.Type.ToString(), r.Title)).ToList(),
            })
            .FirstOrDefaultAsync(ct);

        if (m is null) return null;

        return new ModuleDetailDto(
            m.Id, m.Slug, m.Title, m.Description, m.Summary, m.LevelName, m.LevelSlug, m.RequiredPlanTier,
            m.TrackName, m.CategoryName, m.DurationSeconds, m.ThumbnailUrl, m.IsPreview, m.Tags,
            Access(activeTier, m.IsPreview, m.RequiredPlanTier), m.PublishedAt, m.Resources);
    }

    private static ModuleAccess Access(int? activeTier, bool isPreview, int requiredTier) =>
        isPreview ? ModuleAccess.Preview
        : Entitlement.CanAccess(activeTier, false, requiredTier) ? ModuleAccess.Entitled
        : ModuleAccess.Locked;

    private record Row(
        Guid Id, string Slug, string Title, string? Summary, string LevelName, string LevelSlug,
        int RequiredPlanTier, string TrackName, string? CategoryName, int DurationSeconds,
        string? ThumbnailUrl, bool IsPreview, List<string> Tags, DateTimeOffset? PublishedAt);
}
