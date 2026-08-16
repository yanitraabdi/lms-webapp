// INVERTA M2 — program, enrollment and session-access contracts (KAK §9.3–§9.4, §9.14).
using Academy.Application.Billing;
using Academy.Domain.Enums;

namespace Academy.Application.Programs;

// ---------------------------------------------------------------- public (landing)

public record PublicSessionDto(
    Guid Id, int OrderIndex, string Type, string Title, string? Description,
    int? DurationSeconds, DateTimeOffset? ScheduledAt, string? LiveMode);

public record PublicProgramDto(
    Guid Id, string Name, string Slug, string Description, string? Summary,
    decimal PriceIdr, int SessionCount, int TotalDurationSeconds,
    IReadOnlyList<PublicSessionDto> Sessions);

// ---------------------------------------------------------------- student

/// <summary>Lock state for one session in the student's program view (KAK §9.3 R4).</summary>
public enum SessionState { Locked, Available, Completed }

public record StudentSessionDto(
    Guid Id, int OrderIndex, string Type, string Title, string? Description,
    int? DurationSeconds, DateTimeOffset? ScheduledAt, string? LiveMode,
    string? JoinUrl, string? Location,          // live details — only when unlocked
    SessionState State,
    decimal PercentComplete,
    bool HasAssessment);

public record StudentProgramDto(
    Guid ProgramId, string Name, string Slug, string EnrollmentStatus,
    Guid? BatchId, string? BatchName, DateTimeOffset? BatchStartDate,
    int CompletedCount, int SessionCount,
    Guid? NextSessionId,
    IReadOnlyList<StudentSessionDto> Sessions);

public record EnrollmentDto(
    Guid Id, Guid ProgramId, string ProgramName, string ProgramSlug,
    string Status, decimal AmountPaidIdr, DateTimeOffset? EnrolledAt);

// ---------------------------------------------------------------- admin

public record AdminProgramDto(
    Guid Id, string Name, string Slug, string Description, string? Summary,
    decimal PriceIdr, string Status, int SessionCount, int EnrollmentCount);

public record UpsertProgramRequest(
    string Name, string? Slug, string Description, string? Summary,
    decimal PriceIdr, bool Published);

public record AdminSessionDto(
    Guid Id, Guid ProgramId, int OrderIndex, string Type, string Title, string? Description,
    string? ProviderAssetId, int? DurationSeconds,
    DateTimeOffset? ScheduledAt, string? LiveMode, string? JoinUrl, string? Location,
    Guid? AssessmentId);

public record UpsertSessionRequest(
    string Type, string Title, string? Description, int OrderIndex,
    string? ProviderAssetId, int? DurationSeconds,
    DateTimeOffset? ScheduledAt, string? LiveMode, string? JoinUrl, string? Location,
    Guid? AssessmentId);

public record ReorderSessionsRequest(IReadOnlyList<Guid> SessionIdsInOrder);

public record AdminBatchDto(Guid Id, Guid ProgramId, string Name, DateTimeOffset StartDate, string Status);
public record UpsertBatchRequest(string Name, DateTimeOffset StartDate, string Status);

// ---------------------------------------------------------------- services

public interface IProgramService
{
    /// <summary>Published program by slug for the SSG landing page. Null when absent/unpublished.</summary>
    Task<PublicProgramDto?> GetPublicAsync(string slug, CancellationToken ct = default);

    /// <summary>The enrolled student's view: ordered sessions with per-session lock state.</summary>
    Task<StudentProgramDto> GetForStudentAsync(Guid userId, Guid programId, CancellationToken ct = default);
}

public interface IEnrollmentService
{
    /// <summary>Creates a PendingPayment enrollment + a one-time invoice. GRANTS NOTHING (GR-2).</summary>
    Task<CheckoutSession> CheckoutAsync(Guid userId, Guid programId, Guid? batchId, CancellationToken ct = default);
    Task<IReadOnlyList<EnrollmentDto>> ListMineAsync(Guid userId, CancellationToken ct = default);
    Task<bool> IsEnrolledAsync(Guid userId, Guid programId, CancellationToken ct = default);
}

/// <summary>
/// THE GATE (GR-1). Every protected resource calls this before doing anything.
/// canAccess = isEnrolled(program) AND sessionUnlocked(linear lock).
/// </summary>
public interface ISessionAccessService
{
    Task<bool> CanAccessAsync(Guid userId, Guid sessionId, CancellationToken ct = default);

    /// <summary>Same check, but throws <see cref="ProgramException"/> (403) instead of returning false.</summary>
    Task EnsureAccessAsync(Guid userId, Guid sessionId, CancellationToken ct = default);
}

/// <summary>
/// The ONLY component that completes a session and advances the linear lock (GR-8).
/// Idempotent and non-retroactive — never un-completes.
/// </summary>
public interface ISessionCompletionService
{
    /// <summary>Re-evaluates the session against its completion rule; returns true if it is complete.</summary>
    Task<bool> TryCompleteAsync(Guid userId, Guid sessionId, CancellationToken ct = default);

    /// <summary>Whether the session is already recorded complete.</summary>
    Task<bool> IsCompleteAsync(Guid userId, Guid sessionId, CancellationToken ct = default);
}

public interface IProgramAdminService
{
    Task<IReadOnlyList<AdminProgramDto>> ListAsync(CancellationToken ct = default);
    Task<AdminProgramDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<AdminProgramDto> CreateAsync(Guid actor, UpsertProgramRequest req, CancellationToken ct = default);
    Task UpdateAsync(Guid actor, Guid id, UpsertProgramRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid actor, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<AdminSessionDto>> ListSessionsAsync(Guid programId, CancellationToken ct = default);
    Task<AdminSessionDto> CreateSessionAsync(Guid actor, Guid programId, UpsertSessionRequest req, CancellationToken ct = default);
    Task UpdateSessionAsync(Guid actor, Guid sessionId, UpsertSessionRequest req, CancellationToken ct = default);
    Task DeleteSessionAsync(Guid actor, Guid sessionId, CancellationToken ct = default);
    Task ReorderSessionsAsync(Guid actor, Guid programId, ReorderSessionsRequest req, CancellationToken ct = default);

    Task<IReadOnlyList<AdminBatchDto>> ListBatchesAsync(Guid programId, CancellationToken ct = default);
    Task<AdminBatchDto> CreateBatchAsync(Guid actor, Guid programId, UpsertBatchRequest req, CancellationToken ct = default);
    Task UpdateBatchAsync(Guid actor, Guid batchId, UpsertBatchRequest req, CancellationToken ct = default);
    Task DeleteBatchAsync(Guid actor, Guid batchId, CancellationToken ct = default);

    /// <summary>Support path: revoke access without deleting any learner data (GR-7).</summary>
    Task RevokeEnrollmentAsync(Guid actor, Guid enrollmentId, CancellationToken ct = default);
}

public class ProgramException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
