using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using JiggerJot.Api.Features.Catalog;
using JiggerJot.Api.Features.Inventory;
using JiggerJot.Api.Tests.Infrastructure;
using JiggerJot.Core.Entities;
using JiggerJot.Infrastructure.Persistence;
using JiggerJot.Infrastructure.Persistence.Seed;
using JiggerJot.Infrastructure.Repositories;

namespace JiggerJot.Api.Tests.Catalog;

/// <summary>
/// FILTER-1 (FEATURES §11): exploring the whole catalog rather than only what is makeable — by
/// ingredient or category, method, glass and serving type, all combinable with each other, with the
/// name search and with the two makeability toggles.
/// <para>
/// The ingredient filter is the one with an argument behind it. There is no "main spirit" column and
/// there never will be (JJ-014): a drink with two spirits or none makes that field a lie. So the
/// filter reads the recipe lines, and it matches an ingredient's name, its category and its
/// subcategory at once — "rum" finds every rum, "dark rum" finds the dark ones, and "elderflower"
/// finds a thing no editor would have thought to tag (JJ-015, JJ-016).
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CatalogFilterTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private readonly Guid _household = Guid.CreateVersion7();

    private static CocktailBrowseHandler Handler(AppDbContext db) =>
        new(new EfRepository<Cocktail>(db),
            new EfRepository<TenantInventory>(db),
            new EfRepository<IngredientSubstitution>(db));

    private async Task SeedAsync()
    {
        await using var db = Fixture.CreateContext();
        await new CatalogSeeder(db, NullLogger<CatalogSeeder>.Instance).SeedAsync();
    }

    private async Task<PagedResponse<CocktailSummary>> BrowseAsync(CocktailBrowseRequest request)
    {
        await using var db = Fixture.CreateContext(_household);
        return await Handler(db).BrowseAsync(request, default);
    }

    private static CocktailBrowseRequest All => new(null, 1, 100);

    /// <summary>The ingredients on one cocktail, by name, category and subcategory.</summary>
    private async Task<List<(string Name, string Category, string? Sub)>> LinesOfAsync(Guid id)
    {
        await using var db = Fixture.CreateContext();
        return [.. await db.CocktailIngredients.IgnoreQueryFilters()
            .Where(l => l.CocktailId == id)
            .Select(l => new ValueTuple<string, string, string?>(
                l.Ingredient!.Name, l.Ingredient!.Category!.Name, l.Ingredient!.Subcategory!.Name))
            .ToListAsync()];
    }

    [Fact]
    public async Task ByIngredientName_MatchesTheRecipeLines()
    {
        await SeedAsync();

        var page = await BrowseAsync(All with { Ingredient = "Campari" });

        Assert.NotEmpty(page.Items);
        Assert.Contains(page.Items, c => c.Name == "Negroni");
        foreach (var item in page.Items)
            Assert.Contains(await LinesOfAsync(item.Id),
                l => l.Name.Contains("Campari", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ByCategory_MatchesDrinksWhoseIngredientIsFiledThere()
    {
        await SeedAsync();

        // A category name that no INGREDIENT is called, so a pass here can only have come from the
        // category. Campari's category, and Campari is in the Negroni.
        var page = await BrowseAsync(All with { Ingredient = "Amaro and bitter" });

        Assert.Contains(page.Items, c => c.Name == "Negroni");
        Assert.All(page.Items, c => Assert.DoesNotContain("Amaro and bitter", c.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AParentCategory_CatchesEveryChild()
    {
        await SeedAsync();

        // JJ-016, the whole point: nobody types "London dry gin" when they mean gin. Every drink the
        // gin filter returns has a line filed somewhere under gin — by category, subcategory or name.
        var page = await BrowseAsync(All with { Ingredient = "Gin" });

        Assert.NotEmpty(page.Items);
        foreach (var item in page.Items)
        {
            var lines = await LinesOfAsync(item.Id);
            Assert.Contains(lines, l =>
                l.Name.Contains("gin", StringComparison.OrdinalIgnoreCase)
                || l.Category.Contains("gin", StringComparison.OrdinalIgnoreCase)
                || (l.Sub?.Contains("gin", StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // And it is genuinely wider than the name filter would be on its own.
        Assert.Contains(page.Items, c => c.Name == "Negroni");
    }

    [Fact]
    public async Task ByMethod_NarrowsToThatMethod()
    {
        await SeedAsync();

        Guid shake;
        await using (var db = Fixture.CreateContext())
            shake = await db.Methods.Where(m => m.Name == "Shake").Select(m => m.Id).SingleAsync();

        var page = await BrowseAsync(All with { MethodId = shake });

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, c => Assert.Equal("Shake", c.Method));
    }

    [Fact]
    public async Task ByGlass_NarrowsToThatGlass()
    {
        await SeedAsync();

        Guid glassId;
        string glassName;
        await using (var db = Fixture.CreateContext())
        {
            var glass = await db.Cocktails.IgnoreQueryFilters()
                .Where(c => c.GlassTypeId != null)
                .Select(c => new { c.GlassTypeId, Name = c.GlassType!.Name })
                .FirstAsync();
            (glassId, glassName) = (glass.GlassTypeId!.Value, glass.Name);
        }

        var page = await BrowseAsync(All with { GlassTypeId = glassId });

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, c => Assert.Equal(glassName, c.Glass));
    }

    [Fact]
    public async Task AGlassFilter_ExcludesRecipesThatNeverSaid()
    {
        await SeedAsync();

        Guid glassId;
        await using (var db = Fixture.CreateContext())
            glassId = await db.Cocktails.IgnoreQueryFilters()
                .Where(c => c.GlassTypeId != null).Select(c => c.GlassTypeId!.Value).FirstAsync();

        var page = await BrowseAsync(All with { GlassTypeId = glassId });

        // JJ-034: a quarter of the catalog never states a glass, and "no glass" is not a glass. They
        // drop out of a glass filter rather than being swept into whichever one was asked for.
        Assert.All(page.Items, c => Assert.NotNull(c.Glass));
    }

    [Fact]
    public async Task ByServingType_NarrowsToThatKind()
    {
        await SeedAsync();

        var page = await BrowseAsync(All with { ServingType = ServingType.FullDrink });

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, c => Assert.Equal(nameof(ServingType.FullDrink), c.ServingType));
    }

    [Fact]
    public async Task FiltersCombine_WithEachOther()
    {
        await SeedAsync();

        Guid stir;
        await using (var db = Fixture.CreateContext())
            stir = await db.Methods.Where(m => m.Name == "Stir").Select(m => m.Id).SingleAsync();

        var page = await BrowseAsync(All with { Ingredient = "Campari", MethodId = stir });

        // FEATURES §11 says "combinable" and means it: this is an AND, not a last-one-wins.
        Assert.All(page.Items, c => Assert.Equal("Stir", c.Method));
        Assert.Contains(page.Items, c => c.Name == "Negroni");

        var justCampari = await BrowseAsync(All with { Ingredient = "Campari" });
        Assert.True(page.Total <= justCampari.Total);
    }

    [Fact]
    public async Task FiltersCombine_WithTheSearchBox()
    {
        await SeedAsync();

        var page = await BrowseAsync(All with { Search = "negroni", Ingredient = "Campari" });

        Assert.All(page.Items, c => Assert.Contains("negroni", c.Name, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(page.Items, c => c.Name == "Negroni");
    }

    [Fact]
    public async Task FiltersCombine_WithTheMakeabilityToggles()
    {
        await SeedAsync();

        await using (var db = Fixture.CreateContext(_household))
        {
            var inventory = new InventoryHandler(
                new EfRepository<TenantInventory>(db),
                new EfRepository<Ingredient>(db),
                new EfRepository<IngredientCategory>(db),
                new TestCurrentTenant { TenantId = _household },
                new FakeTimeProvider(DateTimeOffset.UtcNow));
            foreach (var name in new[] { "London dry gin", "Campari", "Sweet vermouth" })
            {
                var id = await db.Ingredients.IgnoreQueryFilters()
                    .Where(i => i.TenantId == null && i.Name == name).Select(i => i.Id).SingleAsync();
                await inventory.SetAsync(id, true, default);
            }
        }

        var makeable = await BrowseAsync(All with { Ingredient = "Campari", MakeableOnly = true });

        // This is the pay-off for keeping makeability a filter rather than a screen: it stacks with
        // everything else instead of owning its own route with its own copy of the search box.
        Assert.Contains(makeable.Items, c => c.Name == "Negroni");
        Assert.All(makeable.Items, c => Assert.NotNull(c.Substitutions));

        var almost = await BrowseAsync(All with { Ingredient = "Campari", AlmostMakeableOnly = true });
        Assert.DoesNotContain(almost.Items, c => c.Name == "Negroni");
    }

    [Fact]
    public async Task AFilterThatMatchesNothing_IsAnEmptyPage_NotAnError()
    {
        await SeedAsync();

        var byId = await BrowseAsync(All with { MethodId = Guid.CreateVersion7() });
        var byName = await BrowseAsync(All with { Ingredient = "unobtainium" });

        // A read with a bad id is a caller's typo, not a reason to hand back a 400 — the same
        // reasoning that clamps page numbers rather than rejecting them.
        Assert.Empty(byId.Items);
        Assert.Equal(0, byId.Total);
        Assert.Empty(byName.Items);
    }

    [Fact]
    public async Task ABlankIngredientFilter_IsNoFilterAtAll()
    {
        await SeedAsync();

        var blank = await BrowseAsync(All with { Ingredient = "   " });

        Assert.Equal(CatalogSeeder.LoadCocktails().Cocktails.Count, blank.Total);
    }

    [Fact]
    public async Task TheIngredientFilter_TreatsWildcardsAsText()
    {
        await SeedAsync();

        // "%" means percent, not "anything". Without escaping this would return the whole catalog and
        // look for all the world like a working filter.
        Assert.Empty((await BrowseAsync(All with { Ingredient = "%" })).Items);
    }

    [Fact]
    public async Task TheTotalCountsTheFilteredCatalog_NotTheWholeOne()
    {
        await SeedAsync();

        var filtered = await BrowseAsync(All with { Ingredient = "Campari", PageSize = 1 });

        // The count drives the pager. Reporting the unfiltered total here would offer pages that do
        // not exist.
        Assert.True(filtered.Total > 0);
        Assert.True(filtered.Total < CatalogSeeder.LoadCocktails().Cocktails.Count);
        Assert.Single(filtered.Items);
    }

    [Fact]
    public async Task TheFilterOptions_AreOnlyTheOnesTheCatalogUses()
    {
        await SeedAsync();

        await using var db = Fixture.CreateContext(_household);
        var options = await Handler(db).FilterOptionsAsync(default);

        // The lookups are curated globals and most of them go unused by any given catalog. Offering a
        // glass that returns nothing is a filter that looks broken, so the options come from the
        // cocktails this household can actually see.
        Assert.NotEmpty(options.Methods);
        Assert.NotEmpty(options.Glasses);

        foreach (var method in options.Methods)
            Assert.NotEmpty((await BrowseAsync(All with { MethodId = method.Id })).Items);

        foreach (var glass in options.Glasses)
            Assert.NotEmpty((await BrowseAsync(All with { GlassTypeId = glass.Id })).Items);

        Assert.Equal([.. options.Methods.Select(m => m.Name).Order(StringComparer.Ordinal)],
                     [.. options.Methods.Select(m => m.Name)]);
    }
}
