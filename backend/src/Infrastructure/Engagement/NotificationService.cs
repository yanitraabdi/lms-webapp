using System.Text.Json;
using Academy.Application.Engagement;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Engagement;

public class NotificationService(AppDbContext db) : INotificationService
{
    public async Task<NotificationListDto> ListAsync(Guid userId, bool unreadOnly, CancellationToken ct = default)
    {
        var q = db.Notifications.Where(n => n.UserId == userId && n.Channel == NotificationChannel.InApp);
        if (unreadOnly) q = q.Where(n => n.ReadAt == null);
        var rows = await q.OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync(ct);
        var unread = await db.Notifications.CountAsync(
            n => n.UserId == userId && n.Channel == NotificationChannel.InApp && n.ReadAt == null, ct);
        return new NotificationListDto(rows.Select(Map).ToList(), unread);
    }

    public async Task MarkReadAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (n is { ReadAt: null }) { n.ReadAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); }
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        await db.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
    }

    public async Task<IReadOnlyList<NotificationPrefDto>> GetPreferencesAsync(Guid userId, CancellationToken ct = default)
    {
        var stored = await db.NotificationPreferences.Where(p => p.UserId == userId).ToListAsync(ct);
        var result = new List<NotificationPrefDto>();
        foreach (var cat in NotificationCategories.All)
            foreach (var ch in new[] { NotificationChannel.InApp, NotificationChannel.Email })
            {
                var row = stored.FirstOrDefault(p => p.Category == cat && p.Channel == ch);
                result.Add(new NotificationPrefDto(cat, ch.ToString(), row?.Enabled ?? NotificationCategories.DefaultEnabled(cat, ch)));
            }
        return result;
    }

    public async Task UpdatePreferencesAsync(Guid userId, IReadOnlyList<NotificationPrefDto> prefs, CancellationToken ct = default)
    {
        var stored = await db.NotificationPreferences.Where(p => p.UserId == userId).ToListAsync(ct);
        foreach (var pref in prefs)
        {
            if (!NotificationCategories.All.Contains(pref.Category)) continue;
            if (!Enum.TryParse<NotificationChannel>(pref.Channel, true, out var ch)) continue;
            var row = stored.FirstOrDefault(p => p.Category == pref.Category && p.Channel == ch);
            if (row is null)
                db.NotificationPreferences.Add(new NotificationPreference { Id = Guid.CreateVersion7(), UserId = userId, Category = pref.Category, Channel = ch, Enabled = pref.Enabled });
            else
                row.Enabled = pref.Enabled;
        }
        await db.SaveChangesAsync(ct);
    }

    private static NotificationDto Map(Notification n)
    {
        string title = n.Type, body = "";
        try
        {
            using var doc = JsonDocument.Parse(n.Payload);
            if (doc.RootElement.TryGetProperty("title", out var t)) title = t.GetString() ?? title;
            if (doc.RootElement.TryGetProperty("body", out var b)) body = b.GetString() ?? "";
        }
        catch { /* keep defaults */ }
        return new NotificationDto(n.Id, n.Type, title, body, n.ReadAt != null, n.CreatedAt);
    }
}
