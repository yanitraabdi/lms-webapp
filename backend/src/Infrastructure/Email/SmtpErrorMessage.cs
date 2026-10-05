using System.Net.Mail;
using System.Net.Sockets;

namespace Academy.Infrastructure.Email;

/// <summary>Turns an SmtpException into one actionable Indonesian sentence for the admin test
/// button. Never includes the exception message verbatim — it can echo server details — and never
/// any credential (GR-9).</summary>
public static class SmtpErrorMessage
{
    private const string Auth = "Login SMTP ditolak — periksa SMTP_USERNAME dan SMTP_PASSWORD (API key Resend).";
    private const string Sender = "Alamat pengirim ditolak — pastikan domain SMTP_FROM_ADDRESS sudah Verified di Resend.";
    public const string Unreachable = "Server SMTP tidak dapat dihubungi — periksa SMTP_HOST dan SMTP_PORT.";

    public static string For(SmtpException e)
    {
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

        return $"Pengiriman gagal (kode SMTP {code}).";
    }
}
