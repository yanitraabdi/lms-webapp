using Microsoft.Extensions.Configuration;

namespace Academy.Infrastructure.Learning;

public class VideoOptions
{
    public const string SectionName = "Video";

    /// <summary>Active video provider: "dev" (local sim) or "bunny" (real, needs creds).</summary>
    public string Provider { get; set; } = "dev";

    /// <summary>Key used to sign per-session playback tokens (real: Bunny token auth key).</summary>
    public string SigningKey { get; set; } = "dev-video-signing-key-change-me";

    /// <summary>Playback URL TTL (seconds). Short-lived, per session (GR-3).</summary>
    public int TicketTtlSeconds { get; set; } = 1800;

    /// <summary>Video the dev provider points signed URLs at (public HLS test stream).</summary>
    public string SampleVideoUrl { get; set; } = "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8";

    public string? SampleCaptionsUrl { get; set; }

    public bool IsDev => string.Equals(Provider, "dev", StringComparison.OrdinalIgnoreCase);
}

public static class VideoOptionsFactory
{
    public static VideoOptions Build(IConfiguration config)
    {
        var o = new VideoOptions();
        config.GetSection(VideoOptions.SectionName).Bind(o);
        return o;
    }
}
