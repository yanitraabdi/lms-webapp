using Academy.Application.Abstractions;
using Academy.Infrastructure.Media;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Academy.Api.Endpoints;

/// <summary>Signed media delivery (GR-3). The signature is the credential — an audio element
/// cannot send an Authorization header, so this route is deliberately anonymous.</summary>
public static class MediaEndpoints
{
    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/media/{**key}", async Task<Results<FileStreamHttpResult, NotFound>> (
                string key, long? exp, string? sig,
                IObjectStorage storage, MediaSigner signer, CancellationToken ct) =>
            {
                // Every rejection is a 404: a 403 would confirm the key exists.
                if (exp is not long e || string.IsNullOrEmpty(sig) || !LocalObjectStorage.IsValidKey(key))
                    return TypedResults.NotFound();
                if (!signer.Verify(key, e, sig)) return TypedResults.NotFound();

                var stream = await storage.OpenReadAsync(key, ct);
                if (stream is null) return TypedResults.NotFound();

                return TypedResults.Stream(stream, ContentTypeFor(key), enableRangeProcessing: true);
            })
            .WithTags("Media")
            .RequireRateLimiting("playback");

        return app;
    }

    /// <summary>Derived from the stored key's extension — never from a client-supplied filename.</summary>
    internal static string ContentTypeFor(string key) =>
        Path.GetExtension(key).ToLowerInvariant() switch
        {
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            _ => "application/octet-stream",
        };
}
