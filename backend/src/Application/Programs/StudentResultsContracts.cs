// Learner dashboard (spec 2026-09-03, revised 2026-10-06): the caller's own session-test results.
namespace Academy.Application.Programs;

/// <summary>One submitted attempt of a session test (a Test part). Scores only — never answers or
/// the key (GR-11), and never the final assessment, which the certificate represents.</summary>
public record SessionAttemptDto(
    Guid AttemptId, Guid ProgramId,
    Guid SessionId, string SessionTitle, int SessionOrderIndex,
    Guid PartId, string PartTitle, int PartOrderIndex,
    DateTimeOffset SubmittedAt, int Score, int MaxScore, bool Passed);

public interface IStudentResultsService
{
    /// <summary>The caller's submitted session-test attempts, oldest first. The user is always the
    /// caller — there is deliberately no way to ask for someone else's.</summary>
    Task<IReadOnlyList<SessionAttemptDto>> ListSessionAttemptsAsync(Guid userId, CancellationToken ct = default);
}
