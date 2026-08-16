using System.Text.Json;
using Academy.Application.Abstractions;
using Academy.Application.Programs;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Programs;

/// <summary>Program/session/batch authoring (KAK §9.12). Every mutation is audit-logged.</summary>
public class ProgramAdminService(AppDbContext db, IContentRevalidator revalidator) : IProgramAdminService
{
    // ---------------------------------------------------------------- programs

    public async Task<IReadOnlyList<AdminProgramDto>> ListAsync(CancellationToken ct = default)
        => await db.Programs
            .OrderBy(p => p.Name)
            .Select(p => new AdminProgramDto(
                p.Id, p.Name, p.Slug, p.Description, p.Summary, p.PriceIdr, p.Status.ToString(),
                db.ProgramSessions.Count(s => s.ProgramId == p.Id),
                db.Enrollments.Count(e => e.ProgramId == p.Id)))
            .ToListAsync(ct);

    public async Task<AdminProgramDto?> GetAsync(Guid id, CancellationToken ct = default)
        => await db.Programs
            .Where(p => p.Id == id)
            .Select(p => new AdminProgramDto(
                p.Id, p.Name, p.Slug, p.Description, p.Summary, p.PriceIdr, p.Status.ToString(),
                db.ProgramSessions.Count(s => s.ProgramId == p.Id),
                db.Enrollments.Count(e => e.ProgramId == p.Id)))
            .FirstOrDefaultAsync(ct);

    public async Task<AdminProgramDto> CreateAsync(Guid actor, UpsertProgramRequest req, CancellationToken ct = default)
    {
        var slug = await UniqueSlugAsync(req.Slug ?? Slugify(req.Name), null, ct);
        var program = new Domain.Entities.Program
        {
            Id = Guid.CreateVersion7(),
            Name = req.Name.Trim(),
            Slug = slug,
            Description = req.Description.Trim(),
            Summary = req.Summary?.Trim(),
            PriceIdr = req.PriceIdr,
            Status = req.Published ? ProgramStatus.Published : ProgramStatus.Draft,
            PublishedAt = req.Published ? DateTimeOffset.UtcNow : null,
        };
        db.Programs.Add(program);
        Audit(actor, "program_created", program.Id, new { program.Name, program.Slug });
        await db.SaveChangesAsync(ct);
        await RevalidateAsync(program.Slug, ct);
        return (await GetAsync(program.Id, ct))!;
    }

    public async Task UpdateAsync(Guid actor, Guid id, UpsertProgramRequest req, CancellationToken ct = default)
    {
        var program = await db.Programs.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new ProgramException("Program tidak ditemukan.", 404);

        var wasPublished = program.Status == ProgramStatus.Published;
        program.Name = req.Name.Trim();
        program.Slug = await UniqueSlugAsync(req.Slug ?? program.Slug, id, ct);
        program.Description = req.Description.Trim();
        program.Summary = req.Summary?.Trim();
        program.PriceIdr = req.PriceIdr;
        program.Status = req.Published ? ProgramStatus.Published : ProgramStatus.Draft;
        if (req.Published && !wasPublished) program.PublishedAt = DateTimeOffset.UtcNow;

        Audit(actor, "program_updated", id, new { program.Name, program.Slug, req.Published });
        await db.SaveChangesAsync(ct);
        await RevalidateAsync(program.Slug, ct);
    }

