using System.Security.Cryptography;
using System.Text;
using Academy.Application.Abstractions;

namespace Academy.Infrastructure.Learning;

/// <summary>
/// Local video provider simulating Bunny signed playback: mints a per-session, short-TTL
/// HMAC-signed URL pointing at a sample stream so the player works without credentials.
/// Swapped for BunnyVideoProvider when Video:Provider = "bunny".
/// </summary>
public class DevVideoProvider(VideoOptions options) : IVideoProvider
{
    public string Source => "dev";

    public Task<PlaybackTicket> CreatePlaybackTicketAsync(string assetId, Guid userId, TimeSpan ttl, CancellationToken ct = default)
    {
        var expires = DateTimeOffset.UtcNow.Add(ttl);
        var exp = expires.ToUnixTimeSeconds();
        var token = Sign($"{assetId}.{userId}.{exp}", options.SigningKey);
        var sep = options.SampleVideoUrl.Contains('?') ? '&' : '?';
        var url = $"{options.SampleVideoUrl}{sep}token={token}&exp={exp}";
        return Task.FromResult(new PlaybackTicket(url, expires, options.SampleCaptionsUrl));
    }

    private static string Sign(string payload, string key)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexStringLower(h.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }
}
