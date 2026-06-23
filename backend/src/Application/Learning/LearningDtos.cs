namespace Academy.Application.Learning;

// ---- playback + progress ----

public record PlaybackTicketDto(Guid ModuleId, string Url, DateTimeOffset ExpiresAt, string? CaptionsUrl);

public record ModuleProgressDto(
    Guid ModuleId,
    int ResumePositionSeconds,
    decimal PercentComplete,
    bool Completed,
    DateTimeOffset? CompletedAt);

public record SaveProgressRequest(int PositionSeconds, decimal Percent);

// ---- player context (metadata + track playlist for the player screen) ----

public record ResourceItemDto(string Type, string Title);

public record PlaylistItemDto(Guid Id, string Title, int DurationSeconds, bool Completed, bool IsCurrent);

public record PlayerContextDto(
    Guid ModuleId,
    string Title,
    string Description,
    string LevelName,
    string TrackName,
    int DurationSeconds,
    int ModuleNumber,
    int TrackCount,
    int CompletedInTrack,
    Guid? NextModuleId,
    IReadOnlyList<ResourceItemDto> Resources,
    IReadOnlyList<PlaylistItemDto> Playlist);

// ---- dashboard ----

public record ContinueModuleDto(
    Guid ModuleId,
    string Slug,
    string Title,
    string LevelName,
    string TrackName,
    int DurationSeconds,
    string? ThumbnailUrl,
    decimal PercentComplete);

public record LevelProgressDto(
    Guid LevelId,
    string Name,
    string Slug,
    int TierLevel,
    int CompletedCount,
    int PublishedCount,
    decimal Percent,
    bool Certified,
    bool Unlocked);

public record OverallProgressDto(int CompletedCount, int TotalCount, decimal Percent);

public record DashboardDto(
    int? ActiveTier,
    IReadOnlyList<ContinueModuleDto> ContinueLearning,
    IReadOnlyList<ContinueModuleDto> RecommendedNext,
    IReadOnlyList<LevelProgressDto> Levels,
    OverallProgressDto Overall);

// ---- certificates ----

public record CertificateDto(
    Guid Id,
    Guid LevelId,
    string LevelName,
    DateTimeOffset IssuedAt,
    string VerificationCode,
    int ModuleCount);

public record CertificateVerifyDto(
    bool Valid,
    string VerificationCode,
    string? RecipientName,
    string? LevelName,
    DateTimeOffset? IssuedAt,
    string Issuer);
