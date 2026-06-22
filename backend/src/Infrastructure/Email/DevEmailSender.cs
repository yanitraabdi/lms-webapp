using Academy.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Academy.Infrastructure.Email;

/// <summary>Dev/local IEmailSender: logs the link to the API console so auth flows are
/// fully testable without SES. Replaced by SesEmailSender in a later milestone.</summary>
public class DevEmailSender(ILogger<DevEmailSender> logger) : IEmailSender
{
    public Task SendEmailVerificationAsync(string toEmail, string name, string verifyUrl, CancellationToken ct = default)
    {
        logger.LogInformation("[DEV EMAIL] Verifikasi email → {Email}: {Url}", toEmail, verifyUrl);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string name, string resetUrl, CancellationToken ct = default)
    {
        logger.LogInformation("[DEV EMAIL] Reset kata sandi → {Email}: {Url}", toEmail, resetUrl);
        return Task.CompletedTask;
    }

    public Task SendPasswordChangedAsync(string toEmail, string name, CancellationToken ct = default)
    {
        logger.LogInformation("[DEV EMAIL] Konfirmasi kata sandi diubah → {Email}", toEmail);
        return Task.CompletedTask;
    }
}
