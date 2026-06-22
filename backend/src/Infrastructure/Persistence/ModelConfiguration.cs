// Explicit constraints/indexes/relationships not covered by convention.
// Column names come from EFCore.NamingConventions snake_case (configured in DI).
//
// FK policy (decision: "Full FKs per TSD DDL"):
//   - Retained / financial / integrity data → DeleteBehavior.Restrict
//     (subscriptions, payments, watch_progress, certificates, capstone_submissions,
//      quiz_attempts, and module references from user-data rows). A module/level/user
//      that has retained rows cannot be hard-deleted — consistent with GR-6/GR-7/GR-10
//      (soft-delete + anonymize; never destroy completion history).
//   - Ephemeral, user-scoped engagement → Cascade (removed with the user IF ever hard-deleted;
//      in practice users are soft-deleted + anonymized, so this rarely fires).
//   - Optional/nullable actor links → SetNull (audit actor, product feedback, unassigned seat).
using Academy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Academy.Infrastructure.Persistence;

// ---------------------------------------------------------------- Identity & org

public class UserConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> e)
    {
        e.HasIndex(x => x.Email).IsUnique();
        e.Property(x => x.Email).HasMaxLength(320);
        e.HasMany(x => x.ExternalLogins).WithOne(x => x.User)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.RefreshTokens).WithOne(x => x.User)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserExternalLoginConfig : IEntityTypeConfiguration<UserExternalLogin>
{
    public void Configure(EntityTypeBuilder<UserExternalLogin> e)
        => e.HasIndex(x => new { x.Provider, x.ProviderKey }).IsUnique();
}

public class RefreshTokenConfig : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> e)
    {
        e.HasIndex(x => x.TokenHash);
        e.HasIndex(x => x.FamilyId);
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserTokenConfig : IEntityTypeConfiguration<UserToken>
{
    public void Configure(EntityTypeBuilder<UserToken> e)
    {
        e.HasIndex(x => x.TokenHash);
        e.HasIndex(x => new { x.UserId, x.Purpose });
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrganizationConfig : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> e)
    {
        e.HasOne<User>().WithMany().HasForeignKey(x => x.BillingOwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Seats).WithOne(x => x.Org)
            .HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.Memberships).WithOne(x => x.Org)
            .HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrgSeatConfig : IEntityTypeConfiguration<OrgSeat>
{
    public void Configure(EntityTypeBuilder<OrgSeat> e)
        => e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
}

public class OrgMembershipConfig : IEntityTypeConfiguration<OrgMembership>
{
    public void Configure(EntityTypeBuilder<OrgMembership> e)
    {
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.OrgId, x.UserId }).IsUnique();
    }
}

// ---------------------------------------------------------------- Catalog

public class LevelConfig : IEntityTypeConfiguration<Level>
{
    public void Configure(EntityTypeBuilder<Level> e) => e.HasIndex(x => x.Slug).IsUnique();
}

public class CategoryConfig : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> e) => e.HasIndex(x => x.Slug).IsUnique();
}

public class TagConfig : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> e) => e.HasIndex(x => x.Slug).IsUnique();
}

