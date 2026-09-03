using System.Globalization;
using System.Reflection;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace Academy.Infrastructure.Learning;

/// <summary>Renders an immutable certificate to a PDF (PDFsharp, MIT). Fonts are embedded
/// so it works on Linux/Docker without system fonts. PDF is a deterministic render of the
/// certificate row — generated on demand, not stored.</summary>
public class CertificatePdf
{
    static CertificatePdf()
    {
        GlobalFontSettings.FontResolver ??= new EmbeddedFontResolver();
    }

    private static readonly CultureInfo Id = new("id-ID");

    public byte[] Render(string recipientName, string levelName, string verificationCode, DateTimeOffset issuedAt, string verifyUrl)
    {
        using var doc = new PdfDocument();
        var page = doc.AddPage();
        page.Size = PageSize.A4;
        page.Orientation = PageOrientation.Landscape;
        using var gfx = XGraphics.FromPdfPage(page);

        double w = page.Width.Point, h = page.Height.Point;
        var ink = XColor.FromArgb(0x11, 0x25, 0x3F);
        var muted = XColor.FromArgb(0x51, 0x60, 0x7A);
        // Brand violet #7F00FF. The PDF renders on demand rather than being stored, so an
        // already-issued certificate re-renders in the new colour — deliberate and approved.
        // Immutability (GR-6) is untouched: score, section breakdown, recipient and verification
        // code are unchanged; only the ink differs.
        var primary = XColor.FromArgb(0x7F, 0x00, 0xFF);
        var gold = XColor.FromArgb(0xC2, 0x71, 0x0C);

        gfx.DrawRectangle(XBrushes.White, 0, 0, w, h);
        gfx.DrawRectangle(new XPen(XColor.FromArgb(0xDC, 0xE5, 0xEF), 2), 22, 22, w - 44, h - 44);
        gfx.DrawRectangle(new XPen(gold, 3), 32, 32, w - 64, h - 64);

        var brand = new XFont("DejaVu", 13, XFontStyleEx.Bold);
        var title = new XFont("DejaVu", 30, XFontStyleEx.Bold);
        var body = new XFont("DejaVu", 15);
        var name = new XFont("DejaVu", 38, XFontStyleEx.Bold);
        var levelFont = new XFont("DejaVu", 20, XFontStyleEx.Bold);
        var small = new XFont("DejaVu", 10);
        var codeFont = new XFont("DejaVu", 13, XFontStyleEx.Bold);

        void Center(string text, XFont font, XColor color, double y) =>
            gfx.DrawString(text, font, new XSolidBrush(color), new XRect(0, y, w, 0), XStringFormats.TopCenter);

        Center("AI PRODUCTIVITY ACADEMY", brand, primary, 64);
        Center("Sertifikat Kelulusan", title, ink, 92);
        Center("Dengan ini menyatakan bahwa", body, muted, 158);
        Center(recipientName, name, ink, 182);
        Center("telah berhasil menyelesaikan", body, muted, 246);
        Center(levelName, levelFont, primary, 270);
        Center($"Tanggal terbit: {issuedAt.ToString("dd MMMM yyyy", Id)}", small, muted, 322);

        gfx.DrawString("Kode verifikasi", small, new XSolidBrush(muted), 60, h - 92);
        gfx.DrawString(verificationCode, codeFont, new XSolidBrush(ink), 60, h - 74);
        gfx.DrawString($"Verifikasi: {verifyUrl}", small, new XSolidBrush(muted), 60, h - 54);

        using var ms = new MemoryStream();
        doc.Save(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// INVERTA score-based certificate (KAK §9.10). Shows per-section scaled scores and the
    /// predicted total, and carries the mandatory "prediction, not an official ETS score"
    /// disclaimer on the face of the document (GR-14).
    /// </summary>
    public byte[] RenderProgramCertificate(
        string recipientName, string programName, string verificationCode, DateTimeOffset issuedAt,
        int? totalScore, string? predictedBand, IReadOnlyDictionary<string, int> scaledScores,
        string verifyUrl, string disclaimer)
    {
        using var doc = new PdfDocument();
        var page = doc.AddPage();
        page.Size = PageSize.A4;
        page.Orientation = PageOrientation.Landscape;
        using var gfx = XGraphics.FromPdfPage(page);

        double w = page.Width.Point, h = page.Height.Point;
        var ink = XColor.FromArgb(0x11, 0x25, 0x3F);
        var muted = XColor.FromArgb(0x51, 0x60, 0x7A);
        // Brand violet #7F00FF. The PDF renders on demand rather than being stored, so an
        // already-issued certificate re-renders in the new colour — deliberate and approved.
        // Immutability (GR-6) is untouched: score, section breakdown, recipient and verification
        // code are unchanged; only the ink differs.
        var primary = XColor.FromArgb(0x7F, 0x00, 0xFF);
        var gold = XColor.FromArgb(0xC2, 0x71, 0x0C);

        gfx.DrawRectangle(XBrushes.White, 0, 0, w, h);
        gfx.DrawRectangle(new XPen(XColor.FromArgb(0xDC, 0xE5, 0xEF), 2), 22, 22, w - 44, h - 44);
        gfx.DrawRectangle(new XPen(gold, 3), 32, 32, w - 64, h - 64);

        var brand = new XFont("DejaVu", 13, XFontStyleEx.Bold);
        var title = new XFont("DejaVu", 26, XFontStyleEx.Bold);
        var body = new XFont("DejaVu", 14);
        var nameFont = new XFont("DejaVu", 34, XFontStyleEx.Bold);
        var programFont = new XFont("DejaVu", 17, XFontStyleEx.Bold);
        var scoreFont = new XFont("DejaVu", 44, XFontStyleEx.Bold);
        var sectionFont = new XFont("DejaVu", 11);
        var small = new XFont("DejaVu", 9);
        var codeFont = new XFont("DejaVu", 12, XFontStyleEx.Bold);

        void Center(string text, XFont font, XColor color, double y) =>
            gfx.DrawString(text, font, new XSolidBrush(color), new XRect(0, y, w, 0), XStringFormats.TopCenter);

        Center("INVERTA", brand, primary, 56);
        Center("Sertifikat Prediksi TOEFL", title, ink, 80);
        Center("Diberikan kepada", body, muted, 138);
        Center(recipientName, nameFont, ink, 160);
        Center("atas penyelesaian program", body, muted, 216);
        Center(programName, programFont, primary, 236);

        // Predicted total — the headline number, explicitly labelled as a prediction.
        if (totalScore is int total)
        {
            Center("SKOR PREDIKSI TOEFL", small, muted, 276);
            Center(total.ToString(), scoreFont, gold, 290);
            if (!string.IsNullOrWhiteSpace(predictedBand))
                Center(predictedBand!, sectionFont, muted, 342);
        }

        // Per-section scaled scores, laid out evenly.
        if (scaledScores.Count > 0)
        {
            var y = totalScore is null ? 292.0 : 364.0;
            var order = new[] { "Listening", "Structure", "Reading" };
            var shown = order.Where(scaledScores.ContainsKey).ToList();
            var slot = w / (shown.Count + 1);
            for (var i = 0; i < shown.Count; i++)
            {
                var x = slot * (i + 1);
                gfx.DrawString(shown[i], sectionFont, new XSolidBrush(muted),
                    new XRect(x - 60, y, 120, 0), XStringFormats.TopCenter);
                gfx.DrawString(scaledScores[shown[i]].ToString(), programFont, new XSolidBrush(ink),
                    new XRect(x - 60, y + 14, 120, 0), XStringFormats.TopCenter);
            }
        }

        Center($"Tanggal terbit: {issuedAt.ToString("dd MMMM yyyy", Id)}", small, muted, h - 132);

        // Mandatory disclaimer, wrapped across the footer (GR-14).
        var tf = new XTextFormatter(gfx) { Alignment = XParagraphAlignment.Center };
        tf.DrawString(disclaimer, small, new XSolidBrush(muted),
            new XRect(70, h - 112, w - 140, 40), XStringFormats.TopLeft);

        gfx.DrawString("Kode verifikasi", small, new XSolidBrush(muted), 60, h - 62);
        gfx.DrawString(verificationCode, codeFont, new XSolidBrush(ink), 60, h - 46);
        gfx.DrawString($"Verifikasi: {verifyUrl}", small, new XSolidBrush(muted), 60, h - 30);

        using var ms = new MemoryStream();
        doc.Save(ms);
        return ms.ToArray();
    }
}

/// <summary>Serves the embedded DejaVu fonts to PDFsharp (no system-font dependency).</summary>
internal sealed class EmbeddedFontResolver : IFontResolver
{
    private const string Regular = "Academy.Infrastructure.Assets.DejaVuSans.ttf";
    private const string Bold = "Academy.Infrastructure.Assets.DejaVuSans-Bold.ttf";

    public byte[] GetFont(string faceName)
    {
        var resource = faceName.Contains("Bold", StringComparison.OrdinalIgnoreCase) ? Bold : Regular;
        using var s = typeof(EmbeddedFontResolver).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded font not found: {resource}");
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        => new(isBold ? "DejaVuSans-Bold" : "DejaVuSans");
}
