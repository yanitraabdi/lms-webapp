namespace Academy.Application.Assessments;

/// <summary>Pure validation for an admin audio upload. Messages are shown to the admin.</summary>
public static class MediaUpload
{
    private static readonly Dictionary<string, string> Accepted = new(StringComparer.OrdinalIgnoreCase)
    {
        ["audio/mpeg"] = ".mp3",
        ["audio/mp4"] = ".m4a",
        ["audio/x-m4a"] = ".m4a",
        ["audio/wav"] = ".wav",
        ["audio/x-wav"] = ".wav",
        ["audio/ogg"] = ".ogg",
    };

    /// <summary>Null when acceptable; otherwise the reason, in Bahasa Indonesia.</summary>
    public static string? Validate(string? contentType, long length, long maxBytes)
    {
        var type = Normalize(contentType);
        if (type is null || !Accepted.ContainsKey(type))
            return "Format audio tidak didukung. Gunakan MP3, M4A, WAV, atau OGG.";
        if (length <= 0)
            return "Berkas audio kosong.";
        if (length > maxBytes)
            return $"Berkas audio terlalu besar. Maksimum {Describe(maxBytes)}.";
        return null;
    }

    /// <summary>The extension for a stored object. Derived from the content type — never from
    /// a client-supplied filename.</summary>
    public static string ExtensionFor(string contentType)
        => Normalize(contentType) is string t && Accepted.TryGetValue(t, out var ext) ? ext : ".bin";

    /// <summary>MB reads naturally at production limits; KB keeps a small limit from rounding to "0 MB".</summary>
    private static string Describe(long bytes)
        => bytes >= 1024 * 1024 ? $"{bytes / (1024 * 1024)} MB" : $"{bytes / 1024} KB";

    private static string? Normalize(string? contentType)
        => string.IsNullOrWhiteSpace(contentType) ? null : contentType.Split(';')[0].Trim();
}
