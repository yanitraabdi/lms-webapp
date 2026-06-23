using System.Security.Cryptography;
using Academy.Application.Learning;
using Academy.Domain;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Auth;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Academy.Infrastructure.Learning;

public class CertificateService(
    AppDbContext db,
    CertificatePdf pdf,
    Academy.Application.Abstractions.INotificationSender notifier,
    IOptions<AuthOptions> authOptions) : ICertificateService
{
    // Unambiguous alphabet (no 0/O/1/I) for human-readable verification codes.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private readonly string _frontendBase = authOptions.Value.FrontendBaseUrl.TrimEnd('/');

    public async Task TryIssueForLevelAsync(Guid userId, Guid levelId, CancellationToken ct = default)
    {
        // Immutable once issued — never re-evaluate (GR-6).
        if (await db.Certificates.AnyAsync(c => c.UserId == userId && c.LevelId == levelId, ct))
            return;

        var published = await db.Modules
            .Where(m => m.Track.LevelId == levelId && m.Status == ModuleStatus.Published)
            .Select(m => m.Id)
            .ToListAsync(ct);
        if (published.Count == 0) return;

        var completed = await db.WatchProgress
            .Where(w => w.UserId == userId && w.Completed && published.Contains(w.ModuleId))
            .Select(w => w.ModuleId)
            .ToListAsync(ct);

        if (!LevelCompletion.IsComplete(published, completed.ToHashSet())) return;

        db.Certificates.Add(new Certificate
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            LevelId = levelId,
            IssuedAt = DateTimeOffset.UtcNow,
            VerificationCode = NewCode(),
            CompletedModuleIds = published.ToList(), // snapshot of the qualifying set
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Unique (user, level) race — another request issued it first. Immutable, so fine.
            return;
        }

        var levelName = await db.Levels.Where(l => l.Id == levelId).Select(l => l.Name).FirstOrDefaultAsync(ct) ?? "Level";
        await notifier.DispatchAsync(userId, Academy.Application.Engagement.NotificationCategories.Progress,
            "certificate_issued", "Sertifikat terbit!", $"Selamat! Sertifikat Level {levelName} Anda telah terbit.", ct);
    }

    public async Task<IReadOnlyList<CertificateDto>> GetMineAsync(Guid userId, CancellationToken ct = default)
        => await db.Certificates
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.IssuedAt)
            .Select(c => new CertificateDto(
                c.Id, c.LevelId,
                db.Levels.Where(l => l.Id == c.LevelId).Select(l => l.Name).FirstOrDefault() ?? "Level",
                c.IssuedAt, c.VerificationCode, c.CompletedModuleIds.Count))
            .ToListAsync(ct);

    public async Task<CertificateVerifyDto?> VerifyAsync(string code, CancellationToken ct = default)
    {
        var cert = await db.Certificates.FirstOrDefaultAsync(c => c.VerificationCode == code, ct);
        if (cert is null)
            return new CertificateVerifyDto(false, code, null, null, null, "AI Productivity Academy");

        var name = await db.Users.IgnoreQueryFilters()
            .Where(u => u.Id == cert.UserId).Select(u => u.Name).FirstOrDefaultAsync(ct);
        var levelName = await db.Levels
            .Where(l => l.Id == cert.LevelId).Select(l => l.Name).FirstOrDefaultAsync(ct);

        return new CertificateVerifyDto(true, cert.VerificationCode, name, levelName, cert.IssuedAt, "AI Productivity Academy");
    }

    public async Task<(byte[] Pdf, string FileName)?> GetPdfAsync(Guid userId, Guid certificateId, CancellationToken ct = default)
    {
        var cert = await db.Certificates.FirstOrDefaultAsync(c => c.Id == certificateId && c.UserId == userId, ct);
        if (cert is null) return null;

        var name = await db.Users.IgnoreQueryFilters()
            .Where(u => u.Id == cert.UserId).Select(u => u.Name).FirstOrDefaultAsync(ct) ?? "Peserta";
        var levelName = await db.Levels
            .Where(l => l.Id == cert.LevelId).Select(l => l.Name).FirstOrDefaultAsync(ct) ?? "Level";

        var verifyUrl = $"{_frontendBase}/verify/{cert.VerificationCode}";
        var bytes = pdf.Render(name, levelName, cert.VerificationCode, cert.IssuedAt, verifyUrl);
        return (bytes, $"sertifikat-{cert.VerificationCode}.pdf");
    }

    private static string NewCode()
    {
        Span<char> chars = stackalloc char[8];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return $"AIPA-{new string(chars)}";
    }
}
