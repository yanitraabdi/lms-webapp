using Academy.Application.Abstractions;

namespace Academy.Integration.Tests;

/// <summary>Test double that captures the verify/reset links instead of sending email,
/// so tests can drive the email-verification and password-reset flows end to end.</summary>
public class CapturingEmailSender : IEmailSender
{
    public string? LastVerifyUrl { get; private set; }
    public string? LastResetUrl { get; private set; }
    public int PasswordChangedCount { get; private set; }

    public Task SendEmailVerificationAsync(string toEmail, string name, string verifyUrl, CancellationToken ct = default)
    {
        LastVerifyUrl = verifyUrl;
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string name, string resetUrl, CancellationToken ct = default)
    {
        LastResetUrl = resetUrl;
        return Task.CompletedTask;
    }

    public Task SendPasswordChangedAsync(string toEmail, string name, CancellationToken ct = default)
    {
        PasswordChangedCount++;
        return Task.CompletedTask;
    }

    // ---- Billing (M3) ----
    public int SubscriptionConfirmationCount { get; private set; }
    public int PaymentFailedCount { get; private set; }
    public int SubscriptionExpiredCount { get; private set; }

    public Task SendSubscriptionConfirmationAsync(string toEmail, string name, string planName, decimal amountIdr, DateTimeOffset periodEnd, CancellationToken ct = default)
    {
        SubscriptionConfirmationCount++;
        return Task.CompletedTask;
    }

    public Task SendPaymentFailedAsync(string toEmail, string name, string planName, CancellationToken ct = default)
    {
        PaymentFailedCount++;
        return Task.CompletedTask;
    }

    public Task SendSubscriptionExpiredAsync(string toEmail, string name, string planName, CancellationToken ct = default)
    {
        SubscriptionExpiredCount++;
        return Task.CompletedTask;
    }

    public int NotificationEmailCount { get; private set; }

    public Task SendNotificationAsync(string toEmail, string name, string title, string body, CancellationToken ct = default)
    {
        NotificationEmailCount++;
        return Task.CompletedTask;
    }

    // ---- INVERTA (M2): enrollment receipt ----
    public int EnrollmentReceiptCount { get; private set; }
    public string? LastEnrollmentProgram { get; private set; }

    public Task SendEnrollmentReceiptAsync(string toEmail, string name, string programName, decimal amountIdr, CancellationToken ct = default)
    {
        EnrollmentReceiptCount++;
        LastEnrollmentProgram = programName;
        return Task.CompletedTask;
    }

    // ---- INVERTA (M4): certificate delivery ----
    public int CertificateEmailCount { get; private set; }
    public string? LastCertificateCode { get; private set; }
    public int? LastCertificateScore { get; private set; }

    public Task SendCertificateAsync(string toEmail, string name, string programName, string verificationCode,
        int? totalScore, string verifyUrl, CancellationToken ct = default)
    {
        CertificateEmailCount++;
        LastCertificateCode = verificationCode;
        LastCertificateScore = totalScore;
        return Task.CompletedTask;
    }

    // ---- INVERTA (M5): live-session reminder ----
    public int LiveReminderCount { get; private set; }
    public string? LastLiveSessionTitle { get; private set; }
    private readonly Dictionary<string, int> _liveRemindersByTitle = [];

    /// <summary>Reminders sent for one session title. The sweep is global, so tests sharing a
    /// database must assert per-session rather than on the total.</summary>
    public int LiveRemindersFor(string sessionTitle)
        => _liveRemindersByTitle.TryGetValue(sessionTitle, out var n) ? n : 0;

    public Task SendLiveSessionReminderAsync(string toEmail, string name, string programName, string sessionTitle,
        DateTimeOffset scheduledAt, string? joinUrl, string? location, CancellationToken ct = default)
    {
        LiveReminderCount++;
        LastLiveSessionTitle = sessionTitle;
        lock (_liveRemindersByTitle)
            _liveRemindersByTitle[sessionTitle] = LiveRemindersFor(sessionTitle) + 1;
        return Task.CompletedTask;
    }
}
