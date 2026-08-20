using System.Text.Json;
using Academy.Application.Abstractions;
using Academy.Application.Programs;
using Academy.Domain;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Assessments;
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
        // A brand-new program has no sessions, so it can never be ready. Refuse directly rather
        // than running the full check against a program that does not exist yet.
        if (req.Published)
            throw new ProgramException(
                "Program baru harus dibuat sebagai draf. Terbitkan setelah kontennya lengkap.", 409);

        var slug = await UniqueSlugAsync(req.Slug ?? Slugify(req.Name), null, ct);
        var program = new Domain.Entities.Program
        {
            Id = Guid.CreateVersion7(),
            Name = req.Name.Trim(),
            Slug = slug,
            Description = req.Description.Trim(),
            Summary = req.Summary?.Trim(),
            PriceIdr = req.PriceIdr,
            Status = ProgramStatus.Draft,
            PublishedAt = null,
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

        // Only the draft->published TRANSITION is gated. Editing an already-published program
        // (including re-saving it with published:true unchanged) stays free, or an admin could
        // never fix a typo without un-publishing first and taking the live page down. Content can
        // already drift unready after publish via un-gated endpoints (DeleteSessionAsync,
        // PUT /score-bands) — this was never meant to be airtight, only to catch it at the gate.
        if (req.Published && program.Status != ProgramStatus.Published)
        {
            var readiness = await GetReadinessAsync(id, ct);
            if (!readiness.Ready)
                throw new ProgramException(
                    "Program belum siap diterbitkan. " + string.Join(" ",
                        readiness.Checks.Where(c => c.Blocking && !c.Passed)
                                        .Select(c => c.Detail ?? c.Title)), 409);
        }

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

    // ---------------------------------------------------------------- readiness

    public async Task<ProgramReadinessDto> GetReadinessAsync(Guid programId, CancellationToken ct = default)
    {
        var program = await db.Programs.FirstOrDefaultAsync(p => p.Id == programId, ct)
            ?? throw new ProgramException("Program tidak ditemukan.", 404);

        var sessions = await db.ProgramSessions
            .Where(s => s.ProgramId == programId)
            .Select(s => new { s.Id, s.Type, s.Title, s.AssessmentId })
            .ToListAsync(ct);

        var checks = new List<ReadinessCheckDto>
        {
            new("has_sessions", "Program memiliki sesi", sessions.Count > 0, true,
                sessions.Count == 0 ? "Belum ada sesi pada program ini." : null),
        };

        // Every attached assessment must actually have questions.
        var empty = new List<string>();
        foreach (var s in sessions.Where(s => s.AssessmentId != null))
            if (!await db.AssessmentQuestions.AnyAsync(q => q.AssessmentId == s.AssessmentId, ct))
                empty.Add(s.Title);
        checks.Add(new("gating_tests_populated", "Semua tes memiliki soal", empty.Count == 0, true,
            empty.Count > 0 ? $"Tes tanpa soal: {string.Join(", ", empty)}." : null));

        var final = sessions.FirstOrDefault(s => s.Type == SessionType.FinalAssessment);
        var finalAssessmentId = final?.AssessmentId;
        checks.Add(new("final_assessment_present", "Tes akhir terpasang", finalAssessmentId is not null, true,
            final is null ? "Belum ada sesi tes akhir."
            : finalAssessmentId is null ? "Sesi tes akhir belum memiliki tes." : null));

        checks.Add(await FinalSectionsCheckAsync(finalAssessmentId, ct));
        checks.Add(await ScoreBandsCheckAsync(programId, ct));

        checks.Add(new("price_set", "Harga sudah diisi", program.PriceIdr > 0, false,
            program.PriceIdr > 0 ? null : "Harga masih 0."));

        return new ProgramReadinessDto(checks.All(c => !c.Blocking || c.Passed), checks);
    }

    /// <summary>Each configured section must hold exactly the number of questions it declares.</summary>
    private async Task<ReadinessCheckDto> FinalSectionsCheckAsync(Guid? assessmentId, CancellationToken ct)
    {
        const string key = "final_sections_populated";
        const string title = "Bagian tes akhir lengkap";

        if (assessmentId is not Guid id)
            return new(key, title, false, true, "Tes akhir belum terpasang.");

        var config = AssessmentService.ParseConfig(
            await db.Assessments.Where(a => a.Id == id).Select(a => a.Config).FirstAsync(ct));

        if (config.Sections.Count == 0)
            return new(key, title, false, true, "Tes akhir belum memiliki konfigurasi bagian.");

        var problems = new List<string>();
        foreach (var section in config.Sections)
        {
            var actual = await db.AssessmentQuestions
                .CountAsync(q => q.AssessmentId == id && q.Question.Section == section.Section, ct);
            if (actual != section.Questions)
                problems.Add($"{section.Section} {actual}/{section.Questions}");
        }

        return new(key, title, problems.Count == 0, true,
            problems.Count > 0 ? $"Jumlah soal belum sesuai: {string.Join(", ", problems)}." : null);
    }

    /// <summary>Every raw score in every ITP section must map exactly once, within its scaled band.</summary>
    private async Task<ReadinessCheckDto> ScoreBandsCheckAsync(Guid programId, CancellationToken ct)
    {
        const string key = "score_bands_complete";
        const string title = "Tabel konversi skor lengkap";

        var bands = await db.ScoreBandMappings
            .Where(b => b.ProgramId == programId)
            .Select(b => new { b.Section, b.MinRaw, b.MaxRaw, b.ScaledScore })
            .ToListAsync(ct);

        if (bands.Count == 0)
            return new(key, title, false, true, "Tabel konversi skor belum diisi.");

        var problems = new List<string>();
        foreach (var (section, maxRaw, scaledMax) in new[]
        {
            (QuestionSection.Listening, ToeflScoring.ListeningQuestions, ToeflScoring.ListeningScaledMax),
            (QuestionSection.Structure, ToeflScoring.StructureQuestions, ToeflScoring.StructureScaledMax),
            (QuestionSection.Reading,   ToeflScoring.ReadingQuestions,   ToeflScoring.ReadingScaledMax),
        })
        {
            var rows = bands.Where(b => b.Section == section).ToList();

            var missing = Enumerable.Range(0, maxRaw + 1)
                .Where(raw => !rows.Any(r => raw >= r.MinRaw && raw <= r.MaxRaw))
                .ToList();
            if (missing.Count > 0)
                problems.Add($"{section}: skor {Describe(missing)} belum dipetakan");

            var duplicated = Enumerable.Range(0, maxRaw + 1)
                .Where(raw => rows.Count(r => raw >= r.MinRaw && raw <= r.MaxRaw) > 1)
                .ToList();
            if (duplicated.Count > 0)
                problems.Add($"{section}: skor {Describe(duplicated)} dipetakan lebih dari sekali");

            var outOfRange = rows.Count(r => !ToeflScoring.IsValidScaled(r.ScaledScore, scaledMax));
            if (outOfRange > 0)
                problems.Add($"{section}: {outOfRange} nilai skala di luar {ToeflScoring.ScaledMin}-{scaledMax}");
        }

        return new(key, title, problems.Count == 0, true,
            problems.Count > 0 ? string.Join("; ", problems) + "." : null);
    }

    private static string Describe(List<int> missing) =>
        missing.Count <= 5 ? string.Join(", ", missing) : $"{missing[0]}-{missing[^1]} ({missing.Count} nilai)";

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
