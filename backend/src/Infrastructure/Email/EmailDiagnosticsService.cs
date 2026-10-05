using System.Net.Mail;
using Academy.Application.Abstractions;
using Academy.Application.Admin;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Email;

public class EmailDiagnosticsService(EmailOptions options, IEmailSender sender, AppDbContext db) : IEmailDiagnosticsService
{
    /// <summary>Bounds one test send. Public init (no InternalsVisibleTo in this repo) so tests can shorten it.</summary>
    public TimeSpan SendTimeout { get; init; } = TimeSpan.FromSeconds(20);

    public EmailStatusDto GetStatus() => new(
        options.IsSmtp ? "smtp" : "dev", options.Host, options.Port,
        options.FromAddress, options.FromName, options.ReplyTo);

    public async Task<EmailTestResultDto> SendTestAsync(Guid adminId, CancellationToken ct = default)
    {
        var admin = await db.Users.Where(u => u.Id == adminId)
            .Select(u => new { u.Email, u.Name }).FirstOrDefaultAsync(ct)
            ?? throw new AdminException("Akun admin tidak ditemukan.", 404);

        if (!options.IsSmtp)
            return new(false, "Mode dev — email hanya ditulis ke log API, tidak benar-benar dikirim.");

        // SmtpClient.Timeout does not apply to SendMailAsync, so an unreachable host (dropped SYN)
        // would hang the button; bound it here. A timeout surfaces as OperationCanceledException,
        // not SmtpException, and must not become a 500.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(SendTimeout);
        try
        {
            await sender.SendTestAsync(admin.Email, admin.Name, timeout.Token);
        }
        catch (SmtpException e)
        {
            throw new AdminException(SmtpErrorMessage.For(e), 502);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AdminException(SmtpErrorMessage.Unreachable, 502);
        }
        return new(true, $"Email uji terkirim ke {admin.Email}. Periksa kotak masuk (dan folder spam).");
    }
}
