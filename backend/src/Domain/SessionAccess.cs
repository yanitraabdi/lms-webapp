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

    public static bool CanTransition(EnrollmentStatus from, EnrollmentStatus to) => (from, to) switch
    {
        (EnrollmentStatus.PendingPayment, EnrollmentStatus.Active) => true,   // verified webhook
        (EnrollmentStatus.Active, EnrollmentStatus.Completed) => true,        // finished the program
        (EnrollmentStatus.PendingPayment, EnrollmentStatus.Revoked) => true,  // abandoned / expired invoice
        (EnrollmentStatus.Active, EnrollmentStatus.Revoked) => true,          // refund / admin action
        (EnrollmentStatus.Completed, EnrollmentStatus.Revoked) => true,
        _ => false,
    };
}
