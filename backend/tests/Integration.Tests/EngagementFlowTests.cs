using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Academy.Application.Auth;
using Academy.Application.Engagement;
using Academy.Infrastructure.Engagement;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

public class EngagementFlowTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    [Fact]
    public async Task Faq_returns_seeded_published_items()
    {
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<FaqSeeder>().SeedAsync();

        var faq = await _client.GetFromJsonAsync<List<FaqItemDto>>("/api/faq", Json);
        Assert.NotNull(faq);
        Assert.NotEmpty(faq!);
        Assert.All(faq, f => Assert.False(string.IsNullOrWhiteSpace(f.Question)));
    }

    [Fact]
    public async Task Contact_stores_submission_and_validates()
    {
        var bad = await _client.PostAsJsonAsync("/api/contact", new { name = "", email = "nope", message = "" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var email = $"c{Guid.NewGuid():N}@test.local";
        var ok = await _client.PostAsJsonAsync("/api/contact", new { name = "Budi", email, message = "Halo, ada pertanyaan." });
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);

        await WithDb(async db => Assert.True(await db.ContactSubmissions.AnyAsync(c => c.Email == email)));
    }

    [Fact]
    public async Task Tour_and_survey_persist_and_reflect_in_state()
    {
        var (token, userId) = await User();

        var before = await AuthedGet<OnboardingStateDto>("/api/onboarding", token);
        Assert.False(before.TourCompleted);
        Assert.False(before.SurveyCompleted);

        (await Authed(HttpMethod.Post, "/api/onboarding/tour", token,
            new { tourKey = "dashboard_first_run", status = "Completed" })).EnsureSuccessStatusCode();
        // Idempotent upsert — second call doesn't duplicate.
        (await Authed(HttpMethod.Post, "/api/onboarding/tour", token,
            new { tourKey = "dashboard_first_run", status = "Completed" })).EnsureSuccessStatusCode();

        (await Authed(HttpMethod.Post, "/api/onboarding/survey", token, new
        {
            role = "ops_manager",
            goals = new[] { "produktivitas", "workflow-tim" },
            preferredTools = new[] { "claude", "chatgpt" },
        })).EnsureSuccessStatusCode();

        var after = await AuthedGet<OnboardingStateDto>("/api/onboarding", token);
        Assert.True(after.TourCompleted);
        Assert.True(after.SurveyCompleted);

        await WithDb(async db =>
        {
            Assert.Equal(1, await db.TourStates.CountAsync(t => t.UserId == userId && t.TourKey == "dashboard_first_run"));
            Assert.Equal(1, await db.OnboardingSurveys.CountAsync(s => s.UserId == userId));
        });
    }

    // ---- helpers ----

    private async Task<(string token, Guid userId)> User()
    {
        var email = $"u{Guid.NewGuid():N}@test.local";
        var res = await _client.PostAsJsonAsync("/api/auth/register", new { name = "T", email, password = Pw });
        res.EnsureSuccessStatusCode();
        var tokens = (await res.Content.ReadFromJsonAsync<AuthTokens>())!;
        return (tokens.AccessToken, tokens.User.Id);
    }

    private Task<HttpResponseMessage> Authed(HttpMethod method, string url, string token, object? body = null)
    {
        var req = new HttpRequestMessage(method, url) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };
        if (body is not null) req.Content = JsonContent.Create(body);
        return _client.SendAsync(req);
    }

    private async Task<T> AuthedGet<T>(string url, string token)
    {
        var res = await Authed(HttpMethod.Get, url, token);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task WithDb(Func<AppDbContext, Task> action)
    {
        using var scope = factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
