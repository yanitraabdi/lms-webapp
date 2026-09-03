using Academy.Application.Admin;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Admin;

/// <summary>
/// Admin dashboard figures, all counted from INVERTA tables.
///
/// This service used to measure the archived subscription product: active subscriptions, a
/// breakdown per plan tier, and a most-watched list grouped by <c>ModuleId</c>. After the pivot
/// those numbers were frozen at whatever they held on the day it happened — and the most-watched
/// panel was outright broken, because <see cref="Domain.Entities.WatchProgress"/> carries both a
/// dormant <c>ModuleId</c> and INVERTA's <c>SessionId</c>, and every INVERTA row leaves the former
/// null. All of them collapsed into one bucket whose title resolved to "—".
/// </summary>
public class AdminAnalyticsService(AppDbContext db) : IAdminAnalyticsService
{
    private const int TopWatched = 5;

    public async Task<AdminAnalyticsDto> GetAsync(CancellationToken ct = default)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-30);

        var totalUsers = await db.Users.CountAsync(ct);
        var signups = await db.Users.CountAsync(u => u.CreatedAt >= since, ct);

        // Active OR Completed: both grant access, so both are people currently in the programme.
        // Revoked and PendingPayment are not, and a revoked row is retained rather than deleted
        // (GR-7), so counting every row would quietly overstate the figure for ever.
        var activeEnrollments = await db.Enrollments.CountAsync(
            e => e.Status == EnrollmentStatus.Active || e.Status == EnrollmentStatus.Completed, ct);

        var byStatusRaw = await db.Enrollments
            .GroupBy(e => e.Status)
            .Select(grp => new { Status = grp.Key, Count = grp.Count() })
            .ToListAsync(ct);
        var byStatus = byStatusRaw
            .OrderBy(x => x.Status)
            .Select(x => new EnrollmentStatusCountDto(x.Status.ToString(), x.Count))
            .ToList();

        // SessionCompletions, not WatchProgress.Completed. The latter mixes archived module
        // watching with INVERTA session watching, and completion is what the linear lock actually
        // turns on — a session completes by watching AND passing its gating test, or by attendance.
        var completions = await db.SessionCompletions.CountAsync(c => c.CompletedAt >= since, ct);

        // One shared certificates table: the archived per-level certificate and INVERTA's
        // attempt-keyed one both live here, so this counts both. Correct for a total.
        var certs = await db.Certificates.CountAsync(ct);

        var watch = await db.WatchProgress
            .Where(w => w.SessionId != null)
            .GroupBy(w => w.SessionId!.Value)
            .Select(grp => new { SessionId = grp.Key, Viewers = grp.Count() })
            .OrderByDescending(x => x.Viewers)
            .Take(TopWatched)
            .ToListAsync(ct);

        var ids = watch.Select(w => w.SessionId).ToList();
        var titles = await db.ProgramSessions.Where(s => ids.Contains(s.Id))
            .Select(s => new { s.Id, s.Title }).ToListAsync(ct);
        var mostWatched = watch
            .Select(w => new ModuleWatchDto(
                titles.FirstOrDefault(t => t.Id == w.SessionId)?.Title ?? "—", w.Viewers))
            .ToList();

        return new AdminAnalyticsDto(
            totalUsers, signups, activeEnrollments, byStatus, completions, certs, mostWatched);
    }
}
