using System.Net;
using System.Net.Http.Headers;
using Academy.Application.Abstractions;
using Academy.Infrastructure.Media;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>
/// GR-3: media URLs are minted server-side, short-TTL, after an access check. The signature IS
/// the credential here — an &lt;audio&gt; element cannot send an Authorization header.
/// </summary>
public class MediaDeliveryTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> SeedAsync(string key, byte[] bytes)
    {
        using var scope = factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        await storage.PutAsync(key, new MemoryStream(bytes), "audio/mpeg");
        return key;
    }

    private MediaSigner Signer()
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<MediaSigner>();
    }

    [Fact]
    public async Task A_valid_signature_streams_the_object()
    {
        var key = await SeedAsync("audio/ok.mp3", "hello-audio"u8.ToArray());

        var res = await _client.GetAsync(Signer().Sign(key));

        res.EnsureSuccessStatusCode();
        Assert.Equal("audio/mpeg", res.Content.Headers.ContentType?.MediaType);
        Assert.Equal("hello-audio", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_range_request_returns_a_partial_slice()
    {
        var key = await SeedAsync("audio/range.mp3", "0123456789"u8.ToArray());

        var req = new HttpRequestMessage(HttpMethod.Get, Signer().Sign(key));
        req.Headers.Range = new RangeHeaderValue(2, 5);
        var res = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.PartialContent, res.StatusCode);
        Assert.Equal("2345", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_expired_url_is_refused()
    {
        var key = await SeedAsync("audio/expired.mp3", [1, 2, 3]);
        var signer = Signer();

        // Sign for a moment already past.
        var exp = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds();
        var sig = signer.SignatureFor(key, exp);

        var res = await _client.GetAsync($"/api/media/{key}?exp={exp}&sig={sig}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task A_tampered_signature_is_refused()
    {
        var key = await SeedAsync("audio/tampered.mp3", [1, 2, 3]);
        var url = Signer().Sign(key);

        var res = await _client.GetAsync(url.Replace("sig=", "sig=00"));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task A_signature_minted_for_another_key_does_not_work_here()
    {
        await SeedAsync("audio/mine.mp3", [1, 2, 3]);
        var other = await SeedAsync("audio/yours.mp3", [4, 5, 6]);
        var signer = Signer();

        // Take the query from a URL signed for "yours" and paste it onto "mine".
        var query = signer.Sign(other).Split('?')[1];
        var res = await _client.GetAsync($"/api/media/audio/mine.mp3?{query}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task An_unknown_key_is_indistinguishable_from_a_bad_signature()
    {
        var missing = await _client.GetAsync(Signer().Sign("audio/absent.mp3"));
        var badSig = await _client.GetAsync("/api/media/audio/absent.mp3?exp=99999999999&sig=deadbeef");

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, badSig.StatusCode);
    }

    [Fact]
    public async Task A_traversal_key_is_refused()
    {
        var res = await _client.GetAsync("/api/media/..%2F..%2Fetc%2Fpasswd?exp=99999999999&sig=deadbeef");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
