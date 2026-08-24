using System.Security.Cryptography;
using System.Text.Json;
using Academy.Application.Abstractions;
using Academy.Application.Assessments;
using Academy.Domain.Entities;
using Academy.Infrastructure.Auth;
using Academy.Infrastructure.Learning;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Academy.Infrastructure.Assessments;

/// <summary>
/// Score-based certificate issuance (KAK §9.10). Immutable and anchored to an attempt: a granted
/// retake inserts a NEW certificate and never mutates an existing one (GR-6). Every surface states
/// plainly that the score is an INVERTA prediction, not an official ETS result (GR-14).
/// </summary>
public class ProgramCertificateService(
    AppDbContext db,
    IScoreConversionService conversion,
    CertificatePdf pdf,
    IEmailSender email,
    IOptions<AuthOptions> authOptions,
    ILogger<ProgramCertificateService> logger) : IProgramCertificateService
{
    public const string Disclaimer =
        "Skor ini adalah PREDIKSI yang diterbitkan INVERTA berdasarkan simulasi internal. " +
        "Ini BUKAN skor TOEFL resmi dan tidak diterbitkan oleh ETS. " +
        "TOEFL adalah merek dagang terdaftar milik Educational Testing Service (ETS).";

    private string FrontendBase => authOptions.Value.FrontendBaseUrl.TrimEnd('/');

    public async Task<ProgramCertificateDto?> TryIssueForAttemptAsync(
        Guid userId, Guid attemptId, CancellationToken ct = default)
    {
        var attempt = await db.Attempts.FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, ct);
        if (attempt is null || attempt.SubmittedAt is null) return null;

        // Immutable: one certificate per attempt. A retake is a different attempt ⇒ a new row.
        var existing = await db.Certificates.FirstOrDefaultAsync(c => c.AttemptId == attemptId, ct);
        if (existing is not null) return await MapAsync(existing, ct);

        var session = await db.ProgramSessions
            .Where(s => s.AssessmentId == attempt.AssessmentId)
            .Select(s => new { s.ProgramId, ProgramName = s.Program.Name })
            .FirstOrDefaultAsync(ct);
        if (session is null) return null;                 // not a program-attached assessment

        var raw = ParseInts(attempt.SectionScores);

        ScoreConversionResult conversionResult;
        try
        {
            conversionResult = await conversion.ConvertAsync(session.ProgramId, raw, ct);
        }
        catch (AssessmentException ex)
        {
            // FAIL LOUDLY (KAK §9.9.4): no certificate, explicit admin-visible error, and the
            // learner's submitted attempt is preserved for re-issue once the table is fixed.
            logger.LogError(
                "Certificate NOT issued for attempt {AttemptId}: score conversion failed — {Reason}",
                attemptId, ex.Message);
            return null;
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

        var certificate = new Certificate
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            LevelId = null,                               // dormant catalog field
            ProgramId = session.ProgramId,
            AttemptId = attemptId,
            IssuedAt = DateTimeOffset.UtcNow,
            VerificationCode = NewCode(),
            SectionScores = JsonSerializer.Serialize(conversionResult.RawScores),
            ScaledScores = JsonSerializer.Serialize(conversionResult.ScaledScores),
            TotalScaledScore = conversionResult.Total,
            PredictedBand = conversionResult.PredictedBand,
        };
        db.Certificates.Add(certificate);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Concurrent finalize raced us; the other one won. Certificates are immutable either way.
            var raced = await db.Certificates.FirstOrDefaultAsync(c => c.AttemptId == attemptId, ct);
            return raced is null ? null : await MapAsync(raced, ct);
        }

        if (user is not null)
            await email.SendCertificateAsync(
                user.Email, user.Name, session.ProgramName, certificate.VerificationCode,
                conversionResult.Total, $"{FrontendBase}/verify/{certificate.VerificationCode}", ct);

        return await MapAsync(certificate, ct);
    }

    public async Task<IReadOnlyList<ProgramCertificateDto>> GetMineAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await db.Certificates
            .Where(c => c.UserId == userId && c.ProgramId != null)
            .OrderByDescending(c => c.IssuedAt)
            .Select(c => new
            {
                c.Id, c.ProgramId, ProgramName = c.ProgramId == null ? null : c.Program!.Name,
                c.IssuedAt, c.VerificationCode, c.TotalScaledScore, c.PredictedBand, c.SectionScores,
            })
            .ToListAsync(ct);

        return rows.Select(c => new ProgramCertificateDto(
            c.Id, c.ProgramId, c.ProgramName ?? "Program", c.IssuedAt, c.VerificationCode,
            c.TotalScaledScore, c.PredictedBand, ParseInts(c.SectionScores))).ToList();
    }

    public async Task<CertificateVerificationDto> VerifyAsync(string code, CancellationToken ct = default)
    {
        var cert = await db.Certificates
            .Where(c => c.VerificationCode == code)
            .Select(c => new
            {
                c.Id, c.UserId, c.IssuedAt, c.VerificationCode, c.TotalScaledScore,
                c.PredictedBand, c.SectionScores, c.ProgramId,
                ProgramName = c.ProgramId == null ? null : c.Program!.Name,
            })
            .FirstOrDefaultAsync(ct);

        // An unknown code is a valid-false answer, never an error page (KAK §9.10 R6).
        if (cert is null)
            return new CertificateVerificationDto(false, null, null, null, null, null, null, null, Disclaimer);

        var name = await db.Users.IgnoreQueryFilters()
            .Where(u => u.Id == cert.UserId).Select(u => u.Name).FirstOrDefaultAsync(ct);

        return new CertificateVerificationDto(
            true, cert.VerificationCode, name ?? "Peserta", cert.ProgramName ?? "Program",
            cert.IssuedAt, cert.TotalScaledScore, cert.PredictedBand,
            ParseInts(cert.SectionScores), Disclaimer);
    }

    public async Task<(byte[] Pdf, string FileName)?> GetPdfAsync(
        Guid userId, Guid certificateId, CancellationToken ct = default)
    {
        var cert = await db.Certificates
            .FirstOrDefaultAsync(c => c.Id == certificateId && c.UserId == userId, ct);
        if (cert is null) return null;

        var name = await db.Users.Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync(ct) ?? "Peserta";
        var programName = cert.ProgramId is null
            ? "Program"
            : await db.Programs.Where(p => p.Id == cert.ProgramId).Select(p => p.Name).FirstAsync(ct);

        var bytes = pdf.RenderProgramCertificate(
            name, programName, cert.VerificationCode, cert.IssuedAt,
            cert.TotalScaledScore, cert.PredictedBand,
            ParseInts(cert.ScaledScores ?? "{}"),
            $"{FrontendBase}/verify/{cert.VerificationCode}",
            Disclaimer);

        return (bytes, $"sertifikat-{cert.VerificationCode}.pdf");
    }

    private async Task<ProgramCertificateDto> MapAsync(Certificate c, CancellationToken ct)
    {
        var programName = c.ProgramId is null
            ? "Program"
            : await db.Programs.Where(p => p.Id == c.ProgramId).Select(p => p.Name).FirstAsync(ct);
        return new ProgramCertificateDto(
            c.Id, c.ProgramId, programName, c.IssuedAt, c.VerificationCode,
            c.TotalScaledScore, c.PredictedBand, ParseInts(c.SectionScores ?? "{}"));
    }

    /// <summary>INV-XXXXXXXX from an unambiguous alphabet (no 0/O, 1/I) — KAK §9.10 R5.</summary>
    private static string NewCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var chars = new char[8];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return $"INV-{new string(chars)}";
    }

    private static Dictionary<string, int> ParseInts(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? []; }
        catch { return []; }
    }
}
