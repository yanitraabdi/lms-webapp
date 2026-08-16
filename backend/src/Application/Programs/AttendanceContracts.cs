// INVERTA M5 — live-session attendance and admin operations (KAK §9.11–§9.12).
namespace Academy.Application.Programs;

// ---------------------------------------------------------------- attendance

public record AttendanceRowDto(
    Guid UserId, string Name, string Email, bool Attended,
    DateTimeOffset? MarkedAt, bool SessionCompleted);

public record AttendanceRosterDto(
    Guid SessionId, string SessionTitle, DateTimeOffset? ScheduledAt,
    string? LiveMode, string? JoinUrl, string? Location,
    int EnrolledCount, int AttendedCount,
    IReadOnlyList<AttendanceRowDto> Rows);

public record MarkAttendanceRequest(IReadOnlyList<Guid> UserIds, bool Attended);

public interface IAttendanceService
{
    /// <summary>Every actively-enrolled learner for the session's program, with attendance state.</summary>
    Task<AttendanceRosterDto> GetRosterAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Marks (or unmarks) attendance. Marking attended completes the live session and advances the
    /// linear lock. Unmarking removes the attendance flag only — completion is never revoked (GR-8).
    /// </summary>
    Task MarkAsync(Guid actor, Guid sessionId, IReadOnlyList<Guid> userIds, bool attended, CancellationToken ct = default);

    /// <summary>Bulk convenience: mark every enrolled learner attended (admin waiver path).</summary>
    Task MarkAllAsync(Guid actor, Guid sessionId, CancellationToken ct = default);
}

/// <summary>Sends the H-1 reminder for upcoming live sessions. Idempotent per session.</summary>
public interface ILiveSessionReminder
{
    Task<int> SendDueRemindersAsync(CancellationToken ct = default);
}

// ---------------------------------------------------------------- admin operations

public record AdminEnrollmentDto(
    Guid Id, Guid UserId, string UserName, string UserEmail,
    Guid ProgramId, string ProgramName, string Status,
    decimal AmountPaidIdr, DateTimeOffset? EnrolledAt,
    int CompletedSessions, int TotalSessions);

public record AdminEnrollmentListDto(IReadOnlyList<AdminEnrollmentDto> Items, int Total);

public record GrantEnrollmentRequest(string Email, Guid ProgramId);

public record AdminAttemptDto(
    Guid Id, Guid UserId, string UserName, string UserEmail,
    Guid AssessmentId, string AssessmentTitle, string Kind,
    DateTimeOffset StartedAt, DateTimeOffset? SubmittedAt,
    bool AutoSubmitted, bool ProctorFlagged, bool Reinstated,
    int TotalScore, int MaxScore, bool Passed,
    int? TotalScaledScore, string? PredictedBand, int StrikeCount);

public record AdminAttemptListDto(IReadOnlyList<AdminAttemptDto> Items, int Total);

public record ProctorEventDto(Guid Id, string Kind, DateTimeOffset OccurredAt, string ClientMeta);

public record AdminAttemptDetailDto(
    AdminAttemptDto Attempt,
    IReadOnlyList<ProctorEventDto> Events,
    IReadOnlyDictionary<string, int> SectionScores);

public interface IAdminOperationsService
{
    Task<AdminEnrollmentListDto> ListEnrollmentsAsync(
        string? search, string? status, Guid? programId, int skip, int take, CancellationToken ct = default);

    /// <summary>Support path: grant access without a payment (comp / refund correction). Audit-logged.</summary>
    Task GrantEnrollmentAsync(Guid actor, GrantEnrollmentRequest req, CancellationToken ct = default);

    Task<AdminAttemptListDto> ListAttemptsAsync(
        bool? flaggedOnly, Guid? programId, int skip, int take, CancellationToken ct = default);

    /// <summary>Attempt with its full proctor-event trail — the evidence for a dispute.</summary>
    Task<AdminAttemptDetailDto?> GetAttemptAsync(Guid attemptId, CancellationToken ct = default);
}
