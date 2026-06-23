namespace Academy.Application.Learning;

/// <summary>Playback (entitlement-gated, GR-3), watch progress + auto-completion, and the
/// learner dashboard. Completing a module may trigger certificate issuance via
/// <see cref="ICertificateService"/>.</summary>
public interface ILearningService
{
    Task<PlaybackTicketDto> GetPlaybackAsync(Guid userId, Guid moduleId, CancellationToken ct = default);
    Task<PlayerContextDto> GetPlayerContextAsync(Guid userId, Guid moduleId, CancellationToken ct = default);
    Task<ModuleProgressDto?> GetProgressAsync(Guid userId, Guid moduleId, CancellationToken ct = default);
    Task<ModuleProgressDto> SaveProgressAsync(Guid userId, Guid moduleId, int positionSeconds, decimal percent, CancellationToken ct = default);
    Task<DashboardDto> GetDashboardAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>Certificate issuance (idempotent + immutable, GR-6), listing, public verification,
/// and on-demand PDF rendering.</summary>
public interface ICertificateService
{
    /// <summary>Issue a certificate if the level is now 100% complete and none exists yet. No-op otherwise.</summary>
    Task TryIssueForLevelAsync(Guid userId, Guid levelId, CancellationToken ct = default);
    Task<IReadOnlyList<CertificateDto>> GetMineAsync(Guid userId, CancellationToken ct = default);
    Task<CertificateVerifyDto?> VerifyAsync(string code, CancellationToken ct = default);
    Task<(byte[] Pdf, string FileName)?> GetPdfAsync(Guid userId, Guid certificateId, CancellationToken ct = default);
}

/// <summary>Domain-ish failure surfaced to the API as a problem-details response.</summary>
public class LearningException(string message, int statusCode = 403) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
