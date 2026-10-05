using System.Text.Json;
using Academy.Application.Assessments;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Assessments;

/// <summary>Question-bank CRUD (KAK §9.6.3 / §9.12). Admin-only; every mutation is audit-logged.</summary>
public class QuestionBankService(AppDbContext db) : IQuestionBankService
{
    public async Task<IReadOnlyList<AdminQuestionDto>> ListAsync(
        string? bank, string? section, string? search, CancellationToken ct = default)
    {
        var q = db.Questions.AsQueryable();

        if (Enum.TryParse<QuestionBank>(bank, true, out var b))
            q = q.Where(x => x.Bank == b);
        if (Enum.TryParse<QuestionSection>(section, true, out var parsed))
            q = q.Where(x => x.Section == parsed);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(x => EF.Functions.ILike(x.Prompt, $"%{search.Trim()}%"));

        // Project raw json + usage count, then map in memory (EF can't translate the json parsing).
        var rows = await q
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .Select(x => new
            {
                x.Id, x.Section, x.Bank, x.Type, x.Prompt, x.Choices, x.Correct,
                x.AudioRef, x.PassageRef, x.Tags,
                UsedIn = db.AssessmentQuestions.Count(aq => aq.QuestionId == x.Id),
            })
            .ToListAsync(ct);

        return rows.Select(x => new AdminQuestionDto(
            x.Id, x.Section.ToString(), x.Bank.ToString(), x.Type.ToString(), x.Prompt,
            ParseStrings(x.Choices), ParseInts(x.Correct),
            x.AudioRef, x.PassageRef, ParseStrings(x.Tags), x.UsedIn)).ToList();
    }

    public async Task<AdminQuestionDto> CreateAsync(
        Guid actor, UpsertQuestionRequest req, CancellationToken ct = default)
    {
        if (!Enum.TryParse<QuestionBank>(req.Bank, true, out var bank))
            throw new AssessmentException("Pilih bank soal.");
        Validate(req);
        var question = new Question { Id = Guid.CreateVersion7() };
        Apply(question, req);
        question.Bank = bank;
        db.Questions.Add(question);
        Audit(actor, "question_created", question.Id, new { question.Section, req.Prompt });
        await db.SaveChangesAsync(ct);
        return Map(question, 0);
    }

    public async Task UpdateAsync(Guid actor, Guid id, UpsertQuestionRequest req, CancellationToken ct = default)
    {
        Validate(req);
        var question = await db.Questions.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AssessmentException("Soal tidak ditemukan.", 404);
        Apply(question, req);
        Audit(actor, "question_updated", id, new { question.Section, req.Prompt });
        await db.SaveChangesAsync(ct);
    }

    public async Task MoveAsync(Guid actor, Guid id, string bank, CancellationToken ct = default)
    {
        if (!Enum.TryParse<QuestionBank>(bank, true, out var target))
            throw new AssessmentException("Pilih bank soal.");
        var question = await db.Questions.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AssessmentException("Soal tidak ditemukan.", 404);
        if (question.Bank == target) return;

        // A test may only hold questions of its own bank; moving one out from under a test of the
        // kind it serves would break that, so the admin detaches it first.
        var blockingKind = target == QuestionBank.SessionTest ? AssessmentKind.Final : AssessmentKind.Gating;
        if (await db.AssessmentQuestions.AnyAsync(
                aq => aq.QuestionId == id && aq.Assessment.Kind == blockingKind, ct))
            throw new AssessmentException(
                blockingKind == AssessmentKind.Final
                    ? "Soal ini masih dipakai di tes akhir. Lepas dari tes tersebut terlebih dahulu."
                    : "Soal ini masih dipakai di tes sesi. Lepas dari tes tersebut terlebih dahulu.", 409);

        var from = question.Bank;
        question.Bank = target;
        question.UpdatedAt = DateTimeOffset.UtcNow;
        Audit(actor, "question_moved", id, new { from = from.ToString(), to = target.ToString() });
        await db.SaveChangesAsync(ct);
    }

    public async Task<QuestionBankCountsDto> CountsAsync(CancellationToken ct = default)
    {
        var rows = await db.Questions.GroupBy(q => q.Bank)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        int Of(QuestionBank b) => rows.FirstOrDefault(r => r.Key == b)?.Count ?? 0;
        return new QuestionBankCountsDto(Of(QuestionBank.Simulation), Of(QuestionBank.SessionTest));
    }

    public async Task DeleteAsync(Guid actor, Guid id, CancellationToken ct = default)
    {
        var question = await db.Questions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (question is null) return;

        // A question referenced by an assessment is part of a scored history — unlink it first.
        if (await db.AssessmentQuestions.AnyAsync(aq => aq.QuestionId == id, ct))
            throw new AssessmentException(
                "Soal masih dipakai pada satu atau lebih tes. Lepaskan dari tes tersebut dulu.", 409);

        db.Questions.Remove(question);
        Audit(actor, "question_deleted", id, new { question.Prompt });
        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- helpers

    private static void Validate(UpsertQuestionRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Prompt))
            throw new AssessmentException("Pertanyaan wajib diisi.");
        if (req.Choices.Count < 2)
            throw new AssessmentException("Minimal 2 pilihan jawaban.");
        if (req.Choices.Any(string.IsNullOrWhiteSpace))
            throw new AssessmentException("Pilihan jawaban tidak boleh kosong.");
        if (req.Correct.Count == 0)
            throw new AssessmentException("Tandai minimal satu jawaban benar.");
        if (req.Correct.Any(i => i < 0 || i >= req.Choices.Count))
            throw new AssessmentException("Indeks jawaban benar di luar jangkauan pilihan.");
    }

    private static void Apply(Question q, UpsertQuestionRequest req)
    {
        q.Section = Enum.TryParse<QuestionSection>(req.Section, true, out var s) ? s : QuestionSection.General;
        q.Type = QuestionType.Mcq;
        q.Prompt = req.Prompt.Trim();
        q.Choices = JsonSerializer.Serialize(req.Choices.Select(c => c.Trim()));
        q.Correct = JsonSerializer.Serialize(req.Correct.Distinct().OrderBy(i => i));
        q.AudioRef = req.AudioRef?.Trim();
        q.PassageRef = req.PassageRef?.Trim();
        q.Tags = JsonSerializer.Serialize(req.Tags ?? []);
    }

    private static AdminQuestionDto Map(Question q, int usedIn) => new(
        q.Id, q.Section.ToString(), q.Bank.ToString(), q.Type.ToString(), q.Prompt,
        ParseStrings(q.Choices), ParseInts(q.Correct),
        q.AudioRef, q.PassageRef, ParseStrings(q.Tags), usedIn);

    private void Audit(Guid actor, string action, Guid target, object metadata)
        => db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.CreateVersion7(),
            ActorUserId = actor,
            Action = action,
            Target = target.ToString(),
            Metadata = JsonSerializer.Serialize(metadata),
        });

    internal static List<string> ParseStrings(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }

    internal static List<int> ParseInts(string json)
    {
        try { return JsonSerializer.Deserialize<List<int>>(json) ?? []; }
        catch { return []; }
    }
}
