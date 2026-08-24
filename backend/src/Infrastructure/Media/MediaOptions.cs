using Microsoft.Extensions.Configuration;

namespace Academy.Infrastructure.Media;

/// <summary>
/// Media storage and signed-delivery settings. Mirrors <see cref="Learning.VideoOptions"/>.
/// SigningKey MUST be overridden in deployed config — see the note on the default below.
/// </summary>
public class MediaOptions
{
    public const string StorageSection = "Storage";
    public const string MediaSection = "Media";

    /// <summary>Active object-storage provider: "local" (disk dev-sim) or "r2" (later).</summary>
    public string Provider { get; set; } = "local";

    /// <summary>Filesystem root for the local provider. Mounted as a docker volume.</summary>
    public string Root { get; set; } = "/data/media";

    /// <summary>Key used to sign media URLs. Separate from Video:SigningKey so rotating one
    /// does not invalidate the other. NEVER ship this default.</summary>
    public string SigningKey { get; set; } = "dev-media-signing-key-change-me";

    /// <summary>How long a signed media URL stays valid. Deliberately long: a section recording
    /// can run 35 minutes and every seek re-requests the URL, so a short TTL breaks playback
    /// partway through. Two hours exceeds the longest ITP section (Reading, 55 minutes).</summary>
    public int UrlTtlMinutes { get; set; } = 120;

    /// <summary>Largest accepted upload. A 35-minute MP3 is roughly 30 MB.</summary>
    public long MaxUploadBytes { get; set; } = 104_857_600;
}

public static class MediaOptionsFactory
{
    public static MediaOptions Build(IConfiguration config)
    {
        var o = new MediaOptions();
        config.GetSection(MediaOptions.StorageSection).Bind(o);
        config.GetSection(MediaOptions.MediaSection).Bind(o);
        return o;
    }
}
