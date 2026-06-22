namespace Academy.Application.Catalog;

/// <summary>Read-side catalog queries. Only published content is returned. When a userId is
/// supplied, each module carries the user's access state (entitled / preview / locked).</summary>
public interface ICatalogService
{
    Task<CatalogFacetsDto> GetFacetsAsync(CancellationToken ct = default);
    Task<CatalogPageDto> GetCatalogAsync(CatalogFilters filters, Guid? userId, CancellationToken ct = default);
    Task<ModuleDetailDto?> GetModuleBySlugAsync(string slug, Guid? userId, CancellationToken ct = default);
}

/// <summary>Resolves a user's current entitlement tier (highest active subscription), or null.</summary>
public interface IEntitlementService
{
    Task<int?> GetActiveTierAsync(Guid userId, CancellationToken ct = default);
}
