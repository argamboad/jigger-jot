using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using JiggerJot.Api.Features.Catalog;
using JiggerJot.Api.Tests.Infrastructure;
using JiggerJot.Core.Entities;
using JiggerJot.Infrastructure.Persistence;
using JiggerJot.Infrastructure.Persistence.Seed;
using JiggerJot.Infrastructure.Repositories;

namespace JiggerJot.Api.Tests.Catalog;

/// <summary>
/// CKTL-2: browsing the catalog. Run against the REAL seeded catalog rather than a handful of
/// fixtures, because the things most likely to break here are things only volume shows: ordering
/// that is stable until two drinks share a name, paging that drifts, and a search that matches too
/// much. Seeding is a couple of seconds and buys all three.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CocktailBrowseTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private readonly Guid _household = Guid.CreateVersion7();

    /// <summary>
    /// How many cocktails the seed file actually holds. Read from the file rather than written down,
    /// because a test that hard-codes the catalog's size is a test about the catalog — it breaks when
    /// the seed changes for reasons that have nothing to do with browsing, which is exactly what
    /// happened when the shipped catalog went from 969 recipes to a starter set.
    /// </summary>
    private static int SeededCount => CatalogSeeder.LoadCocktails().Cocktails.Count;

    private static CocktailBrowseHandler Handler(AppDbContext db) =>
        new(new EfRepository<Cocktail>(db),
            new EfRepository<TenantInventory>(db),
            new EfRepository<IngredientSubstitution>(db));

    private async Task SeedAsync()
    {
        await using var db = Fixture.CreateContext();
        await new CatalogSeeder(db, NullLogger<CatalogSeeder>.Instance).SeedAsync();
    }

    [Fact]
    public async Task Browse_ReturnsTheFirstPage_OrderedByName()
    {
        await SeedAsync();

        await using var db = Fixture.CreateContext(_household);
        var page = await Handler(db).BrowseAsync(new CocktailBrowseRequest(null, 1, 20), default);

        Assert.Equal(Math.Min(20, SeededCount), page.Items.Count);
        Assert.Equal(1, page.Page);
        Assert.Equal(SeededCount, page.Total);

        // Ordering is Postgres's linguistic collation, and it is NOT .NET's ordinal one. They differ
        // on real catalog data: the database files "Absinthe (Special) Cocktail" among the other
        // Absinthes because the collation looks past the punctuation, while an ordinal comparer sorts
        // "(" ahead of every letter and puts it first. The database is right — that is what a person
        // expects from an alphabetical list — so this asserts the order is STABLE and starts where it
        // should, rather than re-deriving it with a comparer that disagrees with the query.
        var again = await Handler(db).BrowseAsync(new CocktailBrowseRequest(null, 1, 20), default);
        Assert.Equal(page.Items.Select(i => i.Name), again.Items.Select(i => i.Name));
        Assert.StartsWith("A", page.Items[0].Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Browse_PagesWithoutRepeatingOrSkipping()
    {
        await SeedAsync();

        await using var db = Fixture.CreateContext(_household);
        var handler = Handler(db);

        // Half the catalog, rounded UP, so two pages cover it exactly however many there are. Rounding
        // down leaves an orphan on page three and the assertion below counts one short.
        var pageSize = Math.Max(1, (SeededCount + 1) / 2);
        var first = await handler.BrowseAsync(new CocktailBrowseRequest(null, 1, pageSize), default);
        var second = await handler.BrowseAsync(new CocktailBrowseRequest(null, 2, pageSize), default);

        // The catalog holds a name that appears in both books and one that appears twice within a
        // single book, so an ordering keyed on name alone is not a total order and pages would
        // quietly overlap. This is the assertion that catches it.
        var ids = first.Items.Concat(second.Items).Select(i => i.Id).ToList();
        Assert.Equal(SeededCount, ids.Count);
        Assert.Equal(SeededCount, ids.Distinct().Count());
    }

    [Fact]
    public async Task Browse_PastTheEnd_IsEmptyRatherThanAnError()
    {
        await SeedAsync();

        await using var db = Fixture.CreateContext(_household);
        var page = await Handler(db).BrowseAsync(new CocktailBrowseRequest(null, 9999, 20), default);

        Assert.Empty(page.Items);
        Assert.Equal(SeededCount, page.Total);   // the count still tells the caller where the end was
    }

    [Fact]
    public async Task Search_MatchesAnywhereInTheName_CaseInsensitively()
    {
        await SeedAsync();

        await using var db = Fixture.CreateContext(_household);
        var page = await Handler(db).BrowseAsync(new CocktailBrowseRequest("MARTINI", 1, 100), default);

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, i => Assert.Contains("martini", i.Name, StringComparison.OrdinalIgnoreCase));
        // "Dry Martini" has to be reachable by typing "martini", or the search is a prefix search
        // wearing a search's name.
        Assert.Contains(page.Items, i => i.Name.Equals("Dry Martini", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Search_ForNothingInParticular_ReturnsNothing()
    {
        await SeedAsync();

        await using var db = Fixture.CreateContext(_household);
        var page = await Handler(db).BrowseAsync(new CocktailBrowseRequest("zzzz no such drink", 1, 20), default);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);
    }

    [Fact]
    public async Task Browse_ShowsSharedAndOwn_AndNeverAnotherHouseholds()
    {
        await SeedAsync();
        await SeedHouseholdCocktailAsync(_household, "Zzz My Own Drink");
        var stranger = Guid.CreateVersion7();
        await SeedHouseholdCocktailAsync(stranger, "Zzz Their Own Drink");

        await using var db = Fixture.CreateContext(_household);
        var page = await Handler(db).BrowseAsync(new CocktailBrowseRequest("Zzz", 1, 20), default);

        // The whole JJ-031 shape, seen from the feature that will actually use it.
        Assert.Single(page.Items);
        Assert.Equal("Zzz My Own Drink", page.Items[0].Name);
        Assert.True(page.Items[0].IsOwn);
    }

    [Fact]
    public async Task Browse_TellsTheTwoGinFizzesApart_ByTheirSource()
    {
        await SeedAsync();

        await using var db = Fixture.CreateContext(_household);
        var page = await Handler(db).BrowseAsync(new CocktailBrowseRequest("Gin Fizz", 1, 20), default);

        // Two drinks, one name, different books. Without the source in the summary the browse list
        // shows a duplicate row and no way to tell which is which.
        var fizzes = page.Items.Where(i => i.Name.Equals("Gin Fizz", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Equal(2, fizzes.Count);
        Assert.Equal(2, fizzes.Select(f => f.Source).Distinct().Count());
        Assert.All(fizzes, f => Assert.False(string.IsNullOrWhiteSpace(f.Source)));
    }

    [Fact]
    public async Task Browse_CarriesGlassMethodAndIngredientCount_AndToleratesTheMissingOnes()
    {
        await SeedAsync();

        await using var db = Fixture.CreateContext(_household);
        var page = await Handler(db).BrowseAsync(new CocktailBrowseRequest(null, 1, 200), default);

        Assert.All(page.Items, i => Assert.True(i.IngredientCount > 0));
        Assert.Contains(page.Items, i => i.Glass is not null);
        // JJ-034 reaching the surface: a recipe that never said which glass shows no glass, and the
        // browse list has to render that rather than fall over.
        Assert.Contains(page.Items, i => i.Glass is null || i.Method is null);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    public async Task Browse_ClampsAnUnreasonablePage(int requested, int expected)
    {
        await SeedAsync();

        await using var db = Fixture.CreateContext(_household);
        var page = await Handler(db).BrowseAsync(new CocktailBrowseRequest(null, requested, 20), default);

        Assert.Equal(expected, page.Page);
        Assert.NotEmpty(page.Items);
    }

    [Theory]
    [InlineData(0, CocktailBrowseRequest.DefaultPageSize)]
    [InlineData(5000, CocktailBrowseRequest.MaxPageSize)]
    public async Task Browse_ClampsAnUnreasonablePageSize(int requested, int expected)
    {
        await SeedAsync();

        await using var db = Fixture.CreateContext(_household);
        var page = await Handler(db).BrowseAsync(new CocktailBrowseRequest(null, 1, requested), default);

        // A caller asking for 5000 rows gets a page, not a database dump — the cap is the endpoint's
        // only protection against one request walking the whole catalog.
        Assert.Equal(expected, page.PageSize);
        Assert.Equal(Math.Min(expected, SeededCount), page.Items.Count);
    }

    private async Task SeedHouseholdCocktailAsync(Guid tenantId, string name)
    {
        await using var db = Fixture.CreateContext();
        var lookups = new CatalogLookups(
            (await db.IngredientCategories.FirstAsync(c => c.ParentId == null)).Id,
            (await db.GlassTypes.FirstAsync()).Id,
            (await db.Methods.FirstAsync()).Id,
            (await db.Units.FirstAsync()).Id);

        var ingredient = await db.Ingredients.FirstAsync(i => i.TenantId == null);
        await CatalogSeed.CocktailAsync(db, lookups, name, tenantId, ingredient.Id);
    }
}
