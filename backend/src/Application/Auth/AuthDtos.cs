namespace Academy.Application.Auth;

public record RegisterRequest(string Name, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string NewPassword);
public record VerifyEmailRequest(string Token);
public record ResendVerificationRequest(string Email);

public record UserDto(Guid Id, string Email, string Name, string Role, bool EmailVerified);

/// <summary>Issued to the Next BFF: the refresh token is set as an httpOnly cookie by Next;
/// the SPA only ever sees the access token + user.</summary>
public record AuthTokens(string AccessToken, string RefreshToken, int ExpiresInSeconds, UserDto User);
