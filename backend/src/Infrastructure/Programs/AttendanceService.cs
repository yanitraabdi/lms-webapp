using System.Text.Json;
using Academy.Application.Programs;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Programs;

/// <summary>
/// Admin-marked live-session attendance (KAK §9.11). Marking attended is what completes a live
/// session, so this is one of the three completion paths feeding the linear lock (GR-8).
/// No Zoom API in v1 — attendance is a deliberate human step.
/// </summary>
public class AttendanceService(AppDbContext db, ISessionCompletionService completion) : IAttendanceService
{
    public async Task<AttendanceRosterDto> GetRosterAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.ProgramSessions
            .Where(s => s.Id == sessionId)
            .Select(s => new { s.Id, s.ProgramId, s.Title, s.Type, s.ScheduledAt, s.LiveMode, s.JoinUrl, s.Location })
            .FirstOrDefaultAsync(ct)
            ?? throw new ProgramException("Sesi tidak ditemukan.", 404);

        if (session.Type != SessionType.Live)
            throw new ProgramException("Sesi ini bukan sesi live.", 400);

        // Roster = everyone with access to the program. Enrollment has no User navigation,
        // so join explicitly rather than relying on one.
        var learners = await (
            from e in db.Enrollments
            join u in db.Users on e.UserId equals u.Id
            where e.ProgramId == session.ProgramId
                  && (e.Status == EnrollmentStatus.Active || e.Status == EnrollmentStatus.Completed)
            select new { e.UserId, u.Name, u.Email })
            .ToListAsync(ct);

        var attendance = await db.LiveAttendances
            .Where(a => a.SessionId == sessionId)
            .Select(a => new { a.UserId, a.Attended, a.UpdatedAt })
            .ToListAsync(ct);
        var byUser = attendance.ToDictionary(a => a.UserId);

        var completed = (await db.SessionCompletions
            .Where(c => c.SessionId == sessionId)
            .Select(c => c.UserId)
            .ToListAsync(ct)).ToHashSet();

        var rows = learners
            .OrderBy(l => l.Name)
            .Select(l => new AttendanceRowDto(
                l.UserId, l.Name, l.Email,
                byUser.TryGetValue(l.UserId, out var a) && a.Attended,
                byUser.TryGetValue(l.UserId, out var b) ? b.UpdatedAt : null,
                completed.Contains(l.UserId)))
            .ToList();

        return new AttendanceRosterDto(
            session.Id, session.Title, session.ScheduledAt,
            session.LiveMode?.ToString(), session.JoinUrl, session.Location,
            rows.Count, rows.Count(r => r.Attended), rows);
    }

    public async Task MarkAsync(
        Guid actor, Guid sessionId, IReadOnlyList<Guid> userIds, bool attended, CancellationToken ct = default)
    {
        var session = await db.ProgramSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new ProgramException("Sesi tidak ditemukan.", 404);
        if (session.Type != SessionType.Live)
            throw new ProgramException("Kehadiran hanya berlaku untuk sesi live.", 400);
        if (userIds.Count == 0) return;

        var existing = await db.LiveAttendances
            .Where(a => a.SessionId == sessionId && userIds.Contains(a.UserId))
            .ToListAsync(ct);
        var byUser = existing.ToDictionary(a => a.UserId);

        foreach (var userId in userIds.Distinct())
        {
            if (byUser.TryGetValue(userId, out var row))
            {
                row.Attended = attended;
                row.MarkedByUserId = actor;
                row.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                db.LiveAttendances.Add(new LiveAttendance
                {
                    Id = Guid.CreateVersion7(),
                    SessionId = sessionId,
                    UserId = userId,
                    Attended = attended,
                    MarkedByUserId = actor,
                });
            }
        }

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.CreateVersion7(),
            ActorUserId = actor,
            Action = attended ? "attendance_marked" : "attendance_unmarked",
            Target = sessionId.ToString(),
            Metadata = JsonSerializer.Serialize(new { count = userIds.Count }),
        });
        await db.SaveChangesAsync(ct);

        // Marking attended completes the session. Unmarking deliberately does NOT un-complete it —
        // completion is non-retroactive (GR-8); an admin error must not close a learner's progress.
        if (attended)
            foreach (var userId in userIds.Distinct())
                await completion.TryCompleteAsync(userId, sessionId, ct);
    }

    public async Task MarkAllAsync(Guid actor, Guid sessionId, CancellationToken ct = default)
    {
        var roster = await GetRosterAsync(sessionId, ct);
        await MarkAsync(actor, sessionId, roster.Rows.Select(r => r.UserId).ToList(), true, ct);
    }
}
