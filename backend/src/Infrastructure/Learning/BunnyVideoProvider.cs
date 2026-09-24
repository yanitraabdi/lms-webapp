using Academy.Application.Abstractions;

namespace Academy.Infrastructure.Learning;

/// <summary>
/// Real playback through Bunny Stream.
///
/// The asset id is Bunny's video GUID, pasted into a session's "Bunny asset id" field by an admin.
/// Every viewing mints its own short-lived signed URL after the access check the caller already
/// made (GR-1, GR-3) — nothing is public, and nothing is stored.
///
/// The URL is the direct HLS playlist on the video library's pull zone rather than Bunny's iframe
/// embed, because the player in this app plays HLS itself; an embed would mean handing playback,
/// and the watch-progress reporting the linear lock depends on, to a third-party iframe.
/// </summary>
public class BunnyVideoProvider(VideoOptions options) : IVideoProvider
{
    public string Source => "bunny";

    public Task<PlaybackTicket> CreatePlaybackTicketAsync(
        string assetId, Guid userId, TimeSpan ttl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(assetId))
            throw new InvalidOperationException(
                "This session has no Bunny video id. Set it on the session in /admin.");

        var expiresAt = DateTimeOffset.UtcNow.Add(ttl);

        // Bunny lays a video out as /{videoId}/playlist.m3u8 with its segments alongside, so the
        // signed directory is the video's own folder: one token covers the playlist AND every
        // segment the player fetches afterwards.
        var url = BunnyTokenSigner.SignDirectory(
            options.SigningKey, options.PullZoneHostname,
            directoryPath: $"/{assetId}/",
            filePath: $"/{assetId}/playlist.m3u8",
            expiresAt);

        // Captions live under the same signed directory, so they need no token of their own.
        var captions = string.IsNullOrWhiteSpace(options.CaptionsLanguage)
            ? null
            : BunnyTokenSigner.SignDirectory(
                options.SigningKey, options.PullZoneHostname,
                directoryPath: $"/{assetId}/",
                filePath: $"/{assetId}/captions/{options.CaptionsLanguage}.vtt",
                expiresAt);

        return Task.FromResult(new PlaybackTicket(url, expiresAt, captions));
    }
}
