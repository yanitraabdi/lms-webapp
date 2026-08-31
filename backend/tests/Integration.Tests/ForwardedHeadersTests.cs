using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

namespace Academy.Integration.Tests;

/// <summary>
/// The API is never reached directly: the browser goes through the Next same-origin proxy, and in
/// the tunnel deploy through Cloudflare before that. Without forwarded-header handling,
/// Connection.RemoteIpAddress is the FRONTEND CONTAINER's address for every request, so every
/// per-IP rate limit collapses into a single bucket shared by all users.
///
/// These tests drive the rate limiter, because that is the thing the collapse actually broke —
/// asserting on RemoteIpAddress alone would pass even if the limiter still partitioned wrongly.
/// </summary>
public class ForwardedHeadersTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private const string Pw = "Password123";

    /// <summary>
    /// The shared factory disables throttling (PermitLimit 100000) so the suite is not rate-limited.
    /// These tests need it ON, so they take a host with a small window — the container and database
    /// are reused, only the setting differs.
    /// </summary>
    private HttpClient Throttled() =>
        factory.WithWebHostBuilder(b => b.UseSetting("RateLimits:PermitLimit", "5")).CreateClient();

    /// <summary>Burns login attempts until the limiter rejects, or gives up. Returns the attempt
    /// count that first saw a 429, or null if the window never closed.</summary>
    private static async Task<int?> BurnUntilLimited(HttpClient client, string? forwardedFor, int max = 30)
    {
        for (var i = 1; i <= max; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new { email = $"nobody{i}@test.local", password = Pw }),
            };
            if (forwardedFor is not null) req.Headers.Add("X-Forwarded-For", forwardedFor);

            var res = await client.SendAsync(req);
            if (res.StatusCode == HttpStatusCode.TooManyRequests) return i;
        }
        return null;
    }

    [Fact]
    public async Task Two_clients_behind_the_proxy_get_separate_rate_limit_budgets()
    {
        var client = Throttled();

        // Exhaust one client's window. Both requests arrive from the same connection — only the
        // forwarded header distinguishes them, which is exactly the production topology.
        var limitedAt = await BurnUntilLimited(client, "203.0.113.10");
        Assert.NotNull(limitedAt);

        // A different forwarded client must NOT inherit the exhausted budget.
        var other = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "someone@test.local", password = Pw }),
        };
        other.Headers.Add("X-Forwarded-For", "203.0.113.99");

        var res = await client.SendAsync(other);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, res.StatusCode);
    }

    [Fact]
    public async Task A_single_client_is_still_limited()
    {
        // The partitioning fix must not become a way to escape limiting altogether.
        var client = Throttled();
        Assert.NotNull(await BurnUntilLimited(client, "198.51.100.7"));
    }

    [Fact]
    public async Task Only_the_rightmost_forwarded_entry_is_trusted()
    {
        // Cloudflare APPENDS the true client IP to whatever the caller sent, so the rightmost
        // entry is the one it vouches for. A caller who prepends a fake address must not be able
        // to escape their own budget by varying it.
        var client = Throttled();

        var limitedAt = await BurnUntilLimited(client, "198.51.100.42");
        Assert.NotNull(limitedAt);

        // Same real client (rightmost), different spoofed prefix — still limited.
        var spoofed = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "spoof@test.local", password = Pw }),
        };
        spoofed.Headers.Add("X-Forwarded-For", "10.9.9.9, 198.51.100.42");

        var res = await client.SendAsync(spoofed);
        Assert.Equal(HttpStatusCode.TooManyRequests, res.StatusCode);
    }
}
