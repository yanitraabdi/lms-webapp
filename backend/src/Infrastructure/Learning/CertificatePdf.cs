using System.Globalization;
using System.Reflection;
using PdfSharp;
using PdfSharp.Drawing;
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
        var primary = XColor.FromArgb(0x00, 0x50, 0xE6);
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
