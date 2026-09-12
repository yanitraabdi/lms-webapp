using System.Globalization;
using System.Net;
using System.Text;

namespace Academy.Infrastructure.Email;

/// <summary>Subject plus both bodies. Every message is sent multipart: some clients, and most
/// spam filters, treat an HTML-only email as a smell.</summary>
public record EmailBody(string Subject, string Html, string Text);

/// <summary>
/// The five transactional emails INVERTA actually sends, in Bahasa Indonesia: three for
/// authentication, plus the enrolment receipt and the certificate.
///
/// Pure functions with no SMTP dependency, so what a learner actually receives can be asserted in
/// a test — which is the only way to catch the failures that matter here: a link that did not make
/// it into the body, a name that broke the markup around it, a rupiah amount formatted by whatever
/// culture the container happened to boot with, or a certificate email that forgets to say the
/// score is a prediction (GR-14).
/// </summary>
public static class EmailTemplates
{
    private const string Brand = "#7F00FF";
    private const string ProductName = "INVERTA";

    /// <summary>
    /// Rupiah, formatted explicitly rather than by ambient culture.
    ///
    /// A container with ICU trimmed, or simply a different locale, turns CultureInfo-based
    /// formatting into "Rp1,500,000" or "Rp1500000" — and the one place a learner checks an
    /// amount most carefully is the receipt for money they just paid. IDR is charged as whole
    /// rupiah, so there are no minor units to show.
    /// </summary>
    private static readonly NumberFormatInfo Rupiah = new()
    {
        NumberGroupSeparator = ".",
        NumberDecimalDigits = 0,
    };

    public static string FormatIdr(decimal amount)
        => "Rp" + Math.Round(amount, MidpointRounding.AwayFromZero).ToString("N", Rupiah);

    /// <summary>
    /// Western Indonesia Time, as a fixed +7 offset.
    ///
    /// Fixed rather than TimeZoneInfo("Asia/Jakarta") because WIB has had no daylight saving since
    /// 1964, so the offset is exact — and a fixed offset needs no tzdata in the container, which
    /// is one less thing that can be absent from a slim base image.
    ///
    /// Indonesia spans WIB/WITA/WITK (+7/+8/+9). This product is Jakarta-centric and every string
    /// in it is Bahasa Indonesia, so WIB is the right default; a learner in Makassar reads the
    /// label and adds an hour. Make it per-programme config the day that is not good enough.
    /// </summary>
    private static readonly TimeSpan Wib = TimeSpan.FromHours(7);

    /// <summary>Spelled out rather than taken from CultureInfo("id-ID"), for the reason given on
    /// <see cref="Rupiah"/> — and because that call does not throw when globalization is
    /// unavailable, it quietly returns English.</summary>
    private static readonly string[] Days =
        ["Minggu", "Senin", "Selasa", "Rabu", "Kamis", "Jumat", "Sabtu"];

    private static readonly string[] Months =
        ["", "Januari", "Februari", "Maret", "April", "Mei", "Juni",
         "Juli", "Agustus", "September", "Oktober", "November", "Desember"];

    /// <summary>
    /// "Sabtu, 12 September 2026, 19.00 WIB".
    ///
    /// The API container runs UTC, so the stored instant MUST be shifted before it is printed.
    /// Sent raw it reads seven hours early, and a learner who trusts it misses the class — the
    /// worst failure this particular email has available to it.
    /// </summary>
    public static string FormatWib(DateTimeOffset instant)
    {
        var t = instant.ToOffset(Wib);
        return $"{Days[(int)t.DayOfWeek]}, {t.Day} {Months[t.Month]} {t.Year}, " +
               $"{t.Hour:00}.{t.Minute:00} WIB";
    }

    // ---------------------------------------------------------------- auth

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

    // ---------------------------------------------------------------- transactional

    /// <summary>
    /// Payment confirmation. Deliberately NOT called a faktur or kuitansi pajak: those are
    /// regulated documents with content requirements this does not meet, and claiming to be one
    /// is a tax problem rather than a copy problem.
    /// </summary>
    public static EmailBody EnrollmentReceipt(string name, string programName, decimal amountIdr, string appUrl) => Build(
        subject: $"Pembayaran diterima — {programName}",
        name: name,
        lead: "Pembayaran Anda sudah kami terima dan akses ke program sudah aktif. " +
              "Simpan email ini sebagai bukti pembayaran Anda.",
        buttonLabel: "Mulai belajar",
        url: appUrl,
        details:
        [
            ("Program", programName),
            ("Jumlah dibayar", FormatIdr(amountIdr)),
        ],
        footer: "Program ini adalah pembelian satu kali — tidak ada tagihan berulang dan tidak " +
                "ada yang perlu dibatalkan. Ada pertanyaan soal pembayaran ini? Balas email ini.");

