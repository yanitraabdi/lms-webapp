// Infrastructure composition root. Api calls AddInfrastructure(configuration).
using Academy.Application.Abstractions;
using Academy.Application.Auth;
using Academy.Application.Catalog;
using Academy.Infrastructure.Auth;
using Academy.Infrastructure.Catalog;
using Academy.Infrastructure.Email;
using Academy.Infrastructure.Jobs;
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

        // Provider adapters (IVideoProvider, IPaymentGateway, IObjectStorage, INotificationSender)
        // are registered in their respective milestones (M3/M4/M7).

        return services;
    }
}
