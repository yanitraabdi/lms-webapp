// Infrastructure composition root. Api calls AddInfrastructure(configuration).
using Academy.Application.Abstractions;
using Academy.Application.Admin;
using Academy.Application.Auth;
using Academy.Application.Billing;
using Academy.Application.Catalog;
using Academy.Application.Engagement;
using Academy.Application.Learning;
using Academy.Infrastructure.Admin;
using Academy.Infrastructure.Engagement;
using Academy.Infrastructure.Auth;
using Academy.Infrastructure.Billing;
using Academy.Infrastructure.Catalog;
using Academy.Infrastructure.Email;
using Academy.Infrastructure.Jobs;
using Academy.Infrastructure.Learning;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Academy.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? "Host=localhost;Port=5432;Database=academy;Username=academy;Password=academy";

        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name))
            .UseSnakeCaseNamingConvention());

        // Auth (M1)
        services.AddSingleton(Options.Create(AuthOptionsFactory.Build(configuration)));
        services.AddSingleton<JwtTokenService>();
        services.AddSingleton<UserPasswordHasher>();
        services.AddScoped<IEmailSender, DevEmailSender>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddHostedService<AccountAnonymizationService>();

        // Catalog + entitlement (M2)
        services.AddScoped<IEntitlementService, EntitlementService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<CatalogSeeder>();

        // Billing / payments (M3)
        var billing = BillingOptionsFactory.Build(configuration);
        services.AddSingleton(billing);
        // DevPaymentGateway simulates Xendit locally; swap to XenditGateway when Billing:Provider="xendit".
        services.AddScoped<IPaymentGateway, DevPaymentGateway>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IPaymentWebhookProcessor, PaymentWebhookProcessor>();
        services.AddScoped<IBillingReconciler, BillingReconciler>();
        services.AddScoped<PlansSeeder>();
        services.AddHostedService<BillingReconcileService>();

        // Player / progress / certificates (M4)
        services.AddSingleton(VideoOptionsFactory.Build(configuration));
        // DevVideoProvider simulates Bunny signed playback; swap to BunnyVideoProvider when Video:Provider="bunny".
        services.AddScoped<IVideoProvider, DevVideoProvider>();
        services.AddSingleton<CertificatePdf>();
        services.AddScoped<ICertificateService, CertificateService>();
        services.AddScoped<ILearningService, LearningService>();

        // Admin (M5)
        services.AddSingleton(RevalidateOptionsFactory.Build(configuration));
        services.AddHttpClient<IContentRevalidator, NextContentRevalidator>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<ICurriculumAdminService, CurriculumAdminService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IAdminAnalyticsService, AdminAnalyticsService>();
        services.AddScoped<DevAdminSeeder>();

        // Content + onboarding (M6)
        services.AddScoped<IContentService, ContentService>();
        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddScoped<FaqSeeder>();

        // Engagement (M7): notifications, notes, ratings, quizzes, completion gating
        services.AddScoped<INotificationSender, NotificationSender>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotesService, NotesService>();
        services.AddScoped<IModuleFeedbackService, ModuleFeedbackService>();
        services.AddScoped<IModuleCompletionService, ModuleCompletionService>();
        services.AddScoped<IQuizService, QuizService>();
        services.AddScoped<IQuizAdminService, QuizAdminService>();

        return services;
    }
}
