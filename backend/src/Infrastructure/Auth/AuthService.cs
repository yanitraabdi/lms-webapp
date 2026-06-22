using Academy.Application.Abstractions;
using Academy.Application.Auth;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Academy.Infrastructure.Auth;

public class AuthService(
    AppDbContext db,
    JwtTokenService jwt,
    UserPasswordHasher hasher,
    IEmailSender emailSender,
    IOptions<AuthOptions> options) : IAuthService
{
    private readonly AuthOptions _o = options.Value;

    // ---- Register / login ----

    public async Task<AuthTokens> RegisterAsync(RegisterRequest req, CancellationToken ct = default)
    {
        var email = Normalize(req.Email);
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct))
            throw new AuthException("email_taken", "Email sudah terdaftar.", 409);

        var user = new User { Email = email, Name = req.Name.Trim(), Role = UserRole.User, Status = UserStatus.Active };
        user.PasswordHash = hasher.Hash(user, req.Password);
        db.Users.Add(user);

        var (_, raw) = NewRefresh(user, Guid.CreateVersion7());
        await db.SaveChangesAsync(ct);

        await SendVerificationAsync(user, ct);
        return BuildTokens(user, raw);
    }

    public async Task<AuthTokens> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var email = Normalize(req.Email);
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || user.AnonymizedAt is not null || user.PasswordHash is null)
            throw new AuthException("invalid_credentials", "Email atau kata sandi salah.", 401);

        if (user.LockoutEndsAt is { } until && until > DateTimeOffset.UtcNow)
            throw new AuthException("locked_out", "Akun terkunci sementara. Coba lagi nanti.", 423);

        if (!hasher.Verify(user, user.PasswordHash, req.Password))
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= _o.MaxFailedAttempts)
            {
                user.LockoutEndsAt = DateTimeOffset.UtcNow.AddMinutes(_o.LockoutMinutes);
                user.AccessFailedCount = 0;
            }
            await db.SaveChangesAsync(ct);
            throw new AuthException("invalid_credentials", "Email atau kata sandi salah.", 401);
        }

        user.AccessFailedCount = 0;
        user.LockoutEndsAt = null;
        if (user.DeletedAt is not null) // recover a soft-deleted account within the grace window
        {
            user.DeletedAt = null;
            user.Status = UserStatus.Active;
        }

        var (_, raw) = NewRefresh(user, Guid.CreateVersion7());
        await db.SaveChangesAsync(ct);
        return BuildTokens(user, raw);
    }

    // ---- Refresh rotation + reuse detection ----

    public async Task<AuthTokens> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = TokenHasher.Hash(refreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null)
            throw new AuthException("invalid_token", "Sesi tidak valid.", 401);

        if (token.RevokedAt is not null)
        {
            // Reuse of an already-rotated token → assume theft, revoke the whole family.
            await db.RefreshTokens.Where(t => t.FamilyId == token.FamilyId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow), ct);
            throw new AuthException("token_reuse", "Sesi tidak valid. Silakan masuk lagi.", 401);
        }

        if (token.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new AuthException("expired_token", "Sesi kedaluwarsa. Silakan masuk lagi.", 401);

        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == token.UserId, ct);
        if (user is null || user.AnonymizedAt is not null || user.DeletedAt is not null)
            throw new AuthException("invalid_token", "Sesi tidak valid.", 401);

        var (next, raw) = NewRefresh(user, token.FamilyId);
        token.RevokedAt = DateTimeOffset.UtcNow;
        token.ReplacedByTokenId = next.Id;
        await db.SaveChangesAsync(ct);
        return BuildTokens(user, raw);
    }

    public Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = TokenHasher.Hash(refreshToken);
        return db.RefreshTokens.Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow), ct);
    }

    public Task LogoutAllAsync(Guid userId, CancellationToken ct = default) => RevokeAllSessions(userId, ct);

    // ---- Email verification ----

    public async Task VerifyEmailAsync(string token, CancellationToken ct = default)
    {
        var ut = await FindValidTokenAsync(token, UserTokenPurpose.EmailVerification, ct);
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == ut.UserId, ct)
            ?? throw new AuthException("invalid_token", "Tautan verifikasi tidak valid.", 400);
        user.EmailVerified = true;
        ut.ConsumedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task ResendVerificationAsync(string email, CancellationToken ct = default)
    {
        var e = Normalize(email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == e, ct);
        if (user is null || user.EmailVerified) return; // silent: don't reveal account state
        await InvalidateTokensAsync(user.Id, UserTokenPurpose.EmailVerification, ct);
        await SendVerificationAsync(user, ct);
    }

    // ---- Password change / reset ----

    public async Task<AuthTokens> ChangePasswordAsync(Guid userId, ChangePasswordRequest req, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new AuthException("not_found", "Pengguna tidak ditemukan.", 404);
        if (user.PasswordHash is null || !hasher.Verify(user, user.PasswordHash, req.CurrentPassword))
            throw new AuthException("invalid_password", "Kata sandi saat ini salah.", 400);

        user.PasswordHash = hasher.Hash(user, req.NewPassword);
        await RevokeAllSessions(userId, ct);                 // invalidate other devices
        var (_, raw) = NewRefresh(user, Guid.CreateVersion7()); // start a fresh session here
        await db.SaveChangesAsync(ct);
        await emailSender.SendPasswordChangedAsync(user.Email, user.Name, ct);
        return BuildTokens(user, raw);
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var e = Normalize(email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == e, ct);
        if (user is null || user.PasswordHash is null) return; // silent

        await InvalidateTokensAsync(user.Id, UserTokenPurpose.PasswordReset, ct);
        var raw = TokenHasher.NewRawToken();
        db.UserTokens.Add(new UserToken
        {
            UserId = user.Id,
            Purpose = UserTokenPurpose.PasswordReset,
            TokenHash = TokenHasher.Hash(raw),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(_o.PasswordResetHours),
        });
        await db.SaveChangesAsync(ct);
        await emailSender.SendPasswordResetAsync(user.Email, user.Name, $"{_o.FrontendBaseUrl}/reset-password?token={raw}", ct);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest req, CancellationToken ct = default)
    {
        var ut = await FindValidTokenAsync(req.Token, UserTokenPurpose.PasswordReset, ct);
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == ut.UserId, ct)
            ?? throw new AuthException("invalid_token", "Tautan reset tidak valid.", 400);

        user.PasswordHash = hasher.Hash(user, req.NewPassword);
        user.EmailVerified = true; // proven via emailed link
        ut.ConsumedAt = DateTimeOffset.UtcNow;
        await RevokeAllSessions(user.Id, ct);
        await db.SaveChangesAsync(ct);
        await emailSender.SendPasswordChangedAsync(user.Email, user.Name, ct);
    }

    // ---- Account ----

    public async Task<UserDto> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new AuthException("not_found", "Pengguna tidak ditemukan.", 404);
        return ToDto(user);
    }

    public async Task DeleteAccountAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new AuthException("not_found", "Pengguna tidak ditemukan.", 404);
        // (M3+) require any active subscription to be canceled first.
        user.DeletedAt = DateTimeOffset.UtcNow;
        user.Status = UserStatus.Deleted;
        await RevokeAllSessions(userId, ct);
        await db.SaveChangesAsync(ct);
    }

    // ---- Helpers ----

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
    private static UserDto ToDto(User u) => new(u.Id, u.Email, u.Name, u.Role.ToString(), u.EmailVerified);

    private (RefreshToken entity, string raw) NewRefresh(User user, Guid familyId)
    {
        var raw = TokenHasher.NewRawToken();
        var entity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = TokenHasher.Hash(raw),
            FamilyId = familyId,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_o.RefreshTokenDays),
        };
        db.RefreshTokens.Add(entity);
        return (entity, raw);
    }

    private AuthTokens BuildTokens(User user, string rawRefresh)
    {
        var (access, expires) = jwt.CreateAccessToken(user);
        return new AuthTokens(access, rawRefresh, expires, ToDto(user));
    }

    private Task RevokeAllSessions(Guid userId, CancellationToken ct) =>
        db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow), ct);

    private Task InvalidateTokensAsync(Guid userId, UserTokenPurpose purpose, CancellationToken ct) =>
        db.UserTokens.Where(t => t.UserId == userId && t.Purpose == purpose && t.ConsumedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ConsumedAt, DateTimeOffset.UtcNow), ct);

    private async Task<UserToken> FindValidTokenAsync(string raw, UserTokenPurpose purpose, CancellationToken ct)
    {
        var hash = TokenHasher.Hash(raw);
        var ut = await db.UserTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.Purpose == purpose, ct);
        if (ut is null || ut.ConsumedAt is not null || ut.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new AuthException("invalid_token", "Tautan tidak valid atau kedaluwarsa.", 400);
        return ut;
    }

    private async Task SendVerificationAsync(User user, CancellationToken ct)
    {
        var raw = TokenHasher.NewRawToken();
        db.UserTokens.Add(new UserToken
        {
            UserId = user.Id,
            Purpose = UserTokenPurpose.EmailVerification,
            TokenHash = TokenHasher.Hash(raw),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(_o.EmailVerificationHours),
        });
        await db.SaveChangesAsync(ct);
        await emailSender.SendEmailVerificationAsync(user.Email, user.Name, $"{_o.FrontendBaseUrl}/verify-email?token={raw}", ct);
    }
}
