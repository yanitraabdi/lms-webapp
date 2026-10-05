using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Academy.Application.Auth;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using Academy.Application.Abstractions;
using Academy.Application.Programs;
using Academy.Infrastructure.Learning;

namespace Academy.Integration.Tests;

/// <summary>
/// The admin picks a session's video from the Bunny library instead of pasting a GUID. These pin
/// the mapping against a captured Bunny response — not a live call, which would need credentials
/// and network in CI — and the failure modes, each of which must leave the admin a way forward.
/// </summary>
public class VideoLibraryTests
{
    private const string LibraryId = "123456";

    /// <summary>The documented shape of GET /library/{id}/videos, with extra fields we ignore.</summary>
    private const string BunnyBody = """
        {"totalItems":2,"currentPage":1,"itemsPerPage":50,"items":[
          {"guid":"6f1d2c3b-0000-4000-8000-000000000001","title":"Sesi 1 — Format TOEFL","length":912,
           "status":4,"encodeProgress":100,"views":3,"dateUploaded":"2026-09-30T10:00:00","isPublic":false,"storageSize":104857600},
          {"guid":"6f1d2c3b-0000-4000-8000-000000000002","title":"Sesi 2 — Listening","length":0,
           "status":3,"encodeProgress":63,"views":0,"dateUploaded":"2026-10-01T10:00:00","isPublic":false,"storageSize":0}]}
        """;

