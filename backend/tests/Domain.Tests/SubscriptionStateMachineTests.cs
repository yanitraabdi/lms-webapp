using Academy.Domain;
using Academy.Domain.Enums;

namespace Academy.Domain.Tests;

public class SubscriptionStateMachineTests
{
    [Theory]
    [InlineData(SubscriptionStatus.Active, SubscriptionStatus.PastDue, true)]   // payment fail
    [InlineData(SubscriptionStatus.Active, SubscriptionStatus.Active, true)]    // renewal
    [InlineData(SubscriptionStatus.Active, SubscriptionStatus.Canceled, true)]  // user cancel
    [InlineData(SubscriptionStatus.PastDue, SubscriptionStatus.Grace, true)]    // retries exhausted
    [InlineData(SubscriptionStatus.PastDue, SubscriptionStatus.Active, true)]   // retry success
    [InlineData(SubscriptionStatus.Grace, SubscriptionStatus.Expired, true)]    // grace ends
    [InlineData(SubscriptionStatus.Grace, SubscriptionStatus.Active, true)]     // late payment
    [InlineData(SubscriptionStatus.Canceled, SubscriptionStatus.Expired, true)] // period end
    [InlineData(SubscriptionStatus.Active, SubscriptionStatus.Expired, false)]  // must pass through grace
    [InlineData(SubscriptionStatus.Expired, SubscriptionStatus.Active, false)]  // terminal — new row instead
    [InlineData(SubscriptionStatus.Canceled, SubscriptionStatus.Active, false)] // no un-cancel
    public void CanTransition_enforces_spec(SubscriptionStatus from, SubscriptionStatus to, bool expected)
        => Assert.Equal(expected, SubscriptionStateMachine.CanTransition(from, to));

    [Theory]
    [InlineData(SubscriptionStatus.Active, true)]
    [InlineData(SubscriptionStatus.PastDue, true)]
    [InlineData(SubscriptionStatus.Grace, true)]
    [InlineData(SubscriptionStatus.Canceled, true)]   // entitled until period end
    [InlineData(SubscriptionStatus.Expired, false)]
    public void GrantsAccess_within_period(SubscriptionStatus status, bool expected)
    {
        var now = DateTimeOffset.UnixEpoch;
        Assert.Equal(expected, SubscriptionStateMachine.GrantsAccess(status, now.AddDays(5), now));
    }

    [Fact]
    public void GrantsAccess_false_after_period_end()
    {
        var now = DateTimeOffset.UnixEpoch;
        Assert.False(SubscriptionStateMachine.GrantsAccess(SubscriptionStatus.Active, now.AddDays(-1), now));
    }
}
