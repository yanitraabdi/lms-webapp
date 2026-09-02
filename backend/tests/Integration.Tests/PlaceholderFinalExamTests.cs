using Academy.Application.Programs;
using Academy.Domain.Enums;
using Academy.Infrastructure.Assessments;
using Academy.Infrastructure.Persistence;
using Academy.Infrastructure.Auth;
using Academy.Infrastructure.Programs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>
/// The placeholder final exam exists for one reason: while the bank held 16 of the 140 items the
/// ITP format requires, the exam, its timer, proctoring, scoring, the certificate and the public
/// verify page could not be exercised end to end by anyone. This fills the shape so that chain is
/// testable before the real syllabus lands.
///
/// It is scaffolding, so the tests that matter are about SHAPE and REPLACEABILITY, not content.
/// </summary>
public class PlaceholderFinalExamTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private async Task<T> Seeded<T>(Func<IServiceProvider, AppDbContext, Task<T>> read)
    {
        using var scope = factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        // Same order as Program.cs: the import is attributed to an admin, so one must exist.
        await sp.GetRequiredService<DevAdminSeeder>().SeedAsync();
        await sp.GetRequiredService<ProgramSeeder>().SeedAsync();
        await sp.GetRequiredService<PlaceholderFinalExamSeeder>().SeedAsync();
        return await read(sp, sp.GetRequiredService<AppDbContext>());
    }

    [Fact]
    public async Task Seeding_fills_the_final_assessment_with_the_ITP_shape()
    {
        // 50/40/50 is not a preference — ProgramReadiness refuses to publish anything else.
        var counts = await Seeded(async (_, db) => await db.AssessmentQuestions
            .Where(aq => aq.Assessment.Kind == AssessmentKind.Final)
            .GroupBy(aq => aq.Question.Section)
            .Select(g => new { Section = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Section, x => x.Count));

        Assert.Equal(50, counts[QuestionSection.Listening]);
        Assert.Equal(40, counts[QuestionSection.Structure]);
        Assert.Equal(50, counts[QuestionSection.Reading]);
        Assert.Equal(140, counts.Values.Sum());
    }

    [Fact]
    public async Task Every_seeded_question_is_marked_as_placeholder_content()
    {
        // The one thing that must never slip: a placeholder item reaching a paying learner
        // indistinguishable from real content. The marker is visible in the bank AND in the exam.
        var prompts = await Seeded(async (_, db) => await db.AssessmentQuestions
            .Where(aq => aq.Assessment.Kind == AssessmentKind.Final)
            .Select(aq => aq.Question.Prompt)
            .ToListAsync());

        Assert.Equal(140, prompts.Count);
        Assert.All(prompts, p => Assert.StartsWith("[CONTOH]", p));
    }

    [Fact]
    public async Task The_seeded_exam_can_actually_be_failed()
    {
        // A placeholder exam whose answers are all "A" cannot be failed by anyone guessing, which
        // makes it useless for testing the half of the product that handles failure. No single
        // letter may carry enough of the key to pass on its own.
        var correct = await Seeded(async (_, db) => await db.AssessmentQuestions
            .Where(aq => aq.Assessment.Kind == AssessmentKind.Final)
            .Select(aq => aq.Question.Correct)
            .ToListAsync());

        var byIndex = correct
            .Select(json => System.Text.Json.JsonSerializer.Deserialize<List<int>>(json) ?? [])
            .Where(c => c.Count > 0)
            .GroupBy(c => c[0])
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.True(byIndex.Values.Max() < 70,
            $"one answer position covers {byIndex.Values.Max()} of 140 items — guess-proof it");
    }

    [Fact]
    public async Task Seeding_twice_does_not_duplicate_the_questions()
    {
        // Idempotent like every other seeder here: containers restart, and the exam must not grow.
        var total = await Seeded(async (sp, db) =>
        {
            await sp.GetRequiredService<PlaceholderFinalExamSeeder>().SeedAsync();
            return await db.AssessmentQuestions
                .CountAsync(aq => aq.Assessment.Kind == AssessmentKind.Final);
        });

        Assert.Equal(140, total);
    }

    [Fact]
    public async Task The_programme_passes_readiness_once_the_exam_is_seeded()
    {
        // The point of the whole exercise. Before this, "final_assessment_questions" and
        // "listening_audio_present" were the two checks nobody could satisfy without 140 items.
        var checks = await Seeded(async (sp, db) =>
        {
            var programId = await db.Programs
                .Where(p => p.Slug == ProgramSeeder.Slug).Select(p => p.Id).FirstAsync();
            return await sp.GetRequiredService<IProgramAdminService>().GetReadinessAsync(programId);
        });

        var failed = checks.Checks.Where(c => !c.Passed && c.Blocking).Select(c => c.Key).ToList();
        Assert.True(failed.Count == 0, $"still blocking: {string.Join(", ", failed)}");
    }
}
