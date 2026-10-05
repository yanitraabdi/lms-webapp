namespace Academy.Application.Admin;

/// <summary>The active email relay, for the admin card. Never carries Username or Password (GR-9).</summary>
public record EmailStatusDto(string Provider, string Host, int Port, string FromAddress, string FromName, string ReplyTo);

public record EmailTestResultDto(bool Sent, string Message);

public interface IEmailDiagnosticsService
{
    EmailStatusDto GetStatus();

    /// <summary>Sends the test email to the signed-in admin's OWN address — never anyone else's.
    /// Throws AdminException 502 with an actionable message when the relay refuses.</summary>
    Task<EmailTestResultDto> SendTestAsync(Guid adminId, CancellationToken ct = default);
}
