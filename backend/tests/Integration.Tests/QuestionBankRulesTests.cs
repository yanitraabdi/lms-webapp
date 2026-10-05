using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Academy.Application.Assessments;
using Academy.Application.Auth;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>Question banks (spec 2026-10-05): list/create/move by bank, and a test draws only from its own bank.</summary>
public class QuestionBankRulesTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";
    private const string OtherBank = "Soal dari bank lain tidak bisa dipakai di tes ini.";

    [Fact]
    public async Task Creating_a_question_without_a_bank_is_refused()
    {
        var admin = await AdminToken();
        var res = await Authed(HttpMethod.Post, "/api/admin/questions", admin, new
        {
            section = "Reading", prompt = "Q", choices = new[] { "a", "b" }, correct = new[] { 0 },
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("Pilih bank soal.", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task List_filters_by_bank()
    {
        var admin = await AdminToken();
        var mark = Guid.NewGuid().ToString("N");
        var sim = await NewQuestion(admin, "Simulation", mark);
        var ses = await NewQuestion(admin, "SessionTest", mark);

        var onlySim = await List(admin, $"bank=Simulation&search={mark}");
        var onlySes = await List(admin, $"bank=SessionTest&search={mark}");
        var both = await List(admin, $"search={mark}");

        Assert.Equal([sim.Id], onlySim.Select(q => q.Id));
        Assert.Equal([ses.Id], onlySes.Select(q => q.Id));
        Assert.Equal("Simulation", onlySim[0].Bank);
        Assert.Equal(2, both.Count);
    }

    [Fact]
    public async Task A_session_test_refuses_a_simulation_question()
    {
        var admin = await AdminToken();
        var q = await NewQuestion(admin, "Simulation");
        var a = await NewAssessment(admin, "Gating");
        var res = await Authed(HttpMethod.Put, $"/api/admin/assessments/{a}/questions", admin,
            new { questionIdsInOrder = new[] { q.Id } });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains(OtherBank, await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_test_keeps_a_question_it_already_held()
    {
        var admin = await AdminToken();
        var held = await NewQuestion(admin, "Simulation");
        var fresh = await NewQuestion(admin, "SessionTest");
        var a = await NewAssessment(admin, "Gating");
        await Attach(a, held.Id);

        var res = await Authed(HttpMethod.Put, $"/api/admin/assessments/{a}/questions", admin,
            new { questionIdsInOrder = new[] { held.Id, fresh.Id } });

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        var ids = await WithDbResult(db => db.AssessmentQuestions.Where(x => x.AssessmentId == a)
            .OrderBy(x => x.OrderIndex).Select(x => x.QuestionId).ToListAsync());
        Assert.Equal([held.Id, fresh.Id], ids);
    }

    [Fact]
    public async Task A_final_test_refuses_a_session_question()
    {
        var admin = await AdminToken();
        var q = await NewQuestion(admin, "SessionTest");
        var a = await NewAssessment(admin, "Final");
        var res = await Authed(HttpMethod.Put, $"/api/admin/assessments/{a}/questions", admin,
            new { questionIdsInOrder = new[] { q.Id } });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains(OtherBank, await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Moving_is_refused_while_a_test_of_the_other_kind_uses_it()
    {
        var admin = await AdminToken();

        var sim = await NewQuestion(admin, "Simulation");
        await Attach(await NewAssessment(admin, "Final"), sim.Id);
        var r1 = await Move(admin, sim.Id, "SessionTest");
        Assert.Equal(HttpStatusCode.Conflict, r1.StatusCode);
        Assert.Contains("tes akhir", await r1.Content.ReadAsStringAsync());

        var ses = await NewQuestion(admin, "SessionTest");
        await Attach(await NewAssessment(admin, "Gating"), ses.Id);
        var r2 = await Move(admin, ses.Id, "Simulation");
        Assert.Equal(HttpStatusCode.Conflict, r2.StatusCode);
        Assert.Contains("tes sesi", await r2.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Moving_an_unused_question_changes_its_bank_and_is_audited()
    {
        var admin = await AdminToken();
        var mark = Guid.NewGuid().ToString("N");
        var q = await NewQuestion(admin, "SessionTest", mark);

        Assert.Equal(HttpStatusCode.NoContent, (await Move(admin, q.Id, "Simulation")).StatusCode);

        var moved = await List(admin, $"bank=Simulation&search={mark}");
        Assert.Equal([q.Id], moved.Select(x => x.Id));
        Assert.True(await WithDbResult(db => db.AuditLogs.AnyAsync(
            l => l.Action == "question_moved" && l.Target == q.Id.ToString())));
    }

    [Fact]
    public async Task Moving_to_the_same_bank_is_a_no_op()
    {
        var admin = await AdminToken();
        var q = await NewQuestion(admin, "SessionTest");

        Assert.Equal(HttpStatusCode.NoContent, (await Move(admin, q.Id, "SessionTest")).StatusCode);

        Assert.Equal(QuestionBank.SessionTest, await WithDbResult(db =>
            db.Questions.Where(x => x.Id == q.Id).Select(x => x.Bank).SingleAsync()));
    }

    [Fact]
    public async Task Updating_a_question_never_changes_its_bank()
    {
        var admin = await AdminToken();
        var q = await NewQuestion(admin, "SessionTest");

        var res = await Authed(HttpMethod.Put, $"/api/admin/questions/{q.Id}", admin, new
        {
            section = "Reading", prompt = "baru", choices = new[] { "a", "b" }, correct = new[] { 0 },
            bank = "Simulation",
        });
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        Assert.Equal(QuestionBank.SessionTest, await WithDbResult(db =>
            db.Questions.Where(x => x.Id == q.Id).Select(x => x.Bank).SingleAsync()));
    }

    [Fact]
    public async Task Counts_are_per_bank()
    {
        var admin = await AdminToken();
        var before = await Counts(admin);
        await NewQuestion(admin, "Simulation");
        await NewQuestion(admin, "SessionTest");
        var after = await Counts(admin);

        Assert.Equal(before.Simulation + 1, after.Simulation);
        Assert.Equal(before.SessionTest + 1, after.SessionTest);
    }

    // ================================================================ helpers

    private async Task<AdminQuestionDto> NewQuestion(string admin, string bank, string? mark = null)
    {
        var res = await Authed(HttpMethod.Post, "/api/admin/questions", admin, new
        {
            bank, section = "Reading", prompt = $"Q {mark ?? Guid.NewGuid().ToString("N")} {Guid.NewGuid():N}",
            choices = new[] { "a", "b" }, correct = new[] { 0 },
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AdminQuestionDto>(Json))!;
    }

    private async Task<Guid> NewAssessment(string admin, string kind)
    {
        var res = await Authed(HttpMethod.Post, "/api/admin/assessments", admin, new
        {
            kind, title = "Tes",
            config = new { passThreshold = 1, retakeCap = (int?)null, proctoringEnabled = false, sections = Array.Empty<object>() },
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AdminAssessmentDto>(Json))!.Id;
    }

    /// <summary>Puts a question straight into a test, bypassing the compose rule (as legacy data would be).</summary>
    private Task Attach(Guid assessment, Guid question) => WithDb(async db =>
    {
        db.AssessmentQuestions.Add(new AssessmentQuestion
        { Id = Guid.CreateVersion7(), AssessmentId = assessment, QuestionId = question, OrderIndex = 1 });
        await db.SaveChangesAsync();
    });

    private Task<HttpResponseMessage> Move(string admin, Guid id, string bank) =>
        Authed(HttpMethod.Post, $"/api/admin/questions/{id}/move", admin, new { bank });

    private async Task<List<AdminQuestionDto>> List(string admin, string query)
    {
        var res = await Authed(HttpMethod.Get, $"/api/admin/questions?{query}", admin);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<AdminQuestionDto>>(Json))!;
    }

    private async Task<QuestionBankCountsDto> Counts(string admin)
    {
        var res = await Authed(HttpMethod.Get, "/api/admin/questions/counts", admin);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<QuestionBankCountsDto>(Json))!;
    }

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
