using Academy.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Logging;

namespace Academy.Infrastructure.Email;

/// <summary>
/// Sends the five real emails over SMTP: the three authentication ones, the enrolment receipt and
/// the certificate. The rest still log to the console, inherited from <see cref="DevEmailSender"/>
/// — the five archived subscription messages, which nothing sends, and the live-session reminder,
/// whose body is not written.
///
/// Configured for Google Workspace with an app password; any SMTP relay is the same settings.
/// </summary>
public class SmtpEmailSender(
    EmailOptions options, IOptions<AuthOptions> authOptions, ILogger<DevEmailSender> logger)
    : DevEmailSender(logger)
{
    /// <summary>Where "Mulai belajar" points. The same base the verify and reset links use, so
    /// there is one setting to get wrong rather than two.</summary>
    private string AppUrl => $"{authOptions.Value.FrontendBaseUrl.TrimEnd('/')}/app";

    public override Task SendEmailVerificationAsync(
        string toEmail, string name, string verifyUrl, CancellationToken ct = default)
        => SendAsync(toEmail, name, EmailTemplates.Verification(name, verifyUrl), ct);

    public override Task SendPasswordResetAsync(
        string toEmail, string name, string resetUrl, CancellationToken ct = default)
        => SendAsync(toEmail, name, EmailTemplates.PasswordReset(name, resetUrl), ct);

    public override Task SendPasswordChangedAsync(
        string toEmail, string name, CancellationToken ct = default)
        => SendAsync(toEmail, name, EmailTemplates.PasswordChanged(name), ct);

    public override Task SendEnrollmentReceiptAsync(
        string toEmail, string name, string programName, decimal amountIdr, CancellationToken ct = default)
        => SendAsync(toEmail, name,
            EmailTemplates.EnrollmentReceipt(name, programName, amountIdr, AppUrl), ct);

    public override Task SendCertificateAsync(
        string toEmail, string name, string programName, string verificationCode,
        int? totalScore, string verifyUrl, CancellationToken ct = default)
        => SendAsync(toEmail, name,
            EmailTemplates.Certificate(name, programName, verificationCode, totalScore, verifyUrl), ct);

    private async Task SendAsync(string toEmail, string name, EmailBody body, CancellationToken ct)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(options.FromAddress, options.FromName),
            Subject = body.Subject,
            Body = body.Text,
            IsBodyHtml = false,
        };
        message.To.Add(new MailAddress(toEmail, name));
        if (!string.IsNullOrWhiteSpace(options.ReplyTo)) message.ReplyToList.Add(options.ReplyTo);

        // Text is the body, HTML the alternate view: a client that cannot render HTML still gets
        // a readable message with the link in it, rather than a blank one.
        message.AlternateViews.Add(
            AlternateView.CreateAlternateViewFromString(body.Html, null, MediaTypeNames.Text.Html));

        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = true,                                   // STARTTLS on 587
            Credentials = new NetworkCredential(options.Username, options.Password),
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        try
        {
            await client.SendMailAsync(message, ct);
        }
        catch (SmtpException e)
        {
            // The address is logged; the body is not, because it carries a single-use token that
            // grants account access. Rethrown rather than swallowed: the caller decides, and a
            // silently dropped verification mail is exactly the failure this replaced.
            logger.LogError(e, "SMTP send failed → {Email}: {Subject}", toEmail, body.Subject);
            throw;
        }
    }
}
