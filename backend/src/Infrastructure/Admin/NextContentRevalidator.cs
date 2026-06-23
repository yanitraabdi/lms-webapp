using System.Net.Http.Json;
using Academy.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Academy.Infrastructure.Admin;

public class RevalidateOptions
{
    public const string SectionName = "Revalidate";

    /// <summary>Next.js base URL the API calls to revalidate (e.g. http://frontend:3000).</summary>
    public string? FrontendUrl { get; set; }

    /// <summary>Shared secret matched against the Next route's REVALIDATE_SECRET.</summary>
    public string? Secret { get; set; }

    public bool Enabled => !string.IsNullOrWhiteSpace(FrontendUrl) && !string.IsNullOrWhiteSpace(Secret);
}

public static class RevalidateOptionsFactory
{
    public static RevalidateOptions Build(IConfiguration config)
    {
        var o = new RevalidateOptions();
        config.GetSection(RevalidateOptions.SectionName).Bind(o);
        return o;
    }
}

/// <summary>Posts affected paths to the Next on-demand revalidation route. Best-effort:
/// disabled (no-op) when unconfigured, and never throws into the calling mutation.</summary>
public class NextContentRevalidator(
    HttpClient http,
    RevalidateOptions options,
    ILogger<NextContentRevalidator> logger) : IContentRevalidator
{
    public async Task RevalidateAsync(IReadOnlyCollection<string> paths, CancellationToken ct = default)
    {
        if (!options.Enabled || paths.Count == 0) return;
        try
        {
            var res = await http.PostAsJsonAsync(
                $"{options.FrontendUrl!.TrimEnd('/')}/api/revalidate",
                new { secret = options.Secret, paths }, ct);
            if (!res.IsSuccessStatusCode)
                logger.LogWarning("Revalidation returned {Status}.", (int)res.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Content revalidation failed (best-effort, ignored).");
        }
    }
}
