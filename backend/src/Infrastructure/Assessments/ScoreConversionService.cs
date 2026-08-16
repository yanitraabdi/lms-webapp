using Academy.Application.Assessments;
using Academy.Domain;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Assessments;

/// <summary>
/// Raw → scaled → predicted TOEFL band (KAK §9.9), following the ITP convention:
/// each section converts via the admin-maintained table, then
/// <c>Total = ROUND((L + S + R) × 10 / 3)</c>.
///
/// Engineering owns this mechanism; the BUSINESS owns the conversion values. An unmapped raw score
/// must FAIL LOUDLY (§9.9.4) — never a blank, zero, or guessed band, and no certificate.
/// </summary>
public class ScoreConversionService(AppDbContext db) : IScoreConversionService
{
    /// <summary>Sections that contribute to the ITP total, in the published order.</summary>
    private static readonly QuestionSection[] ItpSections =
        [QuestionSection.Listening, QuestionSection.Structure, QuestionSection.Reading];

    public async Task<ScoreConversionResult> ConvertAsync(
        Guid programId, IReadOnlyDictionary<string, int> rawScores, CancellationToken ct = default)
    {
        var bands = await db.ScoreBandMappings
            .Where(b => b.ProgramId == programId)
            .Select(b => new { b.Section, b.MinRaw, b.MaxRaw, b.ScaledScore, b.PredictedBand })
            .ToListAsync(ct);

        if (bands.Count == 0)
            throw new AssessmentException(
                "Tabel konversi skor belum dikonfigurasi untuk program ini. " +
                "Hubungi administrator — sertifikat tidak dapat diterbitkan.", 409);

        var scaled = new Dictionary<string, int>();

        foreach (var section in ItpSections)
        {
            var name = section.ToString();
            if (!rawScores.TryGetValue(name, out var raw))
                throw new AssessmentException(
                    $"Skor mentah untuk bagian {name} tidak ditemukan. Sertifikat tidak dapat diterbitkan.", 409);

            var match = bands.FirstOrDefault(b => b.Section == section && raw >= b.MinRaw && raw <= b.MaxRaw)
                ?? throw new AssessmentException(
                    $"Skor mentah {raw} pada bagian {name} tidak ada dalam tabel konversi. " +
                    "Sertifikat tidak dapat diterbitkan sampai tabel dilengkapi.", 409);

            scaled[name] = match.ScaledScore;
        }

        var total = ToeflScoring.Total(
            scaled[nameof(QuestionSection.Listening)],
            scaled[nameof(QuestionSection.Structure)],
            scaled[nameof(QuestionSection.Reading)]);

        if (!ToeflScoring.IsValidTotal(total))
            throw new AssessmentException(
                $"Total konversi ({total}) di luar rentang sah {ToeflScoring.TotalMin}–{ToeflScoring.TotalMax}. " +
                "Periksa tabel konversi.", 409);

        // Optional descriptive label: the band row covering the TOTAL, if the admin defined one.
        var label = bands
            .Where(b => b.PredictedBand != null && total >= b.MinRaw && total <= b.MaxRaw)
            .Select(b => b.PredictedBand)
            .FirstOrDefault();

        return new ScoreConversionResult(rawScores, scaled, total, label);
    }
}
