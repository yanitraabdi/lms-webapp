using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Academy.Application.Auth;
using Academy.Domain.Enums;
using Academy.Infrastructure.Media;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>
/// Admin audio upload. Content type and size are validated server-side, and the stored
/// extension comes from the content type — never from a client-supplied filename.
/// </summary>
public class MediaUploadEndpointTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    private static MultipartFormDataContent Form(byte[] bytes, string contentType, string filename)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", filename } };
    }

    private async Task<HttpResponseMessage> Upload(string? token, MultipartFormDataContent form)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/admin/media/audio") { Content = form };
        if (token is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(req);
    }

    [Fact]
    public async Task An_admin_upload_is_stored_and_then_serves()
    {
        var res = await Upload(await AdminToken(), Form("real-audio"u8.ToArray(), "audio/mpeg", "clip.mp3"));
        res.EnsureSuccessStatusCode();

        var key = (await res.Content.ReadFromJsonAsync<KeyDto>(Json))!.Key;
        Assert.StartsWith("audio/", key);

        string url;
        using (var scope = factory.Services.CreateScope())
            url = scope.ServiceProvider.GetRequiredService<MediaSigner>().Sign(key);

        var played = await _client.GetAsync(url);
        played.EnsureSuccessStatusCode();
        Assert.Equal("real-audio", await played.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_extension_comes_from_the_content_type_not_the_filename()
    {
        var res = await Upload(await AdminToken(), Form([1, 2, 3], "audio/mpeg", "evil.exe"));
        res.EnsureSuccessStatusCode();

        var key = (await res.Content.ReadFromJsonAsync<KeyDto>(Json))!.Key;
        Assert.EndsWith(".mp3", key);
        Assert.DoesNotContain("evil", key);
    }

    [Fact]
    public async Task A_non_audio_content_type_is_refused()
    {
        var res = await Upload(await AdminToken(), Form([1, 2, 3], "video/mp4", "movie.mp4"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("Format audio", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_empty_file_is_refused()
    {
        var res = await Upload(await AdminToken(), Form([], "audio/mpeg", "empty.mp3"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        // Assert the message too: a bare 400 would also be satisfied by a binding failure.
        Assert.Contains("Berkas audio kosong", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_learner_cannot_upload()
    {
        var res = await Upload(await LearnerToken(), Form([1, 2, 3], "audio/mpeg", "clip.mp3"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task An_anonymous_request_cannot_upload()
    {
        var res = await Upload(null, Form([1, 2, 3], "audio/mpeg", "clip.mp3"));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    private record KeyDto(string Key);

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
            var u = await db.Users.FirstAsync(x => x.Email == email);
            u.Role = UserRole.Admin;
            await db.SaveChangesAsync();
        }
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Pw });
        return (await login.Content.ReadFromJsonAsync<AuthTokens>())!.AccessToken;
    }
}
