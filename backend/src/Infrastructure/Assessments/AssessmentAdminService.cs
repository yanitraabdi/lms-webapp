using System.Text.Json;
using Academy.Application.Assessments;
using Academy.Domain;
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
                AttachedSessionId = db.SessionParts.Where(p => p.AssessmentId == a.Id).Select(p => (Guid?)p.SessionId).FirstOrDefault()
                    ?? db.ProgramSessions.Where(s => s.AssessmentId == a.Id && s.Type != SessionType.Video).Select(s => (Guid?)s.Id).FirstOrDefault(),
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
                q.Question.Id, q.Question.Section, q.Question.Bank, q.Question.Type, q.Question.Prompt,
                q.Question.Choices, q.Question.Correct, q.Question.AudioRef,
                q.Question.PassageRef, q.Question.Tags,
            })
            .ToListAsync(ct);

        var attachedSessionId = await db.SessionParts.Where(p => p.AssessmentId == id).Select(p => (Guid?)p.SessionId).FirstOrDefaultAsync(ct)
            ?? await db.ProgramSessions.Where(s => s.AssessmentId == id && s.Type != SessionType.Video).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
        var attemptCount = await db.Attempts.CountAsync(x => x.AssessmentId == id, ct);

        // Admin DOES see the answer key — that's the whole point of authoring (contrast GR-11,
        // which governs the STUDENT-facing DTO).
        var mapped = questions.Select(q => new AdminQuestionDto(
            q.Id, q.Section.ToString(), q.Bank.ToString(), q.Type.ToString(), q.Prompt,
            QuestionBankService.ParseStrings(q.Choices), QuestionBankService.ParseInts(q.Correct),
            q.AudioRef, q.PassageRef, QuestionBankService.ParseStrings(q.Tags), 0)).ToList();

        return new AdminAssessmentDto(
            a.Id, a.Kind.ToString(), a.Title, AssessmentService.ParseConfig(a.Config),
            mapped.Count, mapped, attachedSessionId, attemptCount);
    }

    /// <summary>
    /// A retake cap of 0 would make <c>used &gt;= cap</c> true before the first attempt, locking the
    /// test — and with it the linear program — permanently. Unlimited is expressed as null.
    ///
    /// A pass mark below 1 is the mirror image: every attempt clears it, including one that
    /// answered nothing. Absent (null) is different from zero and stays allowed — it is how a
    /// final assessment says it has no pass mark at all.
    /// </summary>
    private static void Validate(AssessmentConfig config)
    {
        if (config.RetakeCap is int cap && cap < 1)
            throw new AssessmentException(
                "Attempt limit must be at least 1. Leave empty for no limit.");

        if (config.PassThreshold is int mark && mark < 1)
            throw new AssessmentException(
                "Pass mark must be at least 1. Leave empty if this test has no pass mark.");

        if (config.DiscussionAfterFailures is int n && n < 1)
            throw new AssessmentException(
                "The discussion video opens after at least 1 failed attempt. Leave empty if it opens only after a pass.");

        if (config.AudioPlayLimit is int plays && plays < 1)
            throw new AssessmentException("Audio play limit must be at least 1.");
    }

    /// <summary>
    /// The pass mark must be reachable. A mark above the number of questions the test holds makes
    /// it unpassable, and an unpassable gating test holds the whole linear programme behind it for
    /// every learner, with no retry and no admin reset that can help.
    ///
    /// Checked from BOTH sides, because either can move: the mark changes on update, the count
    /// changes when the questions are composed. Checking one alone leaves the other as the way in.
    ///
    /// A test with no questions yet is exempt — that is every test between being created and being
    /// composed, and the composition call is what will judge it.
    /// </summary>
    private static void ValidateAgainstQuestions(AssessmentConfig config, int questionCount)
    {
        if (config.PassThreshold is int mark && questionCount > 0 && mark > questionCount)
            throw new AssessmentException(
                $"Pass mark ({mark}) can't exceed the number of questions ({questionCount}).");
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
            ?? throw new AssessmentException("Test not found.", 404);
        var (configJson, config) = Resolve(a.Config, req);
        ValidateAgainstQuestions(config, await db.AssessmentQuestions.CountAsync(q => q.AssessmentId == id, ct));
        if (Enum.TryParse<AssessmentKind>(req.Kind, true, out var k) && k != a.Kind)
        {
            // The kind decides which question bank the test may draw from; changing it under existing questions breaks that.
            if (await db.AssessmentQuestions.AnyAsync(q => q.AssessmentId == id, ct)
                || await db.Attempts.AnyAsync(x => x.AssessmentId == id, ct))
                throw new AssessmentException("The test type can't be changed once the test has questions.", 400);
            a.Kind = k;
        }
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
            throw new AssessmentException("This test can't be deleted because learners have already taken it.", 409);

        if (await db.SessionParts.AnyAsync(p => p.AssessmentId == id, ct))
            throw new AssessmentException("This test is still used in a session. Delete its part first.", 409);

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
        var assessment = await db.Assessments.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new AssessmentException("Test not found.", 404);

        var ids = req.QuestionIdsInOrder.Distinct().ToList();
        if (ids.Count != req.QuestionIdsInOrder.Count)
            throw new AssessmentException("The list contains duplicate questions.");

        var existing = await db.Questions.Where(q => ids.Contains(q.Id)).Select(q => q.Id).ToListAsync(ct);
        if (existing.Count != ids.Count)
            throw new AssessmentException("Some questions were not found in the question bank.", 400);

        // A test draws only from its own bank (spec 2026-10-05). Questions it already holds are
        // exempt: no copies were made when banks were introduced, and past attempts point at them.
        var bank = QuestionBanks.For(assessment.Kind);
        var held = await db.AssessmentQuestions.Where(aq => aq.AssessmentId == id)
            .Select(aq => aq.QuestionId).ToListAsync(ct);
        var added = ids.Except(held).ToList();
        if (await db.Questions.AnyAsync(q => added.Contains(q.Id) && q.Bank != bank, ct))
            throw new AssessmentException("Questions from another bank can't be used in this test.", 400);

        // Refused BEFORE anything is removed: composing fewer questions than the stored pass mark
        // would leave a test nobody can pass.
        ValidateAgainstQuestions(AssessmentService.ParseConfig(assessment.Config), ids.Count);

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
            ?? throw new AssessmentException("Session not found.", 404);

        if (assessmentId is Guid aid && !await db.Assessments.AnyAsync(a => a.Id == aid, ct))
            throw new AssessmentException("Test not found.", 404);

        // A video's test lives on its Test part; only other types keep the session column.
        if (session.Type != SessionType.Video) session.AssessmentId = assessmentId;
        if (session.Type == SessionType.Video)
        {
            var tests = await db.SessionParts
                .Where(p => p.SessionId == sessionId && p.Kind == SessionPartKind.Test).ToListAsync(ct);
            if (tests.Count > 1)
                throw new AssessmentException("This session has several tests. Manage them through the parts list.", 409);

            var current = tests.SingleOrDefault();
            if (current is not null && current.AssessmentId != assessmentId
                && await db.Attempts.AnyAsync(a => a.AssessmentId == current.AssessmentId, ct))
                throw new AssessmentException("Learners have already taken this test, so it can't be replaced.", 409);

            if (assessmentId is null)
            {
                if (current is not null)
                {
                    var discussion = await db.SessionParts.FirstOrDefaultAsync(p =>
                        p.SessionId == sessionId && p.Kind == SessionPartKind.Discussion
                        && p.OrderIndex == current.OrderIndex + 1, ct);
                    if (discussion is not null)
                        throw new AssessmentException("Delete this test's discussion video first.", 409);
                    db.SessionParts.Remove(current);
                }
            }
            else if (current is not null)
            {
                current.AssessmentId = assessmentId;
            }
            else
            {
                var lastOrder = await db.SessionParts.Where(p => p.SessionId == sessionId)
                    .MaxAsync(p => (int?)p.OrderIndex, ct) ?? 0;
                db.SessionParts.Add(new SessionPart
                {
                    Id = Guid.CreateVersion7(), SessionId = sessionId, OrderIndex = lastOrder + 1,
                    Kind = SessionPartKind.Test, Title = "Tes sesi", AssessmentId = assessmentId,
                });
            }
        }

        Audit(actor, assessmentId is null ? "assessment_detached" : "assessment_attached",
              sessionId, new { assessmentId });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex, DbErrors.SessionPartAssessmentIndex))
        {
            throw new AssessmentException("This test is already used in another session.", 409);
        }
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
