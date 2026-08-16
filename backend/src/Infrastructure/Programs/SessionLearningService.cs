using Academy.Application.Abstractions;
using Academy.Application.Programs;
using Academy.Domain;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Learning;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Programs;

/// <summary>
/// Session playback and watch progress (KAK §9.5). Every entry point goes through
/// <see cref="ISessionAccessService"/> first (GR-1); completion is delegated to
/// <see cref="ISessionCompletionService"/> (GR-8).
/// </summary>
public class SessionLearningService(
    AppDbContext db,
    IVideoProvider video,
    ISessionAccessService access,
    ISessionCompletionService completion,
    VideoOptions options) : ISessionLearningService
{
    public async Task<SessionPlaybackDto> GetPlaybackAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        await access.EnsureAccessAsync(userId, sessionId, ct);

        var s = await db.ProgramSessions
            .Where(x => x.Id == sessionId)
            .Select(x => new { x.Id, x.Type, x.ProviderAssetId })
            .FirstOrDefaultAsync(ct)
            ?? throw new ProgramException("Sesi tidak ditemukan.", 404);

        if (s.Type != SessionType.Video)
            throw new ProgramException("Sesi ini bukan sesi video.", 400);

        // The asset id never leaves the server — it is exchanged for a short-TTL signed URL (GR-3).
        var assetId = s.ProviderAssetId ?? s.Id.ToString();
        var ticket = await video.CreatePlaybackTicketAsync(
            assetId, userId, TimeSpan.FromSeconds(options.TicketTtlSeconds), ct);

        return new SessionPlaybackDto(s.Id, ticket.Url, ticket.ExpiresAt, ticket.CaptionsUrl);
    }

    public async Task<SessionContextDto> GetContextAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        await access.EnsureAccessAsync(userId, sessionId, ct);

        var s = await db.ProgramSessions
            .Where(x => x.Id == sessionId)
            .Select(x => new
            {
                x.Id, x.ProgramId, ProgramName = x.Program.Name, x.OrderIndex, x.Type, x.Title,
                x.Description, x.DurationSeconds, x.ScheduledAt, x.LiveMode, x.JoinUrl, x.Location,
                x.AssessmentId,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new ProgramException("Sesi tidak ditemukan.", 404);

        var progress = await LoadProgressAsync(userId, sessionId, ct);

        var hasAssessment = s.AssessmentId is Guid aid
            && await db.AssessmentQuestions.AnyAsync(q => q.AssessmentId == aid, ct);
        var assessmentPassed = s.AssessmentId is Guid aid2
            && await db.Attempts.AnyAsync(a =>
                a.UserId == userId && a.AssessmentId == aid2 && a.SubmittedAt != null && a.Passed, ct);

        var nextSessionId = await db.ProgramSessions
            .Where(x => x.ProgramId == s.ProgramId && x.OrderIndex > s.OrderIndex)
            .OrderBy(x => x.OrderIndex)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

        var nextUnlocked = nextSessionId is Guid nid && await access.CanAccessAsync(userId, nid, ct);

        return new SessionContextDto(
            s.Id, s.ProgramId, s.ProgramName, s.OrderIndex, s.Type.ToString(), s.Title, s.Description,
            s.DurationSeconds, s.ScheduledAt, s.LiveMode?.ToString(), s.JoinUrl, s.Location,
            progress, hasAssessment, assessmentPassed, nextSessionId, nextUnlocked);
    }

    public async Task<SessionProgressDto> GetProgressAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        await access.EnsureAccessAsync(userId, sessionId, ct);
        return await LoadProgressAsync(userId, sessionId, ct);
    }

    public async Task<SessionProgressDto> SaveProgressAsync(
        Guid userId, Guid sessionId, int positionSeconds, decimal percent, CancellationToken ct = default)
    {
        await access.EnsureAccessAsync(userId, sessionId, ct);

        var row = await db.WatchProgress
            .FirstOrDefaultAsync(w => w.UserId == userId && w.SessionId == sessionId, ct);

        if (row is null)
        {
            row = new WatchProgress
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                SessionId = sessionId,      // module_id stays null — CHECK enforces exactly one
            };
            db.WatchProgress.Add(row);
        }

        row.ResumePositionSeconds = Math.Max(0, positionSeconds);
        row.PercentComplete = Math.Max(row.PercentComplete, Math.Clamp(percent, 0m, 100m)); // monotonic
        row.LastWatchedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        // Watching alone may not be enough — a gating test still has to pass (GR-8).
        await completion.TryCompleteAsync(userId, sessionId, ct);

        return await LoadProgressAsync(userId, sessionId, ct);
    }

    private async Task<SessionProgressDto> LoadProgressAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var row = await db.WatchProgress
            .Where(w => w.UserId == userId && w.SessionId == sessionId)
            .Select(w => new { w.ResumePositionSeconds, w.PercentComplete })
            .FirstOrDefaultAsync(ct);

        var completed = await completion.IsCompleteAsync(userId, sessionId, ct);
        var completedAt = completed
            ? await db.SessionCompletions
                .Where(c => c.UserId == userId && c.SessionId == sessionId)
                .Select(c => (DateTimeOffset?)c.CompletedAt).FirstOrDefaultAsync(ct)
            : null;

        var percent = row?.PercentComplete ?? 0m;
        return new SessionProgressDto(
            sessionId, row?.ResumePositionSeconds ?? 0, percent, completed, completedAt,
            CompletionPolicy.IsModuleComplete(percent));
    }
}
