using System.Text.RegularExpressions;

namespace Academy.Application.Assessments;

/// <summary>
/// Deterministic storage keys for listening audio: "L01.mp3" becomes "audio/l01.mp3", computed the
/// same way whether the caller has the uploaded file or only the filename written in an import
/// sheet. That is what lets the two steps happen in either order, with no filename-to-key mapping
/// table to keep in sync.
///
/// The result is a trust boundary — it goes into a URL that the media route hands to storage — so
/// it is built from an allow-list, forced to start with a letter or digit (which kills every
/// "." / ".." / leading-dot trick), and capped well inside LocalObjectStorage's 200-character
/// limit. Anything that does not survive that returns null rather than a guessed key.
/// </summary>
public static partial class AudioKey
{
    private const int MaxBasenameLength = 100;

    /// <summary>The key for a stored object, or null when the basename sanitises to nothing usable.</summary>
    public static string? Build(string? basename, string extension)
    {
        if (basename is null || !ExtensionPattern().IsMatch(extension)) return null;

        var cleaned = new string(basename
            .ToLowerInvariant()
            .Where(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')
            .ToArray());

        // Must START with a letter or digit: that is what makes "..", ".", and "---" impossible,
        // and it is a stronger rule than trimming, which "..." would survive as "".
        if (cleaned.Length == 0 || !char.IsAsciiLetterOrDigit(cleaned[0])) return null;
        if (cleaned.Length > MaxBasenameLength) cleaned = cleaned[..MaxBasenameLength];

        return $"audio/{cleaned}{extension}";
    }

    /// <summary>
    /// The key for a filename written in an import sheet. Only the basename is taken — any path
    /// segments a spreadsheet may carry are dropped, not resolved.
    /// </summary>
    public static string? FromFilename(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename)) return null;

        // Split on both separators by hand: Path.GetFileName does not treat '\' as a separator on
        // Linux, and a sheet authored on Windows can carry either.
        var name = filename.Trim();
        var cut = name.LastIndexOfAny(['/', '\\']);
        if (cut >= 0) name = name[(cut + 1)..];

        var dot = name.LastIndexOf('.');
        if (dot <= 0) return null;                       // no extension, or a leading-dot name

        return Build(name[..dot], name[dot..].ToLowerInvariant());
    }

    [GeneratedRegex(@"\A\.[a-z0-9]{1,6}\z")]
    private static partial Regex ExtensionPattern();
}
