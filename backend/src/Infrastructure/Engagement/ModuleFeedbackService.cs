using Academy.Application.Engagement;
using Academy.Domain.Entities;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Engagement;

public class ModuleFeedbackService(AppDbContext db) : IModuleFeedbackService
{
    public async Task<ModuleFeedbackDto> GetAsync(Guid? userId, Guid moduleId, CancellationToken ct = default)
    {
        var all = await db.ModuleFeedbacks.Where(f => f.ModuleId == moduleId).Select(f => f.Rating).ToListAsync(ct);
        var avg = all.Count > 0 ? Math.Round(all.Average(), 2) : 0d;

        int? myRating = null;
        string? myComment = null;
        if (userId is Guid uid)
        {
            var mine = await db.ModuleFeedbacks.FirstOrDefaultAsync(f => f.ModuleId == moduleId && f.UserId == uid, ct);
            myRating = mine?.Rating;
            myComment = mine?.Comment;
        }
        return new ModuleFeedbackDto(myRating, myComment, avg, all.Count);
    }

    public async Task UpsertAsync(Guid userId, Guid moduleId, int rating, string? comment, CancellationToken ct = default)
    {
        rating = Math.Clamp(rating, 1, 5);
        var existing = await db.ModuleFeedbacks.FirstOrDefaultAsync(f => f.ModuleId == moduleId && f.UserId == userId, ct);
        if (existing is null)
            db.ModuleFeedbacks.Add(new ModuleFeedback { Id = Guid.CreateVersion7(), UserId = userId, ModuleId = moduleId, Rating = rating, Comment = comment?.Trim() });
        else { existing.Rating = rating; existing.Comment = comment?.Trim(); }
        await db.SaveChangesAsync(ct);
    }
}
