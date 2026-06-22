namespace Academy.Application.Auth;

/// <summary>Auth use-cases. Implemented in Infrastructure (data + crypto live there).</summary>
public interface IAuthService
{
    Task<AuthTokens> RegisterAsync(RegisterRequest req, CancellationToken ct = default);
    Task<AuthTokens> LoginAsync(LoginRequest req, CancellationToken ct = default);
    Task<AuthTokens> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
    Task LogoutAllAsync(Guid userId, CancellationToken ct = default);

    Task VerifyEmailAsync(string token, CancellationToken ct = default);
    Task ResendVerificationAsync(string email, CancellationToken ct = default);

    Task<AuthTokens> ChangePasswordAsync(Guid userId, ChangePasswordRequest req, CancellationToken ct = default);
    Task ForgotPasswordAsync(string email, CancellationToken ct = default);
    Task ResetPasswordAsync(ResetPasswordRequest req, CancellationToken ct = default);

    Task<UserDto> GetMeAsync(Guid userId, CancellationToken ct = default);
    Task DeleteAccountAsync(Guid userId, CancellationToken ct = default);
}