    private sealed class StubHandler(HttpStatusCode code, string body, string contentType = "application/json") : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Last = request;
            return Task.FromResult(new HttpResponseMessage(code)
            {
                Content = new StringContent(body, Encoding.UTF8, contentType),
            });
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new HttpRequestException("connection refused");
    }

    private static BunnyVideoLibrary Library(HttpMessageHandler handler) => new(
        new HttpClient(handler),
        new VideoOptions { Provider = "bunny", LibraryId = LibraryId, ApiKey = "library-api-key" });

    [Fact]
    public void Signature_is_sha256_hex_of_library_key_expiry_and_video()
    {
        // Independently computed: sha256("123456" + "library-api-key" + "1760000000" + "vid-1").
        var expected = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes("123456library-api-key1760000000vid-1"))).ToLowerInvariant();
        Assert.Equal(expected, BunnyUploadSigner.Sign("123456", "library-api-key", 1760000000, "vid-1"));
        Assert.Equal(64, expected.Length);
    }

    [Fact]
    public async Task Creating_an_upload_creates_the_video_and_returns_a_signed_ticket()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"guid":"6f1d2c3b-0000-4000-8000-0000000000aa","title":"Sesi 9","status":0}""");
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var ticket = await Library(handler).CreateUploadAsync("Sesi 9");

        Assert.Equal(HttpMethod.Post, handler.Last!.Method);
        Assert.Equal($"https://video.bunnycdn.com/library/{LibraryId}/videos", handler.Last.RequestUri!.ToString());
        Assert.Equal("library-api-key", handler.Last.Headers.GetValues("AccessKey").Single());
        Assert.Equal("6f1d2c3b-0000-4000-8000-0000000000aa", ticket.VideoId);
        Assert.Equal(LibraryId, ticket.LibraryId);
        Assert.Equal("https://video.bunnycdn.com/tusupload", ticket.Endpoint);
        Assert.InRange(ticket.ExpiresAt, before + 7200 - 5, before + 7200 + 5);
        Assert.Equal(BunnyUploadSigner.Sign(LibraryId, "library-api-key", ticket.ExpiresAt, ticket.VideoId), ticket.Signature);
        // GR-9: the key is never part of what the browser receives.
        Assert.DoesNotContain("library-api-key", JsonSerializer.Serialize(ticket));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Bunny menolak kunci API. Periksa BUNNY_API_KEY.")]
    [InlineData(HttpStatusCode.Forbidden, "Bunny menolak kunci API. Periksa BUNNY_API_KEY.")]
    [InlineData(HttpStatusCode.InternalServerError, "Bunny membalas 500. Coba lagi.")]
    public async Task Bunny_refusing_the_create_is_a_502_with_a_reason(HttpStatusCode code, string message)
    {
        var e = await Assert.ThrowsAsync<ProgramException>(
            () => Library(new StubHandler(code, "{}")).CreateUploadAsync("Sesi 9"));
        Assert.Equal(502, e.StatusCode);
        Assert.Equal(message, e.Message);
    }

    [Fact]
    public async Task Bunny_unreachable_on_create_is_a_502()
    {
        var e = await Assert.ThrowsAsync<ProgramException>(
            () => Library(new ThrowingHandler()).CreateUploadAsync("Sesi 9"));
        Assert.Equal(502, e.StatusCode);
        Assert.Equal("Bunny tidak dapat dihubungi. Coba lagi.", e.Message);
    }

    [Fact]
    public void Renewing_signs_a_fresh_ticket_for_the_same_video_without_calling_bunny()
    {
        var handler = new ThrowingHandler();           // any call would throw
        var ticket = Library(handler).RenewUpload("6f1d2c3b-0000-4000-8000-0000000000aa");
        Assert.Equal("6f1d2c3b-0000-4000-8000-0000000000aa", ticket.VideoId);
        Assert.Equal(BunnyUploadSigner.Sign(LibraryId, "library-api-key", ticket.ExpiresAt, ticket.VideoId), ticket.Signature);
    }

    [Fact]
    public async Task Unavailable_library_refuses_uploads_with_409()
    {
        var lib = new UnavailableVideoLibrary("dev");
        var e = await Assert.ThrowsAsync<ProgramException>(() => lib.CreateUploadAsync("x"));
        Assert.Equal(409, e.StatusCode);
        Assert.Equal("Unggah video hanya tersedia saat Bunny aktif.", e.Message);
        Assert.Equal(409, Assert.Throws<ProgramException>(() => lib.RenewUpload("6f1d2c3b-0000-4000-8000-0000000000aa")).StatusCode);
    }

    [Fact]
    public async Task Bunny_videos_are_mapped_with_their_length_and_status()
    {
        var page = await Library(new StubHandler(HttpStatusCode.OK, BunnyBody)).ListAsync(null, 1);

        Assert.Null(page.Unavailable);
        Assert.Equal(2, page.TotalItems);
        var ready = page.Items[0];
        Assert.Equal("6f1d2c3b-0000-4000-8000-000000000001", ready.Id);
        Assert.Equal("Sesi 1 — Format TOEFL", ready.Title);
        Assert.Equal(912, ready.LengthSeconds);
        Assert.Equal("Finished", ready.Status);
        Assert.Equal("Transcoding", page.Items[1].Status);
        Assert.Equal(63, page.Items[1].EncodeProgress);
    }

    [Theory]
    [InlineData(0, "Created")]
    [InlineData(2, "Processing")]
    [InlineData(4, "Finished")]
    [InlineData(5, "Error")]
    [InlineData(6, "UploadFailed")]
    [InlineData(99, "Unknown")]
    public void Every_bunny_status_has_a_name(int status, string name)
    {
        // The frontend keys off these names, so Bunny's numbering never leaks past this class.
        Assert.Equal(name, BunnyVideoLibrary.StatusName(status));
    }

    [Fact]
    public async Task The_request_is_authenticated_scoped_to_the_library_and_carries_the_search()
    {
        var handler = new StubHandler(HttpStatusCode.OK, BunnyBody);
        await Library(handler).ListAsync("sesi 1", 2);

        var req = handler.Last!;
        Assert.Equal("library-api-key", req.Headers.GetValues("AccessKey").Single());
        Assert.Equal($"/library/{LibraryId}/videos", req.RequestUri!.AbsolutePath);
        Assert.Contains("search=sesi%201", req.RequestUri.Query);
        Assert.Contains("page=2", req.RequestUri.Query);
    }

    [Fact]
    public async Task A_rejected_api_key_says_which_key_to_use()
    {
        // The usual mistake is pasting the ACCOUNT key or the token key. Name the right one.
        var page = await Library(new StubHandler(HttpStatusCode.Unauthorized, "")).ListAsync(null, 1);

        Assert.Empty(page.Items);
        Assert.Contains("kunci API", page.Unavailable);
        Assert.Contains("pustaka", page.Unavailable);
    }

    [Fact]
    public async Task An_unreachable_bunny_falls_back_to_manual_entry_instead_of_failing()
    {
        var page = await Library(new ThrowingHandler()).ListAsync(null, 1);

        Assert.Empty(page.Items);
        Assert.Contains("manual", page.Unavailable);
    }

    [Theory]
    [InlineData("not json", "application/json")]
    [InlineData("<html>portal</html>", "text/html")]
    public async Task A_response_that_cannot_be_read_falls_back_instead_of_failing(string body, string contentType)
    {
        var page = await Library(new StubHandler(HttpStatusCode.OK, body, contentType)).ListAsync(null, 1);

        Assert.Empty(page.Items);
        Assert.False(string.IsNullOrEmpty(page.Unavailable));
    }

    [Fact]
    public async Task A_null_items_list_is_an_empty_library_not_a_crash()
    {
        var page = await Library(new StubHandler(HttpStatusCode.OK,
            """{"totalItems":0,"currentPage":1,"items":null}""")).ListAsync(null, 1);

        Assert.Empty(page.Items);
        Assert.Null(page.Unavailable);
    }

    [Fact]
    public async Task Without_a_library_the_reason_is_reported_not_thrown()
    {
        var page = await new UnavailableVideoLibrary("alasan").ListAsync("x", 1);

        Assert.Empty(page.Items);
        Assert.Equal("alasan", page.Unavailable);
    }
}

/// <summary>
/// The endpoint proxies a call made with a credential that can delete videos, so who may reach it
/// matters more than what it returns. The suite runs under the dev provider, so the body is the
/// stated "unavailable" reason — which is also exactly what the picker must cope with.
/// </summary>
public class VideoLibraryEndpointTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client = factory.CreateClient();
    private const string Pw = "Password123";

    [Fact]
    public async Task Anonymous_requests_are_refused()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _client.GetAsync("/api/admin/video-library")).StatusCode);
    }

    [Fact]
    public async Task Learners_are_refused()
    {
        var token = await Token(UserRole.User);   // the learner role
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Get("/api/admin/video-library", token)).StatusCode);
    }

    [Fact]
    public async Task Admins_get_a_page_that_explains_itself_when_there_is_no_library()
    {
        var res = await Get("/api/admin/video-library?search=sesi", await Token(UserRole.Admin));
        res.EnsureSuccessStatusCode();

        var page = (await res.Content.ReadFromJsonAsync<VideoLibraryPageDto>(Json))!;
        Assert.Empty(page.Items);
        Assert.False(string.IsNullOrWhiteSpace(page.Unavailable));
    }

    private Task<HttpResponseMessage> Get(string url, string token)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url)
        { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };
        return _client.SendAsync(req);
    }

    private async Task<string> Token(UserRole role)
    {
        var email = $"vl{Guid.NewGuid():N}@test.local";
        (await _client.PostAsJsonAsync("/api/auth/register", new { name = "VL", email, password = Pw }))
            .EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Users.FirstAsync(x => x.Email == email);
            u.Role = role;
            await db.SaveChangesAsync();
        }
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Pw });
        return (await login.Content.ReadFromJsonAsync<AuthTokens>())!.AccessToken;
    }
}