    public async Task DeleteAsync(Guid actor, Guid id, CancellationToken ct = default)
    {
        var program = await db.Programs.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (program is null) return;

        // Enrollments are financial/retained (GR-7) — never cascade them away.
        if (await db.Enrollments.AnyAsync(e => e.ProgramId == id, ct))
            throw new ProgramException("Program tidak bisa dihapus karena sudah memiliki pendaftar. Arsipkan saja.", 409);

        db.Programs.Remove(program);
        Audit(actor, "program_deleted", id, new { program.Name });
        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- sessions

    public async Task<IReadOnlyList<AdminSessionDto>> ListSessionsAsync(Guid programId, CancellationToken ct = default)
        => await db.ProgramSessions
            .Where(s => s.ProgramId == programId)
            .OrderBy(s => s.OrderIndex)
            .Select(s => MapSession(s))
            .ToListAsync(ct);

    public async Task<AdminSessionDto> CreateSessionAsync(
        Guid actor, Guid programId, UpsertSessionRequest req, CancellationToken ct = default)
    {
        if (!await db.Programs.AnyAsync(p => p.Id == programId, ct))
            throw new ProgramException("Program tidak ditemukan.", 404);

        var session = new ProgramSession { Id = Guid.CreateVersion7(), ProgramId = programId };
        ApplySession(session, req);
        // Append to the end unless an explicit index was supplied.
        session.OrderIndex = req.OrderIndex > 0
            ? req.OrderIndex
            : (await db.ProgramSessions.Where(s => s.ProgramId == programId)
                   .MaxAsync(s => (int?)s.OrderIndex, ct) ?? 0) + 1;

        db.ProgramSessions.Add(session);
        Audit(actor, "session_created", session.Id, new { programId, session.Title, Type = session.Type.ToString() });
        await SaveSessionsAsync(programId, ct);
        return MapSession(session);
    }

    public async Task UpdateSessionAsync(Guid actor, Guid sessionId, UpsertSessionRequest req, CancellationToken ct = default)
    {
        var session = await db.ProgramSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new ProgramException("Sesi tidak ditemukan.", 404);
        ApplySession(session, req);
        session.OrderIndex = req.OrderIndex;
        Audit(actor, "session_updated", sessionId, new { session.Title, Type = session.Type.ToString() });
        await SaveSessionsAsync(session.ProgramId, ct);
    }

    public async Task DeleteSessionAsync(Guid actor, Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.ProgramSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null) return;

        // Learner records are retained (GR-7); a session referenced by them cannot be removed.
        if (await db.SessionCompletions.AnyAsync(c => c.SessionId == sessionId, ct)
            || await db.WatchProgress.AnyAsync(w => w.SessionId == sessionId, ct))
            throw new ProgramException("Sesi tidak bisa dihapus karena sudah memiliki progres peserta.", 409);

        var programId = session.ProgramId;
        db.ProgramSessions.Remove(session);
        Audit(actor, "session_deleted", sessionId, new { session.Title });
        await SaveSessionsAsync(programId, ct);
    }

