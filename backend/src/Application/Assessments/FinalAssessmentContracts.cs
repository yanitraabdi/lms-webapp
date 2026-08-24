// INVERTA M4 — sectional timed sitting, soft proctoring, ITP scoring, certificate (KAK §9.7–§9.10).
namespace Academy.Application.Assessments;

// ---------------------------------------------------------------- sitting state

/// <summary>
/// Server-authoritative view of an in-progress sitting. Every time value is computed on the
/// server from server-stamped timestamps — a client clock is never trusted (GR-12).
/// </summary>
public record AttemptStateDto(
    Guid AttemptId,
    Guid AssessmentId,
    string Status,                              // InProgress | Submitted
    int SectionIndex,
    int SectionCount,
    string? CurrentSection,
    DateTimeOffset? SectionStartedAt,
    DateTimeOffset? SectionDeadline,
    int SecondsRemaining,
    bool ProctoringEnabled,
    int Strikes,
    int StrikeLimit,
    bool ProctorFlagged,
    IReadOnlyList<StudentQuestionDto> Questions,     // current section ONLY
    IReadOnlyDictionary<string, int> Answers,        // current section ONLY
    IReadOnlyDictionary<string, int> AudioPlaysLeft);

public record AdvanceSectionRequest(IReadOnlyDictionary<string, int>? Answers);

/// <summary>
/// The multi-section timed sitting. Separate from <see cref="IAssessmentService"/> (which owns the
/// simple untimed gating-test flow) because every operation here must first apply elapsed time.
/// </summary>
public interface IFinalAssessmentService
{
    /// <summary>Current state, after enforcing any elapsed deadlines.</summary>
    Task<AttemptStateDto> GetStateAsync(Guid userId, Guid attemptId, CancellationToken ct = default);

    /// <summary>Saves answers for the ACTIVE section only; a closed section is final.</summary>
    Task<AttemptStateDto> SaveAnswersAsync(
        Guid userId, Guid attemptId, IReadOnlyDictionary<string, int> answers, CancellationToken ct = default);

    /// <summary>Closes the active section and opens the next — or finalizes if it was the last.</summary>
    Task<AttemptStateDto> AdvanceSectionAsync(
        Guid userId, Guid attemptId, IReadOnlyDictionary<string, int>? answers, CancellationToken ct = default);

    Task<AttemptResultDto> SubmitAsync(Guid userId, Guid attemptId, CancellationToken ct = default);

    /// <summary>Signed audio URL for a listening question; the play limit is counted server-side.</summary>
    Task<string> GetAudioUrlAsync(Guid userId, Guid attemptId, Guid questionId, CancellationToken ct = default);
}

// ---------------------------------------------------------------- proctoring

public record ProctorEventRequest(string Kind, int? DurationMs);

/// <summary>The server's decision — the client only reports, it never decides (GR-13).</summary>
public record ProctorStateDto(int Strikes, int StrikeLimit, string Action, bool Flagged);

public interface IProctorService
{
    /// <summary>Records an event and returns the authoritative strike state / action.</summary>
    Task<ProctorStateDto> ReportAsync(
        Guid userId, Guid attemptId, string kind, int? durationMs, CancellationToken ct = default);

    /// <summary>Admin support path for a false positive: clears the flag, keeps the event trail.</summary>
    Task ReinstateAsync(Guid actor, Guid attemptId, CancellationToken ct = default);
}

// ---------------------------------------------------------------- scoring

/// <summary>Per-section scaled score plus the ITP total (KAK §9.9).</summary>
public record ScoreConversionResult(
    IReadOnlyDictionary<string, int> RawScores,
    IReadOnlyDictionary<string, int> ScaledScores,
    int Total,
    string? PredictedBand);

public interface IScoreConversionService
{
    /// <summary>
    /// Converts raw section scores to scaled scores and the ITP total.
    /// Throws when any raw score is unmapped — it must FAIL LOUDLY, never emit a blank or
    /// guessed band, and no certificate may be issued in that state (KAK §9.9.4).
    /// </summary>
    Task<ScoreConversionResult> ConvertAsync(
        Guid programId, IReadOnlyDictionary<string, int> rawScores, CancellationToken ct = default);
}

// ---------------------------------------------------------------- certificates

public record ProgramCertificateDto(
    Guid Id, Guid? ProgramId, string ProgramName, DateTimeOffset IssuedAt,
    string VerificationCode, int? TotalScore, string? PredictedBand,
    IReadOnlyDictionary<string, int> SectionScores);

public record CertificateVerificationDto(
    bool Valid, string? Code, string? RecipientName, string? ProgramName,
    DateTimeOffset? IssuedAt, int? TotalScore, string? PredictedBand,
    IReadOnlyDictionary<string, int>? SectionScores,
    /// <summary>Always present, always shown: this is a prediction, not an official ETS score.</summary>
    string Disclaimer);

public interface IProgramCertificateService
{
    /// <summary>
    /// Issues a certificate from a SUBMITTED attempt. Immutable: a granted retake inserts a NEW
    /// certificate and never mutates an existing one (GR-6). No-op if this attempt already has one.
    /// </summary>
    Task<ProgramCertificateDto?> TryIssueForAttemptAsync(Guid userId, Guid attemptId, CancellationToken ct = default);

    Task<IReadOnlyList<ProgramCertificateDto>> GetMineAsync(Guid userId, CancellationToken ct = default);
    Task<CertificateVerificationDto> VerifyAsync(string code, CancellationToken ct = default);
    Task<(byte[] Pdf, string FileName)?> GetPdfAsync(Guid userId, Guid certificateId, CancellationToken ct = default);
}

// ---------------------------------------------------------------- admin: score bands

public record ScoreBandDto(Guid Id, string Section, int MinRaw, int MaxRaw, int ScaledScore, string? PredictedBand);
public record UpsertScoreBandsRequest(IReadOnlyList<ScoreBandDto> Bands);

public interface IScoreBandAdminService
{
    Task<IReadOnlyList<ScoreBandDto>> ListAsync(Guid programId, CancellationToken ct = default);
    Task ReplaceAsync(Guid actor, Guid programId, UpsertScoreBandsRequest req, CancellationToken ct = default);
}
