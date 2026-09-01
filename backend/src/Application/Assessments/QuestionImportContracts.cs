namespace Academy.Application.Assessments;

/// <summary>One planned change. <c>IsUpdate</c> is the line an admin scans before committing —
/// it is where a reused id shows up as "update" when the author meant "new question".</summary>
public record ImportPlanItem(string ExternalId, string Section, string Prompt, bool IsUpdate);

/// <summary>
/// The result of a preview OR a commit — one shape for both, because the screen needs the same
/// error list either way, and a commit that fails validation IS a preview.
/// <c>Committed</c> is the only thing that separates them.
/// </summary>
public record ImportResultDto(
    bool Committed,
    int CreateCount,
    int UpdateCount,
    IReadOnlyDictionary<string, int> PerSection,
    IReadOnlyList<ImportPlanItem> Items,
    IReadOnlyList<ImportError> Errors);

public interface IQuestionImportService
{
    /// <summary>The .xlsx an author starts from.</summary>
    byte[] BuildTemplate();

    /// <summary>Parses and validates. Writes nothing, ever.</summary>
    Task<ImportResultDto> PreviewAsync(Stream file, CancellationToken ct = default);

    /// <summary>
    /// Re-parses, re-validates, and writes only if the file is entirely clean. Never trusts a
    /// plan the client sends back — the bank may have changed since the preview.
    /// </summary>
    Task<ImportResultDto> CommitAsync(Guid actor, Stream file, CancellationToken ct = default);
}
