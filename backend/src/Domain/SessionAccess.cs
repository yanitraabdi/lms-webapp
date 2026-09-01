using Academy.Domain.Enums;

namespace Academy.Domain;

/// <summary>
/// The INVERTA access rule (GR-1, GR-8), replacing the archived product's tier entitlement:
/// <c>canAccess(user, session) = isEnrolled(user, program) &amp;&amp; sessionUnlocked(user, session)</c>.
/// Pure — no EF, no HTTP. Evaluated server-side on EVERY protected resource.
/// </summary>
public static class SessionAccess
{
    /// <summary>Both halves of the gate. <paramref name="previousCompleted"/> is ignored for the first session.</summary>
    public static bool CanAccess(bool enrolled, bool isFirstSession, bool previousCompleted)
        => enrolled && IsUnlocked(isFirstSession, previousCompleted);

    /// <summary>Linear lock: the first session is always open; every other needs its predecessor done.</summary>
    public static bool IsUnlocked(bool isFirstSession, bool previousCompleted)
        => isFirstSession || previousCompleted;

    /// <summary>
    /// Whether a session counts as complete, per type (FSD §3 R2):
    /// video → watched ≥ threshold AND gating test passed; live → admin-marked attended;
    /// final → an attempt was submitted.
    /// </summary>
    public static bool IsSessionComplete(
        SessionType type,
        decimal watchPercent = 0m,
        bool hasGatingTest = false,
        bool gatingTestPassed = false,
        bool attended = false,
        bool assessmentSubmitted = false,
        decimal watchThreshold = CompletionPolicy.ModuleCompleteThresholdPercent) => type switch
        {
            // Watching alone is NOT enough when a gating test exists — the test truly gates.
            SessionType.Video => watchPercent >= watchThreshold && (!hasGatingTest || gatingTestPassed),
            SessionType.Live => attended,
            SessionType.FinalAssessment => assessmentSubmitted,
            _ => false,
        };
}

/// <summary>
/// Enrollment lifecycle (FSD §4 R5). Only paid states grant access, and access is granted
/// ONLY by a verified webhook (GR-2) — never by the checkout success page.
/// </summary>
public static class EnrollmentStateMachine
{
    public static bool GrantsAccess(EnrollmentStatus status)
        => status is EnrollmentStatus.Active or EnrollmentStatus.Completed;

    /// <summary>
    /// Transitions a verified PAYMENT WEBHOOK may perform (GR-2). Deliberately has no path out of
    /// Revoked: a replayed or late-arriving paid event for a refunded enrollment must never hand
    /// access back. Reinstating is a human decision — see <see cref="CanReinstate"/>.
    /// </summary>
    public static bool CanTransition(EnrollmentStatus from, EnrollmentStatus to) => (from, to) switch
    {
        (EnrollmentStatus.PendingPayment, EnrollmentStatus.Active) => true,   // verified webhook
        (EnrollmentStatus.Active, EnrollmentStatus.Completed) => true,        // finished the program
        (EnrollmentStatus.PendingPayment, EnrollmentStatus.Revoked) => true,  // abandoned / expired invoice
        (EnrollmentStatus.Active, EnrollmentStatus.Revoked) => true,          // refund / admin action
        (EnrollmentStatus.Completed, EnrollmentStatus.Revoked) => true,
        _ => false,
    };

    /// <summary>
    /// Whether an ADMIN may put this enrollment back into Active — a revoke made in error, or a
    /// payment dispute resolved in the learner's favour.
    ///
    /// Kept separate from <see cref="CanTransition"/> on purpose. The webhook processor gates
    /// solely on that method, so folding reinstatement into it would mean any replayed paid event
    /// could resurrect a refunded enrollment. Two rules, two callers: widening the support path
    /// can never widen what a webhook is allowed to do.
    ///
    /// Completed is absent because it already grants access; there is nothing to reinstate.
    /// </summary>
    public static bool CanReinstate(EnrollmentStatus from)
        => from is EnrollmentStatus.Revoked or EnrollmentStatus.PendingPayment;
}
