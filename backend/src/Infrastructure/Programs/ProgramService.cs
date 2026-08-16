using Academy.Application.Programs;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Programs;

public class ProgramService(AppDbContext db) : IProgramService
{
    public async Task<PublicProgramDto?> GetPublicAsync(string slug, CancellationToken ct = default)
    {
        var p = await db.Programs
            .Where(x => x.Slug == slug && x.Status == ProgramStatus.Published)
            .Select(x => new { x.Id, x.Name, x.Slug, x.Description, x.Summary, x.PriceIdr })
            .FirstOrDefaultAsync(ct);
        if (p is null) return null;

        // Public syllabus: titles, order and schedule only. No asset ids, no join URLs (KAK §9.5).
        var sessions = await db.ProgramSessions
            .Where(s => s.ProgramId == p.Id)
            .OrderBy(s => s.OrderIndex)
            .Select(s => new PublicSessionDto(
                s.Id, s.OrderIndex, s.Type.ToString(), s.Title, s.Description,
                s.DurationSeconds, s.ScheduledAt, s.LiveMode == null ? null : s.LiveMode.ToString()))
            .ToListAsync(ct);

        return new PublicProgramDto(
            p.Id, p.Name, p.Slug, p.Description, p.Summary, p.PriceIdr,
            sessions.Count, sessions.Sum(s => s.DurationSeconds ?? 0), sessions);
    }

    public async Task<StudentProgramDto> GetForStudentAsync(Guid userId, Guid programId, CancellationToken ct = default)
    {
        var enrollment = await db.Enrollments
            .Where(e => e.UserId == userId && e.ProgramId == programId)
            .Select(e => new { e.Status, e.BatchId })
            .FirstOrDefaultAsync(ct)
            ?? throw new ProgramException("Anda belum terdaftar pada program ini.", 403);

        if (enrollment.Status is not (EnrollmentStatus.Active or EnrollmentStatus.Completed))
            throw new ProgramException("Pendaftaran Anda belum aktif.", 403);

        var program = await db.Programs
            .Where(p => p.Id == programId)
            .Select(p => new { p.Id, p.Name, p.Slug })
            .FirstOrDefaultAsync(ct)
            ?? throw new ProgramException("Program tidak ditemukan.", 404);

        var batch = enrollment.BatchId is Guid bid
            ? await db.ProgramBatches.Where(b => b.Id == bid)
                .Select(b => new { b.Id, b.Name, b.StartDate }).FirstOrDefaultAsync(ct)
            : null;

        var rows = await db.ProgramSessions
            .Where(s => s.ProgramId == programId)
            .OrderBy(s => s.OrderIndex)
            .Select(s => new
            {
                s.Id, s.OrderIndex, s.Type, s.Title, s.Description, s.DurationSeconds,
                s.ScheduledAt, s.LiveMode, s.JoinUrl, s.Location, s.AssessmentId,
                HasAssessment = s.AssessmentId != null
                                && db.AssessmentQuestions.Any(q => q.AssessmentId == s.AssessmentId),
            })
            .ToListAsync(ct);

        var completed = (await db.SessionCompletions
            .Where(c => c.UserId == userId)
            .Select(c => c.SessionId)
            .ToListAsync(ct)).ToHashSet();

        var progress = await db.WatchProgress
            .Where(w => w.UserId == userId && w.SessionId != null)
            .Select(w => new { SessionId = w.SessionId!.Value, w.PercentComplete })
            .ToListAsync(ct);
        var percentBySession = progress.ToDictionary(x => x.SessionId, x => x.PercentComplete);

        // Linear lock: walk in order — a session is available only once its predecessor is complete.
        var sessions = new List<StudentSessionDto>(rows.Count);
        var previousComplete = true;                 // the first session is always unlocked
        Guid? nextSessionId = null;

        foreach (var s in rows)
        {
            var isComplete = completed.Contains(s.Id);
            var state = isComplete ? SessionState.Completed
                      : previousComplete ? SessionState.Available
                      : SessionState.Locked;

            if (state == SessionState.Available && nextSessionId is null) nextSessionId = s.Id;

            var unlocked = state != SessionState.Locked;
            sessions.Add(new StudentSessionDto(
                s.Id, s.OrderIndex, s.Type.ToString(), s.Title, s.Description, s.DurationSeconds,
                s.ScheduledAt, s.LiveMode?.ToString(),
                unlocked ? s.JoinUrl : null,        // live details withheld while locked
                unlocked ? s.Location : null,
                state,
                percentBySession.TryGetValue(s.Id, out var pct) ? pct : 0m,
                s.HasAssessment));

            previousComplete = isComplete;
        }

        return new StudentProgramDto(
            program.Id, program.Name, program.Slug, enrollment.Status.ToString(),
            batch?.Id, batch?.Name, batch?.StartDate,
            sessions.Count(s => s.State == SessionState.Completed), sessions.Count,
            nextSessionId, sessions);
    }
}
