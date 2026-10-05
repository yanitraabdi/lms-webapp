using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;
using Academy.Application.Abstractions;
using Academy.Application.Admin;
using Academy.Application.Auth;
using Academy.Domain.Enums;
using Academy.Infrastructure.Email;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Academy.Integration.Tests;

public class EmailDiagnosticsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    private sealed class FakeSender(Func<CancellationToken, Task> send) : DevEmailSender(NullLogger<DevEmailSender>.Instance)
    {
        public override Task SendTestAsync(string toEmail, string name, CancellationToken ct = default) => send(ct);
    }

    private static EmailOptions Smtp() => new()
    {
        Provider = "smtp", Host = "smtp.resend.com", Username = "resend", Password = "re_secret",
        FromAddress = "noreply@mail.example.com",
    };

    [Fact]
    public async Task Email_routes_require_an_admin()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/admin/email/status")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsync("/api/admin/email/test", null)).StatusCode);
        var learner = (await NewUser(UserRole.User)).token;
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Get, "/api/admin/email/status", learner)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Post, "/api/admin/email/test", learner)).StatusCode);
    }

    [Fact]
    public async Task Status_reports_the_provider_and_never_the_password()
    {
        var res = await Send(HttpMethod.Get, "/api/admin/email/status", (await NewUser(UserRole.Admin)).token);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var raw = await res.Content.ReadAsStringAsync();
        Assert.Equal("dev", JsonDocument.Parse(raw).RootElement.GetProperty("provider").GetString());
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("username", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Test_email_in_dev_mode_says_it_only_logs()
    {
        var res = await Send(HttpMethod.Post, "/api/admin/email/test", (await NewUser(UserRole.Admin)).token);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = (await res.Content.ReadFromJsonAsync<EmailTestResultDto>(Json))!;
        Assert.False(dto.Sent);
        Assert.Equal("Mode dev — email hanya ditulis ke log API, tidak benar-benar dikirim.", dto.Message);
    }

    [Fact]
    public async Task An_smtp_failure_becomes_a_502_with_an_actionable_message()
    {
        var (_, id) = await NewUser(UserRole.Admin);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = new EmailDiagnosticsService(Smtp(),
            new FakeSender(_ => throw new SmtpException(SmtpStatusCode.MailboxUnavailable, "domain not verified")), db);

        var ex = await Assert.ThrowsAsync<AdminException>(() => svc.SendTestAsync(id));

        Assert.Equal(502, ex.StatusCode);
        Assert.Equal("Alamat pengirim ditolak — pastikan domain SMTP_FROM_ADDRESS sudah Verified di Resend.", ex.Message);
        Assert.DoesNotContain("re_secret", ex.Message);
    }

    [Fact]
    public async Task An_smtp_success_names_the_recipient()
    {
        var (_, id) = await NewUser(UserRole.Admin);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var email = (await db.Users.FirstAsync(u => u.Id == id)).Email;
        var svc = new EmailDiagnosticsService(Smtp(), new FakeSender(_ => Task.CompletedTask), db);

        var r = await svc.SendTestAsync(id);

        Assert.True(r.Sent);
        Assert.Equal($"Email uji terkirim ke {email}. Periksa kotak masuk (dan folder spam).", r.Message);
    }

    [Fact]
    public async Task A_relay_that_never_answers_times_out_as_unreachable()
    {
        var (_, id) = await NewUser(UserRole.Admin);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = new EmailDiagnosticsService(Smtp(),
            new FakeSender(ct => Task.Delay(Timeout.Infinite, ct)), db)
        { SendTimeout = TimeSpan.FromMilliseconds(200) };

        var ex = await Assert.ThrowsAsync<AdminException>(() => svc.SendTestAsync(id));

        Assert.Equal(502, ex.StatusCode);
        Assert.Equal("Server SMTP tidak dapat dihubungi — periksa SMTP_HOST dan SMTP_PORT.", ex.Message);
    }

    [Fact]
    public async Task A_sender_that_ignores_cancellation_still_returns_unreachable()
    {
        var (_, id) = await NewUser(UserRole.Admin);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = new EmailDiagnosticsService(Smtp(),
            new FakeSender(_ => new TaskCompletionSource().Task), db)
        { SendTimeout = TimeSpan.FromMilliseconds(200) };

        var ex = await Assert.ThrowsAsync<AdminException>(() => svc.SendTestAsync(id));

        Assert.Equal(502, ex.StatusCode);
        Assert.Equal("Server SMTP tidak dapat dihubungi — periksa SMTP_HOST dan SMTP_PORT.", ex.Message);
    }

    private Task<HttpResponseMessage> Send(HttpMethod m, string url, string token) =>
        _client.SendAsync(new HttpRequestMessage(m, url)
        { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } });

    private async Task<(string token, Guid id)> NewUser(UserRole role)
    {
        var email = $"ed{Guid.NewGuid():N}@test.local";
        (await _client.PostAsJsonAsync("/api/auth/register", new { name = "ED", email, password = Pw })).EnsureSuccessStatusCode();
        Guid id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Users.FirstAsync(x => x.Email == email);
            u.Role = role;
            await db.SaveChangesAsync();
            id = u.Id;
        }
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Pw });
        return ((await login.Content.ReadFromJsonAsync<AuthTokens>())!.AccessToken, id);
    }
}
