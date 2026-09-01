using Academy.Application.Assessments;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Assessments;
using Academy.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>
/// Import fills the BANK; it never assembles an assessment and never deletes anything. Those two
/// boundaries are what make a bad upload harmless: it cannot disturb a live exam, and it cannot
/// remove a question a learner has already answered (GR-7).
/// </summary>
public class QuestionImportTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    /// <summary>Builds a minimal valid workbook. Each row is "id|section|prompt|a|b|answer".</summary>
    private static MemoryStream Workbook(params string[] rows) => Workbook(null, rows);

    private static MemoryStream Workbook((string Id, string Text)? passage, params string[] rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Questions");
        string[] headers = ["id", "section", "prompt", "choice_a", "choice_b", "answer", "passage_id", "audio_file"];
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];

        for (var r = 0; r < rows.Length; r++)
        {
            var values = rows[r].Split('|');
            for (var c = 0; c < values.Length; c++) ws.Cell(r + 2, c + 1).SetValue(values[c]);
        }

        if (passage is not null)
        {
            var ps = wb.AddWorksheet("Passages");
            ps.Cell(1, 1).Value = "passage_id";
            ps.Cell(1, 2).Value = "text";
            ps.Cell(2, 1).SetValue(passage.Value.Id);
            ps.Cell(2, 2).SetValue(passage.Value.Text);
        }

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    /// <summary>Unique per test so the shared database never leaks state between them.</summary>
    private static string Id(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    /// <summary>
    /// A commit audit-logs its actor, and audit_logs.actor_user_id is a real FK — unlike the
    /// service under test, this fixture needs an actual row to point at, not just a fresh Guid.
    /// </summary>
    private static async Task<Guid> SeedActorAsync(AppDbContext db)
    {
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Email = $"{Guid.NewGuid():N}@test.local",
            Name = "Admin",
            Role = UserRole.Admin,
            EmailVerified = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<T> WithService<T>(Func<IQuestionImportService, AppDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        return await work(
            scope.ServiceProvider.GetRequiredService<IQuestionImportService>(),
            scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Fact]
    public async Task A_clean_file_creates_the_questions()
    {
        var id = Id("R");
        var committed = await WithService(async (service, db) =>
        {
            using var file = Workbook($"{id}|Reading|Main idea?|Trade|Rivers|A");
            var result = await service.CommitAsync(await SeedActorAsync(db), file);

            Assert.True(result.Committed);
            Assert.Equal(1, result.CreateCount);
            Assert.Equal(0, result.UpdateCount);
            Assert.Equal(1, result.PerSection["Reading"]);

            return await db.Questions.SingleAsync(q => q.ExternalId == id);
        });

        Assert.Equal(QuestionSection.Reading, committed.Section);
        Assert.Equal("Main idea?", committed.Prompt);
        Assert.Equal("""["Trade","Rivers"]""", committed.Choices);
        Assert.Equal("[0]", committed.Correct);
    }

    [Fact]
    public async Task Passage_text_is_copied_onto_every_question_that_references_it()
    {
        var a = Id("R"); var b = Id("R");
        await WithService(async (service, db) =>
        {
            using var file = Workbook(("P1", "The passage body."),
                $"{a}|Reading|Q1|X|Y|A|P1",
                $"{b}|Reading|Q2|X|Y|B|P1");

            Assert.True((await service.CommitAsync(await SeedActorAsync(db), file)).Committed);

            var stored = await db.Questions.Where(q => q.ExternalId == a || q.ExternalId == b).ToListAsync();
            Assert.All(stored, q => Assert.Equal("The passage body.", q.PassageRef));
            return 0;
        });
    }

    [Fact]
    public async Task Re_importing_the_same_file_updates_in_place_and_creates_nothing()
    {
        // This is the whole reason the feature exists rather than a paste box: "fix one row and
        // re-upload the sheet" has to be safe.
        var id = Id("R");
        await WithService(async (service, db) =>
        {
            using var first = Workbook($"{id}|Reading|Original|X|Y|A");
            await service.CommitAsync(await SeedActorAsync(db), first);
            var originalKey = (await db.Questions.SingleAsync(q => q.ExternalId == id)).Id;

            using var second = Workbook($"{id}|Reading|Corrected|X|Y|B");
            var result = await service.CommitAsync(await SeedActorAsync(db), second);

            Assert.Equal(0, result.CreateCount);
            Assert.Equal(1, result.UpdateCount);

            var stored = await db.Questions.SingleAsync(q => q.ExternalId == id);
            Assert.Equal(originalKey, stored.Id);          // same row, not a replacement
            Assert.Equal("Corrected", stored.Prompt);
            Assert.Equal("[1]", stored.Correct);
            return 0;
        });
    }

    [Fact]
    public async Task One_bad_row_rejects_the_whole_file_and_the_bank_is_unchanged()
    {
        // A partial import leaves the author unsure what landed, which is worse than a clean
        // refusal when the fix is "correct the sheet and upload it again".
        var good = Id("R"); var bad = Id("R");
        await WithService(async (service, db) =>
        {
            using var file = Workbook(
                $"{good}|Reading|Fine|X|Y|A",
                $"{bad}|Speaking|Bad section|X|Y|A");

            var result = await service.CommitAsync(await SeedActorAsync(db), file);

            Assert.False(result.Committed);
            Assert.Single(result.Errors);
            Assert.False(await db.Questions.AnyAsync(q => q.ExternalId == good));
            return 0;
        });
    }

    [Fact]
    public async Task A_preview_writes_nothing()
    {
        var id = Id("R");
        await WithService(async (service, db) =>
        {
            using var file = Workbook($"{id}|Reading|Q|X|Y|A");
            var result = await service.PreviewAsync(file);

            Assert.False(result.Committed);
            Assert.Equal(1, result.CreateCount);
            Assert.Empty(result.Errors);
            Assert.False(await db.Questions.AnyAsync(q => q.ExternalId == id));
            return 0;
        });
    }

    [Fact]
    public async Task A_preview_marks_which_rows_would_be_updated()
    {
        // The only place a human can notice that an author reused an id for a different question.
        var id = Id("R");
        await WithService(async (service, db) =>
        {
            using var first = Workbook($"{id}|Reading|Original|X|Y|A");
            await service.CommitAsync(await SeedActorAsync(db), first);

            using var second = Workbook($"{id}|Reading|Different question entirely|X|Y|A");
            var result = await service.PreviewAsync(second);

            Assert.True(Assert.Single(result.Items).IsUpdate);
            Assert.Equal(1, result.UpdateCount);
            return 0;
        });
    }

    [Fact]
    public async Task A_hand_authored_question_is_never_touched()
    {
        Guid handAuthored = default;
        await WithService(async (service, db) =>
        {
            var question = new Question
            {
                Id = Guid.CreateVersion7(),
                ExternalId = null,
                Section = QuestionSection.Reading,
                Prompt = "Written in the admin UI",
                Choices = """["a","b"]""",
                Correct = "[0]",
            };
            db.Questions.Add(question);
            await db.SaveChangesAsync();
            handAuthored = question.Id;

            using var file = Workbook($"{Id("R")}|Reading|Imported|X|Y|A");
            await service.CommitAsync(await SeedActorAsync(db), file);

            var after = await db.Questions.AsNoTracking().SingleAsync(q => q.Id == handAuthored);
            Assert.Equal("Written in the admin UI", after.Prompt);
            Assert.Null(after.ExternalId);
            return 0;
        });
        Assert.NotEqual(default, handAuthored);
    }

    [Fact]
    public async Task A_blank_optional_cell_leaves_the_stored_value_alone()
    {
        // Presence decides, matching the assessment-config convention. Wiping a working recording
        // because a column was left blank is the more expensive mistake.
        var id = Id("L");
        await WithService(async (service, db) =>
        {
            using var withAudio = Workbook($"{id}|Listening|Q|X|Y|A||L99.mp3");
            await service.CommitAsync(await SeedActorAsync(db), withAudio);
            Assert.Equal("audio/l99.mp3", (await db.Questions.SingleAsync(q => q.ExternalId == id)).AudioRef);

            using var withoutAudio = Workbook($"{id}|Listening|Q corrected|X|Y|A");
            await service.CommitAsync(await SeedActorAsync(db), withoutAudio);

            var stored = await db.Questions.SingleAsync(q => q.ExternalId == id);
            Assert.Equal("Q corrected", stored.Prompt);
            Assert.Equal("audio/l99.mp3", stored.AudioRef);       // survived
            return 0;
        });
    }

    [Fact]
    public async Task A_question_already_used_in_an_assessment_still_updates()
    {
        // Editing a question's text does not invalidate an attempt: attempts store chosen indices,
        // and attempts/proctor_events/certificates are never touched by an import.
        var id = Id("R");
        await WithService(async (service, db) =>
        {
            using var file = Workbook($"{id}|Reading|Original|X|Y|A");
            await service.CommitAsync(await SeedActorAsync(db), file);
            var question = await db.Questions.SingleAsync(q => q.ExternalId == id);

            var assessment = new Assessment
            {
                Id = Guid.CreateVersion7(), Kind = AssessmentKind.Final, Title = "Uji", Config = "{}",
            };
            db.Assessments.Add(assessment);
            db.AssessmentQuestions.Add(new AssessmentQuestion
            {
                Id = Guid.CreateVersion7(),
                AssessmentId = assessment.Id, QuestionId = question.Id, OrderIndex = 0,
            });
            await db.SaveChangesAsync();

            using var corrected = Workbook($"{id}|Reading|Corrected|X|Y|A");
            Assert.True((await service.CommitAsync(await SeedActorAsync(db), corrected)).Committed);

            Assert.Equal("Corrected", (await db.Questions.SingleAsync(q => q.ExternalId == id)).Prompt);
            Assert.True(await db.AssessmentQuestions.AnyAsync(aq => aq.QuestionId == question.Id));
            return 0;
        });
    }

    [Fact]
    public async Task A_commit_is_audit_logged()
    {
        await WithService(async (service, db) =>
        {
            var actor = await SeedActorAsync(db);
            using var file = Workbook($"{Id("R")}|Reading|Q|X|Y|A");
            await service.CommitAsync(actor, file);

            Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "questions_bulk_imported"));
            return 0;
        });
    }

    [Fact]
    public async Task An_audio_reference_with_no_uploaded_object_still_imports()
    {
        // Order-independence: the readiness check, not the importer, is where a missing recording
        // is reported — an admin may upload the file an hour later.
        var id = Id("L");
        await WithService(async (service, db) =>
        {
            using var file = Workbook($"{id}|Listening|Q|X|Y|A||L42.mp3");

            Assert.True((await service.CommitAsync(await SeedActorAsync(db), file)).Committed);
            Assert.Equal("audio/l42.mp3", (await db.Questions.SingleAsync(q => q.ExternalId == id)).AudioRef);
            return 0;
        });
    }
}
