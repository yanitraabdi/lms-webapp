using Academy.Application.Catalog;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Catalog;

/// <summary>Resolves the highest tier from a user's active (or in-grace) subscriptions.
/// Returns null when there is none — so only preview content is accessible (GR-1).</summary>
public class EntitlementService(AppDbContext db) : IEntitlementService
{
    public async Task<int?> GetActiveTierAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var tiers = await db.Subscriptions
            .Where(s => s.UserId == userId
                        && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Grace)
                        && s.CurrentPeriodEnd > now)
            .Select(s => s.Plan.TierLevel)
            .ToListAsync(ct);

        return tiers.Count == 0 ? null : tiers.Max();
    }
}
