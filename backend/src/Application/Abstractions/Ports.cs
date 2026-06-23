// Interface ports (hexagonal boundaries). Implemented in Infrastructure in later milestones.
// M0 intentionally ships EMPTY stubs — no logic, no provider integrations.
namespace Academy.Application.Abstractions;

/// <summary>Video provider (Bunny adapter; dev-sim locally). Mints per-session, short-TTL
/// signed playback URLs after a server-side entitlement check (GR-3). No public/persisted URLs.</summary>
public interface IVideoProvider
{
    string Source { get; }
    Task<PlaybackTicket> CreatePlaybackTicketAsync(string assetId, Guid userId, TimeSpan ttl, CancellationToken ct = default);
}

/// <summary>A short-lived, signed playback URL for one viewing session.</summary>
public record PlaybackTicket(string Url, DateTimeOffset ExpiresAt, string? CaptionsUrl = null);

// IPaymentGateway lives in Academy.Application.Billing (M3) — a full contract, not a stub.

/// <summary>Transactional email (Amazon SES adapter; dev impl logs to console). M1 + later.</summary>
public interface IEmailSender
{
    Task SendEmailVerificationAsync(string toEmail, string name, string verifyUrl, CancellationToken ct = default);
    Task SendPasswordResetAsync(string toEmail, string name, string resetUrl, CancellationToken ct = default);
    Task SendPasswordChangedAsync(string toEmail, string name, CancellationToken ct = default);

    // ---- Billing (M3) ----
    Task SendSubscriptionConfirmationAsync(string toEmail, string name, string planName, decimal amountIdr, DateTimeOffset periodEnd, CancellationToken ct = default);
    Task SendPaymentFailedAsync(string toEmail, string name, string planName, CancellationToken ct = default);
    Task SendSubscriptionExpiredAsync(string toEmail, string name, string planName, CancellationToken ct = default);

    // Generic notification-center email (M7) — governed by user preferences.
    Task SendNotificationAsync(string toEmail, string name, string title, string body, CancellationToken ct = default);
}

/// <summary>Object storage (Cloudflare R2 adapter). Signed URLs for entitled downloads + cert PDFs/thumbnails.</summary>
public interface IObjectStorage
{
    // M4/M5: Task<string> GetSignedUrlAsync(string key, TimeSpan ttl); Task PutAsync(...);
}

/// <summary>Dispatches an engagement notification to a user across enabled channels
/// (in-app row + email), gated by their NotificationPreference matrix (M7).</summary>
public interface INotificationSender
{
    Task DispatchAsync(Guid userId, string category, string type, string title, string body, CancellationToken ct = default);
}

/// <summary>Triggers Next.js on-demand ISR revalidation of public pages after admin
/// content/pricing changes (TSD §4.2). Best-effort — failures must not break the mutation.</summary>
public interface IContentRevalidator
{
    Task RevalidateAsync(IReadOnlyCollection<string> paths, CancellationToken ct = default);
}