    public async Task ReorderSessionsAsync(
        Guid actor, Guid programId, ReorderSessionsRequest req, CancellationToken ct = default)
    {
        var sessions = await db.ProgramSessions.Where(s => s.ProgramId == programId).ToListAsync(ct);
        var byId = sessions.ToDictionary(s => s.Id);

        if (req.SessionIdsInOrder.Count != sessions.Count || req.SessionIdsInOrder.Any(id => !byId.ContainsKey(id)))
            throw new ProgramException("Daftar urutan harus memuat tepat semua sesi program ini.", 400);

        // Two-phase write: the UNIQUE(program_id, order_index) index would trip on a direct swap.
        for (var i = 0; i < req.SessionIdsInOrder.Count; i++)
            byId[req.SessionIdsInOrder[i]].OrderIndex = -(i + 1);
        await db.SaveChangesAsync(ct);

        foreach (var s in sessions) s.OrderIndex = -s.OrderIndex;
        Audit(actor, "sessions_reordered", programId, new { count = sessions.Count });
        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- batches

    public async Task<IReadOnlyList<AdminBatchDto>> ListBatchesAsync(Guid programId, CancellationToken ct = default)
        => await db.ProgramBatches
            .Where(b => b.ProgramId == programId)
            .OrderBy(b => b.StartDate)
            .Select(b => new AdminBatchDto(b.Id, b.ProgramId, b.Name, b.StartDate, b.Status.ToString()))
            .ToListAsync(ct);

    public async Task<AdminBatchDto> CreateBatchAsync(
        Guid actor, Guid programId, UpsertBatchRequest req, CancellationToken ct = default)
    {
        if (!await db.Programs.AnyAsync(p => p.Id == programId, ct))
            throw new ProgramException("Program tidak ditemukan.", 404);

        var batch = new ProgramBatch
        {
            Id = Guid.CreateVersion7(),
            ProgramId = programId,
            Name = req.Name.Trim(),
            StartDate = req.StartDate,
            Status = ParseEnum<BatchStatus>(req.Status, BatchStatus.Upcoming),
        };
        db.ProgramBatches.Add(batch);
        Audit(actor, "batch_created", batch.Id, new { programId, batch.Name });
        await db.SaveChangesAsync(ct);
        return new AdminBatchDto(batch.Id, batch.ProgramId, batch.Name, batch.StartDate, batch.Status.ToString());
    }

    public async Task UpdateBatchAsync(Guid actor, Guid batchId, UpsertBatchRequest req, CancellationToken ct = default)
    {
        var batch = await db.ProgramBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new ProgramException("Batch tidak ditemukan.", 404);
        batch.Name = req.Name.Trim();
        batch.StartDate = req.StartDate;
        batch.Status = ParseEnum(req.Status, batch.Status);
        Audit(actor, "batch_updated", batchId, new { batch.Name });
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteBatchAsync(Guid actor, Guid batchId, CancellationToken ct = default)
    {
        var batch = await db.ProgramBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null) return;
        db.ProgramBatches.Remove(batch);   // enrollments.batch_id is SetNull — enrollments survive
        Audit(actor, "batch_deleted", batchId, new { batch.Name });
        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- enrollment support

    public async Task RevokeEnrollmentAsync(Guid actor, Guid enrollmentId, CancellationToken ct = default)
    {
        var enrollment = await db.Enrollments.FirstOrDefaultAsync(e => e.Id == enrollmentId, ct)
            ?? throw new ProgramException("Pendaftaran tidak ditemukan.", 404);

        if (!Domain.EnrollmentStateMachine.CanTransition(enrollment.Status, EnrollmentStatus.Revoked))
            throw new ProgramException($"Status {enrollment.Status} tidak bisa dicabut.", 409);

        enrollment.Status = EnrollmentStatus.Revoked;   // access only — no learner data is deleted (GR-7)
        Audit(actor, "enrollment_revoked", enrollmentId, new { enrollment.UserId, enrollment.ProgramId });
        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- helpers

    private static AdminSessionDto MapSession(ProgramSession s) => new(
        s.Id, s.ProgramId, s.OrderIndex, s.Type.ToString(), s.Title, s.Description,
        s.ProviderAssetId, s.DurationSeconds,
        s.ScheduledAt, s.LiveMode == null ? null : s.LiveMode.ToString(), s.JoinUrl, s.Location,
        s.AssessmentId);

    private static void ApplySession(ProgramSession s, UpsertSessionRequest req)
    {
        s.Type = ParseEnum(req.Type, SessionType.Video);
        s.Title = req.Title.Trim();
        s.Description = req.Description?.Trim();
        s.ProviderAssetId = req.ProviderAssetId?.Trim();
        s.DurationSeconds = req.DurationSeconds;
        s.ScheduledAt = req.ScheduledAt;
        s.LiveMode = req.LiveMode is null ? null : ParseEnum(req.LiveMode, Domain.Enums.LiveMode.Zoom);
        s.JoinUrl = req.JoinUrl?.Trim();
        s.Location = req.Location?.Trim();
        s.AssessmentId = req.AssessmentId;
    }

    private async Task SaveSessionsAsync(Guid programId, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new ProgramException("Urutan sesi bentrok. Muat ulang lalu coba lagi.", 409);
        }
        var slug = await db.Programs.Where(p => p.Id == programId).Select(p => p.Slug).FirstOrDefaultAsync(ct);
        if (slug is not null) await RevalidateAsync(slug, ct);
    }

    private Task RevalidateAsync(string slug, CancellationToken ct)
        => revalidator.RevalidateAsync(["/", $"/program/{slug}"], ct);

    private void Audit(Guid actor, string action, Guid target, object metadata)
        => db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.CreateVersion7(),
            ActorUserId = actor,
            Action = action,
            Target = target.ToString(),
            Metadata = JsonSerializer.Serialize(metadata),
        });

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : fallback;

    private static string Slugify(string name)
    {
        var chars = name.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var slug = new string(chars);
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return slug.Trim('-') is { Length: > 0 } s ? s : "program";
    }

    private async Task<string> UniqueSlugAsync(string desired, Guid? selfId, CancellationToken ct)
    {
        var slug = Slugify(desired);
        var candidate = slug;
        var n = 2;
        while (await db.Programs.AnyAsync(p => p.Slug == candidate && (selfId == null || p.Id != selfId), ct))
            candidate = $"{slug}-{n++}";
        return candidate;
    }
}
