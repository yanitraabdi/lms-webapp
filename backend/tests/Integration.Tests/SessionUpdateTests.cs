using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Academy.Application.Assessments;
using Academy.Application.Auth;
using Academy.Application.Programs;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>
/// The session update endpoint existed for months with no screen or test calling it. It ran every
/// field through ApplySession, which overwrote the quiz link, the order and the type from the
/// request — so the first edit form wired to it would have silently detached the session's quiz,
/// and the session would then complete on watching alone. These pin the safe semantics: an edit
/// owns the session's CONTENT; the quiz, the order and the type have their own endpoints.
/// </summary>
public class SessionUpdateTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    private static readonly DateTimeOffset LiveAt = new(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private record Ctx(string Admin, Guid VideoId, Guid AssessmentId, Guid LiveId);

    // ---- the quiz stays attached ----

    [Fact]
    public async Task An_edit_that_sends_no_quiz_does_not_detach_the_quiz()
    {
        var c = await Seed();

        var res = await Put(c, c.VideoId, Body("Video", "Judul baru", asset: "sample", duration: 600));
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        // A video's quiz lives on its Test part (2026-10-05).
        using var scope = factory.Services.CreateScope();
        Assert.Equal(c.AssessmentId, await scope.ServiceProvider.GetRequiredService<AppDbContext>().SessionParts
            .Where(p => p.SessionId == c.VideoId && p.Kind == SessionPartKind.Test)
            .Select(p => p.AssessmentId).SingleAsync());
    }

    // ---- order and type have their own rules ----

    [Fact]
    public async Task An_edit_cannot_move_the_session()
    {
        // The live session holds order 2. Sending 2 for the video used to collide with
        // UNIQUE(program_id, order_index) and fail as "Urutan sesi bentrok".
        var c = await Seed();

        var res = await Put(c, c.VideoId, Body("Video", "Sesi video", orderIndex: 2, asset: "sample", duration: 900));
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        Assert.Equal(1, (await Session(c.VideoId)).OrderIndex);
    }

    [Fact]
    public async Task An_edit_cannot_change_the_session_type()
    {
        // Watch progress and completions recorded against a video must not end up attached to a
        // session whose rules are a live session's.
        var c = await Seed();

        await Put(c, c.VideoId, Body("Live", "Sesi video", at: LiveAt));

        Assert.Equal(SessionType.Video, (await Session(c.VideoId)).Type);
    }

    // ---- the edit actually edits ----

    [Fact]
    public async Task An_edit_changes_the_content_fields()
    {
        var c = await Seed();
        var videoId = Guid.NewGuid().ToString();

        await Put(c, c.VideoId, Body("Video", "Sesi 1: Format TOEFL", asset: videoId, duration: 912));

        var stored = await Session(c.VideoId);
        Assert.Equal("Sesi 1: Format TOEFL", stored.Title);
        Assert.Equal(videoId, stored.ProviderAssetId);
        Assert.Equal(912, stored.DurationSeconds);
    }

    // ---- a rescheduled live session is reminded again ----

    [Fact]
    public async Task Rescheduling_a_live_session_re_arms_its_reminder()
    {
        // The reminder sweep claims a session by stamping ReminderSentAt and never revisits it.
        // Without a reset, learners get a reminder for the old slot and none for the new one.
        var c = await Seed();
        await MarkReminderSent(c.LiveId);

        await Put(c, c.LiveId, Body("Live", "Sesi Live", orderIndex: 2, at: LiveAt.AddDays(1)));

        Assert.Null((await Session(c.LiveId)).ReminderSentAt);
    }

    [Fact]
    public async Task Editing_a_live_session_without_moving_it_keeps_the_reminder_claimed()
    {
        // Renaming must not cause a second reminder for the same slot.
        var c = await Seed();
        await MarkReminderSent(c.LiveId);

        await Put(c, c.LiveId, Body("Live", "Sesi Live — nama baru", orderIndex: 2, at: LiveAt));

        Assert.NotNull((await Session(c.LiveId)).ReminderSentAt);
    }

    [Fact]
    public async Task An_edit_that_only_drops_the_seconds_is_not_a_reschedule()
    {
        // The datetime-local control is minute-precision, so the form sends the stored instant
        // truncated to :00. That is not a move and must not email learners a second time.
        var c = await Seed();
        await SetScheduledAt(c.LiveId, LiveAt.AddSeconds(37.123));
        await MarkReminderSent(c.LiveId);

        await Put(c, c.LiveId, Body("Live", "Sesi Live — judul baru", orderIndex: 2, at: LiveAt));

        Assert.NotNull((await Session(c.LiveId)).ReminderSentAt);
    }

    // ================================================================ helpers

    private static object Body(
        string type, string title, int orderIndex = 1, string? asset = null, int? duration = null,
        DateTimeOffset? at = null) => new
    {
        type, title, description = (string?)null, orderIndex,
        providerAssetId = asset, durationSeconds = duration,
        scheduledAt = at, liveMode = at is null ? null : "Zoom",
        joinUrl = (string?)null, location = (string?)null,
        assessmentId = (Guid?)null,          // what a form that does not model the quiz sends
    };

    /// <summary>A program holding a video session (order 1, with a quiz) and a live session (order 2).</summary>
    private async Task<Ctx> Seed()
    {
        var admin = await AdminToken();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var program = await PostJson<AdminProgramDto>("/api/admin/programs", admin, new
        {
            name = $"Edit {suffix}", slug = $"edit-{suffix}", description = "Uji ubah sesi",
            summary = (string?)null, priceIdr = 100000m, published = false,
        });

        var quiz = await PostJson<AdminAssessmentDto>("/api/admin/assessments", admin, new
        {
            kind = "Gating", title = "Kuis",
            config = new { passThreshold = 1, retakeCap = (int?)null, proctoringEnabled = false, sections = Array.Empty<object>() },
        });

        var video = await PostJson<AdminSessionDto>($"/api/admin/programs/{program.Id}/sessions", admin, new
        {
            type = "Video", title = "Sesi video", description = (string?)null, orderIndex = 1,
            providerAssetId = "sample", durationSeconds = 900,
            scheduledAt = (DateTimeOffset?)null, liveMode = (string?)null,
            joinUrl = (string?)null, location = (string?)null, assessmentId = quiz.Id,
        });

        var live = await PostJson<AdminSessionDto>($"/api/admin/programs/{program.Id}/sessions", admin, new
        {
            type = "Live", title = "Sesi Live", description = (string?)null, orderIndex = 2,
            providerAssetId = (string?)null, durationSeconds = (int?)null,
            scheduledAt = LiveAt, liveMode = "Zoom",
            joinUrl = (string?)null, location = (string?)null, assessmentId = (Guid?)null,
        });

        return new Ctx(admin, video.Id, quiz.Id, live.Id);
    }

    private Task<HttpResponseMessage> Put(Ctx c, Guid sessionId, object body)
        => Authed(HttpMethod.Put, $"/api/admin/sessions/{sessionId}", c.Admin, body);

    private async Task<Domain.Entities.ProgramSession> Session(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .ProgramSessions.AsNoTracking().FirstAsync(s => s.Id == id);
    }

    private async Task SetScheduledAt(Guid id, DateTimeOffset at)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await db.ProgramSessions.FirstAsync(x => x.Id == id);
        s.ScheduledAt = at;
        await db.SaveChangesAsync();
    }

    private async Task MarkReminderSent(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await db.ProgramSessions.FirstAsync(x => x.Id == id);
        s.ReminderSentAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

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

    private async Task<T> PostJson<T>(string url, string token, object body)
    {
        var res = await Authed(HttpMethod.Post, url, token, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }
}
