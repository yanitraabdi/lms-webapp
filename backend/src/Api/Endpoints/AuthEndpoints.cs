using System.Security.Claims;
using Academy.Application.Auth;
using FluentValidation;

namespace Academy.Api.Endpoints;

public record RefreshTokenBody(string RefreshToken);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").RequireRateLimiting("auth").WithTags("Auth");

        auth.MapPost("/register", async (RegisterRequest req, IValidator<RegisterRequest> v, IAuthService svc, CancellationToken ct) =>
        {
            await v.ValidateAndThrowAsync(req, ct);
            return Results.Ok(await svc.RegisterAsync(req, ct));
        });

        auth.MapPost("/login", async (LoginRequest req, IValidator<LoginRequest> v, IAuthService svc, CancellationToken ct) =>
        {
            await v.ValidateAndThrowAsync(req, ct);
            return Results.Ok(await svc.LoginAsync(req, ct));
        });

        auth.MapPost("/refresh", async (RefreshTokenBody body, IAuthService svc, CancellationToken ct) =>
            Results.Ok(await svc.RefreshAsync(body.RefreshToken, ct)));

        auth.MapPost("/logout", async (RefreshTokenBody body, IAuthService svc, CancellationToken ct) =>
        {
            await svc.LogoutAsync(body.RefreshToken, ct);
            return Results.NoContent();
        });

        auth.MapPost("/logout-all", async (ClaimsPrincipal user, IAuthService svc, CancellationToken ct) =>
        {
            await svc.LogoutAllAsync(user.UserId(), ct);
            return Results.NoContent();
        }).RequireAuthorization();

        auth.MapPost("/verify-email", async (VerifyEmailRequest req, IAuthService svc, CancellationToken ct) =>
        {
            await svc.VerifyEmailAsync(req.Token, ct);
            return Results.NoContent();
        });

        auth.MapPost("/resend-verification", async (ResendVerificationRequest req, IValidator<ResendVerificationRequest> v, IAuthService svc, CancellationToken ct) =>
        {
            await v.ValidateAndThrowAsync(req, ct);
            await svc.ResendVerificationAsync(req.Email, ct);
            return Results.NoContent();
        });

        auth.MapPost("/change-password", async (ChangePasswordRequest req, IValidator<ChangePasswordRequest> v, ClaimsPrincipal user, IAuthService svc, CancellationToken ct) =>
        {
            await v.ValidateAndThrowAsync(req, ct);
            return Results.Ok(await svc.ChangePasswordAsync(user.UserId(), req, ct));
        }).RequireAuthorization();

        auth.MapPost("/forgot-password", async (ForgotPasswordRequest req, IValidator<ForgotPasswordRequest> v, IAuthService svc, CancellationToken ct) =>
        {
            await v.ValidateAndThrowAsync(req, ct);
            await svc.ForgotPasswordAsync(req.Email, ct);
            return Results.NoContent();
        });

        auth.MapPost("/reset-password", async (ResetPasswordRequest req, IValidator<ResetPasswordRequest> v, IAuthService svc, CancellationToken ct) =>
        {
            await v.ValidateAndThrowAsync(req, ct);
            await svc.ResetPasswordAsync(req, ct);
            return Results.NoContent();
        });

        auth.MapGet("/me", async (ClaimsPrincipal user, IAuthService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetMeAsync(user.UserId(), ct))).RequireAuthorization();

        // Self-service account deletion (UU PDP).
        app.MapDelete("/api/account", async (ClaimsPrincipal user, IAuthService svc, CancellationToken ct) =>
        {
            await svc.DeleteAccountAsync(user.UserId(), ct);
            return Results.NoContent();
        }).RequireAuthorization().RequireRateLimiting("auth").WithTags("Auth");

        return app;
    }
}

internal static class ClaimsPrincipalExtensions
{
    public static Guid UserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue("sub") ?? throw new InvalidOperationException("Missing 'sub' claim."));
}
