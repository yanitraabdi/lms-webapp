// EF Core 10 / Npgsql. snake_case via EFCore.NamingConventions (configured in DI).
using Academy.Domain.Common;
using Academy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Academy.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Identity & org
    public DbSet<User> Users => Set<User>();
    public DbSet<UserExternalLogin> UserExternalLogins => Set<UserExternalLogin>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrgSeat> OrgSeats => Set<OrgSeat>();
    public DbSet<OrgMembership> OrgMemberships => Set<OrgMembership>();

    // Catalog
    public DbSet<Level> Levels => Set<Level>();
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Module> Modules => Set<Module>();
    public DbSet<ModuleTag> ModuleTags => Set<ModuleTag>();
    public DbSet<Resource> Resources => Set<Resource>();

    // Billing
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionEvent> SubscriptionEvents => Set<SubscriptionEvent>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    // Learning
    public DbSet<WatchProgress> WatchProgress => Set<WatchProgress>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
    public DbSet<Capstone> Capstones => Set<Capstone>();
    public DbSet<CapstoneSubmission> CapstoneSubmissions => Set<CapstoneSubmission>();
    public DbSet<Certificate> Certificates => Set<Certificate>();

    // Engagement & system
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<ModuleFeedback> ModuleFeedbacks => Set<ModuleFeedback>();
    public DbSet<FeedbackSubmission> FeedbackSubmissions => Set<FeedbackSubmission>();
    public DbSet<OnboardingSurvey> OnboardingSurveys => Set<OnboardingSurvey>();
    public DbSet<VideoNote> VideoNotes => Set<VideoNote>();
    public DbSet<TourState> TourStates => Set<TourState>();
    public DbSet<FaqItem> FaqItems => Set<FaqItem>();
    public DbSet<ContactSubmission> ContactSubmissions => Set<ContactSubmission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Store ALL enums as text (stable + human-readable) rather than integers.
        // This is the real value-converter fix (HaveConversion), not just a provider-type hint.
        configurationBuilder.Properties<Enum>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // App-assigned UUID v7 PKs — never DB-generated.
        foreach (var e in b.Model.GetEntityTypes())
        {
            var pk = e.FindProperty(nameof(Entity.Id));
            if (pk is not null && pk.ClrType == typeof(Guid))
                pk.ValueGenerated = ValueGenerated.Never;
        }

        // Soft-delete: hide deleted users globally (GR-10).
        b.Entity<User>().HasQueryFilter(u => u.DeletedAt == null);
    }

    public override int SaveChanges()
    {
        Touch();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        Touch();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
    }

    private void Touch()
    {
        foreach (var e in ChangeTracker.Entries<Entity>())
            if (e.State == EntityState.Modified)
                e.Entity.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
