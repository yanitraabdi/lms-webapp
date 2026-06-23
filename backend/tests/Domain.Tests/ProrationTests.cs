using Academy.Domain;

namespace Academy.Domain.Tests;

public class ProrationTests
{
    [Fact]
    public void UpgradeDelta_matches_spec_example()
    {
        // TSD §9.4: Beginner 149k → Intermediate 249k, day 10 of 30 (20 remaining).
        // (249000 - 149000) * 20 / 30 = 66,666.67 → 66,667 whole rupiah.
        var delta = Proration.UpgradeDelta(149_000m, 249_000m, remainingDays: 20, cycleDays: 30);
        Assert.Equal(66_667m, delta);
    }

    [Fact]
    public void UpgradeDelta_is_zero_when_no_days_remain()
        => Assert.Equal(0m, Proration.UpgradeDelta(149_000m, 249_000m, 0, 30));

    [Fact]
    public void UpgradeDelta_never_negative_for_cheaper_target()
        => Assert.Equal(0m, Proration.UpgradeDelta(249_000m, 149_000m, 15, 30));

    [Fact]
    public void UpgradeDelta_full_period_charges_full_difference()
        => Assert.Equal(100_000m, Proration.UpgradeDelta(149_000m, 249_000m, 30, 30));

    [Fact]
    public void UpgradeDelta_guards_zero_cycle()
        => Assert.Equal(0m, Proration.UpgradeDelta(149_000m, 249_000m, 10, 0));

    [Fact]
    public void UpgradeDelta_clamps_remaining_to_cycle()
        => Assert.Equal(100_000m, Proration.UpgradeDelta(149_000m, 249_000m, 99, 30));
}
