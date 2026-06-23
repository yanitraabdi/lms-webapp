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

    public Task SendSubscriptionConfirmationAsync(string toEmail, string name, string planName, decimal amountIdr, DateTimeOffset periodEnd, CancellationToken ct = default)
    {
        logger.LogInformation("[DEV EMAIL] Langganan {Plan} aktif → {Email}: Rp{Amount} s/d {End:yyyy-MM-dd}", planName, toEmail, amountIdr, periodEnd);
        return Task.CompletedTask;
    }

    public Task SendPaymentFailedAsync(string toEmail, string name, string planName, CancellationToken ct = default)
    {
        logger.LogInformation("[DEV EMAIL] Pembayaran {Plan} gagal → {Email}", planName, toEmail);
        return Task.CompletedTask;
    }

    public Task SendSubscriptionExpiredAsync(string toEmail, string name, string planName, CancellationToken ct = default)
    {
        logger.LogInformation("[DEV EMAIL] Langganan {Plan} berakhir → {Email}", planName, toEmail);
        return Task.CompletedTask;
    }

    public Task SendNotificationAsync(string toEmail, string name, string title, string body, CancellationToken ct = default)
    {
        logger.LogInformation("[DEV EMAIL] Notifikasi → {Email}: {Title}", toEmail, title);
        return Task.CompletedTask;
    }
}
