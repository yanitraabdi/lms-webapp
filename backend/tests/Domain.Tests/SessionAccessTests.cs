using Academy.Domain;
using Academy.Domain.Enums;

namespace Academy.Domain.Tests;

public class SessionAccessTests
{
    [Theory]
    [InlineData(false, true, true, false)]   // not enrolled → never, even for session 1
    [InlineData(false, false, true, false)]  // not enrolled → never
    [InlineData(true, true, false, true)]    // enrolled + first session → open
    [InlineData(true, false, false, false)]  // enrolled but predecessor incomplete → locked
    [InlineData(true, false, true, true)]    // enrolled + predecessor complete → open
    public void CanAccess_requires_enrollment_AND_unlock(bool enrolled, bool isFirst, bool prevDone, bool expected)
        => Assert.Equal(expected, SessionAccess.CanAccess(enrolled, isFirst, prevDone));

    [Fact]
    public void First_session_is_always_unlocked()
        => Assert.True(SessionAccess.IsUnlocked(isFirstSession: true, previousCompleted: false));

    // ---- completion per session type (FSD §3 R2) ----

    [Fact]
    public void Video_with_gating_test_is_NOT_complete_on_watching_alone()
        => Assert.False(SessionAccess.IsSessionComplete(
            SessionType.Video, watchPercent: 100m, hasGatingTest: true, gatingTestPassed: false));

    [Fact]
    public void Video_with_gating_test_completes_only_when_test_passed()
        => Assert.True(SessionAccess.IsSessionComplete(
            SessionType.Video, watchPercent: 100m, hasGatingTest: true, gatingTestPassed: true));

    [Fact]
    public void Video_without_gating_test_completes_at_threshold()
        => Assert.True(SessionAccess.IsSessionComplete(SessionType.Video, watchPercent: 90m, hasGatingTest: false));

    [Fact]
    public void Video_below_threshold_is_incomplete_even_with_passed_test()
        => Assert.False(SessionAccess.IsSessionComplete(
            SessionType.Video, watchPercent: 89.9m, hasGatingTest: true, gatingTestPassed: true));

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Live_completes_on_marked_attendance(bool attended, bool expected)
        => Assert.Equal(expected, SessionAccess.IsSessionComplete(SessionType.Live, attended: attended));

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Final_completes_on_submission(bool submitted, bool expected)
        => Assert.Equal(expected, SessionAccess.IsSessionComplete(SessionType.FinalAssessment, assessmentSubmitted: submitted));
}

public class EnrollmentStateMachineTests
{
    [Theory]
    [InlineData(EnrollmentStatus.Active, true)]
    [InlineData(EnrollmentStatus.Completed, true)]
    [InlineData(EnrollmentStatus.PendingPayment, false)]  // paying isn't enough — the webhook must land
    [InlineData(EnrollmentStatus.Revoked, false)]
    public void Only_paid_states_grant_access(EnrollmentStatus status, bool expected)
        => Assert.Equal(expected, EnrollmentStateMachine.GrantsAccess(status));

    [Theory]
    [InlineData(EnrollmentStatus.PendingPayment, EnrollmentStatus.Active, true)]
    [InlineData(EnrollmentStatus.Active, EnrollmentStatus.Completed, true)]
    [InlineData(EnrollmentStatus.Active, EnrollmentStatus.Revoked, true)]
    [InlineData(EnrollmentStatus.Revoked, EnrollmentStatus.Active, false)]        // no resurrection
    [InlineData(EnrollmentStatus.Completed, EnrollmentStatus.Active, false)]
    [InlineData(EnrollmentStatus.PendingPayment, EnrollmentStatus.Completed, false)]
    public void Transitions_are_constrained(EnrollmentStatus from, EnrollmentStatus to, bool expected)
        => Assert.Equal(expected, EnrollmentStateMachine.CanTransition(from, to));

    [Theory]
    [InlineData(EnrollmentStatus.Revoked, true)]           // revoked in error, or a dispute resolved
    [InlineData(EnrollmentStatus.PendingPayment, true)]    // comp grant while an invoice is open
    [InlineData(EnrollmentStatus.Active, false)]           // nothing to reinstate
    [InlineData(EnrollmentStatus.Completed, false)]
    public void An_admin_can_reinstate_a_revoked_enrollment(EnrollmentStatus from, bool expected)
        => Assert.Equal(expected, EnrollmentStateMachine.CanReinstate(from));

    [Fact]
    public void Reinstating_stays_out_of_the_webhook_path()
    {
        // The whole reason these are two methods. The webhook processor gates only on
        // CanTransition, so if reinstatement ever leaked into it, a replayed paid event could
        // hand access back to a refunded learner. This test fails the moment someone merges them.
        Assert.True(EnrollmentStateMachine.CanReinstate(EnrollmentStatus.Revoked));
        Assert.False(EnrollmentStateMachine.CanTransition(EnrollmentStatus.Revoked, EnrollmentStatus.Active));
    }
}
