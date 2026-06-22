// Interface ports (hexagonal boundaries). Implemented in Infrastructure in later milestones.
// M0 intentionally ships EMPTY stubs — no logic, no provider integrations.
namespace Academy.Application.Abstractions;

/// <summary>Video provider (Bunny adapter). M1/M4: upload target, signed playback, webhook handling.</summary>
public interface IVideoProvider
{
    // M4: Task<UploadTarget> CreateUploadTargetAsync(...);
    // M4: Task<string> GetSignedPlaybackUrlAsync(string assetId, Guid userId, TimeSpan ttl);
    // M4: Task HandleProviderWebhookAsync(...);
}

/// <summary>Payment gateway (Xendit adapter). M3: recurring plans, MIT proration charge, webhook verify.</summary>
public interface IPaymentGateway
{
    // M3: create customer / activate recurring plan / charge MIT one-off / update plan amount / verify webhook
}

/// <summary>Transactional email (Amazon SES adapter; dev impl logs to console). M1 + later.</summary>
public interface IEmailSender
{
    Task SendEmailVerificationAsync(string toEmail, string name, string verifyUrl, CancellationToken ct = default);
    Task SendPasswordResetAsync(string toEmail, string name, string resetUrl, CancellationToken ct = default);
    Task SendPasswordChangedAsync(string toEmail, string name, CancellationToken ct = default);
}

/// <summary>Object storage (Cloudflare R2 adapter). Signed URLs for entitled downloads + cert PDFs/thumbnails.</summary>
public interface IObjectStorage
{
    // M4/M5: Task<string> GetSignedUrlAsync(string key, TimeSpan ttl); Task PutAsync(...);
}

/// <summary>Unified notifications (in-app + email). M7 notification center.</summary>
public interface INotificationSender
{
    // M7: Task DispatchAsync(...);
}
