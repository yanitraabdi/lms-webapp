using Academy.Application.Abstractions;
using Academy.Application.Assessments;
using Academy.Infrastructure.Media;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

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
            .RequireRateLimiting("media");

        app.MapPost("/api/admin/media/audio", async Task<Results<Ok<MediaKeyResponse>, ProblemHttpResult>> (
                IFormFile file, IObjectStorage storage, MediaOptions options, CancellationToken ct) =>
            {
                if (MediaUpload.Validate(file.ContentType, file.Length, options.MaxUploadBytes) is string error)
                    return TypedResults.Problem(title: error, statusCode: 400);

                var key = $"audio/{Guid.CreateVersion7():N}{MediaUpload.ExtensionFor(file.ContentType)}";
                await using var stream = file.OpenReadStream();
                await storage.PutAsync(key, stream, file.ContentType, ct);

                return TypedResults.Ok(new MediaKeyResponse(key));
            })
            .RequireAuthorization("Admin")
            .WithTags("Media")
            .DisableAntiforgery()          // required for IFormFile binding in minimal APIs
            // our own MaxUploadBytes is the limit; Kestrel's default is 30 MB. There is no
            // minimal-API DisableRequestSizeLimit(), so the MVC metadata carries the same meaning.
            .WithMetadata(new DisableRequestSizeLimitAttribute());

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

public record MediaKeyResponse(string Key);
