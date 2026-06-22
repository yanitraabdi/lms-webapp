using System.Security.Claims;
using Academy.Application.Catalog;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Academy.Api.Endpoints;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Catalog");

        // Public read endpoints. If a valid bearer is present, per-module access state reflects it.
        // Typed results (Ok<T>) so the OpenAPI doc carries the DTO schemas for the generated client.
        group.MapGet("/catalog/facets", async Task<Ok<CatalogFacetsDto>> (ICatalogService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetFacetsAsync(ct)));

        group.MapGet("/catalog", async Task<Ok<CatalogPageDto>> (
            string[]? level, string[]? category, string[]? tag, string? search, string? sort,
            int? skip, int? take, ClaimsPrincipal user, ICatalogService svc, CancellationToken ct) =>
        {
            var filters = new CatalogFilters(
                level, category, tag, search, sort ?? "curriculum", skip ?? 0, take ?? 24);
            return TypedResults.Ok(await svc.GetCatalogAsync(filters, UserIdOrNull(user), ct));
        });

        group.MapGet("/modules/{slug}", async Task<Results<Ok<ModuleDetailDto>, NotFound>> (
            string slug, ClaimsPrincipal user, ICatalogService svc, CancellationToken ct) =>
        {
            var module = await svc.GetModuleBySlugAsync(slug, UserIdOrNull(user), ct);
            return module is null ? TypedResults.NotFound() : TypedResults.Ok(module);
        });

        return app;
    }

    private static Guid? UserIdOrNull(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("sub"), out var id) ? id : null;
}
