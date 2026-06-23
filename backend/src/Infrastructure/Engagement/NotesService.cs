using Academy.Application.Catalog;
using Academy.Application.Engagement;
using Academy.Application.Learning;
using Academy.Domain;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Engagement;

public class NotesService(AppDbContext db, IEntitlementService entitlement) : INotesService
{
    public async Task<IReadOnlyList<VideoNoteDto>> ListAsync(Guid userId, Guid moduleId, CancellationToken ct = default)
        => await db.VideoNotes.Where(n => n.UserId == userId && n.ModuleId == moduleId)
            .OrderBy(n => n.TimestampSeconds)
            .Select(n => new VideoNoteDto(n.Id, n.TimestampSeconds, n.Type.ToString(), n.Text, n.CreatedAt))
            .ToListAsync(ct);

    public async Task<VideoNoteDto> CreateAsync(Guid userId, Guid moduleId, int timestampSeconds, NoteType type, string? text, CancellationToken ct = default)
    {
        await EnsurePlayableAsync(userId, moduleId, ct);
        var note = new VideoNote
        {
            Id = Guid.CreateVersion7(), UserId = userId, ModuleId = moduleId,
            TimestampSeconds = Math.Max(0, timestampSeconds), Type = type, Text = text?.Trim(),
        };
        db.VideoNotes.Add(note);
        await db.SaveChangesAsync(ct);
        return new VideoNoteDto(note.Id, note.TimestampSeconds, note.Type.ToString(), note.Text, note.CreatedAt);
    }

    public async Task DeleteAsync(Guid userId, Guid noteId, CancellationToken ct = default)
    {
        var n = await db.VideoNotes.FirstOrDefaultAsync(x => x.Id == noteId && x.UserId == userId, ct);
        if (n is not null) { db.VideoNotes.Remove(n); await db.SaveChangesAsync(ct); }
    }

    private async Task EnsurePlayableAsync(Guid userId, Guid moduleId, CancellationToken ct)
    {
        var m = await db.Modules.Where(x => x.Id == moduleId && x.Status == ModuleStatus.Published)
            .Select(x => new { x.IsPreview, x.RequiredPlanTier }).FirstOrDefaultAsync(ct)
            ?? throw new LearningException("Modul tidak ditemukan.", 404);
        if (m.IsPreview) return;
        var tier = await entitlement.GetActiveTierAsync(userId, ct);
        if (!Entitlement.CanAccess(tier, m.IsPreview, m.RequiredPlanTier))
            throw new LearningException("Anda belum memiliki akses ke modul ini.", 403);
    }
}
