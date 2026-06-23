using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Auth;

/// <summary>Seeds a local SuperAdmin so /admin is reachable in dev. Idempotent.
/// Dev-only — remove/replace for real environments.</summary>
public class DevAdminSeeder(AppDbContext db, UserPasswordHasher hasher)
{
    public const string Email = "admin@academy.local";
    public const string Password = "Admin12345!";

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
