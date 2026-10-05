using Academy.Application.Programs;
using Academy.Domain;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Programs;

/// <summary>
/// THE GATE (GR-1, KAK §9.3). Server-side only; every protected resource calls it.
/// <c>canAccess = isEnrolled(program) AND (first session OR previous session complete)</c>.
/// </summary>
public class SessionAccessService(AppDbContext db, ISessionCompletionService completion, SessionPartStates partStates) : ISessionAccessService
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

        var previous = await db.ProgramSessions
            .Where(s => s.ProgramId == session.ProgramId && s.OrderIndex < session.OrderIndex)
            .OrderByDescending(s => s.OrderIndex)
            .Select(s => new { s.Id, s.Type })
            .FirstOrDefaultAsync(ct);
        if (previous is null) return true;     // defensive: nothing before it ⇒ treat as first

        var previousDone = await db.SessionCompletions
            .AnyAsync(c => c.UserId == userId && c.SessionId == previous.Id, ct);

        // A live session the learner enrolled after has no attendance to wait for, so it may
        // complete on sight. TryCompleteAsync re-checks the rule and records it — the completion
        // service stays the only thing that completes a session (GR-8). Live only: being asked
        // about access must not start completing other session types as a side effect.
        if (!previousDone && previous.Type == SessionType.Live)
            previousDone = await completion.TryCompleteAsync(userId, previous.Id, ct);

        return SessionAccess.CanAccess(true, isFirstSession: false, previousCompleted: previousDone);
    }

    public async Task EnsureAccessAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        if (!await CanAccessAsync(userId, sessionId, ct))
            throw new ProgramException("Anda belum memiliki akses ke sesi ini.", 403);
    }

    public async Task<bool> CanAccessPartAsync(Guid userId, Guid sessionId, Guid partId, CancellationToken ct = default)
    {
        if (!await db.SessionParts.AnyAsync(p => p.Id == partId && p.SessionId == sessionId, ct)) return false;
        if (!await CanAccessAsync(userId, sessionId, ct)) return false;
        var states = await partStates.LoadAsync(userId, sessionId, ct);
        return states.Any(s => s.Part.Id == partId && s.Status != PartStatus.Locked);
    }

    public async Task<SessionPart> EnsurePartAccessAsync(Guid userId, Guid sessionId, Guid partId, CancellationToken ct = default)
    {
        var part = await db.SessionParts.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == partId && p.SessionId == sessionId, ct)
            ?? throw new ProgramException("Bagian tidak ditemukan.", 404);
        await EnsureAccessAsync(userId, sessionId, ct);
        var states = await partStates.LoadAsync(userId, sessionId, ct);
        var state = states.FirstOrDefault(s => s.Part.Id == partId)
            ?? throw new ProgramException("Bagian tidak ditemukan.", 404); // deleted concurrently
        if (state.Status == PartStatus.Locked)
            throw new ProgramException("Bagian ini masih terkunci.", 403);
        return part;
    }
}
