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
        var body = EmailTemplates.Verification("Budi", Url);

        // A verification email without its link is not a degraded email, it is a dead end: the
        // learner cannot verify, cannot buy, and has nothing to report but "it didn't work".
        Assert.Contains(Url, part == "html" ? body.Html : body.Text);
    }

    [Fact]
    public void The_html_body_offers_the_bare_url_as_well_as_the_button()
    {
        // Clients strip buttons and plenty of people will not click one. The pasteable URL is
        // what makes the mail work anyway — so it appears twice, in the href and as text.
        var html = EmailTemplates.PasswordReset("Budi", Url).Html;

        Assert.Contains($"href=\"{Url}\"", html);
        var occurrences = html.Split(Url).Length - 1;
        Assert.True(occurrences >= 2, $"expected the URL as href AND as visible text, saw {occurrences}");
    }

    [Fact]
    public void The_password_changed_notice_carries_no_link_at_all()
    {
        // Nothing to click, by design: it is a warning that something already happened. A link
        // here would be a phishing template we had written ourselves.
        var body = EmailTemplates.PasswordChanged("Budi");

        Assert.DoesNotContain("href=", body.Html);
        Assert.DoesNotContain("http", body.Text);
    }

    // ---- the name is user input, and it lands inside markup ----

    [Fact]
    public void A_name_containing_markup_cannot_escape_into_the_html()
    {
        // Registration accepts any name. Interpolated raw, this one closes the surrounding
        // element and the rest of the email is whatever the sender chose to write.
        var body = EmailTemplates.Verification("<script>alert(1)</script>", Url);

        Assert.DoesNotContain("<script>", body.Html);
        Assert.Contains("&lt;script&gt;", body.Html);
    }

    [Fact]
    public void A_blank_name_does_not_produce_an_empty_greeting()
    {
        Assert.Contains("Halo Halo,", EmailTemplates.Verification("   ", Url).Html);
    }

    // ---- the receipt: the amount is the whole point ----

    [Theory]
    [InlineData(1_500_000, "Rp1.500.000")]
    [InlineData(2_500_000.4, "Rp2.500.000")]   // IDR is charged as whole rupiah
    [InlineData(999, "Rp999")]
    [InlineData(0, "Rp0")]
    public void Rupiah_is_formatted_the_indonesian_way_regardless_of_host_culture(decimal amount, string expected)
    {
        // Explicitly, not via CultureInfo: a container with ICU trimmed or a different locale
        // renders "Rp1,500,000" or "Rp1500000", and the receipt is the one number a learner checks
        // most carefully — it is money they just paid.
        Assert.Equal(expected, EmailTemplates.FormatIdr(amount));
    }

    [Fact]
    public void The_receipt_states_the_programme_and_the_amount()
    {
        var body = EmailTemplates.EnrollmentReceipt(
            "Budi", "INVERTA — Persiapan TOEFL", 1_500_000m, "https://inverta.recosta.id/app");

        Assert.Contains("Rp1.500.000", body.Html);
        Assert.Contains("Rp1.500.000", body.Text);
        Assert.Contains("Persiapan TOEFL", body.Text);
    }

    [Fact]
    public void The_receipt_does_not_claim_to_be_a_tax_document()
    {
        // Faktur and kuitansi pajak are regulated documents with content requirements this does
        // not meet. Claiming to be one is a tax problem, not a copy problem.
        var body = EmailTemplates.EnrollmentReceipt("Budi", "Program", 1m, "https://x.test/app");

        Assert.DoesNotContain("faktur", body.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pajak", body.Text, StringComparison.OrdinalIgnoreCase);
    }

    // ---- the certificate: GR-14 governs this one ----

    [Theory]
    [InlineData("html")]
    [InlineData("text")]
    public void The_certificate_email_says_the_score_is_a_prediction_not_an_official_result(string part)
    {
        // GR-14, and this is the message a learner forwards to a university or an employer, so the
        // disclaimer has to be IN it rather than only on the certificate it links to.
        var body = EmailTemplates.Certificate("Budi", "Persiapan TOEFL", "ABC123", 523, "https://x.test/verify/ABC123");
        var content = part == "html" ? body.Html : body.Text;

        Assert.Contains("PREDIKSI", content);
        Assert.Contains("bukan skor TOEFL resmi", content);
        Assert.Contains("ETS", content);
    }

    [Fact]
    public void The_certificate_email_carries_the_code_and_the_verify_link()
    {
        const string url = "https://inverta.recosta.id/verify/ABC123";
        var body = EmailTemplates.Certificate("Budi", "Persiapan TOEFL", "ABC123", 523, url);

        Assert.Contains("ABC123", body.Html);
        Assert.Contains(url, body.Text);
        Assert.Contains("523", body.Html);
    }

    [Fact]
    public void A_certificate_with_no_score_omits_the_row_rather_than_printing_zero()
    {
        // totalScore is nullable. "Skor prediksi: 0" on a certificate email is worse than silence.
        var body = EmailTemplates.Certificate("Budi", "Persiapan TOEFL", "ABC123", null, "https://x.test/v");

        Assert.DoesNotContain("Skor prediksi", body.Html);
        Assert.DoesNotContain("Skor prediksi", body.Text);
    }

    [Fact]
    public void A_programme_name_containing_markup_cannot_escape_into_the_html()
    {
        // Programme titles are admin input and land in the details table and the subject line.
        var body = EmailTemplates.EnrollmentReceipt(
            "Budi", "<img src=x onerror=alert(1)>", 1m, "https://x.test/app");

        Assert.DoesNotContain("<img", body.Html);
        Assert.Contains("&lt;img", body.Html);
    }

    // ---- the reminder: the clock is the whole message ----

    [Fact]
    public void The_time_is_shifted_into_WIB_not_printed_as_stored()
    {
        // The API container runs UTC. A session stored at 12:00Z is 19.00 WIB — printed raw it
        // reads seven hours early, and a learner who trusts it misses the class. That is the worst
        // failure available to this particular email, and it is invisible to anyone testing from
        // a machine already set to Jakarta time.
        var noonUtc = new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal("Sabtu, 12 September 2026, 19.00 WIB", EmailTemplates.FormatWib(noonUtc));
    }

    [Fact]
    public void A_late_evening_utc_time_rolls_into_the_next_day_in_WIB()
    {
        // 20:00Z on Saturday is 03.00 Sunday in Jakarta. Shifting the clock without the date is a
        // reminder for the wrong day.
        var lateUtc = new DateTimeOffset(2026, 9, 12, 20, 0, 0, TimeSpan.Zero);

        Assert.Equal("Minggu, 13 September 2026, 03.00 WIB", EmailTemplates.FormatWib(lateUtc));
    }

    [Fact]
    public void An_instant_with_a_non_utc_offset_is_still_rendered_in_WIB()
    {
        // DateTimeOffset carries an offset and EF can hand one back in a zone nobody expected.
        // The same instant must read the same way regardless of how it arrived.
        var sameInstant = new DateTimeOffset(2026, 9, 12, 14, 0, 0, TimeSpan.FromHours(2));  // 12:00Z

        Assert.Equal("Sabtu, 12 September 2026, 19.00 WIB", EmailTemplates.FormatWib(sameInstant));
    }

    private static readonly DateTimeOffset When = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_online_session_puts_the_join_link_in_front_of_the_learner()
    {
        const string join = "https://meet.example.com/abc-defg-hij";
        var body = EmailTemplates.LiveSessionReminder(
            "Budi", "Persiapan TOEFL", "Sesi Live 1", When, join, null, "https://x.test/app");

        Assert.Contains($"href=\"{join}\"", body.Html);
        Assert.Contains(join, body.Text);
        Assert.Contains("Gabung sesi", body.Html);
        Assert.Contains("19.00 WIB", body.Text);
    }

    [Fact]
    public void An_onsite_session_gives_the_address_and_does_not_offer_a_join_button()
    {
        var body = EmailTemplates.LiveSessionReminder(
            "Budi", "Persiapan TOEFL", "Sesi Live 1", When, null,
            "Jl. Sudirman No. 1, Jakarta", "https://x.test/app");

        Assert.Contains("Jl. Sudirman No. 1, Jakarta", body.Text);
        Assert.DoesNotContain("Gabung sesi", body.Html);
    }

    [Fact]
    public void A_session_with_neither_a_link_nor_a_location_says_so_plainly()
    {
        // An admin can schedule first and fill the details in later, and the H-1 sweep does not
        // wait for them. Silence here reads as the learner's problem to solve.
        var body = EmailTemplates.LiveSessionReminder(
            "Budi", "Persiapan TOEFL", "Sesi Live 1", When, null, null, "https://x.test/app");

        Assert.Contains("belum tersedia", body.Text);
    }

    [Fact]
    public void The_notice_is_absent_when_the_details_are_present()
    {
        // A "details are missing" warning on an email that carries the link would be worse than
        // no warning at all.
        var body = EmailTemplates.LiveSessionReminder(
            "Budi", "Persiapan TOEFL", "Sesi Live 1", When, "https://meet.test/x", null, "https://x.test/app");

        Assert.DoesNotContain("belum tersedia", body.Text);
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
