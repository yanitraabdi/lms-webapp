using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Academy.Application.Assessments;
using Academy.Application.Auth;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>
/// The assessment config is one jsonb bag edited by several admin screens, each modelling a
/// different subset. Whole-document replace meant every screen silently destroyed the fields it
/// did not model — the composer wiped section audio, the test editor reset the audio play limit.
/// The API now merges by key presence, so a partial save is safe.
/// </summary>
public class AssessmentConfigMergeApiTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    [Fact]
    public async Task A_partial_save_keeps_the_fields_it_did_not_send()
    {
        var admin = await AdminToken();
        var id = await Create(admin, new
        {
            passThreshold = 9, retakeCap = 2, proctoringEnabled = true,
            audioPlayLimit = 3, timeLimitMinutes = 20, sections = Array.Empty<object>(),
        });

        // Exactly what a screen that only edits the pass mark would send.
        await Update(admin, id, new { passThreshold = 12 });

        var config = await ConfigOf(admin, id);
        Assert.Equal(12, config.PassThreshold);
        Assert.Equal(3, config.AudioPlayLimit);       // would have been wiped before
        Assert.Equal(20, config.TimeLimitMinutes);    // ditto
        Assert.Equal(2, config.RetakeCap);
    }

    [Fact]
    public async Task A_section_rewritten_without_audio_keeps_its_recording()
    {
        var admin = await AdminToken();
        var id = await Create(admin, new
        {
            passThreshold = 1, proctoringEnabled = false, sections = new[]
            {
                new { section = "Listening", questions = 50, minutes = 35, audioRef = "audio/keep.mp3" },
            },
        });

        // A layout edit that does not model audio at all.
        await Update(admin, id, new { sections = new[] { new { section = "Listening", questions = 40, minutes = 30 } } });

        var section = (await ConfigOf(admin, id)).Sections.Single();
        Assert.Equal(40, section.Questions);
        Assert.Equal("audio/keep.mp3", section.AudioRef);
    }

    [Fact]
    public async Task An_explicit_null_still_clears()
    {
        var admin = await AdminToken();
        var id = await Create(admin, new { passThreshold = 1, retakeCap = 5, proctoringEnabled = false, sections = Array.Empty<object>() });

        await Update(admin, id, new { retakeCap = (int?)null });

        Assert.Null((await ConfigOf(admin, id)).RetakeCap);
    }

    [Fact]
    public async Task Validation_runs_against_the_merged_result_not_the_request()
    {
        var admin = await AdminToken();
        var id = await Create(admin, new { passThreshold = 1, retakeCap = 2, proctoringEnabled = false, sections = Array.Empty<object>() });

        // A cap of 0 makes used >= cap true before the first attempt, locking the session forever.
        var res = await Authed(HttpMethod.Put, $"/api/admin/assessments/{id}",
            admin, new { kind = "Gating", title = "t", config = new { retakeCap = 0 } });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(2, (await ConfigOf(admin, id)).RetakeCap);   // unchanged
    }

    // ---- helpers ----

    private async Task<Guid> Create(string admin, object config)
    {
        var res = await Authed(HttpMethod.Post, "/api/admin/assessments", admin,
            new { kind = "Gating", title = $"Merge {Guid.NewGuid():N}"[..14], config });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdDto>(Json))!.Id;
    }

    private async Task Update(string admin, Guid id, object config) =>
        (await Authed(HttpMethod.Put, $"/api/admin/assessments/{id}", admin,
            new { kind = "Gating", title = "Merge", config })).EnsureSuccessStatusCode();

    private async Task<AssessmentConfig> ConfigOf(string admin, Guid id)
    {
        var res = await Authed(HttpMethod.Get, $"/api/admin/assessments/{id}", admin);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AdminAssessmentDto>(Json))!.Config;
    }

    private record IdDto(Guid Id);

    private async Task<string> AdminToken()
    {
        var email = $"adm{Guid.NewGuid():N}@test.local";
        (await _client.PostAsJsonAsync("/api/auth/register", new { name = "Adm", email, password = Pw }))
            .EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Users.FirstAsync(x => x.Email == email);
            u.Role = UserRole.Admin;
            await db.SaveChangesAsync();
        }
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Pw });
        return (await login.Content.ReadFromJsonAsync<AuthTokens>())!.AccessToken;
    }

    private Task<HttpResponseMessage> Authed(HttpMethod method, string url, string token, object? body = null)
    {
        var req = new HttpRequestMessage(method, url)
        { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };
        if (body is not null) req.Content = JsonContent.Create(body);
        return _client.SendAsync(req);
    }
}
