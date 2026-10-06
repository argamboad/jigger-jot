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
/// MARGA-3: what to buy first when the shelf is empty — the question `UnlockingBottleTests`
/// deliberately left open in <c>AnEmptyShelf_RanksNothing</c>.
/// <para>
/// ALMOST-2 cannot answer it, and that is not a gap in ALMOST-2. It ranks the almost-makeable set,
/// and a household with nothing on its shelf is not one bottle away from anything — the set is
/// empty, so there is nothing to rank. This asks a different question of the same catalog:
/// <b>which bottle do these recipes lean on most, among the ones this household does not already
/// have?</b>
/// </para>
/// <para>
/// The assertions are invariants — "never suggests something already owned", "an optional line does
/// not count", "the order is total" — rather than claims about which bottle wins, because which
/// bottle wins is a property of the catalog and the catalog changes.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class StarterBottleTests(PostgresFixture fixture) : PostgresTestBase(fixture)
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
                .Where(i => i.TenantId == null && i.Name == name).Select(i => i.Id).SingleAsync();
            Assert.True(await inventory.SetAsync(id, true, default), $"could not stock {name}");
        }
    }

    private async Task<IReadOnlyList<StarterBottle>> StartersAsync(int limit = 5)
    {
        await using var db = Fixture.CreateContext(_household);
        return await Handler(db).StarterBottlesAsync(limit, default);
    }

    /// <summary>
    /// A cocktail owned by this household, with one required line and one optional one. Written
    /// straight to the tables rather than through the authoring handler: this is about how lines are
    /// counted, and going through the form would put a second thing in the way of reading the test.
    /// <c>TenantId</c> is set by hand on both tables, because nothing stamps these (JJ-031).
    /// </summary>
    private async Task ADrinkOfOurOwnAsync(string requires, string garnishedWith)
    {
        await using var db = Fixture.CreateContext(_household);

        var ids = await db.Ingredients.IgnoreQueryFilters()
            .Where(i => i.TenantId == null && (i.Name == requires || i.Name == garnishedWith))
            .ToDictionaryAsync(i => i.Name, i => i.Id);

        var cocktail = new Cocktail
        {
            TenantId = _household,
            Name = "The House Test",
            ServingType = ServingType.FullDrink,
            Lines =
            [
                new CocktailIngredient
                {
                    TenantId = _household, IngredientId = ids[requires],
                    IsRequired = true, Role = RecipeRole.Base, DisplayOrder = 1,
                },
                new CocktailIngredient
                {
                    TenantId = _household, IngredientId = ids[garnishedWith],
                    IsRequired = false, Role = RecipeRole.Garnish, DisplayOrder = 2,
                },
            ],
        };

        db.Cocktails.Add(cocktail);
        await db.SaveChangesAsync();
    }

    private static int Appearances(IReadOnlyList<StarterBottle> bottles, string ingredient) =>
        bottles.SingleOrDefault(b => b.Ingredient == ingredient)?.Appears ?? 0;

    [Fact]
    public async Task AnEmptyShelf_StillGetsASuggestion()
    {
        await SeedAsync();

        // The whole reason this exists. ALMOST-2 returns nothing here, correctly, and a household
        // staring at an empty catalog needs somewhere to start rather than a verdict.
        var bottles = await StartersAsync();

        Assert.NotEmpty(bottles);
        Assert.All(bottles, b => Assert.True(b.Appears > 0, $"{b.Ingredient} appears in nothing"));
    }

    [Fact]
    public async Task ItNeverSuggestsSomethingAlreadyOnTheShelf()
    {
        await SeedAsync();
        var first = (await StartersAsync()).First();

        await StockAsync(first.Ingredient);

        // Being told to buy what you just ticked is the one way this can be obviously wrong, and the
        // reason the query reads the shelf at all rather than just ranking the catalog.
        var next = await StartersAsync(limit: 20);
        Assert.DoesNotContain(next, b => b.Ingredient == first.Ingredient);
        Assert.NotEmpty(next);
    }

    [Fact]
    public async Task TheOrderIsTotal_SoTheSuggestionDoesNotChangeOnRefresh()
    {
        await SeedAsync();

        var bottles = await StartersAsync(limit: 20);

        // Count descending, then name. Without the second key two bottles appearing in the same
        // number of recipes would swap places between requests with nothing behind it.
        var expected = bottles
            .OrderByDescending(b => b.Appears)
            .ThenBy(b => b.Ingredient, StringComparer.Ordinal)
            .ToList();
        Assert.Equal([.. expected.Select(b => b.Ingredient)], [.. bottles.Select(b => b.Ingredient)]);

        var again = await StartersAsync(limit: 20);
        Assert.Equal([.. bottles.Select(b => b.Ingredient)], [.. again.Select(b => b.Ingredient)]);
    }

    [Fact]
    public async Task AGarnishNeverMakesABottleLookLoadBearing()
    {
        await SeedAsync();

        var before = await StartersAsync(limit: 200);
        var required = before[0].Ingredient;
        var garnish = before[^1].Ingredient;

        await ADrinkOfOurOwnAsync(requires: required, garnishedWith: garnish);

        // Optional lines never block a drink (JJ-009), so an ingredient that only ever garnishes is
        // not a bottle the catalog leans on — counting it would send someone to buy a lemon twist.
        var after = await StartersAsync(limit: 200);
        Assert.Equal(Appearances(before, required) + 1, Appearances(after, required));
        Assert.Equal(Appearances(before, garnish), Appearances(after, garnish));
    }

    [Fact]
    public async Task ItCountsThisHouseholdsOwnRecipes_AndNobodyElses()
    {
        await SeedAsync();
        var before = await StartersAsync(limit: 200);
        var spirit = before[0].Ingredient;
        var garnish = before[^1].Ingredient;

        await ADrinkOfOurOwnAsync(requires: spirit, garnishedWith: garnish);
        Assert.Equal(Appearances(before, spirit) + 1, Appearances(await StartersAsync(limit: 200), spirit));

        // The same query run as a different household sees the shared catalog and none of the above.
        var stranger = Guid.CreateVersion7();
        await using var db = Fixture.CreateContext(stranger);
        var theirs = await Handler(db).StarterBottlesAsync(200, default);
        Assert.Equal(Appearances(before, spirit), Appearances(theirs, spirit));
    }

    [Fact]
    public async Task TheLimitTakesFromTheTop()
    {
        await SeedAsync();

        var capped = await StartersAsync(limit: 3);
        var full = await StartersAsync(limit: 20);

        Assert.True(capped.Count <= 3);
        Assert.Equal([.. full.Take(capped.Count).Select(b => b.Ingredient)],
                     [.. capped.Select(b => b.Ingredient)]);
    }
}
