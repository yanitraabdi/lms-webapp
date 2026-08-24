using System.Text.Json;
using Academy.Application.Programs;
using Academy.Domain;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Programs;

/// <summary>
/// Operational dashboards so the product can be run without engineering (KAK §9.12):
/// enrollments (with a support grant path) and attempts (with the proctor trail for disputes).
/// </summary>
public class AdminOperationsService(AppDbContext db) : IAdminOperationsService
{
    public async Task<AdminEnrollmentListDto> ListEnrollmentsAsync(
        string? search, string? status, Guid? programId, int skip, int take, CancellationToken ct = default)
    {
        var q = from e in db.Enrollments
                join u in db.Users on e.UserId equals u.Id
                select new { e, u };

        if (programId is Guid pid) q = q.Where(x => x.e.ProgramId == pid);
        if (Enum.TryParse<EnrollmentStatus>(status, true, out var parsed))
            q = q.Where(x => x.e.Status == parsed);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            q = q.Where(x => EF.Functions.ILike(x.u.Name, term) || EF.Functions.ILike(x.u.Email, term));
        }

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(x => x.e.CreatedAt)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 100))
            .Select(x => new
            {
                x.e.Id, x.e.UserId, x.u.Name, x.u.Email, x.e.ProgramId,
                ProgramName = x.e.Program.Name, x.e.Status, x.e.AmountPaidIdr, x.e.EnrolledAt,
                TotalSessions = db.ProgramSessions.Count(s => s.ProgramId == x.e.ProgramId),
                CompletedSessions = db.SessionCompletions.Count(
                    c => c.UserId == x.e.UserId && c.Session.ProgramId == x.e.ProgramId),
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new AdminEnrollmentDto(
            r.Id, r.UserId, r.Name, r.Email, r.ProgramId, r.ProgramName, r.Status.ToString(),
            r.AmountPaidIdr, r.EnrolledAt, r.CompletedSessions, r.TotalSessions)).ToList();

        return new AdminEnrollmentListDto(items, total);
    }

    public async Task GrantEnrollmentAsync(Guid actor, GrantEnrollmentRequest req, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.Trim(), ct)
            ?? throw new ProgramException("Pengguna dengan email tersebut tidak ditemukan.", 404);

        if (!await db.Programs.AnyAsync(p => p.Id == req.ProgramId, ct))
            throw new ProgramException("Program tidak ditemukan.", 404);

        var enrollment = await db.Enrollments
            .FirstOrDefaultAsync(e => e.UserId == user.Id && e.ProgramId == req.ProgramId, ct);

        if (enrollment is null)
        {
            enrollment = new Enrollment
            {
                Id = Guid.CreateVersion7(),
                UserId = user.Id,
                ProgramId = req.ProgramId,
                AmountPaidIdr = 0m,               // comp / support grant, not a payment
            };
            db.Enrollments.Add(enrollment);
        }
        else if (enrollment.GrantsAccess)
        {
            throw new ProgramException("Pengguna sudah terdaftar aktif pada program ini.", 409);
        }

        enrollment.Status = EnrollmentStatus.Active;
        enrollment.EnrolledAt ??= DateTimeOffset.UtcNow;

        // Recorded explicitly: this is the ONE path that grants access without a payment webhook,
        // so it must be visible in the audit trail (GR-2's support-path exception).
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.CreateVersion7(),
            ActorUserId = actor,
            Action = "enrollment_granted_manually",
            Target = enrollment.Id.ToString(),
            Metadata = JsonSerializer.Serialize(new { user.Email, req.ProgramId }),
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<AdminAttemptListDto> ListAttemptsAsync(
        bool? flaggedOnly, Guid? programId, int skip, int take, CancellationToken ct = default)
    {
        var q = from a in db.Attempts
                join u in db.Users on a.UserId equals u.Id
                select new { a, u };

        if (flaggedOnly == true) q = q.Where(x => x.a.ProctorFlagged);
        if (programId is Guid pid)
            q = q.Where(x => db.ProgramSessions.Any(
                s => s.AssessmentId == x.a.AssessmentId && s.ProgramId == pid));

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(x => x.a.StartedAt)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 100))
            .Select(x => new
            {
                x.a.Id, x.a.UserId, x.u.Name, x.u.Email, x.a.AssessmentId,
                AssessmentTitle = x.a.Assessment.Title, Kind = x.a.Assessment.Kind,
                x.a.StartedAt, x.a.SubmittedAt, x.a.AutoSubmitted, x.a.ProctorFlagged, x.a.Reinstated,
                x.a.TotalScore, x.a.MaxScore, x.a.Passed,
                Cert = db.Certificates.Where(c => c.AttemptId == x.a.Id)
                    .Select(c => new { c.TotalScaledScore, c.PredictedBand }).FirstOrDefault(),
                Strikes = db.ProctorEvents.Count(
                    e => e.AttemptId == x.a.Id
                         && (e.Kind == ProctorEventKind.VisibilityHidden || e.Kind == ProctorEventKind.WindowBlur)),
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new AdminAttemptDto(
            r.Id, r.UserId, r.Name, r.Email, r.AssessmentId, r.AssessmentTitle, r.Kind.ToString(),
            r.StartedAt, r.SubmittedAt, r.AutoSubmitted, r.ProctorFlagged, r.Reinstated,
            r.TotalScore, r.MaxScore, r.Passed,
            r.Cert?.TotalScaledScore, r.Cert?.PredictedBand, r.Strikes)).ToList();

        return new AdminAttemptListDto(items, total);
    }

    public async Task<AdminAttemptDetailDto?> GetAttemptAsync(Guid attemptId, CancellationToken ct = default)
    {
        var list = await ListSingleAsync(attemptId, ct);
        if (list is null) return null;

        // The FULL trail, including events that were ignored by the grace rule — that context is
        // exactly what makes a dispute reviewable (KAK §9.8.2).
        var events = await db.ProctorEvents
            .Where(e => e.AttemptId == attemptId)
            .OrderBy(e => e.OccurredAt)
            .Select(e => new ProctorEventDto(e.Id, e.Kind.ToString(), e.OccurredAt, e.ClientMeta))
            .ToListAsync(ct);

        var sectionScores = await db.Attempts
            .Where(a => a.Id == attemptId).Select(a => a.SectionScores).FirstAsync(ct);

        return new AdminAttemptDetailDto(list, events, ParseInts(sectionScores));
    }

    private async Task<AdminAttemptDto?> ListSingleAsync(Guid attemptId, CancellationToken ct)
    {
        var r = await (from a in db.Attempts
                       join u in db.Users on a.UserId equals u.Id
                       where a.Id == attemptId
                       select new
                       {
                           a.Id, a.UserId, u.Name, u.Email, a.AssessmentId,
                           AssessmentTitle = a.Assessment.Title, Kind = a.Assessment.Kind,
                           a.StartedAt, a.SubmittedAt, a.AutoSubmitted, a.ProctorFlagged, a.Reinstated,
                           a.TotalScore, a.MaxScore, a.Passed,
                           Cert = db.Certificates.Where(c => c.AttemptId == a.Id)
                               .Select(c => new { c.TotalScaledScore, c.PredictedBand }).FirstOrDefault(),
                           Strikes = db.ProctorEvents.Count(
                               e => e.AttemptId == a.Id
                                    && (e.Kind == ProctorEventKind.VisibilityHidden
                                        || e.Kind == ProctorEventKind.WindowBlur)),
                       }).FirstOrDefaultAsync(ct);

        return r is null ? null : new AdminAttemptDto(
            r.Id, r.UserId, r.Name, r.Email, r.AssessmentId, r.AssessmentTitle, r.Kind.ToString(),
            r.StartedAt, r.SubmittedAt, r.AutoSubmitted, r.ProctorFlagged, r.Reinstated,
            r.TotalScore, r.MaxScore, r.Passed,
            r.Cert?.TotalScaledScore, r.Cert?.PredictedBand, r.Strikes);
    }

    private static Dictionary<string, int> ParseInts(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? []; }
        catch { return []; }
    }
}
