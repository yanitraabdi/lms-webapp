using Microsoft.Extensions.Configuration;

namespace Academy.Infrastructure.Learning;

public class VideoOptions
{
    public const string SectionName = "Video";

    /// <summary>Active video provider: "dev" (local sim) or "bunny" (real, needs creds).</summary>
    public string Provider { get; set; } = "dev";

    /// <summary>Key used to sign per-session playback tokens (real: Bunny token auth key).</summary>
    public string SigningKey { get; set; } = VideoOptionsFactory.DevSigningKey;

    /// <summary>Playback URL TTL (seconds). Short-lived, per session (GR-3).</summary>
    public int TicketTtlSeconds { get; set; } = 1800;

    /// <summary>Video the dev provider points signed URLs at (public HLS test stream).</summary>
    public string SampleVideoUrl { get; set; } = "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8";

    public string? SampleCaptionsUrl { get; set; }

    /// <summary>
    /// The video library's pull zone hostname, e.g. "vz-1a2b3c4d-e5f.b-cdn.net". Bunny shows it on
    /// the library, and it is NOT the account hostname — every library has its own.
    /// </summary>
    public string PullZoneHostname { get; set; } = "";

    /// <summary>Bunny video library id. Not used to sign playback; kept so uploads and the admin
    /// screens have one place to read it from.</summary>
    public string LibraryId { get; set; } = "";

    /// <summary>Language code of the caption track to offer, e.g. "id". Blank offers none —
    /// which is the honest default, because a caption URL for a track that was never uploaded is
    /// a 403 the player surfaces as a broken subtitle button.</summary>
    public string? CaptionsLanguage { get; set; }

    public bool IsDev => string.Equals(Provider, "dev", StringComparison.OrdinalIgnoreCase);

    public bool IsBunny => string.Equals(Provider, "bunny", StringComparison.OrdinalIgnoreCase);
}

public static class VideoOptionsFactory
{
    public static VideoOptions Build(IConfiguration config)
    {
        var o = new VideoOptions();
        config.GetSection(VideoOptions.SectionName).Bind(o);
        if (o.IsBunny) Validate(o);
        return o;
    }

    /// <summary>
    /// Fails at startup rather than at the moment a learner presses play.
    ///
    /// The same reasoning as the mail relay: an incomplete config that boots gives a healthy API
    /// and a video that 403s for every learner, and the signing key left at its well-known dev
    /// default would sign URLs Bunny rejects — with no message saying which of the two was wrong.
    /// </summary>
    private static void Validate(VideoOptions o)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(o.PullZoneHostname)) missing.Add("Video:PullZoneHostname");
        if (string.IsNullOrWhiteSpace(o.LibraryId)) missing.Add("Video:LibraryId");
        if (string.IsNullOrWhiteSpace(o.SigningKey) || o.SigningKey == DevSigningKey)
            missing.Add("Video:SigningKey (Bunny token authentication key)");

        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Video:Provider is \"bunny\" but {string.Join(", ", missing)} " +
                "is not configured. Set it in server config or the secret store — never in the repo.");
    }

    internal const string DevSigningKey = "dev-video-signing-key-change-me";
}
