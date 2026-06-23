using Academy.Domain.Enums;

namespace Academy.Application.Engagement;

/// <summary>Notification categories (preference matrix rows). Stored on NotificationPreference.Category.</summary>
public static class NotificationCategories
{
    public const string Progress = "progress"; // module done, certificate issued
    public const string Content = "content";   // new/bonus modules
    public const string Billing = "billing";   // payments, renewal, dunning
    public const string Promo = "promo";       // offers, tips

    public static readonly IReadOnlyList<string> All = [Progress, Content, Billing, Promo];

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [Progress] = "Progres & sertifikat",
        [Content] = "Modul & konten baru",
        [Billing] = "Tagihan & langganan",
        [Promo] = "Promosi & tips",
    };

    /// <summary>Default enablement when the user has no explicit preference row.</summary>
    public static bool DefaultEnabled(string category, NotificationChannel channel) => category switch
    {
        Promo => false,
        Content => channel == NotificationChannel.InApp,
        _ => true, // progress + billing: both channels on
    };
}

public record NotificationDto(Guid Id, string Type, string Title, string Body, bool Read, DateTimeOffset CreatedAt);
public record NotificationListDto(IReadOnlyList<NotificationDto> Items, int UnreadCount);

public record NotificationPrefDto(string Category, string Channel, bool Enabled);
public record UpdatePreferencesRequest(IReadOnlyList<NotificationPrefDto> Preferences);

public interface INotificationService
{
    Task<NotificationListDto> ListAsync(Guid userId, bool unreadOnly, CancellationToken ct = default);
    Task MarkReadAsync(Guid userId, Guid id, CancellationToken ct = default);
    Task MarkAllReadAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationPrefDto>> GetPreferencesAsync(Guid userId, CancellationToken ct = default);
    Task UpdatePreferencesAsync(Guid userId, IReadOnlyList<NotificationPrefDto> prefs, CancellationToken ct = default);
}
