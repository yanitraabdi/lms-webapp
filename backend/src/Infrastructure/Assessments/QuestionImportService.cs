using System.Text.Json;
using Academy.Application.Assessments;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Assessments;

/// <summary>
/// Bulk question import. Fills the BANK only — assembling an assessment stays with the existing
/// composer, so a bad import cannot disturb a live exam.
///
/// Preview and commit run the identical code path, and commit re-parses the uploaded file rather
/// than trusting a plan the client hands back: between the two calls the bank may have gained the
/// very ids this file creates.
/// </summary>
public class QuestionImportService(AppDbContext db) : IQuestionImportService
{
    private const int PromptPreviewLength = 90;

    public byte[] BuildTemplate() => XlsxWorkbookReader.BuildTemplate();

    public Task<ImportResultDto> PreviewAsync(Stream file, CancellationToken ct = default)
        => RunAsync(file, actor: null, ct);

    public Task<ImportResultDto> CommitAsync(Guid actor, Stream file, CancellationToken ct = default)
        => RunAsync(file, actor, ct);

    private async Task<ImportResultDto> RunAsync(Stream file, Guid? actor, CancellationToken ct)
    {
        var parsed = QuestionImportParser.Parse(XlsxWorkbookReader.Read(file));

        var ids = parsed.Questions.Select(q => q.ExternalId).ToList();
        // Explicitly typed, not `var`: a collection expression in a ternary has no target type.
        List<Question> existing = ids.Count == 0
            ? []
            : await db.Questions
                .Where(q => q.ExternalId != null && ids.Contains(q.ExternalId))
                .ToListAsync(ct);
        var byExternalId = existing.ToDictionary(q => q.ExternalId!, StringComparer.Ordinal);

        var items = parsed.Questions
            .Select(q => new ImportPlanItem(
                q.ExternalId, q.Section.ToString(), Truncate(q.Prompt), byExternalId.ContainsKey(q.ExternalId)))
            .ToList();

        var perSection = parsed.Questions
            .GroupBy(q => q.Section.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var updateCount = items.Count(i => i.IsUpdate);
        var createCount = items.Count - updateCount;

        // A preview never writes; a commit with any error never writes. Both return the same shape
        // so the screen can render errors identically either way.
        if (actor is not Guid actorId || parsed.Errors.Count > 0)
            return new ImportResultDto(false, createCount, updateCount, perSection, items, parsed.Errors);

        foreach (var question in parsed.Questions)
        {
            if (!byExternalId.TryGetValue(question.ExternalId, out var entity))
            {
                entity = new Question { Id = Guid.CreateVersion7(), ExternalId = question.ExternalId };
                db.Questions.Add(entity);
            }
            Apply(entity, question);
        }

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.CreateVersion7(),
            ActorUserId = actorId,
            Action = "questions_bulk_imported",
            Target = "questions",
            Metadata = JsonSerializer.Serialize(new { created = createCount, updated = updateCount, perSection }),
        });

        // One SaveChanges, one transaction: all-or-nothing at the database level too, not only
        // at the validation level.
        await db.SaveChangesAsync(ct);

        return new ImportResultDto(true, createCount, updateCount, perSection, items, []);
    }

    private static void Apply(Question entity, ParsedQuestion parsed)
    {
        entity.Section = parsed.Section;
        entity.Type = QuestionType.Mcq;
        entity.Prompt = parsed.Prompt;
        entity.Choices = JsonSerializer.Serialize(parsed.Choices);

        // Correct is an int ARRAY meaning "any of these is accepted", not "select all that apply".
        // TOEFL ITP has one correct answer per item, so exactly one index goes in.
        entity.Correct = JsonSerializer.Serialize(new[] { parsed.CorrectIndex });
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        // Presence decides, as it does for the assessment config bag: a blank optional cell keeps
        // whatever is stored. Destroying a working recording or a passage because a column was
        // left empty is the more expensive error, and the admin UI can still clear either one.
        if (parsed.AudioRef is not null) entity.AudioRef = parsed.AudioRef;
        if (parsed.PassageText is not null) entity.PassageRef = parsed.PassageText;
        if (parsed.Tags.Count > 0) entity.Tags = JsonSerializer.Serialize(parsed.Tags);
    }

    private static string Truncate(string prompt) => prompt.Length <= PromptPreviewLength
        ? prompt
        : prompt[..PromptPreviewLength] + "…";
}
