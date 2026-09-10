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
/// ALMOST-1 (FEATURES §10, JJ-019): the drinks a household is <b>exactly one</b> required line short
/// of, each one naming the bottle that would unlock it.
/// <para>
/// This is the shopping driver, so the thing under test is not really the filter — it is the name.
/// "One away" with no ingredient attached is a list of things you cannot make, which the app already
/// had. Every test here therefore asks what is missing, not just what is listed.
/// </para>
/// <para>
/// Like makeability, it is derived at query time and never stored (JJ-003, JJ-019): each test ticks a
/// shelf and asks.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AlmostMakeableTests(PostgresFixture fixture) : PostgresTestBase(fixture)
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
            new EfRepository<IngredientCategory>(db),
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

    private async Task<IReadOnlyList<CocktailSummary>> AlmostAsync(string? search = null)
    {
        await using var db = Fixture.CreateContext(_household);
        var page = await Handler(db).BrowseAsync(
            new CocktailBrowseRequest(search, 1, 100, AlmostMakeableOnly: true), default);
        return page.Items;
    }

    private async Task<IReadOnlyList<CocktailSummary>> MakeableAsync()
    {
        await using var db = Fixture.CreateContext(_household);
        var page = await Handler(db).BrowseAsync(
            new CocktailBrowseRequest(null, 1, 100, MakeableOnly: true), default);
        return page.Items;
    }

    [Fact]
    public async Task OneLineShort_IsListed_AndNamesTheBottle()
    {
        await SeedAsync();
        await StockAsync("London dry gin", "Campari");   // a Negroni, minus the vermouth

        var negroni = Assert.Single(await AlmostAsync(), c => c.Name == "Negroni");

        // FEATURES §10: "each result names the single missing ingredient". This assertion is the
        // feature — buy this one bottle and the drink appears in the other list.
        Assert.Equal("Sweet vermouth", negroni.MissingIngredient);
    }

    [Fact]
    public async Task TwoLinesShort_IsNotListed()
    {
        await SeedAsync();
        await StockAsync("London dry gin");   // no Campari, no vermouth

        // MVP fixes N = 1 (DATA_MODEL, JJ-019). Two away is a wish list, not a shopping list, and
        // relaxing this would put most of the catalog on the screen.
        Assert.DoesNotContain(await AlmostAsync(), c => c.Name == "Negroni");
    }

    [Fact]
    public async Task NothingShort_IsNotListed_BecauseItIsAlreadyMakeable()
    {
        await SeedAsync();
        await StockAsync("London dry gin", "Campari", "Sweet vermouth");

        // The two lists are adjacent, never overlapping (FEATURES §10). A drink you can pour tonight
        // appearing under "one ingredient away" would send someone to a shop for nothing.
        Assert.DoesNotContain(await AlmostAsync(), c => c.Name == "Negroni");
        Assert.Contains(await MakeableAsync(), c => c.Name == "Negroni");
    }

    [Fact]
    public async Task ALineASubstituteCovers_IsNotWhatYouAreMissing()
    {
        await SeedAsync();
        // A White Lady wants gin, Cointreau and lemon juice. This shelf has Curaçao, which the graph
        // allows in place of Cointreau — so the household is short the lemon juice and nothing else.
        await StockAsync("London dry gin", "Curaçao");

        var white = Assert.Single(await AlmostAsync(), c => c.Name == "White Lady");

        // "After substitutions" (FEATURES §10) is the whole difficulty of this slice. Counting
        // unstocked lines instead would call this drink two short and never list it, and naming the
        // first unstocked line would send someone out to buy Cointreau they do not need.
        Assert.Equal("Lemon juice", white.MissingIngredient);
    }

    [Fact]
    public async Task AnAlmostMakeableDrink_StillSaysWhatYouWouldPour()
    {
        await SeedAsync();
        await StockAsync("London dry gin", "Curaçao");

        var white = Assert.Single(await AlmostAsync(), c => c.Name == "White Lady");

        // FEATURES §9 does not stop applying because the drink is one short. Someone buying lemon
        // juice on the strength of this row needs to know the rest of it pours as Curaçao, or the
        // trip to the shop was still not enough.
        var swap = Assert.Single(white.Substitutions);
        Assert.Equal("Cointreau", swap.AsksFor);
        Assert.Equal("Curaçao", swap.YouHave);
    }

    [Fact]
    public async Task AnOptionalLine_IsNeverWhatYouAreMissing()
    {
        await SeedAsync();

        // JJ-009: optional lines never block makeability, so an unstocked garnish cannot be the one
        // thing you are short of. Find a drink with an optional line, stock every required line, and
        // it belongs in "makeable" — not on a shopping list for its garnish.
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

        Assert.DoesNotContain(await AlmostAsync(), c => c.Id == cocktailId);
        Assert.Contains(await MakeableAsync(), c => c.Id == cocktailId);
    }

    [Fact]
    public async Task Search_NarrowsWhatIsOneAway()
    {
        await SeedAsync();
        await StockAsync("London dry gin", "Campari");

        var items = await AlmostAsync("negroni");

        // The filters combine on one list (FEATURES §11) rather than each owning a screen.
        Assert.All(items, c => Assert.Contains("negroni", c.Name, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(items, c => c.Name == "Negroni");
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
                new EfRepository<IngredientCategory>(db),
                new TestCurrentTenant { TenantId = stranger },
                new FakeTimeProvider(DateTimeOffset.UtcNow));
            foreach (var name in new[] { "London dry gin", "Campari" })
            {
                var id = await db.Ingredients.IgnoreQueryFilters()
                    .Where(i => i.TenantId == null && i.Name == name).Select(i => i.Id).SingleAsync();
                await inventory.SetAsync(id, true, default);
            }
        }

        // Their two bottles do not put my Negroni one away. The tenant filter does this, but a
        // shopping list built from someone else's shelf is the failure worth naming.
        Assert.DoesNotContain(await AlmostAsync(), c => c.Name == "Negroni");
    }

    [Fact]
    public async Task BrowsingEverything_NamesNothingMissing()
    {
        await SeedAsync();
        await StockAsync("London dry gin", "Campari");

        await using var db = Fixture.CreateContext(_household);
        var page = await Handler(db).BrowseAsync(new CocktailBrowseRequest(null, 1, 100), default);

        // Outside the filter a row makes no claim about how close it is, so a "you need X" on every
        // line of an unfiltered catalog would be both noise and, for the makeable ones, wrong.
        Assert.All(page.Items, c => Assert.Null(c.MissingIngredient));
        Assert.Equal(CatalogSeeder.LoadCocktails().Cocktails.Count, page.Total);
    }

    [Fact]
    public async Task AskingForBothFilters_AsksForNothing()
    {
        await SeedAsync();
        await StockAsync("London dry gin", "Campari", "Sweet vermouth");

        await using var db = Fixture.CreateContext(_household);
        var page = await Handler(db).BrowseAsync(
            new CocktailBrowseRequest(null, 1, 100, MakeableOnly: true, AlmostMakeableOnly: true),
            default);

        // Nothing is both zero short and one short. The handler applies both predicates and lets the
        // contradiction produce an empty page rather than inventing a precedence rule that the UI
        // would then have to agree with; the UI keeps the two toggles exclusive instead.
        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);
    }
}