    /// <summary>
    /// Certificate issued.
    ///
    /// GR-14 governs this message more than any other: it is the one a learner forwards to a
    /// university or an employer, so it must say in its own words that the score is an INVERTA
    /// prediction and not an official ETS result. The notice is a block of its own rather than a
    /// line in the footer, because a footer is what people skip.
    ///
    /// <paramref name="totalScore"/> is nullable and the row is omitted when it is null — printing
    /// "0" or an empty score on a certificate email is worse than not mentioning it.
    /// </summary>
    public static EmailBody Certificate(
        string name, string programName, string verificationCode, int? totalScore, string verifyUrl)
    {
        var details = new List<(string, string)> { ("Program", programName) };
        if (totalScore is int score) details.Add(("Skor prediksi", score.ToString(CultureInfo.InvariantCulture)));
        details.Add(("Kode verifikasi", verificationCode));

        return Build(
            subject: $"Sertifikat Anda sudah terbit — {programName}",
            name: name,
            lead: "Selamat! Anda telah menyelesaikan program dan sertifikat prediksi TOEFL Anda " +
                  "sudah terbit. Siapa pun dapat memeriksa keasliannya dengan kode di bawah ini.",
            buttonLabel: "Lihat sertifikat",
            url: verifyUrl,
            details: [.. details],
            notice: "Skor pada sertifikat ini adalah PREDIKSI yang dihitung oleh INVERTA, " +
                    "bukan skor TOEFL resmi dan bukan hasil tes dari ETS.",
            footer: "Sertifikat ini tidak pernah berubah. Jika Anda mengikuti tes akhir lagi, " +
                    "sertifikat baru akan diterbitkan dan yang ini tetap berlaku.");
    }

    /// <summary>
    /// H-1 reminder for a live session.
    ///
    /// Online and offline sessions are genuinely different emails and the distinction is the whole
    /// value of the message: online needs the join link in front of the learner, offline needs an
    /// address they can leave the house for. Both fields are nullable, and a session whose details
    /// are not filled in yet still has to produce something honest rather than a blank row — an
    /// admin can schedule first and add the link later, and the sweep does not wait for them.
    /// </summary>
    public static EmailBody LiveSessionReminder(
        string name, string programName, string sessionTitle,
        DateTimeOffset scheduledAt, string? joinUrl, string? location, string appUrl)
    {
        var online = !string.IsNullOrWhiteSpace(joinUrl);
        var onsite = !string.IsNullOrWhiteSpace(location);

        var details = new List<(string, string)>
        {
            ("Sesi", sessionTitle),
            ("Program", programName),
            ("Waktu", FormatWib(scheduledAt)),
        };
        if (onsite) details.Add(("Lokasi", location!));

        return Build(
            subject: $"Besok: {sessionTitle}",
            name: name,
            lead: online
                ? "Sesi live Anda berlangsung besok. Gunakan tombol di bawah ini untuk bergabung " +
                  "pada waktu yang tertera."
                : onsite
                    ? "Sesi live Anda berlangsung besok di lokasi berikut. Mohon datang beberapa " +
                      "menit lebih awal."
                    : "Sesi live Anda berlangsung besok.",
            buttonLabel: online ? "Gabung sesi" : "Buka program",
            url: online ? joinUrl : appUrl,
            details: [.. details],
            // Said only when it is true. A learner whose session has neither a link nor an address
            // an hour before it starts needs to know that is our gap, not theirs to hunt down.
            notice: online || onsite
                ? null
                : "Tautan atau lokasi sesi ini belum tersedia. Kami akan mengirimkannya sebelum " +
                  "sesi dimulai — periksa juga halaman program Anda.",
            footer: "Kehadiran pada sesi live dicatat oleh pengajar, dan sesi ini harus dihadiri " +
                    "untuk membuka sesi berikutnya. Waktu di atas menggunakan WIB.");
    }

    // ---------------------------------------------------------------- the shell

