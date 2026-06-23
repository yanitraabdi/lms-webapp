using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Academy.Application.Abstractions;
using Academy.Application.Admin;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Admin;

public class CurriculumAdminService(AppDbContext db, IContentRevalidator revalidator) : ICurriculumAdminService
{
    // ---------------- Levels ----------------

    public async Task<IReadOnlyList<LevelDto>> GetLevelsAsync(CancellationToken ct = default)
        => await db.Levels.OrderBy(l => l.OrderIndex)
            .Select(l => new LevelDto(l.Id, l.Name, l.Slug, l.RequiredPlanTier, l.OrderIndex,
                l.Status == ModuleStatus.Published, db.Tracks.Count(t => t.LevelId == l.Id)))
            .ToListAsync(ct);

    public async Task<LevelDto> CreateLevelAsync(Guid actor, UpsertLevelRequest req, CancellationToken ct = default)
    {
        var level = new Level
        {
            Id = Guid.CreateVersion7(),
            Name = req.Name.Trim(),
            Slug = Slug(req.Slug, req.Name),
            RequiredPlanTier = req.RequiredPlanTier,
            OrderIndex = req.OrderIndex,
            Status = req.Published ? ModuleStatus.Published : ModuleStatus.Draft,
        };
        db.Levels.Add(level);
        Audit(actor, "level_created", level.Id, new { level.Name, level.Slug });
        await SaveAsync(ct);
        await Revalidate(ct);
        return new LevelDto(level.Id, level.Name, level.Slug, level.RequiredPlanTier, level.OrderIndex, req.Published, 0);
    }

    public async Task UpdateLevelAsync(Guid actor, Guid id, UpsertLevelRequest req, CancellationToken ct = default)
    {
        var level = await db.Levels.FindAsync([id], ct) ?? throw NotFound("Level");
        level.Name = req.Name.Trim();
        level.Slug = Slug(req.Slug, req.Name);
        level.RequiredPlanTier = req.RequiredPlanTier;
        level.OrderIndex = req.OrderIndex;
        level.Status = req.Published ? ModuleStatus.Published : ModuleStatus.Draft;
        Audit(actor, "level_updated", id, new { level.Name });
        await SaveAsync(ct);
        await Revalidate(ct);
    }

    public async Task DeleteLevelAsync(Guid actor, Guid id, CancellationToken ct = default)
    {
        var level = await db.Levels.FindAsync([id], ct) ?? throw NotFound("Level");
        db.Levels.Remove(level);
        Audit(actor, "level_deleted", id, new { level.Name });
        await SaveGuardedAsync(ct);
        await Revalidate(ct);
    }

    // ---------------- Tracks ----------------

    public async Task<IReadOnlyList<TrackDto>> GetTracksAsync(Guid? levelId, CancellationToken ct = default)
    {
        var q = db.Tracks.AsQueryable();
        if (levelId is Guid lid) q = q.Where(t => t.LevelId == lid);
        return await q.OrderBy(t => t.OrderIndex)
            .Select(t => new TrackDto(t.Id, t.LevelId, t.Name, t.Slug, t.OrderIndex, db.Modules.Count(m => m.TrackId == t.Id)))
            .ToListAsync(ct);
    }

    public async Task<TrackDto> CreateTrackAsync(Guid actor, UpsertTrackRequest req, CancellationToken ct = default)
    {
        var track = new Track
        {
            Id = Guid.CreateVersion7(), LevelId = req.LevelId, Name = req.Name.Trim(),
            Slug = Slug(req.Slug, req.Name), OrderIndex = req.OrderIndex,
        };
        db.Tracks.Add(track);
        Audit(actor, "track_created", track.Id, new { track.Name, track.LevelId });
        await SaveAsync(ct);
        await Revalidate(ct);
        return new TrackDto(track.Id, track.LevelId, track.Name, track.Slug, track.OrderIndex, 0);
    }

    public async Task UpdateTrackAsync(Guid actor, Guid id, UpsertTrackRequest req, CancellationToken ct = default)
    {
        var track = await db.Tracks.FindAsync([id], ct) ?? throw NotFound("Track");
        track.LevelId = req.LevelId;
        track.Name = req.Name.Trim();
        track.Slug = Slug(req.Slug, req.Name);
        track.OrderIndex = req.OrderIndex;
        Audit(actor, "track_updated", id, new { track.Name });
        await SaveAsync(ct);
        await Revalidate(ct);
    }

    public async Task DeleteTrackAsync(Guid actor, Guid id, CancellationToken ct = default)
    {
        var track = await db.Tracks.FindAsync([id], ct) ?? throw NotFound("Track");
        db.Tracks.Remove(track);
        Audit(actor, "track_deleted", id, new { track.Name });
        await SaveGuardedAsync(ct);
        await Revalidate(ct);
    }

    // ---------------- Modules ----------------

    public async Task<AdminModuleDetailDto> GetModuleAsync(Guid id, CancellationToken ct = default)
    {
        var m = await db.Modules.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFound("Modul");
        var tagIds = await db.ModuleTags.Where(mt => mt.ModuleId == id).Select(mt => mt.TagId).ToListAsync(ct);
        return Map(m, tagIds);
    }

