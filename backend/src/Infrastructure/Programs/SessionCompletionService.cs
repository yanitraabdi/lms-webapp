using Academy.Application.Programs;
using Academy.Domain;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Programs;

/// <summary>
/// The ONLY component that completes a session and advances the linear lock (GR-8, KAK §9.3 R2).
/// Idempotent and non-retroactive: once a <see cref="SessionCompletion"/> row exists it is never
/// removed, so attaching a gating test to an already-complete session cannot un-complete it.
/// </summary>
public class SessionCompletionService(AppDbContext db) : ISessionCompletionService
{
    public Task<bool> IsCompleteAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
        => db.SessionCompletions.AnyAsync(c => c.UserId == userId && c.SessionId == sessionId, ct);

    public async Task<bool> TryCompleteAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        // Non-retroactive: already complete stays complete, whatever the rules say now.
        if (await IsCompleteAsync(userId, sessionId, ct)) return true;

        var session = await db.ProgramSessions
            .Where(s => s.Id == sessionId)
            .Select(s => new { s.Id, s.ProgramId, s.Type, s.AssessmentId, s.ScheduledAt })
            .FirstOrDefaultAsync(ct);
        if (session is null) return false;

        var (complete, method) = session.Type switch
        {
            SessionType.Video => (await IsVideoCompleteAsync(userId, session.Id, session.AssessmentId, ct),
                                  CompletionMethod.WatchAndTest),
            SessionType.Live => await IsLiveCompleteAsync(
                                      userId, session.Id, session.ProgramId, session.ScheduledAt, ct),
            SessionType.FinalAssessment => (await HasSubmittedAsync(userId, session.AssessmentId, ct),
                                  CompletionMethod.Submitted),
            _ => (false, CompletionMethod.Submitted),
        };
        if (!complete) return false;

        db.SessionCompletions.Add(new SessionCompletion
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            SessionId = sessionId,
            CompletedAt = DateTimeOffset.UtcNow,
            Method = method,
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // UNIQUE(user_id, session_id) — a concurrent request completed it first. Still complete.
        }
        return true;
    }

    /// <summary>Video: watched ≥ threshold AND (no gating assessment OR a passing attempt exists).</summary>
    private async Task<bool> IsVideoCompleteAsync(Guid userId, Guid sessionId, Guid? assessmentId, CancellationToken ct)
    {
        var percent = await db.WatchProgress
            .Where(w => w.UserId == userId && w.SessionId == sessionId)
            .Select(w => (decimal?)w.PercentComplete)
            .FirstOrDefaultAsync(ct) ?? 0m;

        var hasGatingTest = false;
        var gatingPassed = false;
        if (assessmentId is Guid aid)
        {
            // An assessment with no questions cannot gate anything.
            hasGatingTest = await db.AssessmentQuestions.AnyAsync(q => q.AssessmentId == aid, ct);
            if (hasGatingTest)
                gatingPassed = await db.Attempts.AnyAsync(
                    a => a.UserId == userId && a.AssessmentId == aid && a.SubmittedAt != null && a.Passed, ct);
        }

        return SessionAccess.IsSessionComplete(
            SessionType.Video, watchPercent: percent,
            hasGatingTest: hasGatingTest, gatingTestPassed: gatingPassed);
    }

    /// <summary>
    /// Live: admin-marked attended, or enrolled after the session had already started
    /// (<see cref="SessionAccess.EnrolledAfterLiveSession"/>). Attendance wins when both hold, so a
    /// learner an admin did mark is recorded as attended rather than as a late enrolment.
    /// </summary>
    private async Task<(bool Complete, CompletionMethod Method)> IsLiveCompleteAsync(
        Guid userId, Guid sessionId, Guid programId, DateTimeOffset? scheduledAt, CancellationToken ct)
    {
        if (await db.LiveAttendances.AnyAsync(
                a => a.SessionId == sessionId && a.UserId == userId && a.Attended, ct))
            return (true, CompletionMethod.Attended);

        var enrolledAt = await db.Enrollments
            .Where(e => e.UserId == userId && e.ProgramId == programId
                        && (e.Status == EnrollmentStatus.Active || e.Status == EnrollmentStatus.Completed))
            .Select(e => e.EnrolledAt)
            .FirstOrDefaultAsync(ct);

        return (SessionAccess.EnrolledAfterLiveSession(scheduledAt, enrolledAt), CompletionMethod.EnrolledAfterLive);
    }

    private async Task<bool> HasSubmittedAsync(Guid userId, Guid? assessmentId, CancellationToken ct)
        => assessmentId is Guid aid
           && await db.Attempts.AnyAsync(a => a.UserId == userId && a.AssessmentId == aid && a.SubmittedAt != null, ct);
}
