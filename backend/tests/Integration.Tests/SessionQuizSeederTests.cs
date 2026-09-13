using System.Text.Json;
using Academy.Domain.Enums;
using Academy.Infrastructure.Assessments;
using Academy.Infrastructure.Persistence;
using Academy.Infrastructure.Programs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>
/// Product decision: every video session has its own short quiz. Before this, only session 1 had
/// a test, so sessions 2–6 completed on watching alone and the linear lock gated one lesson of six.
/// </summary>
public class SessionQuizSeederTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private async Task<T> Seeded<T>(Func<AppDbContext, Task<T>> read)
    {
        using var scope = factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        // Same order as Program.cs.
        await sp.GetRequiredService<ProgramSeeder>().SeedAsync();
        await sp.GetRequiredService<SampleTestSeeder>().SeedAsync();
        await sp.GetRequiredService<SessionQuizSeeder>().SeedAsync();
        return await read(sp.GetRequiredService<AppDbContext>());
    }

    private record VideoQuiz(int OrderIndex, Guid? AssessmentId, int Questions, string? Config);

    private Task<List<VideoQuiz>> VideoSessions() => Seeded(db => db.ProgramSessions
        .Where(s => s.Program.Slug == ProgramSeeder.Slug && s.Type == SessionType.Video)
        .OrderBy(s => s.OrderIndex)
        .Select(s => new VideoQuiz(
            s.OrderIndex, s.AssessmentId,
            db.AssessmentQuestions.Count(q => q.AssessmentId == s.AssessmentId),
            s.Assessment == null ? null : s.Assessment.Config))
        .ToListAsync());

    [Fact]
    public async Task Every_video_session_has_a_quiz()
    {
        // The whole decision. A video session with no test completes on watching alone, which is
        // the gap this closes.
        var videos = await VideoSessions();

        Assert.NotEmpty(videos);
        Assert.All(videos, v => Assert.NotNull(v.AssessmentId));
    }

    [Fact]
    public async Task The_seeded_quizzes_are_five_questions_with_a_reachable_pass_mark()
    {
        var seeded = (await VideoSessions()).Skip(1).ToList();     // session 1 keeps its own test

        Assert.All(seeded, v =>
        {
            Assert.Equal(5, v.Questions);
            var config = JsonDocument.Parse(v.Config!).RootElement;
            Assert.Equal(3, config.GetProperty("passThreshold").GetInt32());
            Assert.Equal(JsonValueKind.Null, config.GetProperty("retakeCap").ValueKind);  // retried until passed
        });
    }

    [Fact]
    public async Task Session_one_keeps_the_test_it_already_had()
    {
        // Never overwrite a test that exists — the same rule that protects an admin's own quiz.
        var first = (await VideoSessions()).First();

        Assert.Equal(15, first.Questions);
    }

    [Fact]
    public async Task Running_the_seeder_again_adds_nothing()
    {
        var before = await Seeded(db => db.Questions.CountAsync(q => q.ExternalId!.StartsWith("PQ-")));
        var after = await Seeded(db => db.Questions.CountAsync(q => q.ExternalId!.StartsWith("PQ-")));

        Assert.Equal(before, after);
    }

    [Fact]
    public async Task No_single_letter_guess_passes_a_quiz()
    {
        // An earlier placeholder set put nearly every answer on A: guessing one letter scored 79%.
        // A quiz passable that way gates nothing, so for every quiz and every letter, answering
        // that letter throughout must fall short of the pass mark.
        var keys = await Seeded(db => db.AssessmentQuestions
            .Where(aq => aq.Question.ExternalId!.StartsWith("PQ-"))
            .Select(aq => new { aq.AssessmentId, aq.Question.Correct })
            .ToListAsync());

        foreach (var quiz in keys.GroupBy(k => k.AssessmentId))
            for (var letter = 0; letter < 4; letter++)
            {
                var score = quiz.Count(k => JsonSerializer.Deserialize<int[]>(k.Correct)!.Contains(letter));
                Assert.True(score < 3, $"always answering {"ABCD"[letter]} scores {score}/5 and passes");
            }
    }

    [Fact]
    public async Task Every_placeholder_is_marked_and_replaceable_by_id()
    {
        var questions = await Seeded(db => db.Questions
            .Where(q => q.ExternalId!.StartsWith("PQ-"))
            .Select(q => new { q.ExternalId, q.Prompt })
            .ToListAsync());

        Assert.All(questions, q => Assert.StartsWith(SessionQuizSeeder.Marker, q.Prompt));
        Assert.Equal(questions.Count, questions.Select(q => q.ExternalId).Distinct().Count());
    }
}
