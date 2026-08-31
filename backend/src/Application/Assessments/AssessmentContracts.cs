// INVERTA M3/M4 — assessment engine (KAK §9.6–§9.7). Gating tests ship in M3; the timed,
// proctored, multi-section final assessment layers on in M4 using the same contracts.
using System.Text.Json.Serialization;
using Academy.Domain.Enums;

namespace Academy.Application.Assessments;

/// <summary>
/// Parsed form of <c>assessments.config</c> (jsonb). Admin-editable; no value is hard-coded
/// in application logic (KAK §9.6 R2).
/// </summary>
public class AssessmentConfig
{
    /// <summary>Correct answers required to pass. Null ⇒ every question must be correct.</summary>
    public int? PassThreshold { get; set; }

    /// <summary>Maximum attempts. Null ⇒ unlimited (the gating-test default, KAK §9.6).</summary>
    public int? RetakeCap { get; set; }

    /// <summary>Soft proctoring (M4). Off for gating tests by default.</summary>
    public bool ProctoringEnabled { get; set; }

    /// <summary>Per-question audio play limit (M4 listening). Null ⇒ unlimited.</summary>
    public int? AudioPlayLimit { get; set; }

    /// <summary>Sectional layout for the final assessment (M4). Empty for a gating test.</summary>
    public List<AssessmentSectionConfig> Sections { get; set; } = [];

    /// <summary>Whole-test time limit in minutes (M4). Null ⇒ untimed.</summary>
    public int? TimeLimitMinutes { get; set; }
}

public class AssessmentSectionConfig
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public QuestionSection Section { get; set; }
    public int Questions { get; set; }
    public int Minutes { get; set; }

    /// <summary>Optional whole-section recording (Listening). A question's own AudioRef wins.
    /// Lives in the config jsonb, so adding it needs no migration.</summary>
    public string? AudioRef { get; set; }
}

// ---------------------------------------------------------------- learner DTOs

/// <summary>
/// A question as served to a student. Deliberately has NO correct-answer field (GR-11) —
/// this type is the enforcement point, so never add one.
/// </summary>
public record StudentQuestionDto(
    Guid Id, string Section, string Prompt, IReadOnlyList<string> Choices,
    string? PassageRef, bool HasAudio);

public record StudentAssessmentDto(
    Guid Id, Guid? SessionId, string Kind, string Title,
    int QuestionCount, int PassThreshold,
    int? RetakeCap, int AttemptsUsed, bool CanAttempt,
    bool Passed, int? BestScore,
    bool ProctoringEnabled, int? TimeLimitMinutes,
    IReadOnlyList<StudentQuestionDto> Questions);

public record AttemptDto(
    Guid Id, Guid AssessmentId, DateTimeOffset StartedAt, DateTimeOffset? SubmittedAt,
    IReadOnlyDictionary<string, int> Answers);

public record SaveAnswersRequest(IReadOnlyDictionary<string, int> Answers);

public record SubmitAttemptRequest(IReadOnlyDictionary<string, int>? Answers);

public record AttemptResultDto(
    Guid AttemptId, int Score, int MaxScore, bool Passed,
    bool AutoSubmitted, bool ProctorFlagged,
    IReadOnlyDictionary<string, int> SectionScores,
    int? TotalScaledScore, string? PredictedBand,
    bool SessionCompleted, Guid? NextSessionId);

public interface IAssessmentService
{
    /// <summary>The session's assessment, without answers. Null when the session has none.</summary>
    Task<StudentAssessmentDto?> GetForSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default);

    /// <summary>Starts an attempt. StartedAt is SERVER-stamped and the retake cap is enforced here (GR-12).</summary>
    Task<AttemptDto> StartAttemptAsync(Guid userId, Guid assessmentId, CancellationToken ct = default);

    /// <summary>Incremental save so a crashed tab does not lose the sitting (KAK §9.7.2).</summary>
    Task SaveAnswersAsync(Guid userId, Guid attemptId, IReadOnlyDictionary<string, int> answers, CancellationToken ct = default);

    /// <summary>Scores server-side, records the attempt, and advances the linear lock when passed.</summary>
    Task<AttemptResultDto> SubmitAsync(
        Guid userId, Guid attemptId, IReadOnlyDictionary<string, int>? answers, CancellationToken ct = default);

    Task<AttemptResultDto> GetResultAsync(Guid userId, Guid attemptId, CancellationToken ct = default);
}

// ---------------------------------------------------------------- admin DTOs

public record AdminQuestionDto(
    Guid Id, string Section, string Type, string Prompt,
    IReadOnlyList<string> Choices, IReadOnlyList<int> Correct,
    string? AudioRef, string? PassageRef, IReadOnlyList<string> Tags,
    int UsedInAssessments);

public record UpsertQuestionRequest(
    string Section, string Prompt, IReadOnlyList<string> Choices, IReadOnlyList<int> Correct,
    string? AudioRef, string? PassageRef, IReadOnlyList<string>? Tags);

public record AdminAssessmentDto(
    Guid Id, string Kind, string Title, AssessmentConfig Config,
    int QuestionCount, IReadOnlyList<AdminQuestionDto> Questions,
    Guid? AttachedSessionId, int AttemptCount);

/// <summary>
/// Config is a PARTIAL document, merged over the stored one by key presence
/// (<see cref="AssessmentConfigMerge"/>): omit a key to keep it, send it to change it, send it as
/// null to clear it. A screen that models only part of the config can therefore save safely
/// without destroying the fields it does not know about. The response DTO stays fully typed.
/// </summary>
public record UpsertAssessmentRequest(string Kind, string Title, System.Text.Json.Nodes.JsonObject? Config);

public record SetAssessmentQuestionsRequest(IReadOnlyList<Guid> QuestionIdsInOrder);

public interface IQuestionBankService
{
    Task<IReadOnlyList<AdminQuestionDto>> ListAsync(string? section, string? search, CancellationToken ct = default);
    Task<AdminQuestionDto> CreateAsync(Guid actor, UpsertQuestionRequest req, CancellationToken ct = default);
    Task UpdateAsync(Guid actor, Guid id, UpsertQuestionRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid actor, Guid id, CancellationToken ct = default);
}

public interface IAssessmentAdminService
{
    Task<IReadOnlyList<AdminAssessmentDto>> ListAsync(CancellationToken ct = default);
    Task<AdminAssessmentDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<AdminAssessmentDto> CreateAsync(Guid actor, UpsertAssessmentRequest req, CancellationToken ct = default);
    Task UpdateAsync(Guid actor, Guid id, UpsertAssessmentRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid actor, Guid id, CancellationToken ct = default);
    Task SetQuestionsAsync(Guid actor, Guid id, SetAssessmentQuestionsRequest req, CancellationToken ct = default);

    /// <summary>Attaches an assessment to a session (or detaches when null).</summary>
    Task AttachToSessionAsync(Guid actor, Guid sessionId, Guid? assessmentId, CancellationToken ct = default);
}

public class AssessmentException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
