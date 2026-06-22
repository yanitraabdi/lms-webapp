using Academy.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Academy.Infrastructure.Auth;

/// <summary>Password hashing via ASP.NET Core Identity's PBKDF2 hasher (no Identity stores).</summary>
public class UserPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string password) => _hasher.HashPassword(user, password);

    public bool Verify(User user, string hash, string password) =>
        _hasher.VerifyHashedPassword(user, hash, password) != PasswordVerificationResult.Failed;
}
