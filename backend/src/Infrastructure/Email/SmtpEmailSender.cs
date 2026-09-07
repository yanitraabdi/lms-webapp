using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Logging;

namespace Academy.Infrastructure.Email;

/// <summary>
/// Sends the three AUTHENTICATION emails over SMTP. Everything else still logs to the console,
/// inherited from <see cref="DevEmailSender"/>.
///
/// That split is deliberate and worth stating plainly, because a half-real email sender is the
/// kind of thing that gets misremembered as finished: verification, password reset and the
/// password-changed notice are what stand between a learner and a purchase, so they are what was
/// asked for. The enrolment receipt, the certificate email and the live-session reminder are
/// still stubs — a learner who buys the programme today gets no receipt, and one who finishes it
/// gets no certificate in their inbox. Those bodies have not been written yet.
///
/// Configured for Google Workspace with an app password; any SMTP relay is the same settings.
/// </summary>
public class SmtpEmailSender(EmailOptions options, ILogger<DevEmailSender> logger)
    : DevEmailSender(logger)
{
    public override Task SendEmailVerificationAsync(
        string toEmail, string name, string verifyUrl, CancellationToken ct = default)
        => SendAsync(toEmail, name, AuthEmailTemplates.Verification(name, verifyUrl), ct);

    public override Task SendPasswordResetAsync(
        string toEmail, string name, string resetUrl, CancellationToken ct = default)
        => SendAsync(toEmail, name, AuthEmailTemplates.PasswordReset(name, resetUrl), ct);

    public override Task SendPasswordChangedAsync(
        string toEmail, string name, CancellationToken ct = default)
        => SendAsync(toEmail, name, AuthEmailTemplates.PasswordChanged(name), ct);

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
