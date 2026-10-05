using Microsoft.Extensions.Configuration;

namespace Academy.Infrastructure.Email;

/// <summary>
/// Outbound email settings. Mirrors <see cref="Media.MediaOptions"/>.
///
/// Provider "dev" logs every message to the API console (the default, and what every test runs
/// against). Provider "smtp" sends over any SMTP relay — Resend (recommended) or Gmail.
/// </summary>
public class EmailOptions
{
    public const string Section = "Email";

    /// <summary>"dev" (log to console) or "smtp" (actually send).</summary>
    public string Provider { get; set; } = "dev";

    public string Host { get; set; } = "smtp.resend.com";
    public int Port { get; set; } = 587;                  // STARTTLS

    /// <summary>The SMTP login: literally "resend" for Resend; a real mailbox for Gmail (Google
    /// refuses an alias).</summary>
    public string Username { get; set; } = "";

    /// <summary>A Resend API key ("re_…") or a Gmail app password. Server config / secret store
    /// only, never the repo (GR-9).</summary>
    public string Password { get; set; } = "";

    /// <summary>The visible From. Must be on a domain the relay may send for — for Resend, a
    /// domain shown as Verified; otherwise the relay rejects it.</summary>
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "INVERTA";

    /// <summary>Where a learner's reply goes. Blank leaves replies going to FromAddress, which is
    /// wrong when that is a noreply mailbox nobody reads.</summary>
    public string ReplyTo { get; set; } = "";

    public bool IsSmtp => string.Equals(Provider, "smtp", StringComparison.OrdinalIgnoreCase);
}

public static class EmailOptionsFactory
{
    public static EmailOptions Build(IConfiguration config)
    {
        var o = new EmailOptions();
        config.GetSection(EmailOptions.Section).Bind(o);
        if (o.IsSmtp) Validate(o);
        return o;
    }

    /// <summary>
    /// Fails at startup, loudly, rather than at the moment a learner registers.
    ///
    /// The alternative — falling back to the dev logger when the config is incomplete — is the
    /// worst possible behaviour here: the API comes up healthy, registration returns 200, and
    /// nobody discovers the verification mail went to a log file until a paying customer says
    /// they never got it. That is the failure this whole change exists to end.
    /// </summary>
    private static void Validate(EmailOptions o)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(o.Host)) missing.Add($"{EmailOptions.Section}:Host");
        if (string.IsNullOrWhiteSpace(o.Username)) missing.Add($"{EmailOptions.Section}:Username");
        if (string.IsNullOrWhiteSpace(o.Password)) missing.Add($"{EmailOptions.Section}:Password");
        if (string.IsNullOrWhiteSpace(o.FromAddress)) missing.Add($"{EmailOptions.Section}:FromAddress");

        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Email:Provider is \"smtp\" but {string.Join(", ", missing)} " +
                "is not configured. Set it in server config or the secret store — never in the repo.");
    }
}
