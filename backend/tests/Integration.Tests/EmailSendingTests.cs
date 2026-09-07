using Academy.Infrastructure.Email;
using Microsoft.Extensions.Configuration;

namespace Academy.Integration.Tests;

/// <summary>
/// Until now nothing in this product had ever sent an email: IEmailSender had one implementation,
/// DevEmailSender, which logged to the console. Email verification is required before purchase,
/// so no real learner could complete one.
///
/// These are pure unit tests — no server — because the two things that actually break a
/// transactional email are things a running relay would not reveal: a link that did not survive
/// into the body, and a name that broke the markup around it.
/// </summary>
public class EmailSendingTests
{
    private const string Url = "https://inverta.recosta.id/verify-email?token=abc123";

    // ---- the link is the entire point of the message ----

    [Theory]
    [InlineData("html")]
    [InlineData("text")]
    public void The_verification_link_survives_into_both_bodies(string part)
    {
        var body = AuthEmailTemplates.Verification("Budi", Url);

        // A verification email without its link is not a degraded email, it is a dead end: the
        // learner cannot verify, cannot buy, and has nothing to report but "it didn't work".
        Assert.Contains(Url, part == "html" ? body.Html : body.Text);
    }

    [Fact]
    public void The_html_body_offers_the_bare_url_as_well_as_the_button()
    {
        // Clients strip buttons and plenty of people will not click one. The pasteable URL is
        // what makes the mail work anyway — so it appears twice, in the href and as text.
        var html = AuthEmailTemplates.PasswordReset("Budi", Url).Html;

        Assert.Contains($"href=\"{Url}\"", html);
        var occurrences = html.Split(Url).Length - 1;
        Assert.True(occurrences >= 2, $"expected the URL as href AND as visible text, saw {occurrences}");
    }

    [Fact]
    public void The_password_changed_notice_carries_no_link_at_all()
    {
        // Nothing to click, by design: it is a warning that something already happened. A link
        // here would be a phishing template we had written ourselves.
        var body = AuthEmailTemplates.PasswordChanged("Budi");

        Assert.DoesNotContain("href=", body.Html);
        Assert.DoesNotContain("http", body.Text);
    }

    // ---- the name is user input, and it lands inside markup ----

    [Fact]
    public void A_name_containing_markup_cannot_escape_into_the_html()
    {
        // Registration accepts any name. Interpolated raw, this one closes the surrounding
        // element and the rest of the email is whatever the sender chose to write.
        var body = AuthEmailTemplates.Verification("<script>alert(1)</script>", Url);

        Assert.DoesNotContain("<script>", body.Html);
        Assert.Contains("&lt;script&gt;", body.Html);
    }

    [Fact]
    public void A_blank_name_does_not_produce_an_empty_greeting()
    {
        Assert.Contains("Halo Halo,", AuthEmailTemplates.Verification("   ", Url).Html);
    }

    // ---- an incomplete smtp config must not start ----

    private static EmailOptions Build(params (string Key, string Value)[] settings)
        => EmailOptionsFactory.Build(new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s =>
                new KeyValuePair<string, string?>($"Email:{s.Key}", s.Value)))
            .Build());

    [Fact]
    public void The_default_provider_is_the_console_logger()
    {
        Assert.False(Build().IsSmtp);
    }

    [Fact]
    public void An_smtp_provider_missing_its_credentials_refuses_to_start()
    {
        // The alternative — quietly falling back to the console logger — is the worst outcome
        // available: the API reports healthy, registration returns 200, and nobody finds out the
        // mail went to a log file until a paying customer says it never arrived.
        var e = Assert.Throws<InvalidOperationException>(() => Build(
            ("Provider", "smtp"), ("Username", "noreply@example.com"), ("FromAddress", "noreply@example.com")));

        Assert.Contains("Email:Password", e.Message);
    }

    [Fact]
    public void A_complete_smtp_config_is_accepted()
    {
        var o = Build(
            ("Provider", "SMTP"),                     // case-insensitive: config is hand-written
            ("Username", "noreply@example.com"),
            ("Password", "app-password"),
            ("FromAddress", "noreply@example.com"));

        Assert.True(o.IsSmtp);
        Assert.Equal("smtp.gmail.com", o.Host);
        Assert.Equal(587, o.Port);
    }
}
