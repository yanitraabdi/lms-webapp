using System.Text.Json;
using Academy.Application.Assessments;
using Academy.Application.Programs;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Assessments;

/// <summary>
/// The assessment engine (KAK §9.6–§9.7). Invariants enforced here, never client-side:
/// the answer key is never serialized to a student (GR-11); <c>started_at</c> is server-stamped and
/// the retake cap is checked server-side (GR-12); scoring is server-side only; passing advances the
/// linear lock through <see cref="ISessionCompletionService"/> (GR-8).
/// </summary>
public class AssessmentService(
    AppDbContext db,
    ISessionAccessService access,
    ISessionCompletionService completion,
    Media.MediaSigner signer) : IAssessmentService
{
    /// <summary>
    /// Signed audio for a Listening question in a SESSION test.
    ///
    /// Two checks, and both are load-bearing. EnsureAccessAsync is THE GATE (GR-1): audio is
    /// session content, so reaching it must require session access. The question-belongs-to-this
    /// -assessment check is what stops the route becoming a way to read any clip in the bank by
    /// guessing question ids — the learner has access to this session, not to every recording.
    ///
    /// No play limit, unlike the final assessment: see IAssessmentService for why.
    /// </summary>
    public async Task<string> GetGatingAudioUrlAsync(
        Guid userId, Guid sessionId, Guid questionId, CancellationToken ct = default)
    {
        await access.EnsureAccessAsync(userId, sessionId, ct);

        var assessmentId = await db.ProgramSessions
            .Where(s => s.Id == sessionId)
            .Select(s => s.AssessmentId)
            .FirstOrDefaultAsync(ct)
            ?? throw new AssessmentException("Sesi ini tidak memiliki tes.", 404);

        var belongs = await db.AssessmentQuestions
            .AnyAsync(aq => aq.AssessmentId == assessmentId && aq.QuestionId == questionId, ct);
        if (!belongs)
            throw new AssessmentException("Soal ini bukan bagian dari tes sesi ini.", 403);

        var question = await db.Questions
            .Where(q => q.Id == questionId)
            .Select(q => new { q.AudioRef, q.Section })
            .FirstAsync(ct);

        var config = ParseConfig(
            await db.Assessments.Where(a => a.Id == assessmentId).Select(a => a.Config).FirstAsync(ct));

        // A question's own clip wins; otherwise a section recording, if the test has one.
        var storageKey = AudioResolution.StorageKey(question.AudioRef, question.Section, config)
            ?? throw new AssessmentException("Soal ini tidak memiliki audio.", 400);

        return signer.Sign(storageKey);
    }

    public async Task<StudentAssessmentDto?> GetForSessionAsync(
        Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        await access.EnsureAccessAsync(userId, sessionId, ct);

        var assessmentId = await db.ProgramSessions
            .Where(s => s.Id == sessionId).Select(s => s.AssessmentId).FirstOrDefaultAsync(ct);
        if (assessmentId is null) return null;

        return await BuildStudentViewAsync(userId, assessmentId.Value, sessionId, ct);
    }

    public async Task<AttemptDto> StartAttemptAsync(Guid userId, Guid assessmentId, CancellationToken ct = default)
    {
        var (assessment, config, sessionId) = await LoadAsync(assessmentId, ct);
        if (sessionId is Guid sid) await access.EnsureAccessAsync(userId, sid, ct);

        var questionCount = await db.AssessmentQuestions.CountAsync(q => q.AssessmentId == assessmentId, ct);
        if (questionCount == 0)
            throw new AssessmentException("Tes ini belum memiliki soal.", 400);

        // Reuse an unsubmitted attempt so a refresh doesn't burn a retake.
        var open = await db.Attempts
            .FirstOrDefaultAsync(a => a.UserId == userId && a.AssessmentId == assessmentId && a.SubmittedAt == null, ct);
        if (open is not null) return Map(open);

        await EnsureCanAttemptAsync(userId, assessmentId, config, ct);

        var now = DateTimeOffset.UtcNow;
        var attempt = new Attempt
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            AssessmentId = assessmentId,
            StartedAt = now,                       // SERVER-stamped (GR-12)
            Answers = "{}",
            SectionScores = "{}",
            // Sectional sittings (M4) carry per-section server clocks; a gating test has none.
            State = AttemptState.Create(config, now).Serialize(),
            MaxScore = questionCount,
        };
        db.Attempts.Add(attempt);
        await db.SaveChangesAsync(ct);
        return Map(attempt);
    }

    public async Task SaveAnswersAsync(
        Guid userId, Guid attemptId, IReadOnlyDictionary<string, int> answers, CancellationToken ct = default)
    {
        var attempt = await LoadOwnedAttemptAsync(userId, attemptId, ct);
        if (attempt.SubmittedAt is not null)
            throw new AssessmentException("Percobaan ini sudah dikirim.", 409);

        var merged = Parse(attempt.Answers);
        foreach (var (k, v) in answers) merged[k] = v;
        attempt.Answers = JsonSerializer.Serialize(merged);
        await db.SaveChangesAsync(ct);
    }

    public async Task<AttemptResultDto> SubmitAsync(
        Guid userId, Guid attemptId, IReadOnlyDictionary<string, int>? answers, CancellationToken ct = default)
    {
        var attempt = await LoadOwnedAttemptAsync(userId, attemptId, ct);
        if (attempt.SubmittedAt is not null)
            return await BuildResultAsync(attempt, ct);          // idempotent re-submit

        if (answers is not null)
        {
            var merged = Parse(attempt.Answers);
            foreach (var (k, v) in answers) merged[k] = v;
            attempt.Answers = JsonSerializer.Serialize(merged);
        }

        var (_, config, sessionId) = await LoadAsync(attempt.AssessmentId, ct);
        await ScoreAsync(attempt, config, ct);
        attempt.SubmittedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        // Passing satisfies the gating half of the completion rule; the completion service
        // re-evaluates the whole rule (watch threshold AND test) and advances the lock.
        if (sessionId is Guid sid) await completion.TryCompleteAsync(userId, sid, ct);

        return await BuildResultAsync(attempt, ct);
    }

    public async Task<AttemptResultDto> GetResultAsync(Guid userId, Guid attemptId, CancellationToken ct = default)
    {
        var attempt = await LoadOwnedAttemptAsync(userId, attemptId, ct);
        if (attempt.SubmittedAt is null)
            throw new AssessmentException("Percobaan ini belum dikirim.", 409);
        return await BuildResultAsync(attempt, ct);
    }

    // ---------------------------------------------------------------- internals

    /// <summary>Server-side scoring (GR-11). One point per correct answer, no negative marking.</summary>
    private async Task ScoreAsync(Attempt attempt, AssessmentConfig config, CancellationToken ct)
    {
        var questions = await db.AssessmentQuestions
            .Where(q => q.AssessmentId == attempt.AssessmentId)
            .OrderBy(q => q.OrderIndex)                       // explicit order — never id/timestamp
            .Select(q => new { q.QuestionId, q.Question.Section, q.Question.Correct })
            .ToListAsync(ct);

        var answers = Parse(attempt.Answers);
        var perSection = new Dictionary<string, int>();
        var score = 0;

        foreach (var q in questions)
        {
            var section = q.Section.ToString();
            perSection.TryAdd(section, 0);

            if (!answers.TryGetValue(q.QuestionId.ToString(), out var picked)) continue;
            if (!ParseCorrect(q.Correct).Contains(picked)) continue;

            score++;
            perSection[section]++;
        }

        attempt.TotalScore = score;
        attempt.MaxScore = questions.Count;
        attempt.SectionScores = JsonSerializer.Serialize(perSection);
        attempt.Passed = score >= (config.PassThreshold ?? questions.Count);
    }

    private async Task<StudentAssessmentDto> BuildStudentViewAsync(
        Guid userId, Guid assessmentId, Guid? sessionId, CancellationToken ct)
    {
        var (assessment, config, _) = await LoadAsync(assessmentId, ct);

        // NOTE: `Correct` is deliberately absent from this projection — the answer key must not
        // leave the server (GR-11). StudentQuestionDto has no field for it either.
        var questions = await db.AssessmentQuestions
            .Where(q => q.AssessmentId == assessmentId)
            .OrderBy(q => q.OrderIndex)
            .Select(q => new StudentQuestionDto(
                q.QuestionId,
                q.Question.Section.ToString(),
                q.Question.Prompt,
                ParseChoices(q.Question.Choices),
                q.Question.PassageRef,
                q.Question.AudioRef != null))
            .ToListAsync(ct);

        var attempts = await db.Attempts
            .Where(a => a.UserId == userId && a.AssessmentId == assessmentId && a.SubmittedAt != null)
            .Select(a => new { a.TotalScore, a.Passed })
            .ToListAsync(ct);

        var used = attempts.Count;
        var passed = attempts.Any(a => a.Passed);
        var canAttempt = config.RetakeCap is null || used < config.RetakeCap.Value;

        return new StudentAssessmentDto(
            assessmentId, sessionId, assessment.Kind.ToString(), assessment.Title,
            questions.Count, config.PassThreshold ?? questions.Count,
            config.RetakeCap, used, canAttempt,
            passed, attempts.Count > 0 ? attempts.Max(a => a.TotalScore) : null,
            config.ProctoringEnabled, config.TimeLimitMinutes,
            questions);
    }

    private async Task<AttemptResultDto> BuildResultAsync(Attempt attempt, CancellationToken ct)
    {
        var sessionId = await db.ProgramSessions
            .Where(s => s.AssessmentId == attempt.AssessmentId)
            .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);

        var sessionCompleted = false;
        Guid? nextSessionId = null;

        if (sessionId is Guid sid)
        {
            sessionCompleted = await completion.IsCompleteAsync(attempt.UserId, sid, ct);
            var current = await db.ProgramSessions
                .Where(s => s.Id == sid).Select(s => new { s.ProgramId, s.OrderIndex }).FirstAsync(ct);
            nextSessionId = await db.ProgramSessions
                .Where(s => s.ProgramId == current.ProgramId && s.OrderIndex > current.OrderIndex)
                .OrderBy(s => s.OrderIndex)
                .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
        }

        return new AttemptResultDto(
            attempt.Id, attempt.TotalScore, attempt.MaxScore, attempt.Passed,
            attempt.AutoSubmitted, attempt.ProctorFlagged,
            ParseSectionScores(attempt.SectionScores),
            null, null,                                  // scaled score + band: M4 (final assessment)
            sessionCompleted, nextSessionId);
    }

    private async Task EnsureCanAttemptAsync(
        Guid userId, Guid assessmentId, AssessmentConfig config, CancellationToken ct)
    {
        if (config.RetakeCap is not int cap) return;         // null ⇒ unlimited

        var used = await db.Attempts.CountAsync(
            a => a.UserId == userId && a.AssessmentId == assessmentId && a.SubmittedAt != null, ct);
        if (used >= cap)
            throw new AssessmentException(
                cap == 1
                    ? "Tes ini hanya dapat dikerjakan satu kali."
                    : $"Anda telah mencapai batas {cap} kali percobaan.", 409);
    }

    private async Task<(Assessment Assessment, AssessmentConfig Config, Guid? SessionId)> LoadAsync(
        Guid assessmentId, CancellationToken ct)
    {
        var assessment = await db.Assessments.FirstOrDefaultAsync(a => a.Id == assessmentId, ct)
            ?? throw new AssessmentException("Tes tidak ditemukan.", 404);
        var sessionId = await db.ProgramSessions
            .Where(s => s.AssessmentId == assessmentId)
            .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
        return (assessment, ParseConfig(assessment.Config), sessionId);
    }

    private async Task<Attempt> LoadOwnedAttemptAsync(Guid userId, Guid attemptId, CancellationToken ct)
        => await db.Attempts.FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, ct)
           ?? throw new AssessmentException("Percobaan tidak ditemukan.", 404);

    private static AttemptDto Map(Attempt a)
        => new(a.Id, a.AssessmentId, a.StartedAt, a.SubmittedAt, Parse(a.Answers));

    public static AssessmentConfig ParseConfig(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<AssessmentConfig>(json, JsonOpts) ?? new AssessmentConfig();
        }
        catch
        {
            return new AssessmentConfig();
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private static Dictionary<string, int> Parse(string json)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? []; }
        catch { return []; }
    }

    private static IReadOnlyDictionary<string, int> ParseSectionScores(string json) => Parse(json);

    private static List<int> ParseCorrect(string json)
    {
        try { return JsonSerializer.Deserialize<List<int>>(json) ?? []; }
        catch { return []; }
    }

    private static List<string> ParseChoices(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}
