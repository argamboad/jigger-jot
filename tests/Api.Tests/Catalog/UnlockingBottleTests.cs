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
/// ALMOST-2 (JJ-035): the bottle that unlocks the most drinks — the almost-makeable set read the
/// other way round.
/// <para>
/// ALMOST-1 answers per drink: <i>this cocktail is missing that bottle</i>. Ask it eighty-one times
/// and you get a list nobody reads. This groups the same set by the missing ingredient and ranks it,
/// so one sentence can say "buy this and four open up".
/// </para>
/// <para>
/// Almost every assertion here is an <b>invariant</b> rather than a claim about the catalog: the
/// count matches the names, the order is by count then name, the top entry really is the maximum.
/// Those hold whatever the seed contains, which is the lesson six tests learned the hard way when
/// the catalog shrank.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class UnlockingBottleTests(PostgresFixture fixture) : PostgresTestBase(fixture)
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

    private async Task StockAsync(params string[] names) => await StockForAsync(_household, names);

    private async Task StockForAsync(Guid household, params string[] names)
    {
        await using var db = Fixture.CreateContext(household);
        var inventory = new InventoryHandler(
            new EfRepository<TenantInventory>(db),
            new EfRepository<Ingredient>(db),
            new EfRepository<IngredientCategory>(db),
            new TestCurrentTenant { TenantId = household },
            new FakeTimeProvider(DateTimeOffset.UtcNow));

        foreach (var name in names)
        {
            var id = await db.Ingredients.IgnoreQueryFilters()
                .Where(i => i.TenantId == null && i.Name == name).Select(i => i.Id).SingleAsync();
            Assert.True(await inventory.SetAsync(id, true, default), $"could not stock {name}");
        }
    }

    private async Task<IReadOnlyList<UnlockingBottle>> UnlocksAsync(int limit = 5)
    {
        await using var db = Fixture.CreateContext(_household);
        return await Handler(db).UnlockingBottlesAsync(limit, default);
    }

    /// <summary>The almost-makeable rows, which this endpoint is a regrouping of.</summary>
    private async Task<IReadOnlyList<CocktailSummary>> AlmostAsync()
    {
        await using var db = Fixture.CreateContext(_household);
        var page = await Handler(db).BrowseAsync(
            new CocktailBrowseRequest(null, 1, 100, AlmostMakeableOnly: true), default);
        return page.Items;
    }

    /// <summary>A deliberately partial shelf: several drinks within one bottle, in different ways.</summary>
    private Task APartialShelfAsync() =>
        StockAsync("London dry gin", "Campari", "Curaçao", "Lemon juice", "Dry vermouth", "White rum");

    [Fact]
    public async Task ItNamesTheBottleAndTheDrinksItOpens()
    {
        await SeedAsync();
        await StockAsync("London dry gin", "Campari");   // a Negroni, minus the vermouth

        var vermouth = Assert.Single(await UnlocksAsync(), b => b.Ingredient == "Sweet vermouth");

        Assert.Contains("Negroni", vermouth.Cocktails);
    }

    [Fact]
    public async Task TheCountAlwaysEqualsTheNamesItGives()
    {
        await SeedAsync();
        await APartialShelfAsync();

        var bottles = await UnlocksAsync(limit: 20);

        // The card says a number and names drinks. If those two disagree the reader catches it
        // instantly, so they are one value read twice rather than two values computed twice.
        Assert.NotEmpty(bottles);
        Assert.All(bottles, b => Assert.Equal(b.Unlocks, b.Cocktails.Count));
    }

    [Fact]
    public async Task TheTopBottleReallyIsTheMostUnlocking()
    {
        await SeedAsync();
        await APartialShelfAsync();

        var bottles = await UnlocksAsync(limit: 20);

        Assert.NotEmpty(bottles);
        Assert.All(bottles, b => Assert.True(b.Unlocks <= bottles[0].Unlocks));
    }

    [Fact]
    public async Task TiesOrderByName_SoTheCardDoesNotChangeOnRefresh()
    {
        await SeedAsync();
        await APartialShelfAsync();

        var bottles = await UnlocksAsync(limit: 20);

        // Count descending, then name. Without the second key two bottles that each unlock three
        // drinks would swap places between requests with nothing behind it.
        var expected = bottles
            .OrderByDescending(b => b.Unlocks)
            .ThenBy(b => b.Ingredient, StringComparer.Ordinal)
            .ToList();
        Assert.Equal([.. expected.Select(b => b.Ingredient)], [.. bottles.Select(b => b.Ingredient)]);

        // And it is genuinely stable, not merely sorted once.
        var again = await UnlocksAsync(limit: 20);
        Assert.Equal([.. bottles.Select(b => b.Ingredient)], [.. again.Select(b => b.Ingredient)]);
    }

    [Fact]
    public async Task EveryDrinkItNames_IsOneBottleAwayFromExactlyThatBottle()
    {
        await SeedAsync();
        await APartialShelfAsync();

        var bottles = await UnlocksAsync(limit: 20);
        var almost = await AlmostAsync();

        // This is the regrouping test: the two endpoints read the same set, so every claim here has
        // to be findable in the per-drink list, attributed to the same bottle.
        foreach (var bottle in bottles)
            foreach (var name in bottle.Cocktails)
            {
                var row = Assert.Single(almost, c => c.Name == name);
                Assert.Equal(bottle.Ingredient, row.MissingIngredient);
            }

        // ...and nothing in that list is left out of the ranking.
        Assert.Equal(almost.Count, bottles.Sum(b => b.Unlocks));
    }

    [Fact]
    public async Task ASubstitutedLine_NeverCountsTowardABottle()
    {
        await SeedAsync();
        // A White Lady wants gin, Cointreau and lemon juice. Curaçao covers the Cointreau, so this
        // household is short the lemon juice — and no bottle should be credited with the Cointreau.
        await StockAsync("London dry gin", "Curaçao");

        var bottles = await UnlocksAsync(limit: 20);

        Assert.DoesNotContain(bottles, b => b.Ingredient == "Cointreau");
        Assert.Contains(bottles, b => b.Ingredient == "Lemon juice" && b.Cocktails.Contains("White Lady"));
    }

    [Fact]
    public async Task AnEmptyShelf_RanksNothing()
    {
        await SeedAsync();

        // Nothing is one bottle away, so there is nothing to rank. An empty list rather than a guess
        // — suggesting a first purchase to a cold-start household is a different question, and it is
        // logged against MARGA-3 rather than answered here.
        Assert.Empty(await UnlocksAsync());
    }

    [Fact]
    public async Task TheLimitCapsTheBottles_NotTheDrinksTheyName()
    {
        await SeedAsync();
        await APartialShelfAsync();

        var capped = await UnlocksAsync(limit: 2);
        var full = await UnlocksAsync(limit: 20);

        Assert.True(capped.Count <= 2);
        Assert.Equal([.. full.Take(capped.Count).Select(b => b.Ingredient)],
                     [.. capped.Select(b => b.Ingredient)]);

        // Truncating the names would break the count, which the card shows side by side with them.
        Assert.All(capped, b => Assert.Equal(b.Unlocks, b.Cocktails.Count));
    }

    [Fact]
    public async Task AnotherHouseholdsShelf_RanksNothingHere()
    {
        await SeedAsync();
        await StockForAsync(Guid.CreateVersion7(), "London dry gin", "Campari");

        // Their two bottles do not make my shopping list. The tenant filter does this, but a
        // suggestion built from someone else's shelf is the failure worth naming.
        Assert.Empty(await UnlocksAsync());
    }

    [Fact]
    public async Task ItReadsTheHouseholdsOwnCocktailsToo()
    {
        await SeedAsync();
        await StockAsync("London dry gin");

        // A cocktail this household wrote is as real to the ranking as a seeded one — both come
        // through the same shared-or-tenant filter (JJ-031).
        Guid gin, campari;
        await using (var db = Fixture.CreateContext())
        {
            gin = await db.Ingredients.IgnoreQueryFilters()
                .Where(i => i.TenantId == null && i.Name == "London dry gin").Select(i => i.Id).SingleAsync();
            campari = await db.Ingredients.IgnoreQueryFilters()
                .Where(i => i.TenantId == null && i.Name == "Campari").Select(i => i.Id).SingleAsync();
        }

        await using (var db = Fixture.CreateContext(_household))
        {
            var authoring = new CocktailAuthoringHandler(
                new EfRepository<Cocktail>(db),
                new EfRepository<Ingredient>(db),
                new EfRepository<Unit>(db),
                new EfRepository<GlassType>(db),
                new EfRepository<Method>(db),
                new TestCurrentTenant { TenantId = _household });

            var result = await authoring.CreateAsync(new AuthorCocktailRequest(
                "House Bitter", null, null, ServingType.FullDrink, null,
                [
                    new AuthorLineRequest(gin, 30m, null, true, RecipeRole.Base, null),
                    new AuthorLineRequest(campari, 30m, null, true, RecipeRole.Modifier, null),
                ]), default);
            Assert.Equal(AuthorCocktailOutcome.Created, result.Outcome);
        }

        var campariBottle = Assert.Single(await UnlocksAsync(limit: 20), b => b.Ingredient == "Campari");
        Assert.Contains("House Bitter", campariBottle.Cocktails);
    }
}
