// Infrastructure composition root. Api calls AddInfrastructure(configuration).
using Academy.Application.Abstractions;
using Academy.Application.Admin;
using Academy.Application.Auth;
using Academy.Application.Billing;
using Academy.Application.Catalog;
using Academy.Application.Engagement;
using Academy.Application.Learning;
using Academy.Application.Assessments;
using Academy.Application.Programs;
using Academy.Infrastructure.Admin;
using Academy.Infrastructure.Assessments;
using Academy.Infrastructure.Programs;
using Academy.Infrastructure.Engagement;
using Academy.Infrastructure.Auth;
using Academy.Infrastructure.Billing;
using Academy.Infrastructure.Catalog;
using Academy.Infrastructure.Email;
using Academy.Infrastructure.Jobs;
using Academy.Infrastructure.Learning;
using Academy.Infrastructure.Media;
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

        // Retry on transient database failures.
        //
        // Without this, a Postgres restart is an OUTAGE rather than a blip: the connection pool is
        // left holding sockets to a server that has gone, every request hangs on "Attempted to
        // read past the end of the stream", and the API stays wedged until a human restarts it.
        // That happened ten times in two days on the deployed stack, and recovery each time was a
        // manual `docker compose restart api`.
        //
        // Safe to apply globally because nothing in this codebase opens an explicit transaction —
        // the retrying strategy refuses user-initiated transactions, so a BeginTransaction added
        // later will throw at runtime and must be wrapped in the execution strategy.
        //
        // One consequence worth naming: a SaveChanges that reached the server but whose
        // acknowledgement was lost gets retried, so a write can be attempted twice. The webhook
        // path is already built for that — its unique index on external_id is what makes delivery
        // idempotent (GR-2) — and a retry that loses the race surfaces as DbUpdateException,
        // which it already handles.
        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name)
                .EnableRetryOnFailure(
                    maxRetryCount: 5,
                    // Long enough to cover Postgres replaying its write-ahead log on restart,
                    // which took roughly four seconds on the deployed stack.
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null))
            .UseSnakeCaseNamingConvention());

        // Auth (M1)
        services.AddSingleton(Options.Create(AuthOptionsFactory.Build(configuration)));
        services.AddSingleton<JwtTokenService>();
        services.AddSingleton<UserPasswordHasher>();
        // Email:Provider "dev" logs to the console (default); "smtp" actually sends the three
        // authentication emails. The factory throws at startup on an incomplete smtp config —
        // falling back to the logger would leave registration returning 200 while the
        // verification mail went nowhere.
        var email = EmailOptionsFactory.Build(configuration);
        services.AddSingleton(email);
        if (email.IsSmtp) services.AddScoped<IEmailSender, SmtpEmailSender>();
        else services.AddScoped<IEmailSender, DevEmailSender>();
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
        // Media storage (audio). LocalObjectStorage is the dev-sim; when an R2 adapter exists,
        // swap this registration (and add a config switch then, not before).
        services.AddSingleton(MediaOptionsFactory.Build(configuration));
        services.AddSingleton<IObjectStorage, LocalObjectStorage>();
        services.AddSingleton<MediaSigner>();
        // DevVideoProvider simulates Bunny signed playback and points every session at one public
        // test stream; "bunny" plays the session's real video. The factory throws at startup on an
        // incomplete bunny config rather than 403-ing for every learner at play time.
        var videoOptions = VideoOptionsFactory.Build(configuration);
        if (videoOptions.IsBunny)
            services.AddScoped<IVideoProvider, BunnyVideoProvider>();
        else
            services.AddScoped<IVideoProvider, DevVideoProvider>();

        // The admin video picker. Real only under Bunny WITH a library API key; otherwise a stand-in
        // that says why, so the picker falls back to manual entry instead of erroring.
        if (videoOptions.IsBunny && !string.IsNullOrWhiteSpace(videoOptions.ApiKey))
            services.AddHttpClient<IVideoLibrary, BunnyVideoLibrary>();
        else
            services.AddSingleton<IVideoLibrary>(new UnavailableVideoLibrary(videoOptions.IsBunny
                ? "Kunci API Bunny (BUNNY_API_KEY) belum diatur. Isi ID video secara manual."
                : "Pustaka video hanya tersedia saat penyedia video adalah Bunny. Isi ID video secara manual."));
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

        // INVERTA (M2): programs, enrollment, the access gate, completion
        services.AddScoped<IProgramService, ProgramService>();
        services.AddScoped<IEnrollmentService, EnrollmentService>();
        services.AddScoped<SessionPartStates>();
        services.AddScoped<ISessionAccessService, SessionAccessService>();
        services.AddScoped<ISessionCompletionService, SessionCompletionService>();
        services.AddScoped<IProgramAdminService, ProgramAdminService>();
        services.AddScoped<ProgramSeeder>();
        services.AddScoped<Assessments.SampleTestSeeder>();
        services.AddScoped<Assessments.PlaceholderFinalExamSeeder>();
        services.AddScoped<Assessments.SessionQuizSeeder>();

        // INVERTA (M3): session playback/progress + the assessment engine
        services.AddScoped<ISessionLearningService, SessionLearningService>();
        services.AddScoped<IAssessmentService, AssessmentService>();
        services.AddScoped<IQuestionBankService, QuestionBankService>();
        services.AddScoped<IQuestionImportService, QuestionImportService>();
        services.AddScoped<IAssessmentAdminService, AssessmentAdminService>();

        // INVERTA (M4): sectional sitting, proctoring, ITP scoring, certificates
        services.AddScoped<IFinalAssessmentService, FinalAssessmentService>();
        services.AddScoped<IProctorService, ProctorService>();
        services.AddScoped<IScoreConversionService, ScoreConversionService>();
        services.AddScoped<IProgramCertificateService, ProgramCertificateService>();
        services.AddScoped<IScoreBandAdminService, ScoreBandAdminService>();

        // INVERTA (M5): live attendance, H-1 reminders, operational dashboards
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<ILiveSessionReminder, LiveSessionReminder>();
        services.AddScoped<IAdminOperationsService, AdminOperationsService>();
        services.AddHostedService<LiveReminderService>();

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
