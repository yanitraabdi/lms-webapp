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
            // Kestrel's 30 MB default would reject a legitimate section recording before the
            // handler runs, and MaxUploadBytes can only be checked after model binding. There is
            // no minimal-API DisableRequestSizeLimit(), so the MVC metadata carries the same
            // meaning. What bounds this route is FormOptions.MultipartBodyLengthLimit (128 MB by
            // default) and nothing else: it is admin-only but NOT rate-limited, so an authenticated
            // admin can stream up to that ceiling, fully buffered, before MaxUploadBytes is read.
            // Acceptable for the admin surface; do not copy it onto an anonymous route.
            .WithMetadata(new DisableRequestSizeLimitAttribute());

        app.MapPost("/api/admin/media/audio/bulk", async Task<Ok<BulkAudioResponse>> (
                IFormFileCollection files, IObjectStorage storage, MediaOptions options,
                CancellationToken ct) =>
            {
                var items = new List<BulkAudioItem>();

                // Two files in the SAME batch can sanitise to the same key (e.g. two folders each
                // holding their own "01.mp3"). Track what this request has already stored so the
                // second file is refused instead of silently overwriting the first — both would
                // otherwise report success while one recording quietly vanishes. A deliberate
                // re-upload in a LATER request must still overwrite, so this set is request-local,
                // never a cross-request existence check.
                var keysInThisBatch = new Dictionary<string, string>(StringComparer.Ordinal);

                // Per-file, not all-or-nothing: unlike a sheet, uploads are independent, and
                // refusing 49 good recordings over one stray PDF helps nobody at 140 questions.
                foreach (var file in files)
                {
                    if (MediaUpload.Validate(file.ContentType, file.Length, options.MaxUploadBytes) is string error)
                    {
                        items.Add(new BulkAudioItem(file.FileName, null, error));
                        continue;
                    }

                    // The key the IMPORTER will compute from the sheet's audio_file column. Same
                    // rule, one implementation — otherwise every Listening question points at an
                    // object stored under a different name.
                    var key = AudioKey.ForUpload(file.FileName, MediaUpload.ExtensionFor(file.ContentType));
                    if (key is null)
                    {
                        items.Add(new BulkAudioItem(file.FileName, null,
                            "Nama berkas tidak dapat dipakai. Gunakan nama seperti L01.mp3."));
                        continue;
                    }

                    if (keysInThisBatch.TryGetValue(key, out var collidesWith))
                    {
                        items.Add(new BulkAudioItem(file.FileName, null,
                            $"Nama berkas menghasilkan kunci yang sama dengan '{collidesWith}' ({key}). " +
                            "Ganti nama salah satu berkas lalu unggah ulang."));
                        continue;
                    }

                    await using var stream = file.OpenReadStream();
                    await storage.PutAsync(key, stream, file.ContentType, ct);
                    keysInThisBatch[key] = file.FileName;
                    items.Add(new BulkAudioItem(file.FileName, key, null));
                }

                return TypedResults.Ok(new BulkAudioResponse(items));
            })
            .RequireAuthorization("Admin")
            .WithTags("Media")
            .DisableAntiforgery()
            // Same reasoning as the single-file route above: MaxUploadBytes can only be checked
            // after model binding, and a batch of section recordings would trip Kestrel's default
            // long before the handler runs.
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

/// <summary>One uploaded file: its stored key, or the reason it was refused. Never both.</summary>
public record BulkAudioItem(string Filename, string? Key, string? Error);

public record BulkAudioResponse(IReadOnlyList<BulkAudioItem> Items);
