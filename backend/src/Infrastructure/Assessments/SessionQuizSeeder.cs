using System.Text.Json;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Academy.Infrastructure.Programs;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Assessments;

/// <summary>
/// Gives every VIDEO session of the seeded programme its own short quiz — five multiple-choice
/// questions on that session's topic, pass mark 3 — so the linear lock gates each lesson rather than
/// only the first. Sessions 2–6 previously had no test at all, so watching alone completed them.
///
/// PLACEHOLDER content, like <see cref="PlaceholderFinalExamSeeder"/>: every prompt carries
/// "[CONTOH]" and the real syllabus is owed by the business. Each question has an author-assigned
/// external id (PQ-S2-01 …), so replacing them is the normal admin flow — upload a workbook with the
/// same ids at /admin/questions/import and the importer overwrites them in place, attachments intact.
///
/// Never touches a session that already has a test. That is both the idempotency rule and the
/// guarantee an admin's own quiz is never overwritten; it is also why session 1, which carries the
/// 15-question <see cref="SampleTestSeeder"/> test, is left as it is.
///
/// Content is keyed by position among VIDEO sessions, not by order index, so moving the live session
/// within the programme does not hand a lesson another lesson's quiz.
///
/// Adding a quiz to a session a learner already completed does not un-complete it (GR-8): the
/// completion row stands, and only learners who have not yet finished it meet the quiz.
/// </summary>
public class SessionQuizSeeder(AppDbContext db)
{
    public const string Marker = "[CONTOH]";
    private const int PassMark = 3;                       // of 5 — the same 60% as the sample's 9 of 15

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var videos = await db.ProgramSessions
            .Where(s => s.Program.Slug == ProgramSeeder.Slug && s.Type == SessionType.Video)
            .OrderBy(s => s.OrderIndex)
            .ToListAsync(ct);

        var attached = 0;
        for (var i = 0; i < videos.Count; i++)
        {
            var session = videos[i];
            if (session.AssessmentId is not null) continue;              // never overwrite a test
            if (!Quizzes.TryGetValue(i + 1, out var quiz)) continue;     // no content for this slot

            var assessment = new Assessment
            {
                Id = Guid.CreateVersion7(),
                Kind = AssessmentKind.Gating,
                Title = $"Kuis — {quiz.Topic}",
                Config = JsonSerializer.Serialize(new
                {
                    passThreshold = PassMark,
                    retakeCap = (int?)null,            // gating tests are retried until passed
                    proctoringEnabled = false,
                    audioPlayLimit = (int?)null,
                    sections = Array.Empty<object>(),
                    timeLimitMinutes = (int?)null,
                }),
            };
            db.Assessments.Add(assessment);

            for (var q = 0; q < quiz.Items.Length; q++)
            {
                var item = quiz.Items[q];
                var question = new Question
                {
                    Id = Guid.CreateVersion7(),
                    ExternalId = $"PQ-S{i + 1}-{q + 1:00}",
                    Section = quiz.Section,
                    Type = QuestionType.Mcq,
                    Prompt = $"{Marker} {item.Prompt}",
                    Choices = JsonSerializer.Serialize(item.Choices),
                    Correct = JsonSerializer.Serialize(new[] { item.Correct }),
                    PassageRef = quiz.Passage,
                    Tags = JsonSerializer.Serialize(new[] { "placeholder", "quiz", $"session-{i + 1}" }),
                };
                db.Questions.Add(question);
                db.AssessmentQuestions.Add(new AssessmentQuestion
                {
                    Id = Guid.CreateVersion7(),
                    AssessmentId = assessment.Id,
                    QuestionId = question.Id,
                    OrderIndex = q + 1,
                });
            }

            session.AssessmentId = assessment.Id;
            attached++;
        }

        // One save: a session never ends up holding a test with half its questions.
        if (attached > 0) await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- content

    private record Item(string Prompt, string[] Choices, int Correct);
    private record Quiz(string Topic, QuestionSection Section, string? Passage, Item[] Items);

