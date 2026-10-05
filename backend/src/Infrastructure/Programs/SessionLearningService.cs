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
/// Session context, part playback and per-part watch progress (KAK §9.5, parts since 2026-10-05).
/// Every entry point goes through <see cref="ISessionAccessService"/> first (GR-1); completion is
/// delegated to <see cref="ISessionCompletionService"/> (GR-8).
/// </summary>
public class SessionLearningService(
    AppDbContext db,
    IVideoProvider video,
    ISessionAccessService access,
    ISessionCompletionService completion,
    SessionPartStates partStates,
    VideoOptions options) : ISessionLearningService
{
    public async Task<SessionContextDto> GetContextAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        await access.EnsureAccessAsync(userId, sessionId, ct);

        var s = await db.ProgramSessions
            .Where(x => x.Id == sessionId)
            .Select(x => new
            {
                x.Id, x.ProgramId, ProgramName = x.Program.Name, x.OrderIndex, x.Type, x.Title,
                x.Description, x.ScheduledAt, x.LiveMode, x.JoinUrl, x.Location,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new ProgramException("Sesi tidak ditemukan.", 404);

        var states = s.Type == SessionType.Video ? await partStates.LoadAsync(userId, sessionId, ct) : [];
        // No asset id here (GR-3): playback is minted per part, after the gate.
        var parts = states.Select(st => new SessionPartDto(
            st.Part.Id, st.Part.OrderIndex, st.Part.Kind.ToString(), st.Part.Title, st.Status.ToString(),
            st.Part.DurationSeconds, st.ResumePositionSeconds, st.PercentComplete,
            st.Part.AssessmentId, st.AttemptsUsed, st.Facts.FailedAttempts,
            st.Part.Kind == SessionPartKind.Test && st.Facts.Done, st.Facts.DiscussionAfterFailures)).ToList();

        // VIDEO: an admin edit (part removed, test emptied) can make every part Done without a
        // completion event — re-evaluate through the completion service (GR-8); cheap once complete.
        var completed = s.Type == SessionType.Video
            ? await completion.TryCompleteAsync(userId, sessionId, ct)
            : await completion.IsCompleteAsync(userId, sessionId, ct);
        var completedAt = completed
            ? await db.SessionCompletions
                .Where(c => c.UserId == userId && c.SessionId == sessionId)
                .Select(c => (DateTimeOffset?)c.CompletedAt).FirstOrDefaultAsync(ct)
            : null;

        var nextSessionId = await db.ProgramSessions
            .Where(x => x.ProgramId == s.ProgramId && x.OrderIndex > s.OrderIndex)
            .OrderBy(x => x.OrderIndex)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

        var nextUnlocked = nextSessionId is Guid nid && await access.CanAccessAsync(userId, nid, ct);

        return new SessionContextDto(
            s.Id, s.ProgramId, s.ProgramName, s.OrderIndex, s.Type.ToString(), s.Title, s.Description,
            s.ScheduledAt, s.LiveMode?.ToString(), s.JoinUrl, s.Location,
            completed, completedAt, parts, nextSessionId, nextUnlocked);
    }

    public async Task<PartPlaybackDto> GetPartPlaybackAsync(
        Guid userId, Guid sessionId, Guid partId, CancellationToken ct = default)
    {
        var part = await access.EnsurePartAccessAsync(userId, sessionId, partId, ct);
        if (part.Kind == SessionPartKind.Test)
            throw new ProgramException("Bagian ini bukan video.", 400);
        // The asset id never leaves the server — it is exchanged for a short-TTL signed URL (GR-3).
        var ticket = await video.CreatePlaybackTicketAsync(
            part.ProviderAssetId!, userId, TimeSpan.FromSeconds(options.TicketTtlSeconds), ct);
        return new PartPlaybackDto(part.Id, ticket.Url, ticket.ExpiresAt, ticket.CaptionsUrl);
    }

    public async Task<PartProgressDto> GetPartProgressAsync(
        Guid userId, Guid sessionId, Guid partId, CancellationToken ct = default)
    {
        await access.EnsurePartAccessAsync(userId, sessionId, partId, ct);

        var row = await db.WatchProgress
            .Where(w => w.UserId == userId && w.PartId == partId)
            .Select(w => new { w.ResumePositionSeconds, w.PercentComplete })
            .FirstOrDefaultAsync(ct);
        var percent = row?.PercentComplete ?? 0m;

        return new PartProgressDto(partId, row?.ResumePositionSeconds ?? 0, percent,
            CompletionPolicy.IsModuleComplete(percent), await completion.IsCompleteAsync(userId, sessionId, ct));
    }

    public async Task<PartProgressDto> SavePartProgressAsync(
        Guid userId, Guid sessionId, Guid partId, int positionSeconds, decimal percent, CancellationToken ct = default)
    {
        var part = await access.EnsurePartAccessAsync(userId, sessionId, partId, ct);
        if (part.Kind == SessionPartKind.Test)
            throw new ProgramException("Bagian ini bukan video.", 400);

        var row = await db.WatchProgress.FirstOrDefaultAsync(w => w.UserId == userId && w.PartId == partId, ct);
        if (row is null)
        {
            // session_id is kept alongside part_id — CHECK enforces exactly one of module/session.
            row = new WatchProgress { Id = Guid.CreateVersion7(), UserId = userId, SessionId = sessionId, PartId = partId };
            db.WatchProgress.Add(row);
        }

        row.ResumePositionSeconds = Math.Max(0, positionSeconds);
        row.PercentComplete = Math.Max(row.PercentComplete, Math.Clamp(percent, 0m, 100m)); // monotonic
        row.LastWatchedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        // Watching one part may finish the session — the completion service decides (GR-8).
        // Only a part at/over the threshold can finish it, so skip the full re-evaluation otherwise.
        var sessionDone = CompletionPolicy.IsModuleComplete(row.PercentComplete)
            ? await completion.TryCompleteAsync(userId, sessionId, ct)
            : await completion.IsCompleteAsync(userId, sessionId, ct);

        // Built from the saved row, not GetPartProgressAsync: that would re-run the gate per tick.
        return new PartProgressDto(partId, row.ResumePositionSeconds, row.PercentComplete,
            CompletionPolicy.IsModuleComplete(row.PercentComplete), sessionDone);
    }
}
