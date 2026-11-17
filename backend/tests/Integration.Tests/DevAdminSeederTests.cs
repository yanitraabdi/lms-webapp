using Academy.Infrastructure.Auth;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

namespace Academy.Integration.Tests;

/// <summary>The admin password lives in the app, not .env: in Production there is no well-known fallback.</summary>
public class DevAdminSeederTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private async Task<bool> Seed(string environment, string? password, string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DevAdmin:Email"] = email,
            ["DevAdmin:Password"] = password,
        }).Build();
        var env = new HostingEnvironment { EnvironmentName = environment };
        await new DevAdminSeeder(db, scope.ServiceProvider.GetRequiredService<UserPasswordHasher>(), config, env).SeedAsync();
        return await db.Users.AnyAsync(u => u.Email == email);
    }

    private static string NewEmail() => $"seed-{Guid.NewGuid():N}@test.local";

    [Fact]
    public async Task Production_without_a_password_refuses_to_create_the_admin()
    {
        var email = NewEmail();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Seed(Environments.Production, null, email));
        Assert.Contains("ADMIN_PASSWORD", ex.Message);
    }

    [Fact]
    public async Task Production_with_a_password_creates_the_admin_and_later_starts_need_none()
    {
        var email = NewEmail();
        Assert.True(await Seed(Environments.Production, "A-strong-first-password-1!", email));
        Assert.True(await Seed(Environments.Production, null, email)); // admin exists: no password needed
    }

    [Fact]
    public async Task Development_falls_back_to_the_dev_password()
        => Assert.True(await Seed(Environments.Development, null, NewEmail()));
}
