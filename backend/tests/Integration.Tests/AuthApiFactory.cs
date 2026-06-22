using Academy.Application.Abstractions;
using Academy.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Academy.Integration.Tests;

/// <summary>Boots the real API against an ephemeral Postgres (Testcontainers), with the
/// email sender swapped for a capturing fake and the background job disabled.</summary>
public class AuthApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
#pragma warning disable CS0618 // Testcontainers parameterless builder ctor is deprecated; image is set via WithImage.
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .WithDatabase("academy_test")
        .WithUsername("academy")
        .WithPassword("academy")
        .Build();
#pragma warning restore CS0618

    public CapturingEmailSender Email { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _pg.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", "test-only-signing-key-that-is-comfortably-over-32-bytes");
        builder.UseSetting("RateLimits:PermitLimit", "100000"); // don't throttle the test suite
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Email);
            // disable the hourly anonymization background service during tests
            services.RemoveAll<IHostedService>();
        });
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _pg.StartAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _pg.DisposeAsync();
        await base.DisposeAsync();
    }
}
