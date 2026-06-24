using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Academy.Infrastructure.Auth;

/// <summary>Seeds a SuperAdmin so /admin is reachable. Idempotent. Credentials default to the
/// well-known dev pair but are overridable via config (DevAdmin:Email / DevAdmin:Password) so a
/// publicly-exposed deploy can set a strong password without the well-known default.</summary>
public class DevAdminSeeder(AppDbContext db, UserPasswordHasher hasher, IConfiguration config)
{
    public const string DefaultEmail = "admin@academy.local";
    public const string DefaultPassword = "Admin12345!";

    public string Email => config["DevAdmin:Email"] is { Length: > 0 } e ? e : DefaultEmail;
    private string Password => config["DevAdmin:Password"] is { Length: > 0 } p ? p : DefaultPassword;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == Email, ct)) return;

        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Email = Email,
            Name = "Admin",
            Role = UserRole.SuperAdmin,
            EmailVerified = true,
            Status = UserStatus.Active,
        };
        user.PasswordHash = hasher.Hash(user, Password);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
    }
}
