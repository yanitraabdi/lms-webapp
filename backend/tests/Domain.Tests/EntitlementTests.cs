using Academy.Domain;

namespace Academy.Domain.Tests;

public class EntitlementTests
{
    [Theory]
    [InlineData(null, true, 1, true)]    // preview is free for everyone
    [InlineData(null, false, 1, false)]  // no subscription → non-preview locked
    [InlineData(1, false, 1, true)]      // tier exactly meets requirement
    [InlineData(2, false, 1, true)]      // higher tier unlocks lower (cumulative)
    [InlineData(1, false, 2, false)]     // tier below requirement
    [InlineData(0, false, 1, false)]     // free tier can't reach paid content
    public void CanAccess_matches_rule(int? activeTier, bool isPreview, int requiredTier, bool expected)
        => Assert.Equal(expected, Entitlement.CanAccess(activeTier, isPreview, requiredTier));
}
