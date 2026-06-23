using Academy.Application.Abstractions;
using Academy.Application.Catalog;
using Academy.Application.Learning;
using Academy.Domain;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Learning;

public class LearningService(
    AppDbContext db,
    IVideoProvider video,
    IEntitlementService entitlement,
    IModuleCompletionService completion,
    VideoOptions options) : ILearningService
{
    public async Task<PlaybackTicketDto> GetPlaybackAsync(Guid userId, Guid moduleId, CancellationToken ct = default)
    {
        var m = await db.Modules
            .Where(x => x.Id == moduleId && x.Status == ModuleStatus.Published)
            .Select(x => new { x.Id, x.ProviderAssetId, x.IsPreview, x.RequiredPlanTier })
            .FirstOrDefaultAsync(ct)
            ?? throw new LearningException("Modul tidak ditemukan.", 404);

        await EnsurePlayableAsync(userId, m.IsPreview, m.RequiredPlanTier, ct);

        var assetId = m.ProviderAssetId ?? m.Id.ToString();
        var ticket = await video.CreatePlaybackTicketAsync(
            assetId, userId, TimeSpan.FromSeconds(options.TicketTtlSeconds), ct);
        return new PlaybackTicketDto(m.Id, ticket.Url, ticket.ExpiresAt, ticket.CaptionsUrl);
    }

    public async Task<PlayerContextDto> GetPlayerContextAsync(Guid userId, Guid moduleId, CancellationToken ct = default)
    {
        var m = await db.Modules
            .Where(x => x.Id == moduleId && x.Status == ModuleStatus.Published)
            .Select(x => new
            {
                x.Id, x.Title, x.Description, x.DurationSeconds, x.IsPreview, x.RequiredPlanTier,
                TrackId = x.TrackId, TrackName = x.Track.Name, LevelName = x.Track.Level.Name,
                Resources = x.Resources.Select(r => new ResourceItemDto(r.Type.ToString(), r.Title)).ToList(),
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new LearningException("Modul tidak ditemukan.", 404);

        await EnsurePlayableAsync(userId, m.IsPreview, m.RequiredPlanTier, ct);

        var siblings = await db.Modules
            .Where(x => x.TrackId == m.TrackId && x.Status == ModuleStatus.Published)
            .OrderBy(x => x.OrderIndex)
            .Select(x => new { x.Id, x.Title, x.DurationSeconds })
            .ToListAsync(ct);

        var completedIds = (await db.WatchProgress
            .Where(w => w.UserId == userId && w.Completed)
            .Select(w => w.ModuleId).ToListAsync(ct)).ToHashSet();

        var playlist = siblings
            .Select(s => new PlaylistItemDto(s.Id, s.Title, s.DurationSeconds, completedIds.Contains(s.Id), s.Id == moduleId))
            .ToList();

        var index = playlist.FindIndex(p => p.IsCurrent);
        Guid? next = index >= 0 && index + 1 < playlist.Count ? playlist[index + 1].Id : null;
        var completedInTrack = playlist.Count(p => p.Completed);

        return new PlayerContextDto(
            m.Id, m.Title, m.Description, m.LevelName, m.TrackName, m.DurationSeconds,
            index + 1, playlist.Count, completedInTrack, next, m.Resources, playlist);
    }

    public async Task<ModuleProgressDto?> GetProgressAsync(Guid userId, Guid moduleId, CancellationToken ct = default)
    {
        var wp = await db.WatchProgress.FirstOrDefaultAsync(w => w.UserId == userId && w.ModuleId == moduleId, ct);
        return wp is null ? null : Map(wp);
    }

    public async Task<ModuleProgressDto> SaveProgressAsync(Guid userId, Guid moduleId, int positionSeconds, decimal percent, CancellationToken ct = default)
    {
        var m = await db.Modules
            .Where(x => x.Id == moduleId && x.Status == ModuleStatus.Published)
            .Select(x => new { x.Id, x.IsPreview, x.RequiredPlanTier, LevelId = x.Track.LevelId })
            .FirstOrDefaultAsync(ct)
            ?? throw new LearningException("Modul tidak ditemukan.", 404);

        await EnsurePlayableAsync(userId, m.IsPreview, m.RequiredPlanTier, ct);

        var now = DateTimeOffset.UtcNow;
        var wp = await db.WatchProgress.FirstOrDefaultAsync(w => w.UserId == userId && w.ModuleId == moduleId, ct);
        if (wp is null)
        {
            wp = new WatchProgress { Id = Guid.CreateVersion7(), UserId = userId, ModuleId = moduleId };
            db.WatchProgress.Add(wp);
        }

        wp.ResumePositionSeconds = Math.Max(0, positionSeconds);
        wp.PercentComplete = Math.Max(wp.PercentComplete, Math.Clamp(percent, 0m, 100m)); // monotonic
        wp.LastWatchedAt = now;
        await db.SaveChangesAsync(ct);

        // Completion (watch ≥ threshold AND quiz gate) is decided centrally; idempotent + non-retroactive.
        // Updates the same tracked wp instance, so the returned DTO reflects it.
        await completion.TryCompleteAsync(userId, moduleId, ct);

        return Map(wp);
    }

    public async Task<DashboardDto> GetDashboardAsync(Guid userId, CancellationToken ct = default)
    {
        var tier = await entitlement.GetActiveTierAsync(userId, ct);

        var modules = await db.Modules
            .Where(m => m.Status == ModuleStatus.Published)
            .Select(m => new ModuleRow(
                m.Id, m.Slug, m.Title, m.DurationSeconds, m.ThumbnailUrl, m.OrderIndex,
                m.Track.LevelId, m.Track.Level.Name, m.Track.Level.Slug,
                m.Track.Level.RequiredPlanTier, m.Track.Level.OrderIndex,
                m.Track.Name, m.Track.OrderIndex, m.RequiredPlanTier, m.IsPreview))
            .ToListAsync(ct);

        var progress = (await db.WatchProgress.Where(w => w.UserId == userId).ToListAsync(ct))
            .ToDictionary(w => w.ModuleId);
        var certified = (await db.Certificates.Where(c => c.UserId == userId).Select(c => c.LevelId).ToListAsync(ct))
            .ToHashSet();

        bool Entitled(ModuleRow m) => Entitlement.CanAccess(tier, m.IsPreview, m.RequiredPlanTier);
        bool Done(Guid id) => progress.TryGetValue(id, out var p) && p.Completed;

        // Per-level rollups (certified levels always display 100% — GR-6).
        var levels = modules
            .GroupBy(m => new { m.LevelId, m.LevelName, m.LevelSlug, m.LevelTier, m.LevelOrder })
            .OrderBy(g => g.Key.LevelOrder)
            .Select(g =>
            {
                var published = g.Count();
                var completed = g.Count(m => Done(m.Id));
                var isCert = certified.Contains(g.Key.LevelId);
                var pct = isCert ? 100m : published == 0 ? 0m : Math.Round(100m * completed / published, 0);
                return new LevelProgressDto(
                    g.Key.LevelId, g.Key.LevelName, g.Key.LevelSlug, g.Key.LevelTier,
                    isCert ? published : completed, published, pct, isCert,
                    tier is int t && t >= g.Key.LevelTier);
            })
            .ToList();

        var entitled = modules.Where(Entitled).ToList();
        var overallTotal = entitled.Count;
        var overallDone = entitled.Count(m => Done(m.Id));
        var overall = new OverallProgressDto(
            overallDone, overallTotal, overallTotal == 0 ? 0m : Math.Round(100m * overallDone / overallTotal, 0));

        var continueLearning = entitled
            .Where(m => progress.TryGetValue(m.Id, out var p) && !p.Completed && p.PercentComplete > 0)
            .OrderByDescending(m => progress[m.Id].LastWatchedAt)
            .Take(3).Select(ToContinue).ToList();

        var continueIds = continueLearning.Select(c => c.ModuleId).ToHashSet();
        var recommended = entitled
            .Where(m => !Done(m.Id) && !continueIds.Contains(m.Id))
            .OrderBy(m => m.LevelOrder).ThenBy(m => m.TrackOrder).ThenBy(m => m.OrderIndex)
            .Take(2).Select(ToContinue).ToList();

        return new DashboardDto(tier, continueLearning, recommended, levels, overall);

        ContinueModuleDto ToContinue(ModuleRow m) => new(
            m.Id, m.Slug, m.Title, m.LevelName, m.TrackName, m.DurationSeconds, m.ThumbnailUrl,
            progress.TryGetValue(m.Id, out var p) ? p.PercentComplete : 0m);
    }

    private async Task EnsurePlayableAsync(Guid userId, bool isPreview, int requiredTier, CancellationToken ct)
    {
        if (isPreview) return;
        var tier = await entitlement.GetActiveTierAsync(userId, ct);
        if (!Entitlement.CanAccess(tier, isPreview, requiredTier))
            throw new LearningException("Anda belum memiliki akses ke modul ini.", 403);
    }

    private static ModuleProgressDto Map(WatchProgress w) =>
        new(w.ModuleId, w.ResumePositionSeconds, w.PercentComplete, w.Completed, w.CompletedAt);

    private record ModuleRow(
        Guid Id, string Slug, string Title, int DurationSeconds, string? ThumbnailUrl, int OrderIndex,
        Guid LevelId, string LevelName, string LevelSlug, int LevelTier, int LevelOrder,
        string TrackName, int TrackOrder, int RequiredPlanTier, bool IsPreview);
}
