using System.Security.Cryptography;
using System.Text;

namespace Academy.Infrastructure.Learning;

/// <summary>
/// Bunny CDN token authentication, DIRECTORY form ("path style").
///
/// A directory token is not a preference — an HLS stream is a playlist plus hundreds of segment
/// files, and the player requests each one itself. A token that signs only the playlist URL gets
/// 403 on every segment, which looks exactly like a broken video. Bunny's own guidance is to use
/// path-style tokens for HLS so the segments are covered too.
///
/// Ported from Bunny's published C# signer (BunnyWay/BunnyCDN.TokenAuthentication), narrowed to
/// the one case this product needs: a directory token with no IP lock and no country rules. The
/// pieces that look arbitrary are load-bearing and come from that implementation —
///
///   • the signed message is signaturePath + expires + signingData, concatenated with NO separators
///   • signaturePath is the token_path, not the URL path
///   • token_path appears twice: unencoded inside the hash, percent-encoded in the URL
///   • base64 is made URL-safe (+ → -, / → _) and stripped of = padding
///
/// Get any of those wrong and Bunny answers 403 with no clue which part was wrong, so the test
/// pins this against Bunny's own published vector rather than against itself.
/// </summary>
public static class BunnyTokenSigner
{
    /// <summary>
    /// Signs every file under <paramref name="directoryPath"/> (e.g. "/{videoId}/") until
    /// <paramref name="expiresAt"/>, and returns the playable URL for <paramref name="filePath"/>.
    /// </summary>
    public static string SignDirectory(
        string securityKey, string host, string directoryPath, string filePath, DateTimeOffset expiresAt)
    {
        // Bunny's own vector signs "/abc" for the directory "/abc/" — the trailing slash is dropped
        // from the token_path but kept in the file path.
        var tokenPath = directoryPath.TrimEnd('/');
        var expires = expiresAt.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);

        var signingData = $"token_path={tokenPath}";
        var message = Encoding.UTF8.GetBytes(tokenPath + expires + signingData);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(securityKey));
        var token = "HS256-" + Convert.ToBase64String(hmac.ComputeHash(message))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var urlData = $"token_path={Uri.EscapeDataString(tokenPath)}";
        return $"https://{host}/bcdn_token={token}&{urlData}&expires={expires}{filePath}";
    }
}
