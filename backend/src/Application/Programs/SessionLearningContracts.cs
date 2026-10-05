// INVERTA M3 — session playback & progress (KAK §9.5), per part since 2026-10-05.
namespace Academy.Application.Programs;

/// <summary>Short-TTL signed playback URL for ONE video part (GR-3). Never carries the asset id.</summary>
public record PartPlaybackDto(Guid PartId, string Url, DateTimeOffset ExpiresAt, string? CaptionsUrl);

public record PartProgressDto(
    Guid PartId, int ResumePositionSeconds, decimal PercentComplete, bool Done, bool SessionCompleted);

public record SaveProgressRequest(int PositionSeconds, decimal Percent);

/// <summary>A part as the learner sees it. Status: Locked | Open | Done. No asset id (GR-3).</summary>
public record SessionPartDto(
    Guid Id, int OrderIndex, string Kind, string Title, string Status,
    int? DurationSeconds, int ResumePositionSeconds, decimal PercentComplete,
    Guid? AssessmentId, int AttemptsUsed, int FailedAttempts, bool Passed, int? DiscussionAfterFailures);

/// <summary>Everything the session page needs. Parts is empty for live and final sessions.</summary>
public record SessionContextDto(
    Guid Id, Guid ProgramId, string ProgramName, int OrderIndex, string Type,
    string Title, string? Description,
    DateTimeOffset? ScheduledAt, string? LiveMode, string? JoinUrl, string? Location,
    bool Completed, DateTimeOffset? CompletedAt,
    IReadOnlyList<SessionPartDto> Parts,
    Guid? NextSessionId,
    bool NextSessionUnlocked);

public interface ISessionLearningService
{
    Task<SessionContextDto> GetContextAsync(Guid userId, Guid sessionId, CancellationToken ct = default);
    Task<PartPlaybackDto> GetPartPlaybackAsync(Guid userId, Guid sessionId, Guid partId, CancellationToken ct = default);
    Task<PartProgressDto> GetPartProgressAsync(Guid userId, Guid sessionId, Guid partId, CancellationToken ct = default);
    /// <summary>Monotonic percent; delegates completion to ISessionCompletionService (GR-8).</summary>
    Task<PartProgressDto> SavePartProgressAsync(
        Guid userId, Guid sessionId, Guid partId, int positionSeconds, decimal percent, CancellationToken ct = default);
}