    public async Task<AdminModuleDetailDto> CreateModuleAsync(Guid actor, UpsertModuleRequest req, CancellationToken ct = default)
    {
        var m = new Module { Id = Guid.CreateVersion7() };
        Apply(m, req);
        if (req.Published) m.PublishedAt = DateTimeOffset.UtcNow;
        db.Modules.Add(m);
        await SyncTags(m.Id, req.TagIds, ct);
        Audit(actor, "module_created", m.Id, new { m.Title, m.Slug });
        await SaveAsync(ct);
        await Revalidate(ct, m.Slug);
        return Map(m, req.TagIds ?? []);
    }

    public async Task UpdateModuleAsync(Guid actor, Guid id, UpsertModuleRequest req, CancellationToken ct = default)
    {
        var m = await db.Modules.FindAsync([id], ct) ?? throw NotFound("Modul");
        var wasPublished = m.Status == ModuleStatus.Published;
        Apply(m, req);
        if (req.Published && !wasPublished && m.PublishedAt is null) m.PublishedAt = DateTimeOffset.UtcNow;
        m.LastRefreshedAt = DateTimeOffset.UtcNow;
        await SyncTags(id, req.TagIds, ct);
        Audit(actor, "module_updated", id, new { m.Title });
        await SaveAsync(ct);
        await Revalidate(ct, m.Slug);
    }

    public async Task DeleteModuleAsync(Guid actor, Guid id, CancellationToken ct = default)
    {
        var m = await db.Modules.FindAsync([id], ct) ?? throw NotFound("Modul");
        db.Modules.Remove(m);
        Audit(actor, "module_deleted", id, new { m.Title });
        await SaveGuardedAsync(ct);
        await Revalidate(ct, m.Slug);
    }

