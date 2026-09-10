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
/// MAKE-1: what a household can make right now — the question the app exists to answer.
/// <para>
/// Makeability is DERIVED at query time from inventory, recipe lines and substitutions, and is never
/// stored as a flag (JJ-003, JJ-019). Every test here therefore ticks a shelf and asks, rather than
/// setting anything up to be true.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class MakeableTests(PostgresFixture fixture) : PostgresTestBase(fixture)
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

    /// <summary>Ticks the named shared ingredients onto this household's shelf.</summary>
    private async Task StockAsync(params string[] names)
    {
        await using var db = Fixture.CreateContext(_household);
        var inventory = new InventoryHandler(
            new EfRepository<TenantInventory>(db),
            new EfRepository<Ingredient>(db),
            new TestCurrentTenant { TenantId = _household },
            new FakeTimeProvider(DateTimeOffset.UtcNow));

        foreach (var name in names)
        {
            var id = await db.Ingredients.IgnoreQueryFilters()
                .Where(i => i.TenantId == null && i.Name == name)
                .Select(i => i.Id)
                .SingleAsync();
            Assert.True(await inventory.SetAsync(id, true, default), $"could not stock {name}");
        }
    }

    private async Task<IReadOnlyList<CocktailSummary>> MakeableAsync()
    {
        await using var db = Fixture.CreateContext(_household);
        var page = await Handler(db).BrowseAsync(
            new CocktailBrowseRequest(null, 1, 100, MakeableOnly: true), default);
        return page.Items;
    }

    [Fact]
    public async Task AnEmptyShelf_MakesNothing()
    {
        await SeedAsync();

        // The honest answer to "what can I make" with nothing on the shelf, and the one a stored
        // flag would get wrong the moment anyone edited a recipe.
        Assert.Empty(await MakeableAsync());
    }

    [Fact]
    public async Task StockingEveryLine_MakesTheDrink()
    {
        await SeedAsync();
        await StockAsync("London dry gin", "Campari", "Sweet vermouth");

        var makeable = await MakeableAsync();

        Assert.Contains(makeable, c => c.Name == "Negroni");
    }

    [Fact]
    public async Task MissingOneLine_DoesNotMakeTheDrink()
    {
        await SeedAsync();
        await StockAsync("London dry gin", "Campari");   // no vermouth

        Assert.DoesNotContain(await MakeableAsync(), c => c.Name == "Negroni");
    }

    [Fact]
    public async Task ASubstituteCounts_WhenTheGraphAllowsIt()
    {
        await SeedAsync();
        // Curaçao stands in for Cointreau, both ways, by the interchangeable group in SEED-4. A
        // household with the wrong orange liqueur should still be offered the drink — this is the
        // difference between a recipe app and one that knows your shelf.
        await StockAsync("London dry gin", "Curaçao", "Lemon juice");

        var makeable = await MakeableAsync();

        Assert.Contains(makeable, c => c.Name == "White Lady");
    }

    [Fact]
    public async Task AOneWaySubstitution_DoesNotRunBackwards()
    {
        await SeedAsync();
        // Cognac stands in for brandy; brandy does not stand in for cognac (SEED-4). A household with
        // only brandy must not be told it can make a drink that asks for cognac, because it cannot —
        // and a symmetric graph would say otherwise.
        var brandyOnly = await CocktailsMadeWithAsync("Brandy");
        var cognacDrinks = await CocktailNamesRequiringAsync("Cognac");

        Assert.DoesNotContain(brandyOnly, name => cognacDrinks.Contains(name));
    }

    [Fact]
    public async Task AnOptionalLine_NeverBlocks()
    {
        await SeedAsync();

        // JJ-009: a garnish is just an optional line. Find a drink whose only unstocked line is
        // optional, stock everything required, and it must be makeable without the garnish.
        Guid cocktailId;
        string[] required;
        await using (var db = Fixture.CreateContext())
        {
            var candidate = await db.Cocktails.IgnoreQueryFilters()
                .Where(c => c.TenantId == null && c.Lines.Any(l => !l.IsRequired) && c.Lines.Count <= 4)
                .Select(c => new
                {
                    c.Id,
                    Required = c.Lines.Where(l => l.IsRequired).Select(l => l.Ingredient!.Name).ToList(),
                    Optional = c.Lines.Count(l => !l.IsRequired),
                })
                .FirstAsync(c => c.Required.Count > 0 && c.Optional > 0);
            cocktailId = candidate.Id;
            required = [.. candidate.Required.Distinct()];
        }

        await StockAsync(required);

        Assert.Contains(await MakeableAsync(), c => c.Id == cocktailId);
    }

    [Fact]
    public async Task Search_NarrowsWhatIsMakeable()
    {
        await SeedAsync();
        await StockAsync("London dry gin", "Campari", "Sweet vermouth");

        await using var db = Fixture.CreateContext(_household);
        var page = await Handler(db).BrowseAsync(
            new CocktailBrowseRequest("negroni", 1, 20, MakeableOnly: true), default);

        Assert.All(page.Items, c => Assert.Contains("negroni", c.Name, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(page.Items, c => c.Name == "Negroni");
    }

    [Fact]
    public async Task AnotherHouseholdsShelf_ChangesNothingHere()
    {
        await SeedAsync();

        var stranger = Guid.CreateVersion7();
        await using (var db = Fixture.CreateContext(stranger))
        {
            var inventory = new InventoryHandler(
                new EfRepository<TenantInventory>(db),
                new EfRepository<Ingredient>(db),
                new TestCurrentTenant { TenantId = stranger },
                new FakeTimeProvider(DateTimeOffset.UtcNow));
            foreach (var name in new[] { "London dry gin", "Campari", "Sweet vermouth" })
            {
                var id = await db.Ingredients.IgnoreQueryFilters()
                    .Where(i => i.TenantId == null && i.Name == name).Select(i => i.Id).SingleAsync();
                await inventory.SetAsync(id, true, default);
            }
        }

        // Their gin does not make my Negroni. The tenant filter does this, but it is the guarantee the
        // whole feature would be worthless without.
        Assert.Empty(await MakeableAsync());
    }

    [Fact]
    public async Task UntickingAnIngredient_TakesTheDrinkBack()
    {
        await SeedAsync();
        await StockAsync("London dry gin", "Campari", "Sweet vermouth");
        Assert.Contains(await MakeableAsync(), c => c.Name == "Negroni");

        await using (var db = Fixture.CreateContext(_household))
        {
            var inventory = new InventoryHandler(
                new EfRepository<TenantInventory>(db),
                new EfRepository<Ingredient>(db),
                new TestCurrentTenant { TenantId = _household },
                new FakeTimeProvider(DateTimeOffset.UtcNow));
            var campari = await db.Ingredients.IgnoreQueryFilters()
                .Where(i => i.TenantId == null && i.Name == "Campari").Select(i => i.Id).SingleAsync();
            await inventory.SetAsync(campari, false, default);
        }

        // Derived, not stored (JJ-003): the answer changes the moment the shelf does, with nothing to
        // recompute and nothing to invalidate.
        Assert.DoesNotContain(await MakeableAsync(), c => c.Name == "Negroni");
    }

    [Fact]
    public async Task ASubstitutedResult_SaysWhatYouWouldActuallyPour()
    {
        await SeedAsync();
        await StockAsync("London dry gin", "Curaçao", "Lemon juice");

        var white = (await MakeableAsync()).Single(c => c.Name == "White Lady");

        // FEATURES §9: "using Kahlúa in place of Tia Maria". Without this the household is told it
        // can make a drink and finds the bottle missing when it reaches the shelf — the list would be
        // right and still misleading.
        var swap = Assert.Single(white.Substitutions);
        Assert.Equal("Cointreau", swap.AsksFor);
        Assert.Equal("Curaçao", swap.YouHave);
    }

    [Fact]
    public async Task AnExactlyStockedDrink_ClaimsNoSubstitutions()
    {
        await SeedAsync();
        await StockAsync("London dry gin", "Campari", "Sweet vermouth");

        var negroni = (await MakeableAsync()).Single(c => c.Name == "Negroni");

        // The other half: a drink the household can pour as written must not imply a swap it is not
        // making. An always-populated list would be noise on every row.
        Assert.Empty(negroni.Substitutions);
    }

    [Fact]
    public async Task BrowsingEverything_ClaimsNoSubstitutions()
    {
        await SeedAsync();
        await StockAsync("London dry gin", "Curaçao", "Lemon juice");

        await using var db = Fixture.CreateContext(_household);
        var page = await Handler(db).BrowseAsync(new CocktailBrowseRequest(null, 1, 100), default);

        // Outside the makeable filter a row makes no claim about being makeable, so a stray
        // "using X instead of Y" would imply one it has not checked.
        Assert.All(page.Items, c => Assert.Empty(c.Substitutions));
        Assert.Equal(CatalogSeeder.LoadCocktails().Cocktails.Count, page.Total);
    }

    private async Task<List<string>> CocktailsMadeWithAsync(string ingredient)
    {
        await SeedAsync();
        await StockAsync(ingredient);
        return [.. (await MakeableAsync()).Select(c => c.Name)];
    }

    private async Task<HashSet<string>> CocktailNamesRequiringAsync(string ingredient)
    {
        await using var db = Fixture.CreateContext();
        return [.. await db.Cocktails.IgnoreQueryFilters()
            .Where(c => c.Lines.Any(l => l.IsRequired && l.Ingredient!.Name == ingredient))
            .Select(c => c.Name)
            .ToListAsync()];
    }
}
