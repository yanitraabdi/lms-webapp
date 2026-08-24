using System.Text.RegularExpressions;
using Academy.Application.Abstractions;

namespace Academy.Infrastructure.Media;

/// <summary>
/// Stores objects as files beneath <see cref="MediaOptions.Root"/>. Swapped for an R2 adapter
/// when an R2 adapter is written.
/// </summary>
public partial class LocalObjectStorage(MediaOptions options) : IObjectStorage
{
    /// <summary>
    /// A key is exactly one lowercase folder segment and one filename. The serve route takes a
    /// key straight from a URL, so this is a trust boundary: anything with "..", a rooted path,
    /// or a backslash is refused before it reaches the filesystem. The filename may not be "."
    /// or ".." — those collapse away under path normalisation (e.g. "audio/.." resolves onto the
    /// root itself, and "audio/." onto the folder), which Resolve's root check alone would not
    /// catch since neither escapes the root tree. Anchored with \A/\z (not ^/$) so a trailing
    /// newline (e.g. a %0A-smuggled key) cannot sneak past — two spellings of one key must never
    /// validate, since the signed-URL HMAC treats them as different keys.
    /// </summary>
    public static bool IsValidKey(string key) =>
        !string.IsNullOrEmpty(key) && key.Length <= 200 && KeyPattern().IsMatch(key);

    [GeneratedRegex(@"\A[a-z0-9]+/(?!\.{1,2}\z)[A-Za-z0-9._-]+\z")]
    private static partial Regex KeyPattern();

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        if (!IsValidKey(key)) throw new ArgumentException($"Invalid object key: {key}", nameof(key));

        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Write to a temp file and swap atomically, so a mid-upload failure/cancellation can
        // never leave a truncated object in place of a good one.
        var tmp = path + ".tmp";
        try
        {
            await using (var file = File.Create(tmp))
            {
                await content.CopyToAsync(file, ct);
            }
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            File.Delete(tmp);
            throw;
        }
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
    {
        if (!IsValidKey(key)) return Task.FromResult<Stream?>(null);

        string path;
        try
        {
            path = Resolve(key);
        }
        catch (ArgumentException)
        {
            return Task.FromResult<Stream?>(null);
        }

        if (!File.Exists(path)) return Task.FromResult<Stream?>(null);

        try
        {
            // Seekable, so the serve route can satisfy range requests.
            return Task.FromResult<Stream?>(File.OpenRead(path));
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            // TOCTOU: the file vanished between the Exists check and OpenRead.
            return Task.FromResult<Stream?>(null);
        }
    }

    /// <summary>
    /// Resolves a key to an absolute path beneath the (canonicalised) root, and throws if the
    /// result would land outside it. Nothing admitted by <see cref="IsValidKey"/> today can
    /// escape the root (only one "/" is allowed, so the worst case — "audio/..") resolves onto
    /// the root itself, not past it — but this check makes that a property of Resolve itself, so
    /// it still holds if the key pattern is ever loosened.
    /// </summary>
    private string Resolve(string key)
    {
        var root = Path.GetFullPath(options.Root);
        var full = Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException($"Invalid object key: {key}", nameof(key));
        return full;
    }
}
