using System.Text.RegularExpressions;
using Academy.Application.Abstractions;

namespace Academy.Infrastructure.Media;

/// <summary>
/// Stores objects as files beneath <see cref="MediaOptions.Root"/>. Swapped for an R2 adapter
/// when Storage:Provider = "r2".
/// </summary>
public partial class LocalObjectStorage(MediaOptions options) : IObjectStorage
{
    /// <summary>
    /// A key is exactly one lowercase folder segment and one filename. The serve route takes a
    /// key straight from a URL, so this is a trust boundary: anything with "..", a rooted path,
    /// or a backslash is refused before it reaches the filesystem.
    /// </summary>
    public static bool IsValidKey(string key) =>
        !string.IsNullOrEmpty(key) && key.Length <= 200 && KeyPattern().IsMatch(key);

    [GeneratedRegex(@"^[a-z0-9]+/[A-Za-z0-9._-]+$")]
    private static partial Regex KeyPattern();

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        if (!IsValidKey(key)) throw new ArgumentException($"Invalid object key: {key}", nameof(key));

        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
    {
        if (!IsValidKey(key)) return Task.FromResult<Stream?>(null);

        var path = Resolve(key);
        if (!File.Exists(path)) return Task.FromResult<Stream?>(null);

        // Seekable, so the serve route can satisfy range requests.
        return Task.FromResult<Stream?>(File.OpenRead(path));
    }

    private string Resolve(string key) => Path.Combine(options.Root, key.Replace('/', Path.DirectorySeparatorChar));
}
