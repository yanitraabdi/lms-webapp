namespace Academy.Application.Admin;

// ---- DTOs ----

public record AdminModuleDto(
    Guid Id,
    string Title,
    string Slug,
    string LevelName,
    int LevelTier,
    bool Published,
    bool IsPreview,
    int RequiredPlanTier);

public record AdminPlanDto(
    Guid Id,
    string Name,
    int TierLevel,
    decimal PriceMonthly,
    decimal PriceAnnual,
    bool IsActive);

public record SetPublishedRequest(bool Published);

public record PlanPriceUpdate(Guid PlanId, decimal PriceMonthly, decimal PriceAnnual);

public record UpdatePlanPricesRequest(IReadOnlyList<PlanPriceUpdate> Items);

// ---- service ----

/// <summary>Admin content-ops (minimal M5 surface): module publish/unpublish + plan pricing.
/// Every mutation is audit-logged and triggers ISR revalidation of affected public pages.</summary>
public interface IAdminService
{
    Task<IReadOnlyList<AdminModuleDto>> GetModulesAsync(string? search, CancellationToken ct = default);
    Task SetModulePublishedAsync(Guid actorUserId, Guid moduleId, bool published, CancellationToken ct = default);
    Task<IReadOnlyList<AdminPlanDto>> GetPlansAsync(CancellationToken ct = default);
    Task UpdatePlanPricesAsync(Guid actorUserId, IReadOnlyList<PlanPriceUpdate> items, CancellationToken ct = default);
}

public class AdminException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
