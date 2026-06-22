using System.Security.Cryptography;
using System.Text;

namespace Academy.Infrastructure.Auth;

/// <summary>Opaque-token generation + one-way hashing for refresh / verify / reset tokens.
/// Only the hash is persisted; the raw token is delivered to the client (cookie/email).</summary>
public static class TokenHasher
{
    public static string NewRawToken(int bytes = 32)
    {
        var buffer = RandomNumberGenerator.GetBytes(bytes);
        return Convert.ToBase64String(buffer).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
