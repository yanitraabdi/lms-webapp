using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Academy.Application.Assessments;
using Academy.Application.Auth;
using Academy.Application.Programs;
using Academy.Domain;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Academy.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>Task 2 of session parts (2026-10-05): every write path creates parts, and the migration
/// backfill converts legacy video sessions.</summary>
public class SessionPartWriteTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    [Fact]
    public async Task Creating_a_video_session_with_a_video_creates_a_lesson_part()
    {
        var (admin, programId) = await NewProgram();
        var s = await CreateVideoSession(admin, programId, "sample", 600);

        var parts = await PartsOf(s.Id);
        var p = Assert.Single(parts);
        Assert.Equal(SessionPartKind.LessonVideo, p.Kind);
        Assert.Equal(1, p.OrderIndex);
        Assert.Equal("sample", p.ProviderAssetId);
        Assert.Equal(600, p.DurationSeconds);
    }

    [Fact]
    public async Task Creating_a_video_session_without_a_video_creates_no_parts()
    {
        var (admin, programId) = await NewProgram();
        var s = await CreateVideoSession(admin, programId, null, null);

        Assert.Empty(await PartsOf(s.Id));
    }

    [Fact]
    public async Task Attaching_a_test_to_a_video_session_appends_a_test_part()
    {
        var (admin, programId) = await NewProgram();
        var s = await CreateVideoSession(admin, programId, "sample", 600);
        var a = await NewGatingAssessment(admin);

        (await Authed(HttpMethod.Put, $"/api/admin/sessions/{s.Id}/assessment", admin,
            new { assessmentId = a.Id })).EnsureSuccessStatusCode();

        var parts = await PartsOf(s.Id);
        Assert.Equal([SessionPartKind.LessonVideo, SessionPartKind.Test], parts.Select(p => p.Kind));
        Assert.Equal(a.Id, parts[1].AssessmentId);
        // Dual-write until readers switch to parts.
        Assert.Equal(a.Id, await WithDbResult(db => db.ProgramSessions
            .Where(x => x.Id == s.Id).Select(x => x.AssessmentId).SingleAsync()));
    }

    [Fact]
    public async Task Detaching_removes_the_test_part()
    {
        var (admin, programId) = await NewProgram();
        var s = await CreateVideoSession(admin, programId, "sample", 600);
        var a = await NewGatingAssessment(admin);
        (await Authed(HttpMethod.Put, $"/api/admin/sessions/{s.Id}/assessment", admin,
            new { assessmentId = a.Id })).EnsureSuccessStatusCode();

        (await Authed(HttpMethod.Put, $"/api/admin/sessions/{s.Id}/assessment", admin,
            new { assessmentId = (Guid?)null })).EnsureSuccessStatusCode();

        var parts = await PartsOf(s.Id);
        Assert.Equal([SessionPartKind.LessonVideo], parts.Select(p => p.Kind));
    }

    [Fact]
    public async Task Backfill_converts_a_legacy_video_session_and_points_progress_at_the_lesson()
    {
        var (admin, programId) = await NewProgram();
        var a = await NewGatingAssessment(admin);
        var user = await WithDbResult(async db =>
        {
            var u = new User { Id = Guid.CreateVersion7(), Name = "Bf", Email = $"bf{Guid.NewGuid():N}@test.local" };
            db.Users.Add(u);
            await db.SaveChangesAsync();
            return u.Id;
        });
        var sessionId = Guid.CreateVersion7();
        await WithDb(async db =>
        {
            db.ProgramSessions.Add(new ProgramSession
            {
                Id = sessionId, ProgramId = programId, OrderIndex = 99, Type = SessionType.Video,
                Title = "Legacy", ProviderAssetId = "sample", DurationSeconds = 300, AssessmentId = a.Id,
            });
            await db.SaveChangesAsync();
            db.WatchProgress.Add(new WatchProgress
            {
                Id = Guid.CreateVersion7(), UserId = user, SessionId = sessionId,
                PercentComplete = 50, LastWatchedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync(SessionPartBackfill.Sql);
            await db.Database.ExecuteSqlRawAsync(SessionPartBackfill.Sql);   // idempotent
        });

        var parts = await PartsOf(sessionId);
        Assert.Equal([(SessionPartKind.LessonVideo, 1), (SessionPartKind.Test, 2)],
            parts.Select(p => (p.Kind, p.OrderIndex)));
        Assert.Equal(a.Id, parts[1].AssessmentId);
        var progress = await WithDbResult(db => db.WatchProgress.SingleAsync(w => w.SessionId == sessionId));
        Assert.Equal(parts[0].Id, progress.PartId);
    }

    [Fact]
    public async Task A_test_used_by_a_part_cannot_be_deleted()
    {
        var (admin, programId) = await NewProgram();
        var s = await CreateVideoSession(admin, programId, "sample", 600);
        var a = await NewGatingAssessment(admin);
        (await Authed(HttpMethod.Put, $"/api/admin/sessions/{s.Id}/assessment", admin,
            new { assessmentId = a.Id })).EnsureSuccessStatusCode();

        var res = await Authed(HttpMethod.Delete, $"/api/admin/assessments/{a.Id}", admin);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // ---- helpers ----

    private Task<List<SessionPart>> PartsOf(Guid sessionId) => WithDbResult(db => db.SessionParts
        .Where(p => p.SessionId == sessionId).OrderBy(p => p.OrderIndex).ToListAsync());

    private async Task<(string Admin, Guid ProgramId)> NewProgram()
    {
        var admin = await AdminToken();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var program = await PostJson<AdminProgramDto>("/api/admin/programs", admin, new
        {
            name = $"Program {suffix}", slug = $"prog-{suffix}", description = "Uji parts",
            summary = (string?)null, priceIdr = 100000m, published = false,
        });
        return (admin, program.Id);
    }

    private Task<AdminSessionDto> CreateVideoSession(string admin, Guid programId, string? asset, int? seconds)
        => PostJson<AdminSessionDto>($"/api/admin/programs/{programId}/sessions", admin, new
        {
            type = "Video", title = "Sesi", description = (string?)null, orderIndex = 0,
            providerAssetId = asset, durationSeconds = seconds,
            scheduledAt = (DateTimeOffset?)null, liveMode = (string?)null,
            joinUrl = (string?)null, location = (string?)null, assessmentId = (Guid?)null,
        });

    private Task<AdminAssessmentDto> NewGatingAssessment(string admin)
        => PostJson<AdminAssessmentDto>("/api/admin/assessments", admin, new
        {
            kind = "Gating", title = "Tes sesi",
            config = new { passThreshold = 1, retakeCap = (int?)null, proctoringEnabled = false, sections = Array.Empty<object>() },
        });

    private async Task<string> AdminToken()
    {
        var email = $"adm{Guid.NewGuid():N}@test.local";
        (await _client.PostAsJsonAsync("/api/auth/register", new { name = "Adm", email, password = Pw }))
            .EnsureSuccessStatusCode();
        await WithDb(async db =>
        {
            var u = await db.Users.FirstAsync(x => x.Email == email);
            u.Role = UserRole.Admin;
            await db.SaveChangesAsync();
        });
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

    private async Task<T> PostJson<T>(string url, string token, object body)
    {
        var res = await Authed(HttpMethod.Post, url, token, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task WithDb(Func<AppDbContext, Task> action)
    {
        using var scope = factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<T> WithDbResult<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
