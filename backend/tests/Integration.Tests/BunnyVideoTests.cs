using Academy.Infrastructure.Learning;
using Microsoft.Extensions.Configuration;

namespace Academy.Integration.Tests;

/// <summary>
/// Until now the only video provider was DevVideoProvider, which ignores the asset id entirely and
/// points every session at one public test stream. Six lessons, one video — no real content could
/// be published.
///
/// The signature is the whole risk here. Bunny answers a wrong token with a bare 403 and no hint
/// which part of it was wrong, and that 403 arrives on the learner's segment requests rather than
/// anywhere a developer is looking. So the signer is pinned to Bunny's OWN published vector, not
/// to itself: a test that only agrees with the implementation would pass just as happily on a
/// signature Bunny rejects.
/// </summary>
public class BunnyVideoTests
{
    // Straight from BunnyWay/BunnyCDN.TokenAuthentication's TokenSignerTests (DirectoryAndPathAllowed):
    // key "SecurityKey", directory "/abc/", expires 1598024587.
    private const string OfficialKey = "SecurityKey";
    private const string OfficialExpected =
        "https://token-tester.b-cdn.net/bcdn_token=HS256-uVZvT3SbEoVKYJyDJgbcsDmSFf73cv-uNUVaJiKWpbQ" +
        "&token_path=%2Fabc&expires=1598024587/abc/";

    [Fact]
    public void The_signature_matches_bunnys_own_published_vector()
    {
        var url = BunnyTokenSigner.SignDirectory(
            OfficialKey, "token-tester.b-cdn.net",
            directoryPath: "/abc/", filePath: "/abc/",
            expiresAt: DateTimeOffset.FromUnixTimeSeconds(1598024587));

        Assert.Equal(OfficialExpected, url);
    }

    [Fact]
    public void The_token_covers_the_whole_directory_so_hls_segments_authenticate_too()
    {
        // An HLS stream is a playlist plus hundreds of segments the PLAYER fetches itself. A token
        // signing only playlist.m3u8 gets 403 on every segment, which presents as a broken video.
        // Signing the directory means the same token is valid for the files beside it.
        var at = DateTimeOffset.FromUnixTimeSeconds(1598024587);
        const string key = "SecurityKey";

        var playlist = BunnyTokenSigner.SignDirectory(key, "h.b-cdn.net", "/vid/", "/vid/playlist.m3u8", at);
        var segment = BunnyTokenSigner.SignDirectory(key, "h.b-cdn.net", "/vid/", "/vid/720p/0001.ts", at);

        var playlistToken = playlist[..playlist.IndexOf("/vid/playlist.m3u8", StringComparison.Ordinal)];
        var segmentToken = segment[..segment.IndexOf("/vid/720p/0001.ts", StringComparison.Ordinal)];
        Assert.Equal(playlistToken, segmentToken);
    }

    [Fact]
    public void A_different_video_gets_a_different_token()
    {
        // The directory is part of the signed message, so a token minted for one video must not
        // unlock another — otherwise one learner's link is a key to the whole library.
        var at = DateTimeOffset.FromUnixTimeSeconds(1598024587);
        var a = BunnyTokenSigner.SignDirectory("k", "h.b-cdn.net", "/a/", "/a/playlist.m3u8", at);
        var b = BunnyTokenSigner.SignDirectory("k", "h.b-cdn.net", "/b/", "/b/playlist.m3u8", at);

        Assert.NotEqual(
            a[..a.IndexOf("&token_path", StringComparison.Ordinal)],
            b[..b.IndexOf("&token_path", StringComparison.Ordinal)]);
    }

    // ---- the provider ----

    private static VideoOptions Bunny(params (string Key, string Value)[] extra)
    {
        var settings = new List<KeyValuePair<string, string?>>
        {
            new("Video:Provider", "bunny"),
            new("Video:PullZoneHostname", "vz-test.b-cdn.net"),
            new("Video:LibraryId", "12345"),
            new("Video:SigningKey", "token-key"),
        };
        foreach (var (k, v) in extra) settings.Add(new($"Video:{k}", v));
        return VideoOptionsFactory.Build(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
    }

    [Fact]
    public async Task The_ticket_plays_the_sessions_own_video()
    {
        // The point of the whole change: DevVideoProvider ignores the asset id and hands every
        // session the same test stream.
        var ticket = await new BunnyVideoProvider(Bunny())
            .CreatePlaybackTicketAsync("my-video-guid", Guid.NewGuid(), TimeSpan.FromMinutes(30));

        Assert.Contains("/my-video-guid/playlist.m3u8", ticket.Url);
        Assert.StartsWith("https://vz-test.b-cdn.net/", ticket.Url);
        Assert.InRange(ticket.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(25), DateTimeOffset.UtcNow.AddMinutes(35));
    }

    [Fact]
    public async Task A_session_with_no_video_id_fails_loudly_rather_than_serving_a_broken_url()
    {
        // An admin can create a video session and fill the id in later. Signing "" would produce a
        // plausible URL for the pull zone's root that 403s at play time.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new BunnyVideoProvider(Bunny()).CreatePlaybackTicketAsync("  ", Guid.NewGuid(), TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public async Task Captions_are_offered_only_when_a_language_is_configured()
    {
        // A caption URL for a track nobody uploaded is a 403 the player shows as a broken subtitle
        // button, which reads as our bug rather than as missing content.
        var without = await new BunnyVideoProvider(Bunny())
            .CreatePlaybackTicketAsync("v", Guid.NewGuid(), TimeSpan.FromMinutes(5));
        Assert.Null(without.CaptionsUrl);

        var with = await new BunnyVideoProvider(Bunny(("CaptionsLanguage", "id")))
            .CreatePlaybackTicketAsync("v", Guid.NewGuid(), TimeSpan.FromMinutes(5));
        Assert.Contains("/v/captions/id.vtt", with.CaptionsUrl!);
    }

    // ---- an incomplete bunny config must not start ----

    private static VideoOptions Build(params (string Key, string Value)[] settings)
        => VideoOptionsFactory.Build(new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>($"Video:{s.Key}", s.Value)))
            .Build());

    [Fact]
    public void The_default_provider_is_still_the_local_simulator()
    {
        Assert.True(Build().IsDev);
    }

    [Fact]
    public void Bunny_without_a_pull_zone_refuses_to_start()
    {
        var e = Assert.Throws<InvalidOperationException>(() => Build(
            ("Provider", "bunny"), ("LibraryId", "1"), ("SigningKey", "k")));

        Assert.Contains("Video:PullZoneHostname", e.Message);
    }

    [Fact]
    public void Bunny_still_carrying_the_dev_signing_key_refuses_to_start()
    {
        // The most dangerous config of the lot: everything present, so it looks configured, and
        // every URL it signs is rejected by Bunny.
        var e = Assert.Throws<InvalidOperationException>(() => Build(
            ("Provider", "bunny"), ("PullZoneHostname", "vz-x.b-cdn.net"), ("LibraryId", "1")));

        Assert.Contains("Video:SigningKey", e.Message);
    }
}
