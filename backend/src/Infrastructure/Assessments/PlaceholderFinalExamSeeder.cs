using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Academy.Application.Abstractions;
using Academy.Application.Assessments;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Academy.Infrastructure.Programs;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Assessments;

/// <summary>
/// Fills the seeded final assessment with 140 PLACEHOLDER items in the TOEFL ITP shape
/// (50 Listening / 40 Structure / 50 Reading).
///
/// Why this exists: <see cref="ProgramSeeder"/> creates the final assessment with the right config
/// but no questions, and readiness enforces the real ITP layout — so until the bank held 140 items,
/// the exam, its server-authoritative timer, proctoring, ITP scoring, certificate issue and the
/// public verify page could not be exercised end to end by anyone, including QA. This is
/// scaffolding for that chain, not content.
///
/// The items come from an embedded workbook and are loaded through the REAL importer
/// (<see cref="IQuestionImportService"/>), so there is no second content path to keep in step and
/// the importer itself gets exercised on a full-size sheet. Every prompt is prefixed "[CONTOH]" so
/// a placeholder can never be mistaken for something sold to a learner.
///
/// Replacing it is the normal admin flow: keep the ids (PH-L01, PH-S01, PH-R01 …), change the
/// prompts and answers, upload at /admin/questions/import. The importer upserts on id, so real
/// items overwrite these in place — the attachments and ordering below survive untouched.
///
/// Idempotent, and gated by SeedSampleData like every other seeder.
/// </summary>
public class PlaceholderFinalExamSeeder(
    AppDbContext db, IQuestionImportService import, IObjectStorage storage)
{
    private const string Workbook = "Academy.Infrastructure.Assets.PlaceholderFinalExam.xlsx";
    private const string Marker = "[CONTOH]";

    /// <summary>One recording for the whole Listening section. A per-question clip would need 50
    /// files to say the same thing, and the readiness check accepts a section recording alone.</summary>
    private const string ListeningAudioKey = "audio/placeholder-listening-section.m4a";

    /// <summary>The section order a learner sits them in, and the order they are attached in.</summary>
    private static readonly QuestionSection[] ItpOrder =
        [QuestionSection.Listening, QuestionSection.Structure, QuestionSection.Reading];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var assessment = await db.Assessments
            .FirstOrDefaultAsync(a => a.Kind == AssessmentKind.Final, ct);
        if (assessment is null) return;               // ProgramSeeder has not run

        // Idempotent, and deliberately not "any questions at all": a half-attached exam from an
        // interrupted run should be completed, not left broken.
        if (await db.AssessmentQuestions.CountAsync(aq => aq.AssessmentId == assessment.Id, ct) == 140)
            return;

        await ImportQuestionsAsync(ct);
        await WriteListeningAudioAsync(ct);
        await AttachAsync(assessment, ct);
        EnsureSectionAudio(assessment);

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Loads the embedded workbook through the real importer, which upserts on the
    /// author-assigned id — so re-running never duplicates the bank.</summary>
    private async Task ImportQuestionsAsync(CancellationToken ct)
    {
        await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(Workbook)
            ?? throw new InvalidOperationException($"Embedded workbook not found: {Workbook}");

        // The seeder is the actor; audit_logs.actor_user_id is nullable-by-FK to a real user, so
        // the import is attributed to the dev admin when one exists.
        var actor = await db.Users
            .Where(u => u.Role == UserRole.Admin || u.Role == UserRole.SuperAdmin)
            .OrderBy(u => u.CreatedAt)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(ct);

        // Loud, not silent. An earlier draft returned quietly here, and the result was a seeder
        // that appeared to run and attached nothing — the kind of no-op that costs an afternoon.
        if (actor is not Guid actorId)
            throw new InvalidOperationException(
                "Placeholder exam needs an admin to attribute the import to. Run DevAdminSeeder first.");

        var result = await import.CommitAsync(actorId, stream, ct);
        if (!result.Committed)
            throw new InvalidOperationException(
                "Placeholder exam workbook failed validation: " +
                string.Join("; ", result.Errors.Select(e => $"row {e.Row} {e.Column}: {e.Message}")));
    }

    /// <summary>Reuses the sample clip already shipped for the gating test — placeholder speech is
    /// placeholder speech, and one embedded asset is enough to make the player real.</summary>
    private async Task WriteListeningAudioAsync(CancellationToken ct)
    {
        const string asset = "Academy.Infrastructure.Assets.SampleListening.L1.m4a";
        await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(asset)
            ?? throw new InvalidOperationException($"Embedded audio not found: {asset}");
        await storage.PutAsync(ListeningAudioKey, stream, "audio/mp4", ct);
    }

    /// <summary>
    /// Attaches the imported questions in sitting order — Listening, then Structure, then Reading —
    /// because <c>OrderIndex</c> is what the exam plays back, and a shuffled ITP paper is not one.
    /// </summary>
    private async Task AttachAsync(Assessment assessment, CancellationToken ct)
    {
        var questions = await db.Questions
            .Where(q => q.ExternalId != null && q.ExternalId.StartsWith("PH-"))
            .Select(q => new { q.Id, q.Section, q.ExternalId })
            .ToListAsync(ct);

        var attached = await db.AssessmentQuestions
            .Where(aq => aq.AssessmentId == assessment.Id)
            .Select(aq => aq.QuestionId)
            .ToListAsync(ct);
        var already = attached.ToHashSet();

        var order = attached.Count;
        foreach (var section in ItpOrder)
        {
            foreach (var q in questions
                         .Where(q => q.Section == section)
                         .OrderBy(q => q.ExternalId, StringComparer.Ordinal))
            {
                if (!already.Add(q.Id)) continue;
                db.AssessmentQuestions.Add(new AssessmentQuestion
                {
                    Id = Guid.CreateVersion7(),
                    AssessmentId = assessment.Id,
                    QuestionId = q.Id,
                    OrderIndex = order++,
                });
            }
        }
    }

    /// <summary>
    /// Points the Listening section at the recording. Edited as a JSON node rather than
    /// deserialise-and-rewrite, so nothing else in the config bag is disturbed — the same reason
    /// <see cref="AssessmentConfigMerge"/> exists.
    /// </summary>
    private static void EnsureSectionAudio(Assessment assessment)
    {
        var config = JsonNode.Parse(assessment.Config) as JsonObject;
        if (config?["sections"] is not JsonArray sections) return;

        foreach (var node in sections)
        {
            if (node is not JsonObject s) continue;
            if ((string?)s["section"] != nameof(QuestionSection.Listening)) continue;
            if (!string.IsNullOrWhiteSpace((string?)s["audioRef"])) return;   // admin set one
            s["audioRef"] = ListeningAudioKey;
        }

        assessment.Config = config.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    /// <summary>Exposed so the tests can assert the marker without hard-coding it twice.</summary>
    public static string PlaceholderMarker => Marker;
}
