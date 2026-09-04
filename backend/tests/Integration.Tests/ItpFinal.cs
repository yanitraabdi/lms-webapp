using System.Text.Json;
using Academy.Domain;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Application.Abstractions;
using Academy.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>
/// Builds a REAL TOEFL ITP final assessment (50/40/50) for fixtures that need a publishable
/// program. Readiness now checks the declared section layout against
/// <see cref="ToeflScoring"/>, so a toy 1-question-per-section test is no longer publishable —
/// which is the point. 140 questions through the admin API would be 140 HTTP round trips per
/// fixture, so they go in through the DbContext instead; the authoring endpoints themselves are
/// covered by AdminCrudTests.
/// </summary>
internal static class ItpFinal
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The per-question clip every seeded Listening question points at.</summary>
    public const string QuestionAudioRef = "audio/clip.mp3";

    /// <summary>A few bytes are enough — readiness only asks whether the object exists.</summary>
    public static Task StoreAsync(IObjectStorage storage, string key) =>
        storage.PutAsync(key, new MemoryStream([0x49, 0x44, 0x33]), "audio/mpeg");

    internal sealed record Seeded(
        Guid AssessmentId,
        IReadOnlyDictionary<QuestionSection, List<(Guid Id, int Correct)>> Key);

    /// <summary>The ITP section layout as an anonymous object, for tests that POST a config.</summary>
    public static object[] SectionConfig(string? listeningAudioRef = null) =>
    [
        new { section = nameof(QuestionSection.Listening), questions = ToeflScoring.ListeningQuestions, minutes = ToeflScoring.ListeningMinutes, audioRef = listeningAudioRef },
        new { section = nameof(QuestionSection.Structure), questions = ToeflScoring.StructureQuestions, minutes = ToeflScoring.StructureMinutes },
        new { section = nameof(QuestionSection.Reading),   questions = ToeflScoring.ReadingQuestions,   minutes = ToeflScoring.ReadingMinutes },
    ];

    public static async Task<Seeded> SeedAsync(
        AuthApiFactory factory,
        string title = "Simulasi TOEFL ITP",
        int? retakeCap = 1,
        int? audioPlayLimit = null,
        string? sectionAudioRef = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Readiness now resolves every listening AudioRef against object storage, so the fixture
        // has to put the referenced objects there — a ref alone no longer makes a program ready.
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        await StoreAsync(storage, QuestionAudioRef);
        if (!string.IsNullOrWhiteSpace(sectionAudioRef)) await StoreAsync(storage, sectionAudioRef);

        var assessment = new Assessment
        {
            Id = Guid.CreateVersion7(),
            Kind = AssessmentKind.Final,
            Title = title,
            Config = JsonSerializer.Serialize(new
            {
                // Null, as the admin editor writes it: a final has no pass mark — its outcome is
                // a scaled score and a predicted band. The literal 0 that used to sit here was
                // written to match scoring's old `?? 0`, so the fixture agreed with the bug.
                passThreshold = (int?)null,
                retakeCap,
                proctoringEnabled = true,
                audioPlayLimit,
                sections = SectionConfig(sectionAudioRef),
                timeLimitMinutes = (int?)null,
            }, Json),
        };
        db.Assessments.Add(assessment);

        var key = new Dictionary<QuestionSection, List<(Guid, int)>>();
        var order = 0;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        foreach (var (section, count) in new[]
        {
            (QuestionSection.Listening, ToeflScoring.ListeningQuestions),
            (QuestionSection.Structure, ToeflScoring.StructureQuestions),
            (QuestionSection.Reading,   ToeflScoring.ReadingQuestions),
        })
        {
            var list = new List<(Guid, int)>();
            for (var i = 0; i < count; i++)
            {
                // Alternate the key so "answer everything correctly" cannot pass by picking 0.
                var correct = i % 2;
                var question = new Question
                {
                    Id = Guid.CreateVersion7(),
                    Section = section,
                    Type = QuestionType.Mcq,
                    Prompt = $"{section} {i} {suffix}",
                    Choices = """["a","b"]""",
                    Correct = $"[{correct}]",
                    AudioRef = section == QuestionSection.Listening ? QuestionAudioRef : null,
                    PassageRef = null,
                    Tags = "[]",
                };
                db.Questions.Add(question);
                db.AssessmentQuestions.Add(new AssessmentQuestion
                {
                    Id = Guid.CreateVersion7(),
                    AssessmentId = assessment.Id,
                    QuestionId = question.Id,
                    OrderIndex = ++order,
                });
                list.Add((question.Id, correct));
            }
            key[section] = list;
        }

        await db.SaveChangesAsync();
        return new Seeded(assessment.Id, key.ToDictionary(x => x.Key, x => x.Value));
    }
}
