using Academy.Domain;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Assessments;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Programs;

public record PartState(
    SessionPart Part, PartFacts Facts, PartStatus Status,
    int ResumePositionSeconds, decimal PercentComplete, int AttemptsUsed);

/// <summary>
/// Turns one learner's retained records (watch_progress, attempts) into part statuses via the pure
/// rule in <see cref="SessionParts"/>. The ONE place this is computed: the gate, the completion
/// service and the session view all read it, so the rule cannot drift between them.
/// </summary>
public class SessionPartStates(AppDbContext db)
{
    public async Task<IReadOnlyList<PartState>> LoadAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var parts = await db.SessionParts.AsNoTracking()
            .Where(p => p.SessionId == sessionId)
            .OrderBy(p => p.OrderIndex)
            .ToListAsync(ct);
        if (parts.Count == 0) return [];

        var partIds = parts.Select(p => p.Id).ToList();
        var progress = await db.WatchProgress
            .Where(w => w.UserId == userId && w.PartId != null && partIds.Contains(w.PartId.Value))
            .Select(w => new { PartId = w.PartId!.Value, w.PercentComplete, w.ResumePositionSeconds })
            .ToDictionaryAsync(w => w.PartId, ct);

        var testIds = parts.Where(p => p.AssessmentId != null).Select(p => p.AssessmentId!.Value).ToList();
        var attempts = await db.Attempts
            .Where(a => a.UserId == userId && testIds.Contains(a.AssessmentId) && a.SubmittedAt != null)
            .GroupBy(a => a.AssessmentId)
            .Select(g => new { Id = g.Key, Used = g.Count(), Passed = g.Any(a => a.Passed) })
            .ToDictionaryAsync(x => x.Id, ct);
        var withQuestions = (await db.AssessmentQuestions
            .Where(q => testIds.Contains(q.AssessmentId))
            .Select(q => q.AssessmentId).Distinct().ToListAsync(ct)).ToHashSet();
        var discussionAfter = (await db.Assessments
            .Where(a => testIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Config }).ToListAsync(ct))
            .ToDictionary(a => a.Id, a => AssessmentService.ParseConfig(a.Config).DiscussionAfterFailures);

        var facts = new List<PartFacts>(parts.Count);
        foreach (var p in parts)
        {
            if (p.Kind == SessionPartKind.Test && p.AssessmentId is Guid aid)
            {
                attempts.TryGetValue(aid, out var a);
                var used = a?.Used ?? 0;
                var passed = a?.Passed ?? false;
                // A test with no questions cannot gate anything (same rule as before parts).
                var done = passed || !withQuestions.Contains(aid);
                var failed = used - (passed ? 1 : 0);
                facts.Add(new PartFacts(p.Kind, done, Math.Max(0, failed), discussionAfter.GetValueOrDefault(aid)));
            }
            else
            {
                var pct = progress.TryGetValue(p.Id, out var w) ? w.PercentComplete : 0m;
                facts.Add(new PartFacts(p.Kind, CompletionPolicy.IsModuleComplete(pct)));
            }
        }

        var statuses = SessionParts.Statuses(facts);
        return parts.Select((p, i) =>
        {
            progress.TryGetValue(p.Id, out var w);
            var used = p.AssessmentId is Guid aid && attempts.TryGetValue(aid, out var a) ? a.Used : 0;
            return new PartState(p, facts[i], statuses[i], w?.ResumePositionSeconds ?? 0, w?.PercentComplete ?? 0m, used);
        }).ToList();
    }
}
