using System.Security.Cryptography;
using System.Text;

namespace Academy.Infrastructure.Media;

/// <summary>
/// Mints and verifies signed media URLs. Same HMAC scheme as DevVideoProvider, with its own
/// key so rotating one does not invalidate the other.
/// </summary>
public class MediaSigner(MediaOptions options)
{
    /// <summary>A relative, same-origin URL the browser can hand straight to an audio element.</summary>
    public string Sign(string key)
    {
        var exp = DateTimeOffset.UtcNow.AddMinutes(options.UrlTtlMinutes).ToUnixTimeSeconds();
        return $"/api/media/{key}?exp={exp}&sig={SignatureFor(key, exp)}";
    }

    public string SignatureFor(string key, long exp)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(options.SigningKey));
        // Invariant formatting: minimal-API model binding parses `exp` with invariant rules, and a
        // signing payload must never depend on ambient culture.
        return Convert.ToHexStringLower(h.ComputeHash(
            Encoding.UTF8.GetBytes($"{key}.{exp.ToString(System.Globalization.CultureInfo.InvariantCulture)}")));
    }

    public bool Verify(string key, long exp, string sig)
    {
        // Compare raw seconds — DateTimeOffset.FromUnixTimeSeconds throws ArgumentOutOfRangeException
        // outside [-62135596800, 253402300799], which would turn an out-of-range exp into a 500 on
        // this anonymous route instead of the uniform 404 every other rejection returns.
        if (exp < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return false;

        // Fixed-time comparison: a byte-by-byte early exit leaks the signature one nibble at a time.
        var expected = Encoding.UTF8.GetBytes(SignatureFor(key, exp));
        var actual = Encoding.UTF8.GetBytes(sig);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
