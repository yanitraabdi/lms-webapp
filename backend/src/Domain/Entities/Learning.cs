// Progress, quizzes (P1), capstones, certificates (TSD §6.4)
using Academy.Domain.Common;
using Academy.Domain.Enums;

namespace Academy.Domain.Entities;

/// <summary>
/// Video progress. NEVER hard-deleted (GR-7).
/// Targets EXACTLY ONE of <see cref="ModuleId"/> (dormant AI Academy catalog) or
/// <see cref="SessionId"/> (INVERTA program session) — enforced by a DB CHECK constraint.
/// Reused rather than replaced so the delivered player/resume/completion code stays intact
/// (see docs/TSD_Delta_INVERTA_v0.1.md §3.1).
/// </summary>
public class WatchProgress : Entity
{
    public Guid UserId { get; set; }
    public Guid? ModuleId { get; set; }                        // dormant — archived product
    public Guid? SessionId { get; set; }                       // INVERTA program session
    public int ResumePositionSeconds { get; set; }
    public decimal PercentComplete { get; set; }
    public bool Completed { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset LastWatchedAt { get; set; }
}

// ---- Quizzes: P1. Tables exist from InitialCreate; feature ships later. ----
public class Quiz : Entity
{
    public Guid ModuleId { get; set; }
    public Module Module { get; set; } = default!;
    public int PassThreshold { get; set; }                 // e.g. 4 (of 5)
    public bool IsActive { get; set; }
    public ICollection<QuizQuestion> Questions { get; set; } = new List<QuizQuestion>();
}

public class QuizQuestion : Entity
{
    public Guid QuizId { get; set; }
    public Quiz Quiz { get; set; } = default!;
    public string Prompt { get; set; } = default!;
    public string Choices { get; set; } = "[]";            // jsonb array
    public int CorrectIndex { get; set; }
}

public class QuizAttempt : Entity
{
    public Guid UserId { get; set; }
    public Guid QuizId { get; set; }
    public int Score { get; set; }
    public bool Passed { get; set; }
}

// ---- Capstones: encouraged, NON-gating in v1; review surface deferred (TSD §6.4) ----
public class Capstone : Entity
{
    public Guid LevelId { get; set; }
    public Level Level { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Brief { get; set; } = default!;
}

public class CapstoneSubmission : Entity
{
    public Guid UserId { get; set; }
    public Guid CapstoneId { get; set; }
    public string Content { get; set; } = default!;
    public CapstoneSubmissionStatus Status { get; set; } = CapstoneSubmissionStatus.Submitted;
    public DateTimeOffset? ReviewedAt { get; set; }
}

/// <summary>
/// Immutable once issued (GR-6). Retained indefinitely.
/// INVERTA issuance is SCORE-based and anchored to <see cref="AttemptId"/>: a granted retake
/// inserts a NEW certificate and never mutates this one (hence no unique on user+program).
/// <see cref="LevelId"/>/<see cref="CompletedModuleIds"/> are dormant (archived product).
/// </summary>
public class Certificate : Entity
{
    public Guid UserId { get; set; }
    public Guid? LevelId { get; set; }                          // dormant — archived product
    public DateTimeOffset IssuedAt { get; set; }
    public string VerificationCode { get; set; } = default!;    // UNIQUE — public /verify/{code}
    public string? PdfUrl { get; set; }
    public List<Guid> CompletedModuleIds { get; set; } = new(); // jsonb snapshot (dormant)

    // ---- INVERTA: score-based issuance ----
    public Guid? ProgramId { get; set; }
    public Program? Program { get; set; }
    public Guid? AttemptId { get; set; }                        // immutability anchor
    public string? SectionScores { get; set; }                  // jsonb section → raw score
    public string? ScaledScores { get; set; }                   // jsonb section → scaled score
    public int? TotalScaledScore { get; set; }                  // ITP total, 310–677
    public string? PredictedBand { get; set; }                  // "TOEFL Prediction" — never an ETS score
}