public class ModuleConfig : IEntityTypeConfiguration<Module>
{
    public void Configure(EntityTypeBuilder<Module> e)
    {
        e.HasIndex(x => x.Slug).IsUnique();
        e.HasIndex(x => new { x.TrackId, x.OrderIndex });
        e.HasIndex(x => x.IsPreview);
        e.HasOne(x => x.Track).WithMany(x => x.Modules)
            .HasForeignKey(x => x.TrackId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class TrackConfig : IEntityTypeConfiguration<Track>
{
    public void Configure(EntityTypeBuilder<Track> e)
        => e.HasOne(x => x.Level).WithMany(x => x.Tracks)
            .HasForeignKey(x => x.LevelId).OnDelete(DeleteBehavior.Cascade);
}

public class ModuleTagConfig : IEntityTypeConfiguration<ModuleTag>
{
    public void Configure(EntityTypeBuilder<ModuleTag> e)
    {
        e.HasKey(x => new { x.ModuleId, x.TagId });
        e.HasOne(x => x.Module).WithMany(x => x.ModuleTags)
            .HasForeignKey(x => x.ModuleId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Tag).WithMany()
            .HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ResourceConfig : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> e)
        => e.HasOne(x => x.Module).WithMany(x => x.Resources)
            .HasForeignKey(x => x.ModuleId).OnDelete(DeleteBehavior.Cascade);
}

// ---------------------------------------------------------------- Billing

public class PlanConfig : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> e)
    {
        e.Property(x => x.PriceMonthly).HasPrecision(18, 2);
        e.Property(x => x.PriceAnnual).HasPrecision(18, 2);
        e.Property(x => x.IncludedContentMapping).HasColumnType("jsonb");
    }
}

public class SubscriptionConfig : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> e)
    {
        e.Property(x => x.PriceLockedIdr).HasPrecision(18, 2);
        e.HasIndex(x => x.UserId);
        e.HasIndex(x => x.Status);
        e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SubscriptionEventConfig : IEntityTypeConfiguration<SubscriptionEvent>
{
    public void Configure(EntityTypeBuilder<SubscriptionEvent> e)
        => e.HasOne(x => x.Subscription).WithMany(x => x.Events)
            .HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
}

public class PaymentTransactionConfig : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> e)
    {
        e.Property(x => x.AmountIdr).HasPrecision(18, 2);
        e.Property(x => x.XenditIds).HasColumnType("jsonb");
        e.Property(x => x.RawPayload).HasColumnType("jsonb");
        e.HasIndex(x => x.UserId);
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Subscription>().WithMany().HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WebhookEventConfig : IEntityTypeConfiguration<WebhookEvent>
{
    public void Configure(EntityTypeBuilder<WebhookEvent> e)
    {
        e.HasIndex(x => x.ExternalId).IsUnique();          // idempotency
        e.Property(x => x.Payload).HasColumnType("jsonb");
    }
}

// ---------------------------------------------------------------- Learning

public class WatchProgressConfig : IEntityTypeConfiguration<WatchProgress>
{
    public void Configure(EntityTypeBuilder<WatchProgress> e)
    {
        e.HasIndex(x => new { x.UserId, x.ModuleId }).IsUnique();
        e.Property(x => x.PercentComplete).HasPrecision(5, 2);
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Module>().WithMany().HasForeignKey(x => x.ModuleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class QuizConfig : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> e)
    {
        e.HasOne(x => x.Module).WithMany().HasForeignKey(x => x.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.Questions).WithOne(x => x.Quiz)
            .HasForeignKey(x => x.QuizId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class QuizQuestionConfig : IEntityTypeConfiguration<QuizQuestion>
{
    public void Configure(EntityTypeBuilder<QuizQuestion> e)
        => e.Property(x => x.Choices).HasColumnType("jsonb");
}

public class QuizAttemptConfig : IEntityTypeConfiguration<QuizAttempt>
{
    public void Configure(EntityTypeBuilder<QuizAttempt> e)
    {
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Quiz>().WithMany().HasForeignKey(x => x.QuizId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CapstoneConfig : IEntityTypeConfiguration<Capstone>
{
    public void Configure(EntityTypeBuilder<Capstone> e)
        => e.HasOne(x => x.Level).WithMany().HasForeignKey(x => x.LevelId)
            .OnDelete(DeleteBehavior.Cascade);
}

public class CapstoneSubmissionConfig : IEntityTypeConfiguration<CapstoneSubmission>
{
    public void Configure(EntityTypeBuilder<CapstoneSubmission> e)
    {
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Capstone>().WithMany().HasForeignKey(x => x.CapstoneId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CertificateConfig : IEntityTypeConfiguration<Certificate>
{
    public void Configure(EntityTypeBuilder<Certificate> e)
    {
        e.HasIndex(x => x.VerificationCode).IsUnique();
        e.HasIndex(x => new { x.UserId, x.LevelId }).IsUnique();   // one cert per (user, level)
        e.Property(x => x.CompletedModuleIds).HasColumnType("jsonb"); // snapshot
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Level>().WithMany().HasForeignKey(x => x.LevelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// ---------------------------------------------------------------- Engagement & system

public class NotificationConfig : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> e)
    {
        e.Property(x => x.Payload).HasColumnType("jsonb");
        e.HasIndex(x => new { x.UserId, x.ReadAt });
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class NotificationPreferenceConfig : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> e)
    {
        e.HasIndex(x => new { x.UserId, x.Category, x.Channel }).IsUnique();
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ModuleFeedbackConfig : IEntityTypeConfiguration<ModuleFeedback>
{
    public void Configure(EntityTypeBuilder<ModuleFeedback> e)
    {
        e.HasIndex(x => new { x.UserId, x.ModuleId }).IsUnique();
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        e.HasOne<Module>().WithMany().HasForeignKey(x => x.ModuleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FeedbackSubmissionConfig : IEntityTypeConfiguration<FeedbackSubmission>
{
    public void Configure(EntityTypeBuilder<FeedbackSubmission> e)
        => e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
}

public class OnboardingSurveyConfig : IEntityTypeConfiguration<OnboardingSurvey>
{
    public void Configure(EntityTypeBuilder<OnboardingSurvey> e)
    {
        e.Property(x => x.Goals).HasColumnType("jsonb");
        e.Property(x => x.PreferredTools).HasColumnType("jsonb");
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class VideoNoteConfig : IEntityTypeConfiguration<VideoNote>
{
    public void Configure(EntityTypeBuilder<VideoNote> e)
    {
        e.HasIndex(x => new { x.UserId, x.ModuleId });
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        e.HasOne<Module>().WithMany().HasForeignKey(x => x.ModuleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TourStateConfig : IEntityTypeConfiguration<TourState>
{
    public void Configure(EntityTypeBuilder<TourState> e)
    {
        e.HasIndex(x => new { x.UserId, x.TourKey }).IsUnique();
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AuditLogConfig : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> e)
    {
        e.Property(x => x.Metadata).HasColumnType("jsonb");
        e.HasIndex(x => x.ActorUserId);
        e.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
