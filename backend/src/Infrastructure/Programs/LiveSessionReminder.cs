using Academy.Application.Abstractions;
using Academy.Application.Programs;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Academy.Infrastructure.Programs;

/// <summary>
/// H-1 reminder for upcoming live sessions (KAK §9.11 R3). Idempotent: <c>reminder_sent_at</c> is
/// stamped before the first send, so a restart or an overlapping sweep can never double-email.
/// </summary>
public class LiveSessionReminder(
    AppDbContext db,
    IEmailSender email,
    ILogger<LiveSessionReminder> logger) : ILiveSessionReminder
{
    /// <summary>Send when the session starts within this window (i.e. "H-1").</summary>
    public static readonly TimeSpan Lead = TimeSpan.FromHours(24);

    public async Task<int> SendDueRemindersAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.Add(Lead);

        var due = await db.ProgramSessions
            .Where(s => s.Type == SessionType.Live
                        && s.ReminderSentAt == null
                        && s.ScheduledAt != null
                        && s.ScheduledAt > now            // not already started
                        && s.ScheduledAt <= cutoff)
            .Select(s => new
            {
                s.Id, s.ProgramId, s.Title, s.ScheduledAt, s.LiveMode, s.JoinUrl, s.Location,
                ProgramName = s.Program.Name,
            })
            .ToListAsync(ct);

        var sent = 0;

        foreach (var session in due)
        {
            // Claim the session FIRST so a concurrent sweep or a crash mid-send cannot re-send.
            var rows = await db.ProgramSessions
                .Where(s => s.Id == session.Id && s.ReminderSentAt == null)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.ReminderSentAt, now), ct);
            if (rows == 0) continue;                       // another worker claimed it

            var recipients = await (
                from e in db.Enrollments
                join u in db.Users on e.UserId equals u.Id
                where e.ProgramId == session.ProgramId
                      && (e.Status == EnrollmentStatus.Active || e.Status == EnrollmentStatus.Completed)
                select new { u.Email, u.Name })
                .ToListAsync(ct);

            foreach (var r in recipients)
            {
                try
                {
                    await email.SendLiveSessionReminderAsync(
                        r.Email, r.Name, session.ProgramName, session.Title,
                        session.ScheduledAt!.Value, session.JoinUrl, session.Location, ct);
                    sent++;
                }
                catch (Exception ex)
                {
                    // One bad address must not block the rest of the cohort.
                    logger.LogWarning(ex, "Live reminder failed for {Email} (session {SessionId}).",
                        r.Email, session.Id);
                }
            }

            logger.LogInformation(
                "Live reminder sent for session {SessionId} to {Count} learners.", session.Id, recipients.Count);
        }

        return sent;
    }
}
