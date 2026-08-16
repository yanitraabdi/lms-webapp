using System.Text.Json;
using Academy.Application.Assessments;
using Academy.Application.Programs;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Assessments;

/// <summary>
/// The sectional, timed sitting (KAK §9.7). Server-authoritative throughout:
/// section clocks start from a server stamp, a late submit is auto-submitted and scored as-of
/// expiry, sections are sequential and non-returnable, and audio play limits are counted here.
/// </summary>
public class FinalAssessmentService(
    AppDbContext db,
    ISessionAccessService access,
    ISessionCompletionService completion,
    IProgramCertificateService certificates) : IFinalAssessmentService
{
    /// <summary>Two strikes ends the sitting (KAK §9.8.1).</summary>
    public const int StrikeLimit = 2;

    public async Task<AttemptStateDto> GetStateAsync(Guid userId, Guid attemptId, CancellationToken ct = default)
    {
        var attempt = await LoadOwnedAsync(userId, attemptId, ct);
        await EnforceDeadlinesAsync(attempt, ct);
        return await BuildStateAsync(attempt, ct);
    }

    public async Task<AttemptStateDto> SaveAnswersAsync(
        Guid userId, Guid attemptId, IReadOnlyDictionary<string, int> answers, CancellationToken ct = default)
    {
        var attempt = await LoadOwnedAsync(userId, attemptId, ct);
        await EnforceDeadlinesAsync(attempt, ct);

        if (attempt.SubmittedAt is not null)
            throw new AssessmentException("Tes ini sudah selesai.", 409);

        var state = AttemptState.Parse(attempt.State);
        var current = state.Current
            ?? throw new AssessmentException("Tidak ada bagian yang aktif.", 409);

        // Only questions belonging to the ACTIVE section are writable — a closed section is final.
        var allowed = await SectionQuestionIdsAsync(attempt.AssessmentId, current.Section, ct);
        var merged = ParseInts(attempt.Answers);
        foreach (var (k, v) in answers)
            if (allowed.Contains(k)) merged[k] = v;

        attempt.Answers = JsonSerializer.Serialize(merged);
        await db.SaveChangesAsync(ct);
        return await BuildStateAsync(attempt, ct);
    }

    public async Task<AttemptStateDto> AdvanceSectionAsync(
        Guid userId, Guid attemptId, IReadOnlyDictionary<string, int>? answers, CancellationToken ct = default)
    {
        if (answers is { Count: > 0 }) await SaveAnswersAsync(userId, attemptId, answers, ct);

        var attempt = await LoadOwnedAsync(userId, attemptId, ct);
        await EnforceDeadlinesAsync(attempt, ct);
        if (attempt.SubmittedAt is not null) return await BuildStateAsync(attempt, ct);

        var state = AttemptState.Parse(attempt.State);
        await CloseCurrentAndAdvanceAsync(attempt, state, DateTimeOffset.UtcNow, autoSubmitted: false, ct);
        return await BuildStateAsync(attempt, ct);
    }

    public async Task<AttemptResultDto> SubmitAsync(Guid userId, Guid attemptId, CancellationToken ct = default)
    {
        var attempt = await LoadOwnedAsync(userId, attemptId, ct);
        await EnforceDeadlinesAsync(attempt, ct);

        if (attempt.SubmittedAt is null)
            await FinalizeAsync(attempt, DateTimeOffset.UtcNow, autoSubmitted: false, ct);

        return await BuildResultAsync(attempt, ct);
    }

    public async Task<string> GetAudioUrlAsync(
        Guid userId, Guid attemptId, Guid questionId, CancellationToken ct = default)
    {
        var attempt = await LoadOwnedAsync(userId, attemptId, ct);
        await EnforceDeadlinesAsync(attempt, ct);
        if (attempt.SubmittedAt is not null)
            throw new AssessmentException("Tes ini sudah selesai.", 409);

        var question = await db.Questions
            .Where(q => q.Id == questionId)
            .Select(q => new { q.Id, q.AudioRef, q.Section })
            .FirstOrDefaultAsync(ct)
            ?? throw new AssessmentException("Soal tidak ditemukan.", 404);
        if (string.IsNullOrWhiteSpace(question.AudioRef))
            throw new AssessmentException("Soal ini tidak memiliki audio.", 400);

        // The question must belong to this assessment AND the active section.
        var state = AttemptState.Parse(attempt.State);
        var current = state.Current ?? throw new AssessmentException("Tidak ada bagian yang aktif.", 409);
        var allowed = await SectionQuestionIdsAsync(attempt.AssessmentId, current.Section, ct);
        if (!allowed.Contains(questionId.ToString()))
            throw new AssessmentException("Soal ini bukan bagian dari sesi yang sedang berjalan.", 403);

        var config = AssessmentService.ParseConfig(
            await db.Assessments.Where(a => a.Id == attempt.AssessmentId).Select(a => a.Config).FirstAsync(ct));

        // Play limit is counted SERVER-side; the client cannot grant itself another play.
        if (config.AudioPlayLimit is int limit)
        {
            var key = questionId.ToString();
            state.AudioPlays.TryGetValue(key, out var used);
            if (used >= limit)
                throw new AssessmentException("Batas pemutaran audio untuk soal ini sudah tercapai.", 409);
            state.AudioPlays[key] = used + 1;
            attempt.State = state.Serialize();
            await db.SaveChangesAsync(ct);
        }

        // Signed, short-TTL, minted per play (GR-3). Dev storage returns a deterministic dev URL.
        return $"/media/audio/{Uri.EscapeDataString(question.AudioRef)}";
    }

    // ================================================================ internals

    /// <summary>
    /// Applies elapsed time BEFORE any read or write: expired sections are closed at their
    /// deadline and the attempt is auto-submitted if the clock has run out (KAK §9.7.2 R2).
    /// </summary>
    private async Task EnforceDeadlinesAsync(Attempt attempt, CancellationToken ct)
    {
        if (attempt.SubmittedAt is not null) return;

        var state = AttemptState.Parse(attempt.State);
        if (state.Sections.Count == 0) return;      // untimed (gating test) — nothing to enforce

        var now = DateTimeOffset.UtcNow;
        var changed = false;

        // A single call may span several expired sections (e.g. the tab was closed for an hour).
        while (attempt.SubmittedAt is null && state.Current is SectionState current)
        {
            var deadline = state.DeadlineOf(current);
            if (deadline is null || now < deadline.Value) break;

            await CloseCurrentAndAdvanceAsync(attempt, state, deadline.Value, autoSubmitted: true, ct);
            state = AttemptState.Parse(attempt.State);
            changed = true;
        }

        if (changed) await db.SaveChangesAsync(ct);
    }

    /// <summary>Closes the active section at <paramref name="at"/> and opens the next, or finalizes.</summary>
    private async Task CloseCurrentAndAdvanceAsync(
        Attempt attempt, AttemptState state, DateTimeOffset at, bool autoSubmitted, CancellationToken ct)
    {
        if (state.Current is not SectionState current) return;

        current.SubmittedAt = at;

        if (state.IsLastSection)
        {
            attempt.State = state.Serialize();
            await FinalizeAsync(attempt, at, autoSubmitted, ct);
            return;
        }

        state.CurrentIndex++;
        var next = state.Current!;
        next.StartedAt = at;                       // the next clock starts when the previous closed
        attempt.State = state.Serialize();
        attempt.AutoSubmitted |= autoSubmitted;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Scores server-side, stamps submission, completes the session, and issues the certificate.</summary>
    private async Task FinalizeAsync(Attempt attempt, DateTimeOffset at, bool autoSubmitted, CancellationToken ct)
    {
        if (attempt.SubmittedAt is not null) return;

        await ScoreAsync(attempt, ct);
        attempt.SubmittedAt = at;
        attempt.AutoSubmitted |= autoSubmitted;
        await db.SaveChangesAsync(ct);

        var sessionId = await db.ProgramSessions
            .Where(s => s.AssessmentId == attempt.AssessmentId)
            .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
        if (sessionId is Guid sid) await completion.TryCompleteAsync(attempt.UserId, sid, ct);

        // Certificate issuance may legitimately fail loudly when the score map is incomplete
        // (KAK §9.9.4) — that must not lose the learner's submitted attempt, which is already saved.
        await certificates.TryIssueForAttemptAsync(attempt.UserId, attempt.Id, ct);
    }

    private async Task ScoreAsync(Attempt attempt, CancellationToken ct)
    {
        var questions = await db.AssessmentQuestions
            .Where(q => q.AssessmentId == attempt.AssessmentId)
            .OrderBy(q => q.OrderIndex)
            .Select(q => new { q.QuestionId, q.Question.Section, q.Question.Correct })
            .ToListAsync(ct);

        var answers = ParseInts(attempt.Answers);
        var perSection = new Dictionary<string, int>();
        var score = 0;

        foreach (var q in questions)
        {
            var section = q.Section.ToString();
            perSection.TryAdd(section, 0);
            if (!answers.TryGetValue(q.QuestionId.ToString(), out var picked)) continue;
            if (!ParseList(q.Correct).Contains(picked)) continue;
            score++;
            perSection[section]++;
        }

        attempt.TotalScore = score;
        attempt.MaxScore = questions.Count;
        attempt.SectionScores = JsonSerializer.Serialize(perSection);

        var config = AssessmentService.ParseConfig(
            await db.Assessments.Where(a => a.Id == attempt.AssessmentId).Select(a => a.Config).FirstAsync(ct));
        attempt.Passed = score >= (config.PassThreshold ?? 0);
    }

    private async Task<AttemptStateDto> BuildStateAsync(Attempt attempt, CancellationToken ct)
    {
        var config = AssessmentService.ParseConfig(
            await db.Assessments.Where(a => a.Id == attempt.AssessmentId).Select(a => a.Config).FirstAsync(ct));
        var state = AttemptState.Parse(attempt.State);
        var current = state.Current;

        var questions = new List<StudentQuestionDto>();
        var answers = new Dictionary<string, int>();
        var audioLeft = new Dictionary<string, int>();

        if (attempt.SubmittedAt is null && current is not null)
        {
            // Only the ACTIVE section's questions are ever serialized — and never with answers (GR-11).
            var currentSection = ParseSection(current.Section);
            questions = await db.AssessmentQuestions
                .Where(q => q.AssessmentId == attempt.AssessmentId
                            && q.Question.Section == currentSection)
                .OrderBy(q => q.OrderIndex)
                .Select(q => new StudentQuestionDto(
                    q.QuestionId, q.Question.Section.ToString(), q.Question.Prompt,
                    ParseStrings(q.Question.Choices), q.Question.PassageRef, q.Question.AudioRef != null))
                .ToListAsync(ct);

            var saved = ParseInts(attempt.Answers);
            foreach (var q in questions)
            {
                if (saved.TryGetValue(q.Id.ToString(), out var v)) answers[q.Id.ToString()] = v;
                if (config.AudioPlayLimit is int limit && q.HasAudio)
                {
                    state.AudioPlays.TryGetValue(q.Id.ToString(), out var used);
                    audioLeft[q.Id.ToString()] = Math.Max(0, limit - used);
                }
            }
        }

        var deadline = current is null ? null : state.DeadlineOf(current);
        var remaining = deadline is null ? 0
            : Math.Max(0, (int)(deadline.Value - DateTimeOffset.UtcNow).TotalSeconds);

        // Inline the strike kinds: a C# predicate method is not translatable by EF.
        var strikes = config.ProctoringEnabled
            ? await db.ProctorEvents.CountAsync(
                e => e.AttemptId == attempt.Id
                     && (e.Kind == ProctorEventKind.VisibilityHidden || e.Kind == ProctorEventKind.WindowBlur), ct)
            : 0;

        return new AttemptStateDto(
            attempt.Id, attempt.AssessmentId,
            attempt.SubmittedAt is null ? "InProgress" : "Submitted",
            state.CurrentIndex, state.Sections.Count, current?.Section,
            current?.StartedAt, deadline, remaining,
            config.ProctoringEnabled, strikes, StrikeLimit, attempt.ProctorFlagged,
            questions, answers, audioLeft);
    }

    private async Task<AttemptResultDto> BuildResultAsync(Attempt attempt, CancellationToken ct)
    {
        var sessionId = await db.ProgramSessions
            .Where(s => s.AssessmentId == attempt.AssessmentId)
            .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);

        var sessionCompleted = sessionId is Guid sid
            && await completion.IsCompleteAsync(attempt.UserId, sid, ct);

        // The scaled total and band live on the certificate. When the score map is incomplete no
        // certificate exists, so these stay null rather than showing a guessed band (KAK §9.9.4).
        var cert = await db.Certificates
            .Where(c => c.AttemptId == attempt.Id)
            .Select(c => new { c.TotalScaledScore, c.PredictedBand })
            .FirstOrDefaultAsync(ct);

        return new AttemptResultDto(
            attempt.Id, attempt.TotalScore, attempt.MaxScore, attempt.Passed,
            attempt.AutoSubmitted, attempt.ProctorFlagged,
            ParseInts(attempt.SectionScores),
            cert?.TotalScaledScore, cert?.PredictedBand,
            sessionCompleted, null);
    }

    /// <summary>
    /// Loads the caller's own attempt AND re-checks the session gate (GR-1) — a revoked enrollment
    /// must close an in-progress sitting, not just future ones.
    /// </summary>
    private async Task<Attempt> LoadOwnedAsync(Guid userId, Guid attemptId, CancellationToken ct)
    {
        var attempt = await db.Attempts.FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, ct)
            ?? throw new AssessmentException("Percobaan tidak ditemukan.", 404);

        var sessionId = await db.ProgramSessions
            .Where(s => s.AssessmentId == attempt.AssessmentId)
            .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
        if (sessionId is Guid sid) await access.EnsureAccessAsync(userId, sid, ct);

        return attempt;
    }

    private async Task<HashSet<string>> SectionQuestionIdsAsync(Guid assessmentId, string section, CancellationToken ct)
    {
        var parsed = ParseSection(section);
        return (await db.AssessmentQuestions
                .Where(q => q.AssessmentId == assessmentId && q.Question.Section == parsed)
                .Select(q => q.QuestionId)
                .ToListAsync(ct))
            .Select(id => id.ToString()).ToHashSet();
    }

    private static QuestionSection ParseSection(string name)
        => Enum.TryParse<QuestionSection>(name, true, out var s) ? s : QuestionSection.General;

    internal static bool IsStrike(ProctorEventKind kind)
        => kind is ProctorEventKind.VisibilityHidden or ProctorEventKind.WindowBlur;

    private static Dictionary<string, int> ParseInts(string json)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? []; }
        catch { return []; }
    }

    private static List<int> ParseList(string json)
    {
        try { return JsonSerializer.Deserialize<List<int>>(json) ?? []; }
        catch { return []; }
    }

    private static List<string> ParseStrings(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}
