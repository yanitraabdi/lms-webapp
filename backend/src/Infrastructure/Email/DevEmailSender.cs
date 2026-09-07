using Academy.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Academy.Infrastructure.Email;

/// <summary>
/// Dev/local IEmailSender: logs the link to the API console so auth flows are fully testable
/// without a relay. The default provider, and what every test runs against.
///
/// Also the base of <see cref="SmtpEmailSender"/>, which overrides the three authentication
/// methods and inherits the rest — so the seven messages whose bodies are not written yet keep
/// logging instead of quietly doing nothing.
/// </summary>
public class DevEmailSender(ILogger<DevEmailSender> logger) : IEmailSender
{
    /// <summary>Available to the SMTP subclass for its own failure logging.</summary>
    protected readonly ILogger<DevEmailSender> logger = logger;

    public virtual Task SendEmailVerificationAsync(string toEmail, string name, string verifyUrl, CancellationToken ct = default)
    {
        logger.LogInformation("[DEV EMAIL] Verifikasi email → {Email}: {Url}", toEmail, verifyUrl);
        return Task.CompletedTask;
    }

    public virtual Task SendPasswordResetAsync(string toEmail, string name, string resetUrl, CancellationToken ct = default)
    {
        logger.LogInformation("[DEV EMAIL] Reset kata sandi → {Email}: {Url}", toEmail, resetUrl);
        return Task.CompletedTask;
    }

    public virtual Task SendPasswordChangedAsync(string toEmail, string name, CancellationToken ct = default)
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

    public Task SendEnrollmentReceiptAsync(string toEmail, string name, string programName, decimal amountIdr, CancellationToken ct = default)
    {
        logger.LogInformation("[DEV EMAIL] Pendaftaran {Program} berhasil → {Email}: Rp{Amount}", programName, toEmail, amountIdr);
        return Task.CompletedTask;
    }

    public Task SendCertificateAsync(string toEmail, string name, string programName, string verificationCode,
        int? totalScore, string verifyUrl, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[DEV EMAIL] Sertifikat {Program} → {Email}: prediksi {Score}, kode {Code}, {Url}",
            programName, toEmail, totalScore, verificationCode, verifyUrl);
        return Task.CompletedTask;
    }

    public Task SendLiveSessionReminderAsync(string toEmail, string name, string programName, string sessionTitle,
        DateTimeOffset scheduledAt, string? joinUrl, string? location, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[DEV EMAIL] Pengingat sesi live '{Session}' ({Program}) → {Email}: {At:yyyy-MM-dd HH:mm} {Where}",
            sessionTitle, programName, toEmail, scheduledAt, joinUrl ?? location ?? "-");
        return Task.CompletedTask;
    }
}
