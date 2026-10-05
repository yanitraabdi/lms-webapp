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

/// <summary>
/// The video library an admin attaches sessions to: lists videos AND starts uploads (the file goes
/// browser → Bunny directly). Bunny remains the source of truth for the files; delete and rename
/// stay in Bunny's dashboard.
/// </summary>
public interface IVideoLibrary
{
    Task<VideoLibraryPageDto> ListAsync(string? search, int page, CancellationToken ct = default);

    /// <summary>Creates an empty Bunny video and returns a signed TUS ticket for it (spec 2026-10-05).
    /// The file itself never passes through this API. Throws ProgramException 409/502.</summary>
    Task<UploadTicketDto> CreateUploadAsync(string title, CancellationToken ct = default);

    /// <summary>A fresh ticket for an existing video, for an upload that outlived its ticket.</summary>
    UploadTicketDto RenewUpload(string videoId);
}

/// <summary>What the browser needs to upload ONE file straight to Bunny over TUS. Never carries the
/// API key (GR-9) — the signature is scoped to this video and expires.</summary>
public record UploadTicketDto(string VideoId, string LibraryId, long ExpiresAt, string Signature, string Endpoint);

/// <summary>One library video. <c>Status</c> is a name ("Finished", "Processing", …), never
/// Bunny's integer, so the frontend does not depend on Bunny's numbering.</summary>
public record VideoLibraryItemDto(string Id, string Title, int LengthSeconds, string Status, int EncodeProgress);

/// <summary><c>Unavailable</c> is null when <c>Items</c> is the real library, and otherwise says in
/// Indonesian why there is no list — the picker shows it and offers manual entry instead.</summary>
public record VideoLibraryPageDto(
    IReadOnlyList<VideoLibraryItemDto> Items, int Page, int TotalItems, string? Unavailable);

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

    // ---- INVERTA (M2+): one-time program enrollment ----
    /// <summary>Receipt sent once the verified webhook has activated an enrollment (KAK §9.4 R6).</summary>
    Task SendEnrollmentReceiptAsync(string toEmail, string name, string programName, decimal amountIdr, CancellationToken ct = default);

    /// <summary>Certificate delivery after the final assessment (KAK §9.10 R4). The body must state
    /// that the score is a prediction, not an official ETS result (GR-14).</summary>
    Task SendCertificateAsync(string toEmail, string name, string programName, string verificationCode,
        int? totalScore, string verifyUrl, CancellationToken ct = default);

    /// <summary>H-1 reminder before a scheduled live session (KAK §9.11 R3).</summary>
    Task SendLiveSessionReminderAsync(string toEmail, string name, string programName, string sessionTitle,
        DateTimeOffset scheduledAt, string? joinUrl, string? location, CancellationToken ct = default);
}

/// <summary>Object storage (local-disk dev-sim; Cloudflare R2 adapter later).
/// Deliberately minimal: no Delete and no Exists. Replacing an upload orphans the previous
/// object, which costs disk and nothing else, whereas a delete could remove a file another
/// question still references — keys are free-form strings with no reference counting.
/// OpenReadAsync returning null answers the existence question at the only point that asks it.</summary>
public interface IObjectStorage
{
    /// <summary>Stores the stream under <paramref name="key"/>, overwriting any existing object.</summary>
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Opens the object for reading, or null when the key does not exist.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default);
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

public record CreateVideoUploadRequest(string? Title);

/// <summary>Admin use case over IVideoLibrary: validates the title, starts the upload, audits it.
/// Routes stay thin and never touch the DbContext.</summary>
public interface IVideoUploadService
{
    Task<UploadTicketDto> StartAsync(Guid actor, string? title, CancellationToken ct = default);
    UploadTicketDto Renew(string videoId);
}
