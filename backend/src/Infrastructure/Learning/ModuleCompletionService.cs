using Academy.Application.Learning;
using Academy.Domain;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Learning;

public class ModuleCompletionService(AppDbContext db, ICertificateService certificates) : IModuleCompletionService
{
    public async Task<bool> TryCompleteAsync(Guid userId, Guid moduleId, CancellationToken ct = default)
    {
        var wp = await db.WatchProgress.FirstOrDefaultAsync(w => w.UserId == userId && w.ModuleId == moduleId, ct);
        if (wp is null) return false;
        if (wp.Completed) return true;                       // non-retroactive — never un-complete
        if (!CompletionPolicy.IsModuleComplete(wp.PercentComplete)) return false;

        // Quiz gate: a module with an active quiz that has questions needs a passing attempt.
        var quiz = await db.Quizzes
            .Where(q => q.ModuleId == moduleId && q.IsActive)
            .Select(q => new { q.Id, HasQuestions = db.QuizQuestions.Any(x => x.QuizId == q.Id) })
            .FirstOrDefaultAsync(ct);
        if (quiz is { HasQuestions: true })
        {
            var passed = await db.QuizAttempts.AnyAsync(a => a.UserId == userId && a.QuizId == quiz.Id && a.Passed, ct);
            if (!passed) return false;
        }

        wp.Completed = true;
        wp.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var levelId = await db.Modules.Where(m => m.Id == moduleId).Select(m => m.Track.LevelId).FirstAsync(ct);
        await certificates.TryIssueForLevelAsync(userId, levelId, ct);
        return true;
    }
}
