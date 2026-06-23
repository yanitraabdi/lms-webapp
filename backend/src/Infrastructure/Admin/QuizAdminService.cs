using System.Text.Json;
using Academy.Application.Admin;
using Academy.Domain.Entities;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Admin;

public class QuizAdminService(AppDbContext db) : IQuizAdminService
{
    public async Task<AdminQuizDto?> GetAsync(Guid moduleId, CancellationToken ct = default)
    {
        var quiz = await db.Quizzes.FirstOrDefaultAsync(q => q.ModuleId == moduleId, ct);
        if (quiz is null) return null;
        var questions = await db.QuizQuestions.Where(x => x.QuizId == quiz.Id).OrderBy(x => x.Id)
            .Select(x => new AdminQuizQuestionDto(x.Id, x.Prompt, Parse(x.Choices), x.CorrectIndex)).ToListAsync(ct);
        return new AdminQuizDto(quiz.Id, quiz.ModuleId, quiz.PassThreshold, quiz.IsActive, questions);
    }

    public async Task UpsertAsync(Guid actor, Guid moduleId, UpsertQuizRequest req, CancellationToken ct = default)
    {
        _ = await db.Modules.FirstOrDefaultAsync(m => m.Id == moduleId, ct)
            ?? throw new AdminException("Modul tidak ditemukan.", 404);

        var quiz = await db.Quizzes.FirstOrDefaultAsync(q => q.ModuleId == moduleId, ct);
        if (quiz is null)
        {
            quiz = new Quiz { Id = Guid.CreateVersion7(), ModuleId = moduleId };
            db.Quizzes.Add(quiz);
        }
        quiz.PassThreshold = req.PassThreshold;
        quiz.IsActive = req.IsActive;

        // Replace questions wholesale (questions have no dependent rows).
        var existing = await db.QuizQuestions.Where(x => x.QuizId == quiz.Id).ToListAsync(ct);
        db.QuizQuestions.RemoveRange(existing);
        foreach (var q in req.Questions)
            db.QuizQuestions.Add(new QuizQuestion
            {
                Id = Guid.CreateVersion7(), QuizId = quiz.Id, Prompt = q.Prompt.Trim(),
                Choices = JsonSerializer.Serialize(q.Choices), CorrectIndex = q.CorrectIndex,
            });

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.CreateVersion7(), ActorUserId = actor, Action = "quiz_upserted",
            Target = moduleId.ToString(), Metadata = JsonSerializer.Serialize(new { req.PassThreshold, req.IsActive, count = req.Questions.Count }),
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid actor, Guid moduleId, CancellationToken ct = default)
    {
        var quiz = await db.Quizzes.FirstOrDefaultAsync(q => q.ModuleId == moduleId, ct);
        if (quiz is null) return;
        db.Quizzes.Remove(quiz);
        db.AuditLogs.Add(new AuditLog { Id = Guid.CreateVersion7(), ActorUserId = actor, Action = "quiz_deleted", Target = moduleId.ToString(), Metadata = "{}" });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new AdminException("Tidak bisa dihapus karena sudah ada percobaan kuis.", 409); }
    }

    private static List<string> Parse(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}
