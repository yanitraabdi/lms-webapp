// INVERTA M3 — session playback & progress (KAK §9.5).
namespace Academy.Application.Programs;

/// <summary>Short-TTL signed playback URL for ONE viewing session (GR-3). Never carries the asset id.</summary>
public record SessionPlaybackDto(Guid SessionId, string Url, DateTimeOffset ExpiresAt, string? CaptionsUrl);

public record SessionProgressDto(
    Guid SessionId, int ResumePositionSeconds, decimal PercentComplete,
    bool Completed, DateTimeOffset? CompletedAt,
    /// <summary>True once the watch threshold is met — the gating test unlocks on the same page.</summary>
    bool WatchThresholdMet);

public record SaveProgressRequest(int PositionSeconds, decimal Percent);

/// <summary>Everything the session page needs: the session itself plus its gating-test state.</summary>
public record SessionContextDto(
    Guid Id, Guid ProgramId, string ProgramName, int OrderIndex, string Type,
    string Title, string? Description, int? DurationSeconds,
    DateTimeOffset? ScheduledAt, string? LiveMode, string? JoinUrl, string? Location,
    SessionProgressDto Progress,
    bool HasAssessment,
    bool AssessmentPassed,
    Guid? NextSessionId,
    bool NextSessionUnlocked);

public interface ISessionLearningService
{
    /// <summary>Mints a signed URL AFTER the access gate. Video sessions only.</summary>
    Task<SessionPlaybackDto> GetPlaybackAsync(Guid userId, Guid sessionId, CancellationToken ct = default);

    Task<SessionContextDto> GetContextAsync(Guid userId, Guid sessionId, CancellationToken ct = default);

    Task<SessionProgressDto> GetProgressAsync(Guid userId, Guid sessionId, CancellationToken ct = default);

    /// <summary>Monotonic percent; delegates completion to ISessionCompletionService (GR-8).</summary>
    Task<SessionProgressDto> SaveProgressAsync(
        Guid userId, Guid sessionId, int positionSeconds, decimal percent, CancellationToken ct = default);
}
