using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Academy.Application.Abstractions;
using Academy.Application.Auth;
using Academy.Application.Programs;
using Academy.Domain;
using Academy.Infrastructure.Learning;
using Academy.Infrastructure.Persistence;
using Academy.Infrastructure.Programs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>Landing page spec §3: the public syllabus carries each video session's parts (kind + title only).</summary>
public class LandingContentTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";
    private const string Asset = "bunny-secret-asset-xyz";

    private record Seeded(string Admin, string Slug, Guid Video, Guid Live);

    private sealed class FakeRevalidator : IContentRevalidator
    {
        public List<string> Paths { get; } = [];
        public Task RevalidateAsync(IReadOnlyCollection<string> paths, CancellationToken ct = default)
        { Paths.AddRange(paths); return Task.CompletedTask; }
    }

    [Fact]
    public async Task Public_programme_lists_parts_in_order_for_video_sessions()
    {
        var s = await Seed();
        var doc = JsonDocument.Parse(await _client.GetStringAsync($"/api/programs/{s.Slug}"));
        var sessions = doc.RootElement.GetProperty("sessions").EnumerateArray().ToList();

        var video = sessions.Single(x => x.GetProperty("id").GetGuid() == s.Video).GetProperty("parts");
        Assert.Equal(
            [("LessonVideo", "Pengantar"), ("Test", "Kuis 1"), ("Discussion", "Pembahasan 1")],
            video.EnumerateArray().Select(p => (p.GetProperty("kind").GetString()!, p.GetProperty("title").GetString()!)));
        Assert.Empty(sessions.Single(x => x.GetProperty("id").GetGuid() == s.Live).GetProperty("parts").EnumerateArray());
    }

    [Fact]
    public async Task Public_programme_never_exposes_asset_or_assessment_ids()
    {
        var s = await Seed();
        var raw = await _client.GetStringAsync($"/api/programs/{s.Slug}");
        Assert.DoesNotContain("providerassetid", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("assessmentid", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Asset, raw);
    }

    [Fact]
    public async Task Saving_parts_revalidates_the_public_pages()
    {
        var s = await Seed();
        var fake = new FakeRevalidator();
        using var scope = factory.Services.CreateScope();
        var svc = new SessionPartAdminService(
            scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            scope.ServiceProvider.GetRequiredService<VideoOptions>(), fake);

        await svc.SaveAsync(await AnyUser(), s.Video,
            new SaveSessionPartsRequest([new SessionPartInput(null, "LessonVideo", "Baru", Asset, 60, null)]));

        Assert.Equal(["/", $"/program/{s.Slug}"], fake.Paths);
    }

    private async Task<Guid> AnyUser() => await factory.Services.CreateScope().ServiceProvider
        .GetRequiredService<AppDbContext>().Users.Select(u => u.Id).FirstAsync();

    private async Task<Seeded> Seed()
    {
        var admin = await AdminToken();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var program = await PostJson<AdminProgramDto>("/api/admin/programs", admin, new
        {
            name = $"Program {suffix}", slug = $"prog-{suffix}", description = "Uji landing",
            summary = (string?)null, priceIdr = 100000m, published = false,
        });

        var video = await Session(admin, program.Id, "Video", "Sesi 1", 1, new { providerAssetId = Asset, durationSeconds = 900 });
        var live = await Session(admin, program.Id, "Live", "Sesi Live", 2,
            new { scheduledAt = DateTimeOffset.UtcNow.AddDays(30), liveMode = "Zoom", joinUrl = "https://zoom.us/j/1" });
        var fin = await ItpFinal.SeedAsync(factory, "Tes akhir");
        await Session(admin, program.Id, "FinalAssessment", "Sesi Akhir", 3, new { assessmentId = fin.AssessmentId });

        // Parts: lesson, test, discussion (the video session was created with a default lesson part).
        Guid test;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var a = new Academy.Domain.Entities.Assessment
            {
                Id = Guid.CreateVersion7(), Kind = Academy.Domain.Enums.AssessmentKind.Gating, Title = "Tes " + suffix,
                Config = """{"passThreshold":1,"proctoringEnabled":false,"sections":[]}""",
            };
            var q = new Academy.Domain.Entities.Question
            {
                Id = Guid.CreateVersion7(), Section = Academy.Domain.Enums.QuestionSection.Reading, Prompt = "Q " + suffix,
                Choices = """["a","b"]""", Correct = "[0]",
            };
            db.Assessments.Add(a); db.Questions.Add(q);
            db.AssessmentQuestions.Add(new Academy.Domain.Entities.AssessmentQuestion
            { Id = Guid.CreateVersion7(), AssessmentId = a.Id, QuestionId = q.Id, OrderIndex = 1 });
            await db.SaveChangesAsync();
            test = a.Id;
        }
        var lessonId = await WithDbResult(db => db.SessionParts.Where(p => p.SessionId == video).Select(p => p.Id).SingleAsync());
        (await Authed(HttpMethod.Put, $"/api/admin/sessions/{video}/parts", admin, new
        {
            parts = new object[]
            {
                new { id = (Guid?)lessonId, kind = "LessonVideo", title = "Pengantar", providerAssetId = Asset, durationSeconds = 60, assessmentId = (Guid?)null },
                new { id = (Guid?)null, kind = "Test", title = "Kuis 1", providerAssetId = (string?)null, durationSeconds = (int?)null, assessmentId = (Guid?)test },
                new { id = (Guid?)null, kind = "Discussion", title = "Pembahasan 1", providerAssetId = Asset, durationSeconds = 60, assessmentId = (Guid?)null },
            },
        })).EnsureSuccessStatusCode();

        var bands = new List<object>();
        foreach (var (section, maxRaw, scaledMax) in new[]
        {
            ("Listening", ToeflScoring.ListeningQuestions, ToeflScoring.ListeningScaledMax),
            ("Structure", ToeflScoring.StructureQuestions, ToeflScoring.StructureScaledMax),
            ("Reading",   ToeflScoring.ReadingQuestions,   ToeflScoring.ReadingScaledMax),
        })
            for (var raw = 0; raw <= maxRaw; raw++)
            {
                var scaled = ToeflScoring.ScaledMin + (int)Math.Round((double)raw / maxRaw * (scaledMax - ToeflScoring.ScaledMin));
                bands.Add(new { id = Guid.Empty, section, minRaw = raw, maxRaw = raw, scaledScore = scaled, predictedBand = (string?)null });
            }
        (await Authed(HttpMethod.Put, $"/api/admin/programs/{program.Id}/score-bands", admin, new { bands })).EnsureSuccessStatusCode();
        (await Authed(HttpMethod.Put, $"/api/admin/programs/{program.Id}", admin, new
        {
            name = program.Name, slug = program.Slug, description = program.Description,
            summary = program.Summary, priceIdr = program.PriceIdr, published = true,
        })).EnsureSuccessStatusCode();
        return new Seeded(admin, program.Slug, video, live);
    }

    private async Task<Guid> Session(string admin, Guid programId, string type, string title, int order, object extra)
    {
        var body = new Dictionary<string, object?>
        {
            ["type"] = type, ["title"] = title, ["description"] = null, ["orderIndex"] = order,
            ["providerAssetId"] = null, ["durationSeconds"] = null, ["scheduledAt"] = null,
            ["liveMode"] = null, ["joinUrl"] = null, ["location"] = null, ["assessmentId"] = null,
        };
        foreach (var p in extra.GetType().GetProperties()) body[p.Name] = p.GetValue(extra);
        return (await PostJson<AdminSessionDto>($"/api/admin/programs/{programId}/sessions", admin, body)).Id;
    }

    private async Task<string> AdminToken()
    {
        var email = $"adm{Guid.NewGuid():N}@test.local";
        (await _client.PostAsJsonAsync("/api/auth/register", new { name = "Adm", email, password = Pw })).EnsureSuccessStatusCode();
        await WithDbResult(async db =>
        {
            var u = await db.Users.FirstAsync(x => x.Email == email);
            u.Role = Academy.Domain.Enums.UserRole.Admin;
            await db.SaveChangesAsync();
            return 0;
        });
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Pw });
        return (await login.Content.ReadFromJsonAsync<AuthTokens>())!.AccessToken;
    }

    private Task<HttpResponseMessage> Authed(HttpMethod method, string url, string token, object? body = null)
    {
        var req = new HttpRequestMessage(method, url) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };
        if (body is not null) req.Content = JsonContent.Create(body);
        return _client.SendAsync(req);
    }

    private async Task<T> PostJson<T>(string url, string token, object body)
    {
        var res = await Authed(HttpMethod.Post, url, token, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<T> WithDbResult<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}

/// <summary>Pins frontend/lib/itpFormat.ts to ToeflScoring so the landing page can't drift from the real exam.</summary>
public class ItpFormatConstantsTests
{
    [Fact]
    public void Frontend_constant_matches_ToeflScoring()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "lib", "itpFormat.ts"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var ts = File.ReadAllText(Path.Combine(dir!.FullName, "frontend", "lib", "itpFormat.ts"));

        int Section(string name, string field) => int.Parse(Regex.Match(ts,
            $@"{name}:\s*\{{[^}}]*?{field}:\s*(\d+)").Groups[1].Value);
        int Num(string field) => int.Parse(Regex.Match(ts, $@"{field}:\s*(\d+)").Groups[1].Value);

        Assert.Equal(ToeflScoring.ListeningQuestions, Section("listening", "questions"));
        Assert.Equal(ToeflScoring.ListeningMinutes, Section("listening", "minutes"));
        Assert.Equal(ToeflScoring.StructureQuestions, Section("structure", "questions"));
        Assert.Equal(ToeflScoring.StructureMinutes, Section("structure", "minutes"));
        Assert.Equal(ToeflScoring.ReadingQuestions, Section("reading", "questions"));
        Assert.Equal(ToeflScoring.ReadingMinutes, Section("reading", "minutes"));
        Assert.Equal(ToeflScoring.TotalMin, Num("totalMin"));
        Assert.Equal(ToeflScoring.TotalMax, Num("totalMax"));
    }
}
