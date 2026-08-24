// INVERTA: programs, sessions, batches, enrollment (FSD §3–§5; TSD-delta §2).
using Academy.Domain.Common;
using Academy.Domain.Enums;

namespace Academy.Domain.Entities;

/// <summary>A purchasable program (one-time enrollment, not a subscription).</summary>
public class Program : Entity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;               // UNIQUE — public /program/{slug}
    public string Description { get; set; } = default!;
    public string? Summary { get; set; }
    public decimal PriceIdr { get; set; }
    public ProgramStatus Status { get; set; } = ProgramStatus.Draft;
    public DateTimeOffset? PublishedAt { get; set; }

    public ICollection<ProgramSession> Sessions { get; set; } = new List<ProgramSession>();
    public ICollection<ProgramBatch> Batches { get; set; } = new List<ProgramBatch>();
}

/// <summary>Optional dated cohort. Enrollments may belong to one, or be self-paced (null).</summary>
public class ProgramBatch : Entity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = default!;
    public string Name { get; set; } = default!;
    public DateTimeOffset StartDate { get; set; }
    public BatchStatus Status { get; set; } = BatchStatus.Upcoming;
}

/// <summary>One ordered step of a program. Fields are per-type (video | live | final_assessment).</summary>
public class ProgramSession : Entity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = default!;
    public int OrderIndex { get; set; }
    public SessionType Type { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }

    // Type = Video
    public string? ProviderAssetId { get; set; }               // Bunny asset — never sent to students
    public int? DurationSeconds { get; set; }

    // Type = Live
    public DateTimeOffset? ScheduledAt { get; set; }
    public LiveMode? LiveMode { get; set; }
    public string? JoinUrl { get; set; }
    public string? Location { get; set; }
    /// <summary>Stamped when the H-1 reminder went out, so the job never emails twice.</summary>
    public DateTimeOffset? ReminderSentAt { get; set; }

    /// <summary>Gating test (Video) or the final assessment (FinalAssessment). Null = no test.</summary>
    public Guid? AssessmentId { get; set; }
    public Assessment? Assessment { get; set; }
}

/// <summary>
/// Grants access to a program. Created as PendingPayment at checkout and flipped to Active
/// ONLY by a verified Xendit webhook (GR-2). Never deleted — revoke instead.
/// </summary>
public class Enrollment : Entity
{
    public Guid UserId { get; set; }
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = default!;
    public Guid? BatchId { get; set; }                          // null = self-paced
    public ProgramBatch? Batch { get; set; }
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.PendingPayment;
    public decimal AmountPaidIdr { get; set; }
    public string? ProviderRef { get; set; }                    // Xendit invoice ref
    public DateTimeOffset? EnrolledAt { get; set; }

    /// <summary>Only paid states grant access. Revoked/PendingPayment never do.</summary>
    public bool GrantsAccess => Status is EnrollmentStatus.Active or EnrollmentStatus.Completed;
}

/// <summary>Records that a session is done. Idempotent + non-retroactive (GR-8): never removed.</summary>
public class SessionCompletion : Entity
{
    public Guid UserId { get; set; }
    public Guid SessionId { get; set; }
    public ProgramSession Session { get; set; } = default!;
    public DateTimeOffset CompletedAt { get; set; }
    public CompletionMethod Method { get; set; }
}

/// <summary>Admin-marked attendance for a live session (no Zoom API in v1 — FSD §5).</summary>
public class LiveAttendance : Entity
{
    public Guid SessionId { get; set; }
    public ProgramSession Session { get; set; } = default!;
    public Guid UserId { get; set; }
    public bool Attended { get; set; }
    public Guid? MarkedByUserId { get; set; }
}