    /// <summary>
    /// One shell for all five. Table-based and inline-styled on purpose: email clients strip
    /// &lt;style&gt; blocks and have no flexbox, so the layout patterns used in the web app do not
    /// survive here.
    /// </summary>
    private static EmailBody Build(
        string subject, string name, string lead, string? buttonLabel, string? url, string footer,
        (string Label, string Value)[]? details = null, string? notice = null)
    {
        // Every interpolated value is user-controlled or a URL. Encoding is not cosmetic: a name
        // or a programme title containing "<" would otherwise break the surrounding markup, and
        // worse is available.
        var safeName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(name) ? "Halo" : name.Trim());
        var safeUrl = url is null ? null : WebUtility.HtmlEncode(url);

        var html = new StringBuilder()
            .Append("""<!doctype html><html lang="id"><body style="margin:0;padding:24px;background:#F6F7F9;font-family:Helvetica,Arial,sans-serif;color:#1A1A1E;">""")
            .Append("""<table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr><td align="center">""")
            .Append("""<table role="presentation" width="100%" style="max-width:520px;background:#FFFFFF;border-radius:12px;padding:32px;" cellpadding="0" cellspacing="0"><tr><td>""")
            .Append($"""<p style="margin:0 0 24px;font-size:18px;font-weight:800;color:{Brand};letter-spacing:-0.02em;">{ProductName}</p>""")
            .Append($"""<p style="margin:0 0 12px;font-size:15px;">Halo {safeName},</p>""")
            .Append($"""<p style="margin:0 0 24px;font-size:15px;line-height:1.6;">{WebUtility.HtmlEncode(lead)}</p>""");

        if (details is { Length: > 0 })
        {
            html.Append("""<table role="presentation" width="100%" style="margin:0 0 24px;background:#F6F7F9;border-radius:8px;" cellpadding="0" cellspacing="0">""");
            foreach (var (label, value) in details)
                html.Append("""<tr><td style="padding:10px 14px;font-size:13px;color:#5A5A66;">""")
                    .Append(WebUtility.HtmlEncode(label))
                    .Append("""</td><td align="right" style="padding:10px 14px;font-size:14px;font-weight:700;">""")
                    .Append(WebUtility.HtmlEncode(value))
                    .Append("</td></tr>");
            html.Append("</table>");
        }

        if (buttonLabel is not null && safeUrl is not null)
        {
            html.Append($"""<p style="margin:0 0 24px;"><a href="{safeUrl}" style="display:inline-block;background:{Brand};color:#FFFFFF;text-decoration:none;font-weight:700;font-size:15px;padding:12px 22px;border-radius:8px;">{WebUtility.HtmlEncode(buttonLabel)}</a></p>""")
                // Buttons get stripped, and plenty of people simply do not trust one. The bare
                // URL is what makes the mail work when the button does not.
                .Append("""<p style="margin:0 0 24px;font-size:13px;line-height:1.6;color:#5A5A66;">Tombol tidak berfungsi? Salin dan tempel tautan ini ke peramban Anda:<br>""")
                .Append($"""<span style="word-break:break-all;color:{Brand};">{safeUrl}</span></p>""");
        }

        if (notice is not null)
            html.Append("""<p style="margin:0 0 24px;padding:12px 14px;border-radius:8px;background:#FFF7E6;border:1px solid #F5D9A8;font-size:13px;line-height:1.6;">""")
                .Append(WebUtility.HtmlEncode(notice))
                .Append("</p>");

        html.Append($"""<p style="margin:0;font-size:13px;line-height:1.6;color:#5A5A66;">{WebUtility.HtmlEncode(footer)}</p>""")
            .Append("""</td></tr></table></td></tr></table></body></html>""");

        var text = new StringBuilder()
            .Append($"Halo {(string.IsNullOrWhiteSpace(name) ? "Halo" : name.Trim())},\n\n")
            .Append(lead).Append("\n\n");
        if (details is { Length: > 0 })
        {
            foreach (var (label, value) in details) text.Append($"{label}: {value}\n");
            text.Append('\n');
        }
        if (url is not null) text.Append(url).Append("\n\n");
        if (notice is not null) text.Append(notice).Append("\n\n");
        text.Append(footer).Append($"\n\n— {ProductName}\n");

        return new EmailBody(subject, html.ToString(), text.ToString());
    }
}
