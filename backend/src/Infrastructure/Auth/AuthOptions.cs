using Microsoft.Extensions.Configuration;

namespace Academy.Infrastructure.Auth;

public class AuthOptions
{
    public const string SectionName = "Auth";

    public string JwtIssuer { get; set; } = "academy";
    public string JwtAudience { get; set; } = "academy";
    // Dev default only — MUST be overridden in real environments (Jwt__SigningKey).
    public string JwtSigningKey { get; set; } = "dev-only-insecure-signing-key-change-me-min-32-bytes!!";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
    public int EmailVerificationHours { get; set; } = 24;
    public int PasswordResetHours { get; set; } = 2;
    public int DeletionGraceDays { get; set; } = 30;
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    /// <summary>Frontend base URL used to build verify/reset email links.</summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:3001";
}

public static class AuthOptionsFactory
{
    public static AuthOptions Build(IConfiguration config)
    {
        var o = new AuthOptions();
        config.GetSection(AuthOptions.SectionName).Bind(o);
        // Allow Jwt__* env vars (see .env.example) to override the crypto settings.
        if (config["Jwt:SigningKey"] is { Length: > 0 } key) o.JwtSigningKey = key;
        if (config["Jwt:Issuer"] is { Length: > 0 } iss) o.JwtIssuer = iss;
        if (config["Jwt:Audience"] is { Length: > 0 } aud) o.JwtAudience = aud;
        if (config["FrontendBaseUrl"] is { Length: > 0 } fe) o.FrontendBaseUrl = fe;
        else if (config["Cors:AllowedOrigins:0"] is { Length: > 0 } origin) o.FrontendBaseUrl = origin;
        return o;
    }
}
