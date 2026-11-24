using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Academy.Infrastructure.Auth;

/// <summary>Seeds a SuperAdmin so /admin is reachable. Idempotent: the password is used only when
/// the account is first created, so after that it can (and should) be removed from config and
/// changed in the app. Outside Production it falls back to the well-known dev pair; in Production
/// there is no fallback, so a deploy never gets an admin with a well-known password.</summary>
public class DevAdminSeeder(AppDbContext db, UserPasswordHasher hasher, IConfiguration config, IHostEnvironment env)
{
    public const string DefaultEmail = "admin@academy.local";
    public const string DefaultPassword = "Admin12345!";

    public string Email => config["DevAdmin:Email"] is { Length: > 0 } e ? e : DefaultEmail;
    private string? Password => config["DevAdmin:Password"] is { Length: > 0 } p ? p
        : env.IsProduction() ? null : DefaultPassword;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == Email, ct)) return;
        var password = Password ?? throw new InvalidOperationException(
            "No admin account exists yet and DevAdmin:Password is not set. Set ADMIN_PASSWORD for the first start of a new database.");

        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Email = Email,
            Name = "Admin",
            Role = UserRole.SuperAdmin,
            EmailVerified = true,
            Status = UserStatus.Active,
        };
        user.PasswordHash = hasher.Hash(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
    }
}
