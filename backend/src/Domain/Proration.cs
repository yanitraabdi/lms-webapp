namespace Academy.Domain;

/// <summary>App-side proration for mid-cycle upgrades (TSD §9.4).</summary>
public static class Proration
{
    /// <summary>
    /// The immediate prorated charge (whole rupiah) for upgrading from a cheaper to a
    /// pricier plan part-way through the current billing period:
    ///   delta = (newCyclePrice − currentCyclePrice) × remainingDays ÷ cycleDays
    /// Both prices must be for the SAME billing cycle (both monthly, or both annual) as
    /// the subscription. Result is clamped to ≥ 0 and rounded to whole rupiah — IDR is
    /// charged in whole rupiah (CLAUDE.md money rule).
    /// </summary>
    public static decimal UpgradeDelta(decimal currentCyclePrice, decimal newCyclePrice, int remainingDays, int cycleDays)
    {
        if (cycleDays <= 0) return 0m;
        var remaining = Math.Clamp(remainingDays, 0, cycleDays);
        var raw = (newCyclePrice - currentCyclePrice) * remaining / cycleDays;
        return raw <= 0 ? 0m : Math.Round(raw, 0, MidpointRounding.AwayFromZero);
    }
}
