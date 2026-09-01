using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Academy.Application.Auth;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>
/// The import surface over HTTP. The template, preview and commit routes are admin-only, and so
/// is bulk audio — a learner reaching any of them would be reading or writing the answer key.
/// </summary>
public class QuestionImportApiTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    private record PlanItem(string ExternalId, string Section, string Prompt, bool IsUpdate);
    private record ParseError(int Row, string Column, string Message);
    private record Result(
        bool Committed, int CreateCount, int UpdateCount,
        Dictionary<string, int> PerSection, List<PlanItem> Items, List<ParseError> Errors);
    private record BulkItem(string Filename, string? Key, string? Error);
    private record BulkResult(List<BulkItem> Items);

    private static string Id(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private static MultipartFormDataContent Sheet(params string[] rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Questions");
        string[] headers = ["id", "section", "prompt", "choice_a", "choice_b", "answer"];
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
        {
            var values = rows[r].Split('|');
            for (var c = 0; c < values.Length; c++) ws.Cell(r + 2, c + 1).SetValue(values[c]);
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        var file = new ByteArrayContent(ms.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        return new MultipartFormDataContent { { file, "file", "bank.xlsx" } };
    }

    private async Task<HttpResponseMessage> Post(string url, string? token, HttpContent content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> Get(string url, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task An_admin_downloads_a_template_that_is_a_real_workbook()
    {
        var res = await Get("/api/admin/questions/import/template", await AdminToken());
        res.EnsureSuccessStatusCode();

        var bytes = await res.Content.ReadAsByteArrayAsync();
        using var wb = new XLWorkbook(new MemoryStream(bytes));      // throws if it is not xlsx
        Assert.Contains(wb.Worksheets, w => w.Name == "Questions");
    }

    [Fact]
    public async Task Preview_reports_the_plan_without_writing()
    {
        var id = Id("R");
        var res = await Post("/api/admin/questions/import/preview", await AdminToken(),
            Sheet($"{id}|Reading|Q|X|Y|A"));
        res.EnsureSuccessStatusCode();

        var result = (await res.Content.ReadFromJsonAsync<Result>(Json))!;
        Assert.False(result.Committed);
        Assert.Equal(1, result.CreateCount);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Questions.AnyAsync(q => q.ExternalId == id));
    }

    [Fact]
    public async Task Commit_writes_and_reports_what_it_wrote()
    {
        var id = Id("R");
        var res = await Post("/api/admin/questions/import", await AdminToken(), Sheet($"{id}|Reading|Q|X|Y|A"));
        res.EnsureSuccessStatusCode();

        var result = (await res.Content.ReadFromJsonAsync<Result>(Json))!;
        Assert.True(result.Committed);
        Assert.Equal(1, result.CreateCount);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Questions.AnyAsync(q => q.ExternalId == id));
    }

    [Fact]
    public async Task A_file_with_errors_comes_back_200_with_the_errors_and_commits_nothing()
    {
        // Not a 400: the screen renders the same body either way, and a problem-details response
        // has nowhere to put a per-cell error list.
        var id = Id("R");
        var res = await Post("/api/admin/questions/import", await AdminToken(),
            Sheet($"{id}|Speaking|Q|X|Y|A"));
        res.EnsureSuccessStatusCode();

        var result = (await res.Content.ReadFromJsonAsync<Result>(Json))!;
        Assert.False(result.Committed);
        Assert.Equal("section", Assert.Single(result.Errors).Column);
    }

    [Fact]
    public async Task A_non_workbook_upload_is_a_400_with_an_admin_facing_message()
    {
        var junk = new ByteArrayContent("not a spreadsheet"u8.ToArray());
        junk.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var form = new MultipartFormDataContent { { junk, "file", "bank.xlsx" } };

        var res = await Post("/api/admin/questions/import/preview", await AdminToken(), form);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("Excel", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Bulk_audio_stores_each_file_under_the_key_the_importer_will_compute()
    {
        // The contract that makes the two steps order-independent. If these ever disagree, every
        // Listening question points at an object that was stored somewhere else.
        var one = new ByteArrayContent("clip-one"u8.ToArray());
        one.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");
        var two = new ByteArrayContent("clip-two"u8.ToArray());
        two.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");
        var form = new MultipartFormDataContent { { one, "files", "L01.mp3" }, { two, "files", "L02.mp3" } };

        var res = await Post("/api/admin/media/audio/bulk", await AdminToken(), form);
        res.EnsureSuccessStatusCode();

        var result = (await res.Content.ReadFromJsonAsync<BulkResult>(Json))!;
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("audio/l01.mp3", result.Items[0].Key);
        Assert.Equal("audio/l02.mp3", result.Items[1].Key);
        Assert.All(result.Items, i => Assert.Null(i.Error));
    }

    [Fact]
    public async Task One_bad_file_does_not_reject_the_rest_of_the_batch()
    {
        // Unlike the sheet, uploads are independent: refusing 49 good recordings because the
        // fiftieth is a PDF would be pure cruelty at 140 questions.
        var good = new ByteArrayContent("clip"u8.ToArray());
        good.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");
        var bad = new ByteArrayContent("nope"u8.ToArray());
        bad.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var form = new MultipartFormDataContent { { good, "files", "L05.mp3" }, { bad, "files", "notes.pdf" } };

        var res = await Post("/api/admin/media/audio/bulk", await AdminToken(), form);
        res.EnsureSuccessStatusCode();

        var result = (await res.Content.ReadFromJsonAsync<BulkResult>(Json))!;
        Assert.Equal("audio/l05.mp3", result.Items[0].Key);
        Assert.Null(result.Items[1].Key);
        Assert.NotNull(result.Items[1].Error);
    }

    [Theory]
    [InlineData("/api/admin/questions/import/template")]
    public async Task A_learner_cannot_reach_the_template(string url)
        => Assert.Equal(HttpStatusCode.Forbidden, (await Get(url, await LearnerToken())).StatusCode);

    [Theory]
    [InlineData("/api/admin/questions/import/preview")]
    [InlineData("/api/admin/questions/import")]
    [InlineData("/api/admin/media/audio/bulk")]
    public async Task A_learner_cannot_import(string url)
        => Assert.Equal(HttpStatusCode.Forbidden,
            (await Post(url, await LearnerToken(), Sheet($"{Id("R")}|Reading|Q|X|Y|A"))).StatusCode);

    [Theory]
    [InlineData("/api/admin/questions/import/preview")]
    [InlineData("/api/admin/questions/import")]
    [InlineData("/api/admin/media/audio/bulk")]
    public async Task An_anonymous_request_cannot_import(string url)
        => Assert.Equal(HttpStatusCode.Unauthorized,
            (await Post(url, null, Sheet($"{Id("R")}|Reading|Q|X|Y|A"))).StatusCode);

    private async Task<string> LearnerToken()
    {
        var email = $"stu{Guid.NewGuid():N}@test.local";
        (await _client.PostAsJsonAsync("/api/auth/register", new { name = "Stu", email, password = Pw }))
            .EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Pw });
        return (await login.Content.ReadFromJsonAsync<AuthTokens>())!.AccessToken;
    }

    private async Task<string> AdminToken()
    {
        var email = $"adm{Guid.NewGuid():N}@test.local";
        (await _client.PostAsJsonAsync("/api/auth/register", new { name = "Adm", email, password = Pw }))
            .EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(x => x.Email == email);
            user.Role = UserRole.Admin;
            await db.SaveChangesAsync();
        }
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Pw });
        return (await login.Content.ReadFromJsonAsync<AuthTokens>())!.AccessToken;
    }
}
