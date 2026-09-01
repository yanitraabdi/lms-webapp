// INVERTA: assessment engine, proctoring, score bands (FSD §6–§8; TSD-delta §2).
// Supersedes the dormant Quiz/QuizQuestion/QuizAttempt tables (sections, timers, audio, proctoring).
using Academy.Domain.Common;
using Academy.Domain.Enums;

namespace Academy.Domain.Entities;

/// <summary>
/// A gating test (short, after a video) or the multi-section final assessment.
/// Config is a jsonb bag: sections[], per-section time limits, passThreshold, retakeCap,
/// proctoringEnabled, audio playLimit.
/// </summary>
public class Assessment : Entity
{
    public AssessmentKind Kind { get; set; }
    public string Title { get; set; } = default!;
    public string Config { get; set; } = "{}";                  // jsonb — see AssessmentConfig
    public ICollection<AssessmentQuestion> Questions { get; set; } = new List<AssessmentQuestion>();
}

/// <summary>Question-bank item. <see cref="Correct"/> is NEVER serialized to students (GR-11).</summary>
public class Question : Entity
{
    public QuestionSection Section { get; set; }
    public QuestionType Type { get; set; } = QuestionType.Mcq;
    public string Prompt { get; set; } = default!;
    public string Choices { get; set; } = "[]";                 // jsonb string[]
    public string Correct { get; set; } = "[]";                 // jsonb int[] — server-only
    public string? AudioRef { get; set; }                       // R2 key (listening) — signed on serve
    public string? PassageRef { get; set; }                     // reading passage
    public string Tags { get; set; } = "[]";                    // jsonb string[]

    /// <summary>
    /// Author-assigned id from a bulk-import sheet ("L01", "R07") — the upsert key, so re-uploading
    /// a corrected sheet updates in place instead of duplicating the bank. Null for a question
    /// authored by hand in the admin UI, and many of those must coexist, so the unique index is
    /// filtered to non-null values. Stored uppercase: the sheet dedupes case-insensitively, and a
    /// case-sensitive index would otherwise let "L01" and "l01" become two questions.
    /// </summary>
    public string? ExternalId { get; set; }
}

public class AssessmentQuestion : Entity
{
    public Guid AssessmentId { get; set; }
    public Assessment Assessment { get; set; } = default!;
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = default!;
    public int OrderIndex { get; set; }
}

/// <summary>
/// One sitting. StartedAt is SERVER-stamped (GR-12) — a client clock is never trusted.
/// Retained indefinitely (GR-7); a granted retake creates a NEW attempt, never mutating this one.
/// </summary>
public class Attempt : Entity
{
    public Guid UserId { get; set; }
    public Guid AssessmentId { get; set; }
    public Assessment Assessment { get; set; } = default!;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public bool AutoSubmitted { get; set; }                     // timer expiry or proctor strike 2
    public bool ProctorFlagged { get; set; }
    public bool Reinstated { get; set; }                        // admin cleared a false positive
    public string Answers { get; set; } = "{}";                 // jsonb questionId → selected index
    public string SectionScores { get; set; } = "{}";           // jsonb section → raw score

    /// <summary>
    /// Server-authoritative sitting state (jsonb): per-section server-stamped start/submit times,
    /// the active section index, and per-question audio play counts. The client never supplies any
    /// of this — deadlines are computed from it server-side (GR-12).
    /// </summary>
    public string State { get; set; } = "{}";
    public int TotalScore { get; set; }
    public int MaxScore { get; set; }
    public bool Passed { get; set; }
}

/// <summary>Advisory evidence only — the SERVER decides strikes and auto-submit (GR-13).</summary>
public class ProctorEvent : Entity
{
    public Guid AttemptId { get; set; }
    public Attempt Attempt { get; set; } = default!;
    public ProctorEventKind Kind { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string ClientMeta { get; set; } = "{}";              // jsonb
}

/// <summary>
/// Admin-maintained PER-SECTION raw → scaled conversion (KAK §9.9.2), following the TOEFL ITP
/// convention: each section converts to a scaled score, then
/// <c>Total = ROUND((Listening + Structure + Reading) * 10 / 3)</c>, range 310–677.
/// Engineering owns the mechanism; the BUSINESS owns the pedagogically valid values.
/// Ships with a clearly-marked placeholder — an unmapped raw score must fail LOUDLY (no
/// certificate, explicit admin-visible error), never a blank or guessed band (KAK §9.9.4).
/// </summary>
public class ScoreBandMapping : Entity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = default!;
    public QuestionSection Section { get; set; }
    public int MinRaw { get; set; }
    public int MaxRaw { get; set; }
    /// <summary>Scaled section score (Listening/Structure 31–68, Reading 31–67).</summary>
    public int ScaledScore { get; set; }
    /// <summary>Optional descriptive label for the overall result (set on the total, not per section).</summary>
    public string? PredictedBand { get; set; }
}
