using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Academy.Application.Catalog;
using Academy.Infrastructure.Catalog;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

public class CatalogTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _client = factory.CreateClient();

    private async Task SeedAsync()
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync();
    }

    [Fact]
    public async Task Facets_returns_levels_categories_tags()
    {
        await SeedAsync();
        var facets = await _client.GetFromJsonAsync<CatalogFacetsDto>("/api/catalog/facets", Json);
        Assert.NotNull(facets);
        Assert.Contains(facets!.Levels, l => l.Slug == "basic");
        Assert.NotEmpty(facets.Categories);
        Assert.NotEmpty(facets.Tags);
    }

    [Fact]
    public async Task Catalog_filters_by_level_and_search()
    {
        await SeedAsync();
        var basic = await _client.GetFromJsonAsync<CatalogPageDto>("/api/catalog?level=basic&take=100", Json);
        Assert.NotNull(basic);
        Assert.True(basic!.Total >= 12);
        Assert.All(basic.Modules, m => Assert.Equal("Basic", m.LevelName));

        var search = await _client.GetFromJsonAsync<CatalogPageDto>("/api/catalog?search=prompt&take=100", Json);
        Assert.NotNull(search);
        Assert.True(search!.Total >= 1);
    }

    [Fact]
    public async Task Anonymous_sees_preview_and_locked_but_not_entitled()
    {
        await SeedAsync();
        var page = await _client.GetFromJsonAsync<CatalogPageDto>("/api/catalog?take=100", Json);
        Assert.NotNull(page);
        Assert.Contains(page!.Modules, m => m.Access == ModuleAccess.Preview);
        Assert.Contains(page.Modules, m => m.Access == ModuleAccess.Locked);
        Assert.DoesNotContain(page.Modules, m => m.Access == ModuleAccess.Entitled); // no subscriptions yet (M3)
    }

    [Fact]
    public async Task Module_by_slug_returns_detail_or_404()
    {
        await SeedAsync();
        var page = await _client.GetFromJsonAsync<CatalogPageDto>("/api/catalog?take=1", Json);
        var slug = page!.Modules[0].Slug;

        var detail = await _client.GetFromJsonAsync<ModuleDetailDto>($"/api/modules/{slug}", Json);
        Assert.NotNull(detail);
        Assert.Equal(slug, detail!.Slug);
        Assert.False(string.IsNullOrWhiteSpace(detail.Description));

        var missing = await _client.GetAsync("/api/modules/tidak-ada-xyz");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
