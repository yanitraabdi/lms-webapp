// Admin editing of a video session's parts (spec 2026-10-05 §6).
namespace Academy.Application.Programs;

/// <summary>HasLearnerData: progress or attempts exist, so the part cannot be removed or change kind/test.</summary>
public record AdminSessionPartDto(
    Guid Id, int OrderIndex, string Kind, string Title,
    string? ProviderAssetId, int? DurationSeconds,
    Guid? AssessmentId, string? AssessmentTitle, bool HasLearnerData);

/// <summary>Id null ⇒ a new part. Order = position in the list.</summary>
public record SessionPartInput(
    Guid? Id, string Kind, string Title, string? ProviderAssetId, int? DurationSeconds, Guid? AssessmentId);

public record SaveSessionPartsRequest(IReadOnlyList<SessionPartInput> Parts);

public interface ISessionPartAdminService
{
    Task<IReadOnlyList<AdminSessionPartDto>> ListAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>Replaces the whole ordered list in one save, validated as one unit.</summary>
    Task<IReadOnlyList<AdminSessionPartDto>> SaveAsync(
        Guid actor, Guid sessionId, SaveSessionPartsRequest req, CancellationToken ct = default);
}
