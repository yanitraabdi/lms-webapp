// Notifications, feedback, survey, notes, tour, system (TSD §6.5)
using Academy.Domain.Common;
using Academy.Domain.Enums;

namespace Academy.Domain.Entities;

public class Notification : Entity                          // P1
{
    public Guid UserId { get; set; }
    public string Type { get; set; } = default!;
    public string Payload { get; set; } = "{}";            // jsonb
    public NotificationChannel Channel { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

public class NotificationPreference : Entity               // P1 — UNIQUE(UserId, Category, Channel)
{
    public Guid UserId { get; set; }
    public string Category { get; set; } = default!;
    public NotificationChannel Channel { get; set; }
    public bool Enabled { get; set; } = true;
}

public class ModuleFeedback : Entity                        // P1 — UNIQUE(UserId, ModuleId)
{
    public Guid UserId { get; set; }
    public Guid ModuleId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

public class FeedbackSubmission : Entity                    // product-level feedback
{
    public Guid? UserId { get; set; }
    public string Message { get; set; } = default!;
    public string? Context { get; set; }
}

public class OnboardingSurvey : Entity                      // P1 — seeds recommended-next
{
    public Guid UserId { get; set; }
    public string? Role { get; set; }
    public string Goals { get; set; } = "[]";              // jsonb
    public string PreferredTools { get; set; } = "[]";     // jsonb
}

public class VideoNote : Entity                            // P2 — timestamped note/bookmark
{
    public Guid UserId { get; set; }
    public Guid ModuleId { get; set; }
    public int TimestampSeconds { get; set; }
    public NoteType Type { get; set; }
    public string? Text { get; set; }
}

public class TourState : Entity                            // driver.js tour state per user
{
    public Guid UserId { get; set; }
    public string TourKey { get; set; } = default!;
    public TourStatus Status { get; set; }
}

public class FaqItem : Entity
{
    public string Question { get; set; } = default!;
    public string Answer { get; set; } = default!;
    public int OrderIndex { get; set; }
    public bool IsPublished { get; set; } = true;
}

public class ContactSubmission : Entity
{
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Message { get; set; } = default!;
}

public class AuditLog : Entity
{
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = default!;
    public string Target { get; set; } = default!;
    public string Metadata { get; set; } = "{}";           // jsonb
}
