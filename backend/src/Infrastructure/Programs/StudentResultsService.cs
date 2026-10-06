using Academy.Application.Programs;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Programs;

public class StudentResultsService(AppDbContext db) : IStudentResultsService
{
    public async Task<IReadOnlyList<SessionAttemptDto>> ListSessionAttemptsAsync(Guid userId, CancellationToken ct = default)
        // Inner join on the owning Test part: an attempt on a test no longer attached to any part
        // has no row and is omitted (spec §4.1). Gating only — the final is the certificate's.
        => await (
            from a in db.Attempts
            where a.UserId == userId && a.SubmittedAt != null && a.Assessment.Kind == AssessmentKind.Gating
            join p in db.SessionParts on (Guid?)a.AssessmentId equals p.AssessmentId
            orderby a.SubmittedAt
            select new SessionAttemptDto(
                a.Id, p.Session.ProgramId,
                p.SessionId, p.Session.Title, p.Session.OrderIndex,
                p.Id, p.Title, p.OrderIndex,
                a.SubmittedAt!.Value, a.TotalScore, a.MaxScore, a.Passed))
            .ToListAsync(ct);
}
