using System.Text.Json;
using Academy.Application.Assessments;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Assessments;

/// <summary>Assessment authoring: compose a test from bank questions and attach it to a session
/// (KAK §9.6, §9.12). Admin-only; every mutation is audit-logged.</summary>
public class AssessmentAdminService(AppDbContext db) : IAssessmentAdminService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<AdminAssessmentDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await db.Assessments
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id, a.Kind, a.Title, a.Config,
                QuestionCount = db.AssessmentQuestions.Count(q => q.AssessmentId == a.Id),
                AttachedSessionId = db.ProgramSessions
                    .Where(s => s.AssessmentId == a.Id).Select(s => (Guid?)s.Id).FirstOrDefault(),
                AttemptCount = db.Attempts.Count(x => x.AssessmentId == a.Id),
            })
            .ToListAsync(ct);

        return rows.Select(a => new AdminAssessmentDto(
            a.Id, a.Kind.ToString(), a.Title, AssessmentService.ParseConfig(a.Config),
            a.QuestionCount, [], a.AttachedSessionId, a.AttemptCount)).ToList();
    }

    public async Task<AdminAssessmentDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var a = await db.Assessments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return null;

        var questions = await db.AssessmentQuestions
            .Where(q => q.AssessmentId == id)
            .OrderBy(q => q.OrderIndex)
            .Select(q => new
            {
                q.Question.Id, q.Question.Section, q.Question.Type, q.Question.Prompt,
                q.Question.Choices, q.Question.Correct, q.Question.AudioRef,
                q.Question.PassageRef, q.Question.Tags,
            })
            .ToListAsync(ct);

        var attachedSessionId = await db.ProgramSessions
            .Where(s => s.AssessmentId == id).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
        var attemptCount = await db.Attempts.CountAsync(x => x.AssessmentId == id, ct);

        // Admin DOES see the answer key — that's the whole point of authoring (contrast GR-11,
        // which governs the STUDENT-facing DTO).
        var mapped = questions.Select(q => new AdminQuestionDto(
            q.Id, q.Section.ToString(), q.Type.ToString(), q.Prompt,
            QuestionBankService.ParseStrings(q.Choices), QuestionBankService.ParseInts(q.Correct),
            q.AudioRef, q.PassageRef, QuestionBankService.ParseStrings(q.Tags), 0)).ToList();

        return new AdminAssessmentDto(
            a.Id, a.Kind.ToString(), a.Title, AssessmentService.ParseConfig(a.Config),
            mapped.Count, mapped, attachedSessionId, attemptCount);
    }

    /// <summary>
    /// A retake cap of 0 would make <c>used &gt;= cap</c> true before the first attempt, locking the
    /// test — and with it the linear program — permanently. Unlimited is expressed as null.
    /// </summary>
    private static void Validate(AssessmentConfig config)
    {
        if (config.RetakeCap is int cap && cap < 1)
            throw new AssessmentException(
                "Batas percobaan minimal 1. Kosongkan untuk tanpa batas.");
    }

    /// <summary>Merges the partial request over what is stored, then validates the RESULT — a
    /// request that omits retakeCap must still be judged on the cap that will actually apply.</summary>
    private static (string Json, AssessmentConfig Config) Resolve(string? storedJson, UpsertAssessmentRequest req)
    {
        var json = AssessmentConfigMerge.Merge(storedJson, req.Config);
        var config = AssessmentConfigMerge.ToConfig(json, JsonOpts);
        Validate(config);
        return (json, config);
    }

    public async Task<AdminAssessmentDto> CreateAsync(
        Guid actor, UpsertAssessmentRequest req, CancellationToken ct = default)
    {
        var (configJson, _) = Resolve(null, req);
        var assessment = new Assessment
        {
            Id = Guid.CreateVersion7(),
            Kind = Enum.TryParse<AssessmentKind>(req.Kind, true, out var k) ? k : AssessmentKind.Gating,
            Title = req.Title.Trim(),
            Config = configJson,
        };
        db.Assessments.Add(assessment);
        Audit(actor, "assessment_created", assessment.Id, new { assessment.Title, Kind = assessment.Kind.ToString() });
        await db.SaveChangesAsync(ct);
        return (await GetAsync(assessment.Id, ct))!;
    }

    public async Task UpdateAsync(Guid actor, Guid id, UpsertAssessmentRequest req, CancellationToken ct = default)
    {
        var a = await db.Assessments.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AssessmentException("Tes tidak ditemukan.", 404);
        var (configJson, config) = Resolve(a.Config, req);
        a.Kind = Enum.TryParse<AssessmentKind>(req.Kind, true, out var k) ? k : a.Kind;
        a.Title = req.Title.Trim();
        a.Config = configJson;
        Audit(actor, "assessment_updated", id, new { a.Title, config.PassThreshold, config.RetakeCap });
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid actor, Guid id, CancellationToken ct = default)
    {
        var a = await db.Assessments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return;

        // Attempts are retained learner records (GR-7) — a scored test cannot be erased.
        if (await db.Attempts.AnyAsync(x => x.AssessmentId == id, ct))
            throw new AssessmentException("Tes tidak bisa dihapus karena sudah dikerjakan peserta.", 409);

        // Detach from any session first so the FK (SetNull) intent is explicit.
        var sessions = await db.ProgramSessions.Where(s => s.AssessmentId == id).ToListAsync(ct);
        foreach (var s in sessions) s.AssessmentId = null;

        db.Assessments.Remove(a);
        Audit(actor, "assessment_deleted", id, new { a.Title });
        await db.SaveChangesAsync(ct);
    }

    public async Task SetQuestionsAsync(
        Guid actor, Guid id, SetAssessmentQuestionsRequest req, CancellationToken ct = default)
    {
        if (!await db.Assessments.AnyAsync(a => a.Id == id, ct))
            throw new AssessmentException("Tes tidak ditemukan.", 404);

        var ids = req.QuestionIdsInOrder.Distinct().ToList();
        if (ids.Count != req.QuestionIdsInOrder.Count)
            throw new AssessmentException("Terdapat soal duplikat dalam daftar.");

        var existing = await db.Questions.Where(q => ids.Contains(q.Id)).Select(q => q.Id).ToListAsync(ct);
        if (existing.Count != ids.Count)
            throw new AssessmentException("Sebagian soal tidak ditemukan di bank soal.", 400);

        // Replace the composition wholesale; order_index is explicit so the learner's question
        // order is deterministic and matches scoring (never rely on id/timestamp ordering).
        var current = await db.AssessmentQuestions.Where(q => q.AssessmentId == id).ToListAsync(ct);
        db.AssessmentQuestions.RemoveRange(current);
        await db.SaveChangesAsync(ct);           // clear before re-adding: UNIQUE(assessment, order)

        for (var i = 0; i < ids.Count; i++)
            db.AssessmentQuestions.Add(new AssessmentQuestion
            {
                Id = Guid.CreateVersion7(),
                AssessmentId = id,
                QuestionId = ids[i],
                OrderIndex = i + 1,
            });

        Audit(actor, "assessment_questions_set", id, new { count = ids.Count });
        await db.SaveChangesAsync(ct);
    }

    public async Task AttachToSessionAsync(
        Guid actor, Guid sessionId, Guid? assessmentId, CancellationToken ct = default)
    {
        var session = await db.ProgramSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new AssessmentException("Sesi tidak ditemukan.", 404);

        if (assessmentId is Guid aid && !await db.Assessments.AnyAsync(a => a.Id == aid, ct))
            throw new AssessmentException("Tes tidak ditemukan.", 404);

        session.AssessmentId = assessmentId;
        Audit(actor, assessmentId is null ? "assessment_detached" : "assessment_attached",
              sessionId, new { assessmentId });
        await db.SaveChangesAsync(ct);
    }

    private void Audit(Guid actor, string action, Guid target, object metadata)
        => db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.CreateVersion7(),
            ActorUserId = actor,
            Action = action,
            Target = target.ToString(),
            Metadata = JsonSerializer.Serialize(metadata),
        });
}
