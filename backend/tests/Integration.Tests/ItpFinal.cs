using System.Text.Json;
using Academy.Domain;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
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

    internal sealed record Seeded(
        Guid AssessmentId,
        IReadOnlyDictionary<QuestionSection, List<(Guid Id, int Correct)>> Key);

    /// <summary>The ITP section layout as an anonymous object, for tests that POST a config.</summary>
    public static object[] SectionConfig() =>
    [
        new { section = nameof(QuestionSection.Listening), questions = ToeflScoring.ListeningQuestions, minutes = ToeflScoring.ListeningMinutes },
        new { section = nameof(QuestionSection.Structure), questions = ToeflScoring.StructureQuestions, minutes = ToeflScoring.StructureMinutes },
        new { section = nameof(QuestionSection.Reading),   questions = ToeflScoring.ReadingQuestions,   minutes = ToeflScoring.ReadingMinutes },
    ];

    public static async Task<Seeded> SeedAsync(
        AuthApiFactory factory,
        string title = "Simulasi TOEFL ITP",
        int? retakeCap = 1,
        int? audioPlayLimit = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var assessment = new Assessment
        {
            Id = Guid.CreateVersion7(),
            Kind = AssessmentKind.Final,
            Title = title,
            Config = JsonSerializer.Serialize(new
            {
                passThreshold = 0,
                retakeCap,
                proctoringEnabled = true,
                audioPlayLimit,
                sections = SectionConfig(),
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
                    AudioRef = section == QuestionSection.Listening ? "clip.mp3" : null,
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
