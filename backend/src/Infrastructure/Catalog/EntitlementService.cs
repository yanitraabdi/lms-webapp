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
        // Any non-expired subscription still inside its paid period grants its tier —
        // Active, PastDue, Grace, and Canceled-until-period-end all keep access (GR-7,
        // SubscriptionStateMachine.GrantsAccess). Only Expired loses it.
        var tiers = await db.Subscriptions
            .Where(s => s.UserId == userId
                        && s.Status != SubscriptionStatus.Expired
                        && s.CurrentPeriodEnd > now)
            .Select(s => s.Plan.TierLevel)
            .ToListAsync(ct);

        return tiers.Count == 0 ? null : tiers.Max();
    }
}
