using System.Text.Json;
using Academy.Application.Programs;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Learning;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Programs;

public class SessionPartAdminService(AppDbContext db, VideoOptions video) : ISessionPartAdminService
{
    public async Task<IReadOnlyList<AdminSessionPartDto>> ListAsync(Guid sessionId, CancellationToken ct = default)
    {
        var parts = await db.SessionParts.Where(p => p.SessionId == sessionId)
            .OrderBy(p => p.OrderIndex)
            .Select(p => new
            {
                p.Id, p.OrderIndex, p.Kind, p.Title, p.ProviderAssetId, p.DurationSeconds, p.AssessmentId,
                AssessmentTitle = p.Assessment != null ? p.Assessment.Title : null,
                HasLearnerData = db.WatchProgress.Any(w => w.PartId == p.Id)
                    || (p.AssessmentId != null && db.Attempts.Any(a => a.AssessmentId == p.AssessmentId)),
            })
            .ToListAsync(ct);
        return parts.Select(p => new AdminSessionPartDto(
            p.Id, p.OrderIndex, p.Kind.ToString(), p.Title, p.ProviderAssetId, p.DurationSeconds,
            p.AssessmentId, p.AssessmentTitle, p.HasLearnerData)).ToList();
    }

    public async Task<IReadOnlyList<AdminSessionPartDto>> SaveAsync(
        Guid actor, Guid sessionId, SaveSessionPartsRequest req, CancellationToken ct = default)
    {
        var session = await db.ProgramSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new ProgramException("Sesi tidak ditemukan.", 404);
        if (session.Type != SessionType.Video)
            throw new ProgramException("Hanya sesi video yang memiliki bagian.", 400);
        if (req.Parts.Count == 0)
            throw new ProgramException("Sesi video harus memiliki minimal satu bagian.", 400);

        var inputs = req.Parts.Select(Parse).ToList();
        await ValidateShapeAsync(sessionId, inputs, ct);

        var existing = await db.SessionParts.Where(p => p.SessionId == sessionId).ToListAsync(ct);
        var byId = existing.ToDictionary(p => p.Id);
        if (inputs.Any(i => i.Id is Guid id && !byId.ContainsKey(id)))
            throw new ProgramException("Bagian tidak dikenal untuk sesi ini.", 400);

        var keptIds = inputs.Where(i => i.Id != null).Select(i => i.Id!.Value).ToHashSet();
        foreach (var gone in existing.Where(p => !keptIds.Contains(p.Id)))
        {
            if (await HasLearnerDataAsync(gone, ct))
                throw new ProgramException(
                    $"Bagian \"{gone.Title}\" tidak bisa dihapus karena sudah memiliki progres peserta.", 409);
            db.SessionParts.Remove(gone);
        }

        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            SessionPart part;
            if (input.Id is Guid id)
            {
                part = byId[id];
                if ((part.Kind != input.Kind || part.AssessmentId != input.AssessmentId)
                    && await HasLearnerDataAsync(part, ct))
                    throw new ProgramException(
                        $"Jenis atau tes bagian \"{part.Title}\" tidak bisa diubah karena sudah memiliki progres peserta.", 409);
            }
            else
            {
                part = new SessionPart { Id = Guid.CreateVersion7(), SessionId = sessionId };
                db.SessionParts.Add(part);
            }
            part.OrderIndex = i + 1;
            part.Kind = input.Kind;
            part.Title = input.Title;
            part.ProviderAssetId = input.Kind == SessionPartKind.Test ? null : input.ProviderAssetId;
            part.DurationSeconds = input.Kind == SessionPartKind.Test ? null : input.DurationSeconds;
            part.AssessmentId = input.Kind == SessionPartKind.Test ? input.AssessmentId : null;
            part.UpdatedAt = DateTimeOffset.UtcNow;
        }

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.CreateVersion7(), ActorUserId = actor, Action = "session_parts_saved",
            Target = sessionId.ToString(),
            Metadata = JsonSerializer.Serialize(new { count = inputs.Count }),
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex, DbErrors.SessionPartAssessmentIndex))
        {
            // Another session took this test between the check and the save. Any OTHER db error
            // propagates — it must not be reported as "already used".
            throw new ProgramException("Tes ini sudah dipakai di sesi lain.", 409);
        }
        return await ListAsync(sessionId, ct);
    }

    private record Input(Guid? Id, SessionPartKind Kind, string Title, string? ProviderAssetId, int? DurationSeconds, Guid? AssessmentId);

    private static Input Parse(SessionPartInput r)
    {
        if (!Enum.TryParse<SessionPartKind>(r.Kind, true, out var kind))
            throw new ProgramException("Jenis bagian tidak dikenal.", 400);
        if (string.IsNullOrWhiteSpace(r.Title))
            throw new ProgramException("Judul bagian wajib diisi.", 400);
        // Fields that don't belong to the kind are dropped here so no later check sees them.
        var isTest = kind == SessionPartKind.Test;
        return new Input(r.Id, kind, r.Title.Trim(),
            isTest ? null : r.ProviderAssetId?.Trim(), isTest ? null : r.DurationSeconds,
            isTest ? r.AssessmentId : null);
    }

    private async Task ValidateShapeAsync(Guid sessionId, List<Input> inputs, CancellationToken ct)
    {
        for (var i = 0; i < inputs.Count; i++)
        {
            var p = inputs[i];
            if (p.Kind == SessionPartKind.Discussion
                && (i == 0 || inputs[i - 1].Kind != SessionPartKind.Test))
                throw new ProgramException("Video pembahasan harus tepat setelah sebuah tes.", 400);

            if (p.Kind != SessionPartKind.Test)
            {
                if (string.IsNullOrWhiteSpace(p.ProviderAssetId))
                    throw new ProgramException("Bagian video memerlukan video.", 400);
                ProgramAdminService.ValidateVideoAsset(SessionType.Video, p.ProviderAssetId, video);
                continue;
            }

            if (p.AssessmentId is not Guid aid)
                throw new ProgramException("Bagian tes harus memakai tes sesi (bukan tes akhir).", 400);
            var kind = await db.Assessments.Where(a => a.Id == aid).Select(a => (AssessmentKind?)a.Kind).FirstOrDefaultAsync(ct);
            if (kind != AssessmentKind.Gating)
                throw new ProgramException("Bagian tes harus memakai tes sesi (bukan tes akhir).", 400);
            if (await db.SessionParts.AnyAsync(x => x.AssessmentId == aid && x.SessionId != sessionId, ct))
                throw new ProgramException("Tes ini sudah dipakai di sesi lain.", 409);
        }

        var tests = inputs.Where(i => i.AssessmentId != null).Select(i => i.AssessmentId!.Value).ToList();
        if (tests.Count != tests.Distinct().Count())
            throw new ProgramException("Satu tes hanya boleh dipakai di satu bagian.", 400);
    }

    private async Task<bool> HasLearnerDataAsync(SessionPart p, CancellationToken ct)
        => await db.WatchProgress.AnyAsync(w => w.PartId == p.Id, ct)
           || (p.AssessmentId is Guid aid && await db.Attempts.AnyAsync(a => a.AssessmentId == aid, ct));
}
