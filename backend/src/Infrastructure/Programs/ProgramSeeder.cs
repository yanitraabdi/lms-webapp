using System.Text.Json;
using Academy.Domain;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Programs;

/// <summary>
/// Seeds a PLACEHOLDER INVERTA program so the build is runnable end-to-end (KAK §12, FSD Q1).
/// Idempotent. Everything here is admin-editable — the real syllabus, question bank and score
/// conversion values are owed by the business (KAK §17).
/// </summary>
public class ProgramSeeder(AppDbContext db)
{
    public const string Slug = "toefl-preparation";

    /// <summary>PLACEHOLDER price — change in admin, not in code (KAK §6.a).</summary>
    public const decimal PlaceholderPriceIdr = 1_500_000m;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Programs.AnyAsync(p => p.Slug == Slug, ct)) return;

        var program = new Domain.Entities.Program
        {
            Id = Guid.CreateVersion7(),
            Name = "INVERTA — Persiapan TOEFL",
            Slug = Slug,
            Summary = "Program persiapan TOEFL terstruktur: video, tes per sesi, kelas live, dan simulasi tes akhir.",
            Description =
                "[PLACEHOLDER] Program persiapan TOEFL yang dirancang bertahap. Setiap sesi video diikuti tes " +
                "singkat yang wajib lulus sebelum sesi berikutnya terbuka, dilanjutkan sesi live bersama pengajar, " +
                "dan ditutup dengan simulasi tes akhir berformat TOEFL ITP yang menghasilkan prediksi skor. " +
                "Silabus final akan diperbarui oleh tim akademik.",
            PriceIdr = PlaceholderPriceIdr,
            Status = ProgramStatus.Published,
            PublishedAt = DateTimeOffset.UtcNow,
        };
        db.Programs.Add(program);

        var order = 1;
        foreach (var (title, minutes) in PlaceholderLessons)
        {
            db.ProgramSessions.Add(new ProgramSession
            {
                Id = Guid.CreateVersion7(),
                ProgramId = program.Id,
                OrderIndex = order++,
                Type = SessionType.Video,
                Title = title,
                Description = "[PLACEHOLDER] Deskripsi materi akan dilengkapi tim akademik.",
                DurationSeconds = minutes * 60,
                ProviderAssetId = "sample",             // dev video provider asset
                AssessmentId = null,                    // gating test authored in admin (M3)
            });
        }

        db.ProgramSessions.Add(new ProgramSession
        {
            Id = Guid.CreateVersion7(),
            ProgramId = program.Id,
            OrderIndex = order++,
            Type = SessionType.Live,
            Title = "Sesi Live: Tanya Jawab & Strategi Tes",
            Description = "[PLACEHOLDER] Sesi live bersama pengajar. Jadwal akan dikonfirmasi.",
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(14),
            LiveMode = LiveMode.Zoom,
            JoinUrl = "https://example.com/placeholder-live-session",
        });

        var final = new Assessment
        {
            Id = Guid.CreateVersion7(),
            Kind = AssessmentKind.Final,
            Title = "Simulasi TOEFL ITP — Tes Akhir",
            Config = JsonSerializer.Serialize(new
            {
                sections = new object[]
                {
                    new { section = nameof(QuestionSection.Listening), questions = ToeflScoring.ListeningQuestions, minutes = ToeflScoring.ListeningMinutes },
                    new { section = nameof(QuestionSection.Structure), questions = ToeflScoring.StructureQuestions, minutes = ToeflScoring.StructureMinutes },
                    new { section = nameof(QuestionSection.Reading),   questions = ToeflScoring.ReadingQuestions,   minutes = ToeflScoring.ReadingMinutes },
                },
                retakeCap = 1,             // KAK §9.7.2 — one attempt; admin may grant a retake
                proctoringEnabled = true,  // KAK §9.8
                audioPlayLimit = 1,        // KAK §9.7.2
            }),
        };
        db.Assessments.Add(final);

        db.ProgramSessions.Add(new ProgramSession
        {
            Id = Guid.CreateVersion7(),
            ProgramId = program.Id,
            OrderIndex = order,
            Type = SessionType.FinalAssessment,
            Title = "Tes Akhir: Simulasi TOEFL ITP",
            Description = "Tiga bagian berwaktu (Listening, Structure, Reading) dengan pengawasan lunak.",
            AssessmentId = final.Id,
        });

        SeedPlaceholderScoreBands(program.Id);

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// PLACEHOLDER raw→scaled conversion (KAK §9.9.5). Linear interpolation across each section's
    /// scaled band — a structurally valid stand-in, NOT a pedagogically validated mapping.
    /// **The business must replace these values before launch.**
    /// </summary>
    private void SeedPlaceholderScoreBands(Guid programId)
    {
        AddBands(programId, QuestionSection.Listening, ToeflScoring.ListeningQuestions, ToeflScoring.ListeningScaledMax);
        AddBands(programId, QuestionSection.Structure, ToeflScoring.StructureQuestions, ToeflScoring.StructureScaledMax);
        AddBands(programId, QuestionSection.Reading, ToeflScoring.ReadingQuestions, ToeflScoring.ReadingScaledMax);
    }

    private void AddBands(Guid programId, QuestionSection section, int maxRaw, int scaledMax)
    {
        // One row per raw score so every possible raw value maps — an unmapped score must fail
        // loudly (KAK §9.9.4), so the placeholder deliberately leaves no gaps.
        for (var raw = 0; raw <= maxRaw; raw++)
        {
            var scaled = ToeflScoring.ScaledMin
                       + (int)Math.Round((decimal)raw / maxRaw * (scaledMax - ToeflScoring.ScaledMin),
                                         MidpointRounding.AwayFromZero);
            db.ScoreBandMappings.Add(new ScoreBandMapping
            {
                Id = Guid.CreateVersion7(),
                ProgramId = programId,
                Section = section,
                MinRaw = raw,
                MaxRaw = raw,
                ScaledScore = scaled,
                PredictedBand = null,      // descriptive label is set on the total, not per section
            });
        }
    }

    /// <summary>[PLACEHOLDER] syllabus — structure is admin-configurable (FSD Q1).</summary>
    private static readonly (string Title, int Minutes)[] PlaceholderLessons =
    [
        ("Sesi 1: Mengenal Format & Strategi TOEFL", 15),
        ("Sesi 2: Listening — Short Conversations", 15),
        ("Sesi 3: Listening — Longer Talks", 15),
        ("Sesi 4: Structure — Sentence Completion", 15),
        ("Sesi 5: Written Expression — Error Identification", 15),
        ("Sesi 6: Reading — Skimming, Scanning & Vocabulary", 15),
    ];
}
