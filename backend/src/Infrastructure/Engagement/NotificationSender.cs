using System.Text.Json;
using Academy.Application.Abstractions;
using Academy.Application.Engagement;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Engagement;

/// <summary>Writes an in-app notification and/or sends an email, each gated by the user's
/// NotificationPreference for the category (falling back to category defaults).</summary>
public class NotificationSender(AppDbContext db, IEmailSender email) : INotificationSender
{
    public async Task DispatchAsync(Guid userId, string category, string type, string title, string body, CancellationToken ct = default)
    {
        var prefs = await db.NotificationPreferences
            .Where(p => p.UserId == userId && p.Category == category).ToListAsync(ct);

        bool Enabled(NotificationChannel ch) =>
            prefs.FirstOrDefault(p => p.Channel == ch)?.Enabled ?? NotificationCategories.DefaultEnabled(category, ch);

        if (Enabled(NotificationChannel.InApp))
        {
            db.Notifications.Add(new Notification
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                Type = type,
                Channel = NotificationChannel.InApp,
                Payload = JsonSerializer.Serialize(new { title, body, category }),
            });
            await db.SaveChangesAsync(ct);
        }

        if (Enabled(NotificationChannel.Email))
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user is not null) await email.SendNotificationAsync(user.Email, user.Name, title, body, ct);
        }
    }
}
