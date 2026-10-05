using System.Security.Claims;
using Academy.Application.Assessments;
using Academy.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Academy.Api.Endpoints;

/// <summary>
/// Bulk question import (KAK §9.12). Authoring 140 ITP questions through the modal one at a time
/// is the practical bottleneck between a working build and a sellable programme; this is the way
/// round it.
/// </summary>
public static class QuestionImportEndpoints
{
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>140 questions of transcribed prose is well under a megabyte. The ceiling is here
    /// so a mistaken upload is refused before it is buffered, not to bound legitimate use.</summary>
    private const long MaxWorkbookBytes = 5 * 1024 * 1024;

    public static IEndpointRouteBuilder MapQuestionImportEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin/questions")
            .RequireAuthorization("Admin")
            .WithTags("AdminQuestionImport");

        g.MapGet("/import/template", FileContentHttpResult (IQuestionImportService s) =>
            TypedResults.File(s.BuildTemplate(), XlsxContentType, "inverta-bank-soal-template.xlsx"));

        g.MapPost("/import/preview",
                async Task<Results<Ok<ImportResultDto>, ProblemHttpResult>> (
                    IFormFile file, [FromForm] string? bank, IQuestionImportService s, CancellationToken ct) =>
                {
                    if (!TryBank(bank, out var target)) return NoBank();
                    if (TooLarge(file) is ProblemHttpResult problem) return problem;
                    await using var stream = file.OpenReadStream();
                    return TypedResults.Ok(await s.PreviewAsync(target, stream, ct));
                })
            .DisableAntiforgery();          // required for IFormFile binding in minimal APIs

        g.MapPost("/import",
                async Task<Results<Ok<ImportResultDto>, ProblemHttpResult>> (
                    IFormFile file, [FromForm] string? bank, ClaimsPrincipal user, IQuestionImportService s, CancellationToken ct) =>
                {
                    if (!TryBank(bank, out var target)) return NoBank();
                    if (TooLarge(file) is ProblemHttpResult problem) return problem;
                    await using var stream = file.OpenReadStream();
                    // A file with errors comes back 200 with Committed=false and the error list:
                    // problem-details has nowhere to carry a per-cell error table, and the screen
                    // renders the same body either way.
                    return TypedResults.Ok(await s.CommitAsync(user.UserId(), target, stream, ct));
                })
            .DisableAntiforgery();

        return app;
    }

    // TryParse alone accepts numeric strings like "99"; only a DEFINED value is a bank.
    private static bool TryBank(string? bank, out QuestionBank target) =>
        Enum.TryParse(bank, true, out target) && Enum.IsDefined(target);

    private static ProblemHttpResult NoBank() =>
        TypedResults.Problem(title: "Pilih bank soal tujuan impor.", statusCode: 400);

    private static ProblemHttpResult? TooLarge(IFormFile file) => file.Length > MaxWorkbookBytes
        ? TypedResults.Problem(title: "Berkas terlalu besar. Maksimum 5 MB.", statusCode: 400)
        : null;
}