    // ---------------- Categories ----------------

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default)
        => await db.Categories.OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, db.Modules.Count(m => m.CategoryId == c.Id)))
            .ToListAsync(ct);

    public async Task<CategoryDto> CreateCategoryAsync(Guid actor, UpsertCategoryRequest req, CancellationToken ct = default)
    {
        var c = new Category { Id = Guid.CreateVersion7(), Name = req.Name.Trim(), Slug = Slug(req.Slug, req.Name) };
        db.Categories.Add(c);
        Audit(actor, "category_created", c.Id, new { c.Name });
        await SaveAsync(ct);
        return new CategoryDto(c.Id, c.Name, c.Slug, 0);
    }

    public async Task UpdateCategoryAsync(Guid actor, Guid id, UpsertCategoryRequest req, CancellationToken ct = default)
    {
        var c = await db.Categories.FindAsync([id], ct) ?? throw NotFound("Kategori");
        c.Name = req.Name.Trim();
        c.Slug = Slug(req.Slug, req.Name);
        Audit(actor, "category_updated", id, new { c.Name });
        await SaveAsync(ct);
    }

    public async Task DeleteCategoryAsync(Guid actor, Guid id, CancellationToken ct = default)
    {
        var c = await db.Categories.FindAsync([id], ct) ?? throw NotFound("Kategori");
        db.Categories.Remove(c);
        Audit(actor, "category_deleted", id, new { c.Name });
        await SaveGuardedAsync(ct);
    }

    // ---------------- Tags ----------------

    public async Task<IReadOnlyList<TagDto>> GetTagsAsync(CancellationToken ct = default)
        => await db.Tags.OrderBy(t => t.Name).Select(t => new TagDto(t.Id, t.Name, t.Slug)).ToListAsync(ct);

    public async Task<TagDto> CreateTagAsync(Guid actor, UpsertTagRequest req, CancellationToken ct = default)
    {
        var t = new Tag { Id = Guid.CreateVersion7(), Name = req.Name.Trim(), Slug = Slug(req.Slug, req.Name) };
        db.Tags.Add(t);
        Audit(actor, "tag_created", t.Id, new { t.Name });
        await SaveAsync(ct);
        return new TagDto(t.Id, t.Name, t.Slug);
    }

    public async Task UpdateTagAsync(Guid actor, Guid id, UpsertTagRequest req, CancellationToken ct = default)
    {
        var t = await db.Tags.FindAsync([id], ct) ?? throw NotFound("Tag");
        t.Name = req.Name.Trim();
        t.Slug = Slug(req.Slug, req.Name);
        Audit(actor, "tag_updated", id, new { t.Name });
        await SaveAsync(ct);
    }

    public async Task DeleteTagAsync(Guid actor, Guid id, CancellationToken ct = default)
    {
        var t = await db.Tags.FindAsync([id], ct) ?? throw NotFound("Tag");
        db.Tags.Remove(t);
        Audit(actor, "tag_deleted", id, new { t.Name });
        await SaveGuardedAsync(ct);
    }

    // ---------------- Resources ----------------

    public async Task<IReadOnlyList<AdminResourceDto>> GetResourcesAsync(Guid moduleId, CancellationToken ct = default)
        => await db.Resources.Where(r => r.ModuleId == moduleId)
            .Select(r => new AdminResourceDto(r.Id, r.ModuleId, r.Type.ToString(), r.Ref, r.Title)).ToListAsync(ct);

    public async Task<AdminResourceDto> CreateResourceAsync(Guid actor, Guid moduleId, UpsertResourceRequest req, CancellationToken ct = default)
    {
        var r = new Resource { Id = Guid.CreateVersion7(), ModuleId = moduleId, Type = ParseType(req.Type), Ref = req.Ref.Trim(), Title = req.Title.Trim() };
        db.Resources.Add(r);
        Audit(actor, "resource_created", r.Id, new { r.Title, r.ModuleId });
        await SaveAsync(ct);
        return new AdminResourceDto(r.Id, r.ModuleId, r.Type.ToString(), r.Ref, r.Title);
    }

    public async Task UpdateResourceAsync(Guid actor, Guid id, UpsertResourceRequest req, CancellationToken ct = default)
    {
        var r = await db.Resources.FindAsync([id], ct) ?? throw NotFound("Materi");
        r.Type = ParseType(req.Type);
        r.Ref = req.Ref.Trim();
        r.Title = req.Title.Trim();
        Audit(actor, "resource_updated", id, new { r.Title });
        await SaveAsync(ct);
    }

    public async Task DeleteResourceAsync(Guid actor, Guid id, CancellationToken ct = default)
    {
        var r = await db.Resources.FindAsync([id], ct) ?? throw NotFound("Materi");
        db.Resources.Remove(r);
        Audit(actor, "resource_deleted", id, new { r.Title });
        await SaveAsync(ct);
    }

    // ---------------- helpers ----------------

    private static void Apply(Module m, UpsertModuleRequest req)
    {
        m.TrackId = req.TrackId;
        m.CategoryId = req.CategoryId;
        m.Title = req.Title.Trim();
        m.Slug = Slug(req.Slug, req.Title);
        m.Description = req.Description.Trim();
        m.Summary = req.Summary?.Trim();
        m.DurationSeconds = Math.Max(0, req.DurationSeconds);
        m.ProviderAssetId = string.IsNullOrWhiteSpace(req.ProviderAssetId) ? null : req.ProviderAssetId.Trim();
        m.ThumbnailUrl = string.IsNullOrWhiteSpace(req.ThumbnailUrl) ? null : req.ThumbnailUrl.Trim();
        m.OrderIndex = req.OrderIndex;
        m.IsPreview = req.IsPreview;
        m.RequiredPlanTier = req.RequiredPlanTier;
        m.Status = req.Published ? ModuleStatus.Published : ModuleStatus.Draft;
    }

    private async Task SyncTags(Guid moduleId, IReadOnlyList<Guid>? tagIds, CancellationToken ct)
    {
        var existing = await db.ModuleTags.Where(mt => mt.ModuleId == moduleId).ToListAsync(ct);
        db.ModuleTags.RemoveRange(existing);
        foreach (var tagId in (tagIds ?? []).Distinct())
            db.ModuleTags.Add(new ModuleTag { ModuleId = moduleId, TagId = tagId });
    }

    private static AdminModuleDetailDto Map(Module m, IReadOnlyList<Guid> tagIds) => new(
        m.Id, m.TrackId, m.CategoryId, m.Title, m.Slug, m.Description, m.Summary, m.DurationSeconds,
        m.ProviderAssetId, m.ThumbnailUrl, m.OrderIndex, m.Status == ModuleStatus.Published, m.IsPreview,
        m.RequiredPlanTier, tagIds);

    private static ResourceType ParseType(string s) =>
        s.Equals("link", StringComparison.OrdinalIgnoreCase) ? ResourceType.Link : ResourceType.Pdf;

    private void Audit(Guid actor, string action, Guid target, object metadata) =>
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.CreateVersion7(), ActorUserId = actor, Action = action,
            Target = target.ToString(), Metadata = JsonSerializer.Serialize(metadata),
        });

    private static AdminException NotFound(string what) => new($"{what} tidak ditemukan.", 404);

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new AdminException("Gagal menyimpan — slug mungkin sudah dipakai.", 409); }
    }

    private async Task SaveGuardedAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new AdminException("Tidak bisa dihapus karena masih ada data terkait (mis. progres atau sertifikat).", 409); }
    }

    private Task Revalidate(CancellationToken ct, string? moduleSlug = null)
    {
        var paths = moduleSlug is null ? new[] { "/catalog" } : new[] { "/catalog", $"/modules/{moduleSlug}" };
        return revalidator.RevalidateAsync(paths, ct);
    }

    private static string Slug(string? provided, string fallbackFrom)
    {
        var basis = string.IsNullOrWhiteSpace(provided) ? fallbackFrom : provided;
        var sb = new StringBuilder();
        foreach (var ch in basis.Trim().ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(ch) ? ch : '-');
        var slug = Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? Guid.NewGuid().ToString("N")[..8] : slug;
    }
}
