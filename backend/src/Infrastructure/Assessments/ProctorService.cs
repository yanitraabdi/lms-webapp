using System.Text.Json;
using Academy.Application.Assessments;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Assessments;

/// <summary>
/// Soft proctoring (KAK §9.8). The client REPORTS; this service DECIDES (GR-13).
/// Strike 1 warns, strike 2 auto-submits and flags. Every event is persisted for dispute review,
/// and an admin reinstate clears the flag without erasing the trail.
///
/// Honest limitation, carried into the UI copy: this is deterrence, not exam security. It is
/// client-side, defeatable with a second device, and cannot prevent a learner leaving the page.
/// </summary>
public class ProctorService(AppDbContext db, IFinalAssessmentService finals) : IProctorService
{
    /// <summary>Focus losses shorter than this are ignored — OS notifications, accidental focus.</summary>
    public const int GraceMs = 2000;

    public async Task<ProctorStateDto> ReportAsync(
        Guid userId, Guid attemptId, string kind, int? durationMs, CancellationToken ct = default)
    {
        var attempt = await db.Attempts.FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, ct)
            ?? throw new AssessmentException("Percobaan tidak ditemukan.", 404);

        var config = AssessmentService.ParseConfig(
            await db.Assessments.Where(a => a.Id == attempt.AssessmentId).Select(a => a.Config).FirstAsync(ct));

        var reported = Enum.TryParse<ProctorEventKind>(kind, true, out var k) ? k : ProctorEventKind.WindowBlur;

        // Grace rule (KAK §9.8.1): a sub-2s focus loss, or a non-strike kind, is still RECORDED —
        // stored under `Ignored` so the dispute trail is complete while the count stays clean.
        var counts = config.ProctoringEnabled
                     && attempt.SubmittedAt is null
                     && FinalAssessmentService.IsStrike(reported)
                     && !IsBelowGrace(durationMs);

        db.ProctorEvents.Add(new ProctorEvent
        {
            Id = Guid.CreateVersion7(),
            AttemptId = attempt.Id,
            Kind = counts ? reported : ProctorEventKind.Ignored,
            OccurredAt = DateTimeOffset.UtcNow,          // SERVER-stamped
            ClientMeta = JsonSerializer.Serialize(new { durationMs, reported = kind, counted = counts }),
        });
        await db.SaveChangesAsync(ct);

        if (!counts)
            return new ProctorStateDto(
                await CountStrikesAsync(attempt.Id, ct), FinalAssessmentService.StrikeLimit,
                "none", attempt.ProctorFlagged);

        var strikes = await CountStrikesAsync(attempt.Id, ct);

        if (strikes < FinalAssessmentService.StrikeLimit)
        {
            db.ProctorEvents.Add(Decision(attempt.Id, ProctorEventKind.Warned, strikes));
            await db.SaveChangesAsync(ct);
            return new ProctorStateDto(strikes, FinalAssessmentService.StrikeLimit, "warn", attempt.ProctorFlagged);
        }

        // Strike 2 → the SERVER ends the sitting and flags it.
        attempt.ProctorFlagged = true;
        db.ProctorEvents.Add(Decision(attempt.Id, ProctorEventKind.AutoSubmitted, strikes));
        await db.SaveChangesAsync(ct);

        await finals.SubmitAsync(userId, attempt.Id, ct);

        return new ProctorStateDto(strikes, FinalAssessmentService.StrikeLimit, "autoSubmit", true);
    }

    public async Task ReinstateAsync(Guid actor, Guid attemptId, CancellationToken ct = default)
    {
        var attempt = await db.Attempts.FirstOrDefaultAsync(a => a.Id == attemptId, ct)
            ?? throw new AssessmentException("Percobaan tidak ditemukan.", 404);

        attempt.ProctorFlagged = false;
        attempt.Reinstated = true;
        // NOTE: proctor_events are deliberately NOT deleted — the trail survives reinstatement.

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.CreateVersion7(),
            ActorUserId = actor,
            Action = "attempt_reinstated",
            Target = attemptId.ToString(),
            Metadata = JsonSerializer.Serialize(new { attempt.UserId, attempt.AssessmentId }),
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Only counting kinds are strikes; `Ignored` and server decisions never count.</summary>
    private Task<int> CountStrikesAsync(Guid attemptId, CancellationToken ct)
        => db.ProctorEvents
            .Where(e => e.AttemptId == attemptId
                        && (e.Kind == ProctorEventKind.VisibilityHidden || e.Kind == ProctorEventKind.WindowBlur))
            .CountAsync(ct);

    private static bool IsBelowGrace(int? durationMs) => durationMs is int ms && ms < GraceMs;

    private static ProctorEvent Decision(Guid attemptId, ProctorEventKind kind, int strikes) => new()
    {
        Id = Guid.CreateVersion7(),
        AttemptId = attemptId,
        Kind = kind,
        OccurredAt = DateTimeOffset.UtcNow,
        ClientMeta = JsonSerializer.Serialize(new { strikes, decidedBy = "server" }),
    };
}
