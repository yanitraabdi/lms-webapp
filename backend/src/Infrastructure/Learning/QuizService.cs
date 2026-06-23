using System.Text.Json;
using Academy.Application.Catalog;
using Academy.Application.Learning;
using Academy.Domain;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Learning;

public class QuizService(AppDbContext db, IEntitlementService entitlement, IModuleCompletionService completion) : IQuizService
{
    public async Task<QuizDto?> GetForModuleAsync(Guid userId, Guid moduleId, CancellationToken ct = default)
    {
        await EnsurePlayableAsync(userId, moduleId, ct);
        var quiz = await db.Quizzes.FirstOrDefaultAsync(q => q.ModuleId == moduleId && q.IsActive, ct);
        if (quiz is null) return null;

        var questions = await db.QuizQuestions.Where(x => x.QuizId == quiz.Id).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Prompt, x.Choices }).ToListAsync(ct);
        if (questions.Count == 0) return null;

        var attempts = await db.QuizAttempts.Where(a => a.UserId == userId && a.QuizId == quiz.Id)
            .Select(a => new { a.Score, a.Passed }).ToListAsync(ct);

        var dtos = questions.Select(x => new QuizQuestionDto(x.Id, x.Prompt, Parse(x.Choices))).ToList();
        return new QuizDto(quiz.Id, moduleId, quiz.PassThreshold, dtos.Count, dtos,
            attempts.Any(a => a.Passed), attempts.Count > 0 ? attempts.Max(a => a.Score) : null);
    }

    public async Task<QuizResultDto> SubmitAsync(Guid userId, Guid moduleId, IReadOnlyList<int> answers, CancellationToken ct = default)
    {
        await EnsurePlayableAsync(userId, moduleId, ct);
        var quiz = await db.Quizzes.FirstOrDefaultAsync(q => q.ModuleId == moduleId && q.IsActive, ct)
            ?? throw new LearningException("Kuis tidak ditemukan.", 404);
        var questions = await db.QuizQuestions.Where(x => x.QuizId == quiz.Id).OrderBy(x => x.Id).ToListAsync(ct);
        if (questions.Count == 0) throw new LearningException("Kuis belum memiliki pertanyaan.", 400);

        var score = 0;
        for (var i = 0; i < questions.Count; i++)
            if (i < answers.Count && answers[i] == questions[i].CorrectIndex) score++;
        var passed = score >= quiz.PassThreshold;

        db.QuizAttempts.Add(new QuizAttempt { Id = Guid.CreateVersion7(), UserId = userId, QuizId = quiz.Id, Score = score, Passed = passed });
        await db.SaveChangesAsync(ct);

        var completed = passed && await completion.TryCompleteAsync(userId, moduleId, ct);
        return new QuizResultDto(score, questions.Count, passed, completed);
    }

    private async Task EnsurePlayableAsync(Guid userId, Guid moduleId, CancellationToken ct)
    {
        var m = await db.Modules.Where(x => x.Id == moduleId && x.Status == ModuleStatus.Published)
            .Select(x => new { x.IsPreview, x.RequiredPlanTier }).FirstOrDefaultAsync(ct)
            ?? throw new LearningException("Modul tidak ditemukan.", 404);
        if (m.IsPreview) return;
        var tier = await entitlement.GetActiveTierAsync(userId, ct);
        if (!Entitlement.CanAccess(tier, m.IsPreview, m.RequiredPlanTier))
            throw new LearningException("Anda belum memiliki akses ke modul ini.", 403);
    }

    private static List<string> Parse(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}
