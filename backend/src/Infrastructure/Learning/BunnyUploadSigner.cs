using System.Security.Cryptography;
using System.Text;

namespace Academy.Infrastructure.Learning;

/// <summary>Bunny's documented TUS presigned-upload signature:
/// sha256_hex(libraryId + apiKey + expirationTime + videoId).</summary>
public static class BunnyUploadSigner
{
    public const string Endpoint = "https://video.bunnycdn.com/tusupload";
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(2);

    public static string Sign(string libraryId, string apiKey, long expiresAt, string videoId)
        => Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{libraryId}{apiKey}{expiresAt}{videoId}"))).ToLowerInvariant();
}
