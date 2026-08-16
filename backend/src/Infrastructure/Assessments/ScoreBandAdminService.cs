using System.Text.Json;
using Academy.Application.Assessments;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Assessments;

/// <summary>
/// Maintains the per-section raw→scaled conversion table (KAK §9.9.2). Engineering owns this
/// mechanism; the business owns the values. Replacing the table is deliberately wholesale so a
/// partial edit can never leave gaps that would fail issuance later.
/// </summary>
public class ScoreBandAdminService(AppDbContext db) : IScoreBandAdminService
{
    public async Task<IReadOnlyList<ScoreBandDto>> ListAsync(Guid programId, CancellationToken ct = default)
        => await db.ScoreBandMappings
            .Where(b => b.ProgramId == programId)
            .OrderBy(b => b.Section).ThenBy(b => b.MinRaw)
            .Select(b => new ScoreBandDto(
                b.Id, b.Section.ToString(), b.MinRaw, b.MaxRaw, b.ScaledScore, b.PredictedBand))
            .ToListAsync(ct);

    public async Task ReplaceAsync(
        Guid actor, Guid programId, UpsertScoreBandsRequest req, CancellationToken ct = default)
    {
        if (!await db.Programs.AnyAsync(p => p.Id == programId, ct))
            throw new AssessmentException("Program tidak ditemukan.", 404);

        foreach (var b in req.Bands)
        {
            if (!Enum.TryParse<QuestionSection>(b.Section, true, out _))
                throw new AssessmentException($"Bagian '{b.Section}' tidak dikenal.");
            if (b.MinRaw > b.MaxRaw)
                throw new AssessmentException($"Rentang {b.MinRaw}–{b.MaxRaw} tidak valid.");
        }

        // Overlapping ranges would make conversion ambiguous — reject rather than pick one.
        foreach (var group in req.Bands.GroupBy(b => b.Section))
        {
            var ordered = group.OrderBy(b => b.MinRaw).ToList();
            for (var i = 1; i < ordered.Count; i++)
                if (ordered[i].MinRaw <= ordered[i - 1].MaxRaw)
                    throw new AssessmentException(
                        $"Rentang skor bagian {group.Key} tumpang tindih di sekitar {ordered[i].MinRaw}.");
        }

        var existing = await db.ScoreBandMappings.Where(b => b.ProgramId == programId).ToListAsync(ct);
        db.ScoreBandMappings.RemoveRange(existing);

        foreach (var b in req.Bands)
            db.ScoreBandMappings.Add(new ScoreBandMapping
            {
                Id = Guid.CreateVersion7(),
                ProgramId = programId,
                Section = Enum.Parse<QuestionSection>(b.Section, true),
                MinRaw = b.MinRaw,
                MaxRaw = b.MaxRaw,
                ScaledScore = b.ScaledScore,
                PredictedBand = string.IsNullOrWhiteSpace(b.PredictedBand) ? null : b.PredictedBand.Trim(),
            });

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.CreateVersion7(),
            ActorUserId = actor,
            Action = "score_bands_replaced",
            Target = programId.ToString(),
            Metadata = JsonSerializer.Serialize(new { count = req.Bands.Count }),
        });
        await db.SaveChangesAsync(ct);
    }
}
