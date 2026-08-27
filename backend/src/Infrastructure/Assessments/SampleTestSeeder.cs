using System.Reflection;
using System.Text.Json;
using Academy.Application.Abstractions;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Academy.Infrastructure.Programs;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Assessments;

/// <summary>
/// Seeds ONE runnable TOEFL ITP-style sample test (5 Listening / 5 Structure / 5 Reading) and
/// attaches it as the gating test on the seeded program's first session, so a fresh database is
/// demonstrable end-to-end without hand-authoring content.
///
/// Deliberately a GATING test, not a final assessment: <c>FinalFormatCheck</c> requires the real
/// ITP layout (50/40/50), so a five-per-section test would be correctly refused at publish.
/// Gating tests are section-agnostic, which is what makes this legal.
///
/// Idempotent, and gated by SeedSampleData like every other seeder. The listening audio is
/// SYNTHESISED PLACEHOLDER SPEECH shipped as an embedded resource — playable and matched to each
/// question, but not production content (KAK §17 item 4 remains owed).
/// </summary>
public class SampleTestSeeder(AppDbContext db, IObjectStorage storage)
{
    public const string Title = "Contoh Tes TOEFL ITP (15 soal)";

    /// <summary>Stable keys, so re-seeding overwrites rather than orphaning objects.</summary>
    private static string AudioKey(int n) => $"audio/sample-listening-{n}.m4a";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Assessments.AnyAsync(a => a.Title == Title, ct)) return;

        // The program must exist; this hangs off its first session.
        var session = await db.ProgramSessions
            .Where(s => s.Program.Slug == ProgramSeeder.Slug)
            .OrderBy(s => s.OrderIndex)
            .FirstOrDefaultAsync(ct);
        if (session is null) return;

        await WriteAudioAsync(ct);

        var assessment = new Assessment
        {
            Id = Guid.CreateVersion7(),
            Kind = AssessmentKind.Gating,
            Title = Title,
            Config = JsonSerializer.Serialize(new
            {
                passThreshold = 9,          // 9 of 15
                retakeCap = (int?)null,     // unlimited — this is a practice test
                proctoringEnabled = false,
                audioPlayLimit = 2,         // ITP allows one play; two is kinder for a sample
                sections = Array.Empty<object>(),
                timeLimitMinutes = 20,
            }),
        };
        db.Assessments.Add(assessment);

        var order = 0;
        foreach (var (section, prompt, choices, correct, audio, passage) in Items())
        {
            var question = new Question
            {
                Id = Guid.CreateVersion7(),
                Section = section,
                Type = QuestionType.Mcq,
                Prompt = prompt,
                Choices = JsonSerializer.Serialize(choices),
                Correct = JsonSerializer.Serialize(new[] { correct }),
                AudioRef = audio,
                PassageRef = passage,
                Tags = """["sample","itp"]""",
            };
            db.Questions.Add(question);
            db.AssessmentQuestions.Add(new AssessmentQuestion
            {
                Id = Guid.CreateVersion7(),
                AssessmentId = assessment.Id,
                QuestionId = question.Id,
                OrderIndex = ++order,
            });
        }

        session.AssessmentId = assessment.Id;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Copies the embedded placeholder clips into object storage under stable keys.</summary>
    private async Task WriteAudioAsync(CancellationToken ct)
    {
        var asm = Assembly.GetExecutingAssembly();
        for (var n = 1; n <= 5; n++)
        {
            var name = $"Academy.Infrastructure.Assets.SampleListening.L{n}.m4a";
            await using var stream = asm.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Embedded audio not found: {name}");
            await storage.PutAsync(AudioKey(n), stream, "audio/mp4", ct);
        }
    }

    // ---------------------------------------------------------------- content

    /// <summary>
    /// ITP conventions followed deliberately: Listening answers are restatements or inferences
    /// rather than literal repeats; Structure mixes sentence completion with error identification;
    /// Reading is one academic passage with main-idea, detail, vocabulary-in-context, inference and
    /// cause items. Vocabulary sits INSIDE Reading, as it does on a real paper — there is no
    /// separate vocabulary section in TOEFL ITP, and ScoreConversionService scores only these three.
    /// </summary>
    private static IEnumerable<(QuestionSection Section, string Prompt, string[] Choices,
        int Correct, string? Audio, string? Passage)> Items()
    {
        const QuestionSection L = QuestionSection.Listening;
        yield return (L,
            "Man: I'm thinking of taking Professor Klein's economics course next semester.\n" +
            "Woman: You'd better register early — it filled up in two days last year.\n" +
            "Question: What does the woman imply?",
            ["Professor Klein is popular among economists.", "Registration closed two days ago.",
             "The man should sign up quickly.", "The course is too difficult for the man."],
            2, AudioKey(1), null);
        yield return (L,
            "Woman: Did you finish the lab report?\n" +
            "Man: Finish it? I haven't even started collecting the data.\n" +
            "Question: What does the man mean?",
            ["He is nowhere near finished.", "He has already submitted the report.",
             "He lost the data he collected.", "He would like the woman's help."],
            0, AudioKey(2), null);
        yield return (L,
            "Man: The library closes at six on Fridays, doesn't it?\n" +
            "Woman: Actually, they extended the hours this term.\n" +
            "Question: What does the woman say about the library?",
            ["It now closes earlier than before.", "It is closed on Fridays this term.",
             "Its hours have not changed.", "It stays open later than it used to."],
            3, AudioKey(3), null);
        yield return (L,
            "Woman: I could hardly hear the speaker from the back row.\n" +
            "Man: You should have moved up front — there were plenty of empty seats.\n" +
            "Question: What does the man suggest the woman should have done?",
            ["Spoken more loudly herself.", "Sat closer to the speaker.",
             "Arrived before the lecture began.", "Asked the speaker to repeat."],
            1, AudioKey(4), null);
        yield return (L,
            "Man: Are you going to the career fair tomorrow?\n" +
            "Woman: I have a midterm at two, but I'll stop by afterward.\n" +
            "Question: What will the woman probably do?",
            ["Reschedule her midterm examination.", "Miss the career fair entirely.",
             "Go to the fair once her exam is over.", "Meet the man at two o'clock."],
            2, AudioKey(5), null);

        const QuestionSection S = QuestionSection.Structure;
        yield return (S,
            "_____ in 1886, the Statue of Liberty was a gift from the people of France.",
            ["It was completed", "Completed", "Completing it", "Which was completed"],
            1, null, null);
        yield return (S,
            "Not until the nineteenth century _____ widely available to the general public.",
            ["photography became", "photography had become",
             "did photography become", "became photography"],
            2, null, null);
        yield return (S,
            "The committee has met twice this month, but it _____ a decision yet.",
            ["did not reach", "does not reach", "not reached", "has not reached"],
            3, null, null);
        yield return (S,
            "Identify the underlined part that is NOT correct:\n\n" +
            "The number of students (A) who apply to graduate programmes (B) have increased " +
            "(C) steadily over (D) the past decade.",
            ["(A) who", "(B) have", "(C) steadily", "(D) the past decade"],
            1, null, null);
        yield return (S,
            "Identify the underlined part that is NOT correct:\n\n" +
            "Although the experiment was (A) carefully designed, the results were (B) quite " +
            "different (C) than the researchers (D) had predicted.",
            ["(A) carefully", "(B) quite", "(C) than", "(D) had predicted"],
            2, null, null);

        const QuestionSection R = QuestionSection.Reading;
        yield return (R, "What is the passage mainly about?",
            ["Methods of measuring ocean temperature.",
             "The structure of coral reefs and the threat of bleaching.",
             "The classification of tropical marine species.",
             "The commercial harvesting of coral."],
            1, null, Passage);
        yield return (R, "According to the passage, what proportion of marine species do reefs shelter?",
            ["Less than one percent.", "About one half.",
             "About one quarter.", "Nearly all of them."],
            2, null, Passage);
        yield return (R, "The word \"expel\" in the passage is closest in meaning to",
            ["absorb", "nourish", "attract", "drive out"],
            3, null, Passage);
        yield return (R, "It can be inferred from the passage that a coral that has just bleached",
            ["may still recover if conditions improve.", "has already died.",
             "grows more quickly than before.", "no longer contains calcium carbonate."],
            0, null, Passage);
        yield return (R, "According to the passage, recovery from a severe bleaching event is slow because",
            ["the algae rarely return to the reef.",
             "reefs grow only a small amount each year.",
             "fish do not repopulate the crevices.",
             "water temperatures remain elevated."],
            1, null, Passage);
    }

    private const string Passage =
        "Coral reefs occupy less than one percent of the ocean floor, yet they shelter roughly a " +
        "quarter of all marine species. This disproportionate richness arises from the reef's " +
        "structural complexity: the calcium carbonate skeletons secreted by coral polyps create " +
        "countless crevices that serve as shelter for fish, crustaceans, and molluscs. The polyps " +
        "themselves depend on microscopic algae called zooxanthellae, which live within their " +
        "tissues and supply most of the coral's energy through photosynthesis. When water " +
        "temperatures rise even a degree or two above the seasonal maximum, corals expel these " +
        "algae — a phenomenon known as bleaching. A bleached coral is not dead, but it is " +
        "starving, and prolonged bleaching is usually fatal. Because reefs grow slowly, often " +
        "less than a centimetre a year, recovery from a severe bleaching event can take decades.";
}
