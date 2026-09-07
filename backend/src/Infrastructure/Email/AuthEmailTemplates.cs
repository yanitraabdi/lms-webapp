using System.Net;
using System.Text;

namespace Academy.Infrastructure.Email;

/// <summary>Subject plus both bodies. Every message is sent multipart: some clients, and most
/// spam filters, treat an HTML-only email as a smell.</summary>
public record EmailBody(string Subject, string Html, string Text);

/// <summary>
/// The three authentication emails, in Bahasa Indonesia.
///
/// Pure functions with no SMTP dependency, so what a learner actually receives can be asserted
/// in a test — which is the only way to catch the failures that matter here: a link that did not
/// make it into the body, or a name that broke the markup around it.
/// </summary>
public static class AuthEmailTemplates
{
    private const string Brand = "#7F00FF";
    private const string ProductName = "INVERTA";

    public static EmailBody Verification(string name, string verifyUrl) => Build(
        subject: "Verifikasi alamat email Anda",
        name: name,
        lead: "Terima kasih sudah mendaftar di INVERTA. Satu langkah lagi: konfirmasikan bahwa " +
              "alamat email ini benar milik Anda.",
        buttonLabel: "Verifikasi email",
        url: verifyUrl,
        footer: "Jika Anda tidak merasa mendaftar di INVERTA, abaikan saja email ini — " +
                "tidak ada akun yang dibuat tanpa verifikasi ini.");

    public static EmailBody PasswordReset(string name, string resetUrl) => Build(
        subject: "Atur ulang kata sandi Anda",
        name: name,
        lead: "Kami menerima permintaan untuk mengatur ulang kata sandi akun INVERTA Anda. " +
              "Tautan di bawah ini hanya berlaku sebentar dan hanya dapat dipakai satu kali.",
        buttonLabel: "Atur ulang kata sandi",
        url: resetUrl,
        footer: "Jika Anda tidak meminta ini, abaikan email ini. Kata sandi Anda tidak berubah " +
                "selama tautan di atas tidak dibuka.");

    public static EmailBody PasswordChanged(string name) => Build(
        subject: "Kata sandi Anda telah diubah",
        name: name,
        lead: "Kata sandi akun INVERTA Anda baru saja diubah. Anda tidak perlu melakukan apa pun " +
              "jika ini memang Anda.",
        buttonLabel: null,
        url: null,
        footer: "Jika ini BUKAN Anda, segera atur ulang kata sandi Anda dan hubungi kami. " +
                "Seseorang mungkin memiliki akses ke akun Anda.");

    /// <summary>
    /// One shell for all three. Table-based and inline-styled on purpose: email clients strip
    /// &lt;style&gt; blocks and have no flexbox, so the layout patterns used in the web app do not
    /// survive here.
    /// </summary>
    private static EmailBody Build(
        string subject, string name, string lead, string? buttonLabel, string? url, string footer)
    {
        // Every interpolated value is user-controlled or a URL. Encoding is not cosmetic: a name
        // containing "<" would otherwise break the surrounding markup, and worse is available.
        var safeName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(name) ? "Halo" : name.Trim());
        var safeUrl = url is null ? null : WebUtility.HtmlEncode(url);

        var html = new StringBuilder()
            .Append("""<!doctype html><html lang="id"><body style="margin:0;padding:24px;background:#F6F7F9;font-family:Helvetica,Arial,sans-serif;color:#1A1A1E;">""")
            .Append("""<table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr><td align="center">""")
            .Append("""<table role="presentation" width="100%" style="max-width:520px;background:#FFFFFF;border-radius:12px;padding:32px;" cellpadding="0" cellspacing="0"><tr><td>""")
            .Append($"""<p style="margin:0 0 24px;font-size:18px;font-weight:800;color:{Brand};letter-spacing:-0.02em;">{ProductName}</p>""")
            .Append($"""<p style="margin:0 0 12px;font-size:15px;">Halo {safeName},</p>""")
            .Append($"""<p style="margin:0 0 24px;font-size:15px;line-height:1.6;">{WebUtility.HtmlEncode(lead)}</p>""");

        if (buttonLabel is not null && safeUrl is not null)
        {
            html.Append($"""<p style="margin:0 0 24px;"><a href="{safeUrl}" style="display:inline-block;background:{Brand};color:#FFFFFF;text-decoration:none;font-weight:700;font-size:15px;padding:12px 22px;border-radius:8px;">{WebUtility.HtmlEncode(buttonLabel)}</a></p>""")
                // Buttons get stripped, and plenty of people simply do not trust one. The bare
                // URL is what makes the mail work when the button does not.
                .Append("""<p style="margin:0 0 24px;font-size:13px;line-height:1.6;color:#5A5A66;">Tombol tidak berfungsi? Salin dan tempel tautan ini ke peramban Anda:<br>""")
                .Append($"""<span style="word-break:break-all;color:{Brand};">{safeUrl}</span></p>""");
        }

        html.Append($"""<p style="margin:0;font-size:13px;line-height:1.6;color:#5A5A66;">{WebUtility.HtmlEncode(footer)}</p>""")
            .Append("""</td></tr></table></td></tr></table></body></html>""");

        var text = new StringBuilder()
            .Append($"Halo {name.Trim()},\n\n")
            .Append(lead).Append("\n\n");
        if (safeUrl is not null) text.Append(url).Append("\n\n");
        text.Append(footer).Append($"\n\n— {ProductName}\n");

        return new EmailBody(subject, html.ToString(), text.ToString());
    }
}
