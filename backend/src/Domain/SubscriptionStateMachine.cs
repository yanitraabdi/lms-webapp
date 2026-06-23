using Academy.Domain.Enums;

namespace Academy.Domain;

/// <summary>
/// Allowed subscription status transitions (TSD §9.2). Transitions are driven by
/// verified webhooks (payment success/failure), the dunning/grace timeline, and
/// explicit user cancellation. Expired is terminal — re-subscribing creates a new row.
/// </summary>
public static class SubscriptionStateMachine
{
    private static readonly Dictionary<SubscriptionStatus, SubscriptionStatus[]> Allowed = new()
    {
        // payment fail → PastDue; user cancel → Canceled; renewal keeps Active (from==to).
        [SubscriptionStatus.Active] = [SubscriptionStatus.PastDue, SubscriptionStatus.Canceled],
        // retry success → Active; retries exhausted → Grace; user cancel → Canceled.
        [SubscriptionStatus.PastDue] = [SubscriptionStatus.Active, SubscriptionStatus.Grace, SubscriptionStatus.Canceled],
        // late payment → Active; grace window ends → Expired; user cancel → Canceled.
        [SubscriptionStatus.Grace] = [SubscriptionStatus.Active, SubscriptionStatus.Expired, SubscriptionStatus.Canceled],
        // access runs until period end, then Expired.
        [SubscriptionStatus.Canceled] = [SubscriptionStatus.Expired],
        [SubscriptionStatus.Expired] = [],
    };

    public static bool CanTransition(SubscriptionStatus from, SubscriptionStatus to)
        => from == to || (Allowed.TryGetValue(from, out var targets) && Array.IndexOf(targets, to) >= 0);

    /// <summary>A non-expired subscription still inside its paid period grants its tier
    /// (Active, PastDue, Grace, and Canceled-until-period-end all keep access; GR-7).</summary>
    public static bool GrantsAccess(SubscriptionStatus status, DateTimeOffset periodEnd, DateTimeOffset now)
        => status != SubscriptionStatus.Expired && periodEnd > now;
}
