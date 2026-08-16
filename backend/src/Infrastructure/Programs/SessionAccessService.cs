using Academy.Application.Programs;
using Academy.Domain;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Programs;

/// <summary>
/// THE GATE (GR-1, KAK §9.3). Server-side only; every protected resource calls it.
/// <c>canAccess = isEnrolled(program) AND (first session OR previous session complete)</c>.
/// </summary>
public class SessionAccessService(AppDbContext db) : ISessionAccessService
{
    public async Task<bool> CanAccessAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.ProgramSessions
            .Where(s => s.Id == sessionId)
            .Select(s => new { s.Id, s.ProgramId, s.OrderIndex })
            .FirstOrDefaultAsync(ct);
        if (session is null) return false;

        // 1. Enrolled? Only paid states grant access.
        var enrolled = await db.Enrollments.AnyAsync(
            e => e.UserId == userId && e.ProgramId == session.ProgramId
                 && (e.Status == EnrollmentStatus.Active || e.Status == EnrollmentStatus.Completed), ct);
        if (!enrolled) return false;

        // 2. Unlocked? The first session is always open; others need their predecessor complete.
        var firstOrder = await db.ProgramSessions
            .Where(s => s.ProgramId == session.ProgramId)
            .MinAsync(s => (int?)s.OrderIndex, ct);
        var isFirst = firstOrder is not null && session.OrderIndex == firstOrder.Value;
        if (isFirst) return SessionAccess.CanAccess(true, isFirstSession: true, previousCompleted: false);

        var previousId = await db.ProgramSessions
            .Where(s => s.ProgramId == session.ProgramId && s.OrderIndex < session.OrderIndex)
            .OrderByDescending(s => s.OrderIndex)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(ct);
        if (previousId is null) return true;   // defensive: nothing before it ⇒ treat as first

        var previousDone = await db.SessionCompletions
            .AnyAsync(c => c.UserId == userId && c.SessionId == previousId.Value, ct);

        return SessionAccess.CanAccess(true, isFirstSession: false, previousCompleted: previousDone);
    }

    public async Task EnsureAccessAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        if (!await CanAccessAsync(userId, sessionId, ct))
            throw new ProgramException("Anda belum memiliki akses ke sesi ini.", 403);
    }
}
