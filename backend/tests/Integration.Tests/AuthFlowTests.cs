using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Academy.Application.Auth;
using Academy.Domain.Enums;
using Academy.Infrastructure.Auth;
using Academy.Infrastructure.Jobs;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Academy.Integration.Tests;

public class AuthFlowTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    [Fact]
    public async Task Register_returns_tokens_and_rejects_duplicate()
    {
        var email = Unique();
        var tokens = await Register(email);

        Assert.NotEmpty(tokens.AccessToken);
        Assert.NotEmpty(tokens.RefreshToken);
        Assert.False(tokens.User.EmailVerified);
        Assert.Equal(email, tokens.User.Email);

        var dup = await _client.PostAsJsonAsync("/api/auth/register", new { name = "Dup", email, password = Pw });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    [Fact]
    public async Task Login_rejects_wrong_password()
    {
        var email = Unique();
        await Register(email);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(email, "wrong-password")).StatusCode);
        (await Login(email, Pw)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Refresh_rotates_and_detects_reuse()
    {
        var first = await Register(Unique());

        var rotated = await Refresh(first.RefreshToken);
        rotated.EnsureSuccessStatusCode();
        var second = (await rotated.Content.ReadFromJsonAsync<AuthTokens>())!;

        // Reusing the already-rotated token is rejected...
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(first.RefreshToken)).StatusCode);
        // ...and trips family revocation, killing the live token too.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(second.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task VerifyEmail_marks_user_verified()
    {
        var email = Unique();
        var tokens = await Register(email);
        Assert.False(tokens.User.EmailVerified);

        var token = TokenFrom(factory.Email.LastVerifyUrl);
        (await _client.PostAsJsonAsync("/api/auth/verify-email", new { token })).EnsureSuccessStatusCode();

        var login = (await Login(email, Pw)).Content;
        var after = (await login.ReadFromJsonAsync<AuthTokens>())!;
        Assert.True(after.User.EmailVerified);
    }

    [Fact]
    public async Task ChangePassword_revokes_other_sessions()
    {
        var email = Unique();
        var a = await Register(email);
        var b = (await (await Login(email, Pw)).Content.ReadFromJsonAsync<AuthTokens>())!;

        var change = await AuthedSend(HttpMethod.Post, "/api/auth/change-password", b.AccessToken,
            new { currentPassword = Pw, newPassword = "NewPassword456" });
        change.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(a.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(b.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(email, Pw)).StatusCode);
        (await Login(email, "NewPassword456")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ResetPassword_sets_new_password()
    {
        var email = Unique();
        await Register(email);

        (await _client.PostAsJsonAsync("/api/auth/forgot-password", new { email })).EnsureSuccessStatusCode();
        var token = TokenFrom(factory.Email.LastResetUrl);
        (await _client.PostAsJsonAsync("/api/auth/reset-password", new { token, newPassword = "ResetPass789" }))
            .EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(email, Pw)).StatusCode);
        (await Login(email, "ResetPass789")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task LogoutAll_revokes_all_sessions()
    {
        var email = Unique();
        var a = await Register(email);
        var b = (await (await Login(email, Pw)).Content.ReadFromJsonAsync<AuthTokens>())!;

        (await AuthedSend(HttpMethod.Post, "/api/auth/logout-all", b.AccessToken)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(a.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(b.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_recovers_within_grace_then_anonymizes()
    {
        var email = Unique();
        var tokens = await Register(email);

        (await AuthedSend(HttpMethod.Delete, "/api/account", tokens.AccessToken)).EnsureSuccessStatusCode();
        // Within the grace window, logging in restores the account.
        (await Login(email, Pw)).EnsureSuccessStatusCode();

        // Simulate the grace window elapsing, then run the anonymization sweep.
        await WithDb(async db =>
        {
            var u = await db.Users.IgnoreQueryFilters().FirstAsync(x => x.Id == tokens.User.Id);
            u.DeletedAt = DateTimeOffset.UtcNow.AddDays(-40);
            u.Status = UserStatus.Deleted;
            await db.SaveChangesAsync();
        });

        var job = new AccountAnonymizationService(
            factory.Services,
            factory.Services.GetRequiredService<IOptions<AuthOptions>>(),
            NullLogger<AccountAnonymizationService>.Instance);
        Assert.True(await job.RunOnceAsync(default) >= 1);

        await WithDb(async db =>
        {
            var u = await db.Users.IgnoreQueryFilters().FirstAsync(x => x.Id == tokens.User.Id);
            Assert.NotNull(u.AnonymizedAt);
            Assert.Null(u.PasswordHash);
            Assert.StartsWith("deleted+", u.Email);
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(email, Pw)).StatusCode);
    }

    // ---- helpers ----

    private static string Unique() => $"u{Guid.NewGuid():N}@test.local";

    private async Task<AuthTokens> Register(string email, string password = Pw)
    {
        var res = await _client.PostAsJsonAsync("/api/auth/register", new { name = "Test User", email, password });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AuthTokens>())!;
    }

    private Task<HttpResponseMessage> Login(string email, string password) =>
        _client.PostAsJsonAsync("/api/auth/login", new { email, password });

    private Task<HttpResponseMessage> Refresh(string refreshToken) =>
        _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });

    private Task<HttpResponseMessage> AuthedSend(HttpMethod method, string url, string accessToken, object? body = null)
    {
        var req = new HttpRequestMessage(method, url) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) } };
        if (body is not null) req.Content = JsonContent.Create(body);
        return _client.SendAsync(req);
    }

    private static string TokenFrom(string? url)
    {
        Assert.NotNull(url);
        var i = url!.IndexOf("token=", StringComparison.Ordinal);
        Assert.True(i >= 0, "URL has no token query parameter");
        return url[(i + "token=".Length)..];
    }

    private async Task WithDb(Func<AppDbContext, Task> action)
    {
        using var scope = factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
