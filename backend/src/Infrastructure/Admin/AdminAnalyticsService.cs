using Academy.Application.Admin;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Admin;

public class AdminAnalyticsService(AppDbContext db) : IAdminAnalyticsService
{
    public async Task<AdminAnalyticsDto> GetAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var since = now.AddDays(-30);

        var totalUsers = await db.Users.CountAsync(ct);
        var signups = await db.Users.CountAsync(u => u.CreatedAt >= since, ct);
        var activeSubs = await db.Subscriptions.CountAsync(
            s => s.Status != SubscriptionStatus.Expired && s.CurrentPeriodEnd > now, ct);

        var byTierRaw = await db.Subscriptions
            .Where(s => s.Status != SubscriptionStatus.Expired && s.CurrentPeriodEnd > now)
            .GroupBy(s => new { s.Plan.TierLevel, s.Plan.Name })
            .Select(grp => new { grp.Key.TierLevel, grp.Key.Name, Count = grp.Count() })
            .ToListAsync(ct);
        var byTier = byTierRaw
            .OrderBy(x => x.TierLevel)
            .Select(x => new TierCountDto(x.TierLevel, x.Name, x.Count))
            .ToList();

        var completions = await db.WatchProgress.CountAsync(w => w.Completed && w.CompletedAt >= since, ct);
        var certs = await db.Certificates.CountAsync(ct);

        var watch = await db.WatchProgress
            .GroupBy(w => w.ModuleId)
            .Select(grp => new { ModuleId = grp.Key, Viewers = grp.Count() })
            .OrderByDescending(x => x.Viewers)
            .Take(5)
            .ToListAsync(ct);
        var ids = watch.Select(w => w.ModuleId).ToList();
        var titles = await db.Modules.Where(m => ids.Contains(m.Id))
            .Select(m => new { m.Id, m.Title }).ToListAsync(ct);
        var mostWatched = watch
            .Select(w => new ModuleWatchDto(titles.FirstOrDefault(t => t.Id == w.ModuleId)?.Title ?? "—", w.Viewers))
            .ToList();

        return new AdminAnalyticsDto(totalUsers, signups, activeSubs, byTier, completions, certs, mostWatched);
    }
}
