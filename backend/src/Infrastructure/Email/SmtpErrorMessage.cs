using System.Net.Mail;
using System.Net.Sockets;

namespace Academy.Infrastructure.Email;

/// <summary>Turns an SmtpException into one actionable English sentence for the admin test
/// button. Never includes the exception message verbatim — it can echo server details — and never
/// any credential (GR-9).</summary>
public static class SmtpErrorMessage
{
    private const string Auth = "SMTP login rejected — check SMTP_USERNAME and SMTP_PASSWORD (Resend API key).";
    private const string Sender = "Sender address rejected — make sure the SMTP_FROM_ADDRESS domain is Verified in Resend.";
    private const string Recipient = "Recipient address rejected — check your admin account's email address or the suppression list in Resend.";
    public const string Unreachable = "Couldn't reach the SMTP server — check SMTP_HOST and SMTP_PORT.";

    public static string For(SmtpException e)
    {
        if (e is SmtpFailedRecipientException)
            return Recipient;

        var text = (e.Message + " " + e.InnerException?.Message).ToLowerInvariant();
        var code = (int)e.StatusCode;

        // Status codes first, then text: a sender rejection such as "unauthorized sender domain"
        // contains "auth", so the sender text check must run before the auth text check.
        if (e.StatusCode == SmtpStatusCode.ClientNotPermitted || code == 535)
            return Auth;

        if (e.StatusCode is SmtpStatusCode.MailboxUnavailable or SmtpStatusCode.MailboxNameNotAllowed
            || text.Contains("domain") || text.Contains("not verified"))
            return Sender;

        if (text.Contains("auth"))
            return Auth;

        if (e.InnerException is SocketException or IOException || e.StatusCode == SmtpStatusCode.GeneralFailure)
            return Unreachable;

        return $"Sending failed (SMTP code {code}).";
    }
}