    /// <summary>
    /// Keyed by position among the programme's video sessions (1-based).
    ///
    /// Listening items print the conversation as text, because no per-question recordings exist
    /// yet. That makes them answerable by reading — acceptable for placeholders, and exactly why
    /// real Listening content must replace these rather than extend them.
    ///
    /// Correct answers are spread across A–D. An earlier placeholder set put nearly every answer on
    /// A, so a learner guessing one letter scored 79%; a quiz that can be passed that way gates
    /// nothing.
    /// </summary>
    private static readonly Dictionary<int, Quiz> Quizzes = new()
    {
        [2] = new("Listening — Short Conversations", QuestionSection.Listening, null,
        [
            new("Woman: Would you mind closing the window? It's getting cold in here.\n" +
                "Man: Not at all.\nQuestion: What will the man probably do?",
                ["Turn on the heater.", "Leave the room.", "Close the window.", "Open another window."], 2),
            new("Man: I can't believe the bookstore has already sold out of the chemistry textbook.\n" +
                "Woman: Have you tried the reserve desk at the library?\nQuestion: What does the woman suggest?",
                ["Borrowing the book from the library.", "Buying the book online.",
                 "Dropping the chemistry course.", "Asking the professor for a copy."], 0),
            new("Woman: Are Professor Lee's office hours still on Tuesday afternoons?\n" +
                "Man: They were moved to Thursday mornings.\nQuestion: What does the man mean?",
                ["Professor Lee has cancelled office hours.", "The office hours are now on Thursday mornings.",
                 "Professor Lee is only available on Tuesdays.", "The man does not know the schedule."], 1),
            new("Man: Did you enjoy the concert last night?\n" +
                "Woman: I wish I had stayed home.\nQuestion: What does the woman imply?",
                ["She stayed home instead.", "She arrived too late.",
                 "She wants to go again.", "She did not enjoy the concert."], 3),
            new("Woman: The line at the cafeteria is incredibly long.\n" +
                "Man: Why don't we eat at the student center instead?\nQuestion: What does the man suggest?",
                ["Waiting in the cafeteria line.", "Skipping lunch today.",
                 "Eating somewhere else.", "Cooking at home."], 2),
        ]),

        [3] = new("Listening — Longer Talks", QuestionSection.Listening,
            "Talk in a biology class: Today I want to talk about how honeybees share information. " +
            "When a forager bee finds a good source of nectar, she returns to the hive and performs " +
            "what is called the waggle dance. The direction of the dance, measured against straight " +
            "up on the vertical honeycomb, shows the direction of the food relative to the sun. The " +
            "length of the waggle tells the other bees how far away the food is — the longer the " +
            "waggle, the greater the distance. Researchers first decoded this behavior in the 1940s, " +
            "and it remains one of the most sophisticated forms of communication known in insects.",
        [
            new("What is the talk mainly about?",
                ["How honeybees produce honey.", "How honeybees communicate the location of food.",
                 "The history of beekeeping.", "Why honeybee populations are declining."], 1),
            new("According to the speaker, what does the direction of the dance indicate?",
                ["The quality of the nectar.", "The number of bees needed.",
                 "The time of day to leave the hive.", "The direction of the food relative to the sun."], 3),
            new("What does a longer waggle tell the other bees?",
                ["The food is farther away.", "There is more nectar available.",
                 "The hive is in danger.", "The forager is tired."], 0),
            new("When was the waggle dance first decoded?",
                ["In the 1840s.", "Only in recent years.", "In the 1940s.", "It has not yet been decoded."], 2),
            new("What can be inferred about the waggle dance?",
                ["Only the queen bee performs it.", "Bees learn it from other species.",
                 "It works only at night.", "It is considered unusually advanced for insects."], 3),
        ]),

        [4] = new("Structure — Sentence Completion", QuestionSection.Structure, null,
        [
            new("_____ the heavy rain, the outdoor ceremony continued as planned.",
                ["Although", "Despite", "Because of", "In spite"], 1),
            new("The Amazon River carries more water _____ any other river in the world.",
                ["as", "from", "then", "than"], 3),
            new("Rarely _____ such a large audience at a university lecture.",
                ["have we seen", "we have seen", "we saw", "seen we have"], 0),
            new("Penicillin, _____ by Alexander Fleming in 1928, transformed modern medicine.",
                ["which discovered", "was discovered", "discovered", "discovering"], 2),
            new("The more carefully you proofread, _____ mistakes you will make.",
                ["fewer", "the less", "less", "the fewer"], 3),
        ]),

        [5] = new("Written Expression — Error Identification", QuestionSection.Structure, null,
        [
            new("Identify the underlined part that is NOT correct:\n\n" +
                "The scientist (A) who conducted the experiment (B) were (C) awarded a prize (D) last year.",
                ["(A) who", "(B) were", "(C) awarded", "(D) last year"], 1),
            new("Identify the underlined part that is NOT correct:\n\n" +
                "Mount Everest is (A) the most highest (B) mountain (C) above sea level (D) in the world.",
                ["(A) the most highest", "(B) mountain", "(C) above", "(D) in the world"], 0),
            new("Identify the underlined part that is NOT correct:\n\n" +
                "(A) Neither the manager nor (B) the employees (C) was aware (D) of the change.",
                ["(A) Neither", "(B) the employees", "(C) was", "(D) of"], 2),
            new("Identify the underlined part that is NOT correct:\n\n" +
                "The results (A) of the survey (B) was published (C) in a widely (D) read journal.",
                ["(A) of", "(B) was", "(C) in", "(D) read"], 1),
            new("Identify the underlined part that is NOT correct:\n\n" +
                "The children were (A) so excited about the trip (B) that they (C) could not sleep " +
                "(D) during all the night.",
                ["(A) so excited", "(B) that", "(C) could not sleep", "(D) during all the night"], 3),
        ]),

        [6] = new("Reading — Skimming, Scanning & Vocabulary", QuestionSection.Reading,
            "Each autumn, millions of monarch butterflies leave their breeding grounds in the northern " +
            "United States and Canada and travel as far as 4,000 kilometers to overwintering sites in " +
            "the mountains of central Mexico. What makes this journey remarkable is that no single " +
            "butterfly completes the full round trip: the monarchs that fly south in autumn are several " +
            "generations removed from those that left Mexico the previous spring. Scientists believe " +
            "the butterflies navigate using a time-compensated sun compass located in their antennae, " +
            "which allows them to hold a southwesterly heading as the sun moves across the sky. On " +
            "cloudy days, they may also rely on the Earth's magnetic field. In recent decades, the " +
            "overwintering population has declined sharply, largely because of habitat loss and the " +
            "reduced availability of milkweed, the only plant on which monarch caterpillars feed.",
        [
            new("What is the passage mainly about?",
                ["The life cycle of the caterpillar.", "The climate of central Mexico.",
                 "The migration of monarch butterflies.", "How scientists tag insects."], 2),
            new("The word \"remarkable\" in the passage is closest in meaning to",
                ["extraordinary", "dangerous", "predictable", "recent"], 0),
            new("According to the passage, how do monarchs navigate on sunny days?",
                ["By following rivers.", "By following other species.",
                 "By using the stars.", "By using a sun compass in their antennae."], 3),
            new("It can be inferred from the passage that the monarchs arriving in Canada in summer",
                ["flew south the previous autumn.", "are descendants of the butterflies that overwintered in Mexico.",
                 "never feed on milkweed.", "travel faster than those flying south."], 1),
            new("According to the passage, why has the overwintering population declined?",
                ["Colder winters in Mexico.", "An increase in predators.",
                 "Habitat loss and less milkweed.", "Damage to the magnetic field."], 2),
        ]),
    };
}
