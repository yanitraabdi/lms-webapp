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
}
