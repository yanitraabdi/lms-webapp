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
}
