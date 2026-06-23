using Academy.Application.Engagement;
using Academy.Domain.Entities;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Engagement;

public class ContentService(AppDbContext db) : IContentService
{
    public async Task<IReadOnlyList<FaqItemDto>> GetFaqAsync(CancellationToken ct = default)
        => await db.FaqItems
            .Where(f => f.IsPublished)
            .OrderBy(f => f.OrderIndex)
            .Select(f => new FaqItemDto(f.Id, f.Question, f.Answer))
            .ToListAsync(ct);

    public async Task SubmitContactAsync(ContactRequest req, CancellationToken ct = default)
    {
        db.ContactSubmissions.Add(new ContactSubmission
        {
            Id = Guid.CreateVersion7(),
            Name = req.Name.Trim(),
            Email = req.Email.Trim(),
            Message = req.Message.Trim(),
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task SubmitFeedbackAsync(Guid? userId, FeedbackRequest req, CancellationToken ct = default)
    {
        db.FeedbackSubmissions.Add(new FeedbackSubmission
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Message = req.Message.Trim(),
            Context = req.Context,
        });
        await db.SaveChangesAsync(ct);
    }
}
