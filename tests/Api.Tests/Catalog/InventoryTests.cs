using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using JiggerJot.Api.Features.Inventory;
using JiggerJot.Api.Tests.Infrastructure;
using JiggerJot.Core.Entities;
using JiggerJot.Infrastructure.Persistence;
using JiggerJot.Infrastructure.Persistence.Seed;
using JiggerJot.Infrastructure.Repositories;

namespace JiggerJot.Api.Tests.Catalog;

/// <summary>
/// INV: the shelf. The checklist the whole product turns on — until a household has told the app what
/// it owns, "what can I make" has no answer.
/// <para>
/// <c>TenantInventory</c> is the one JiggerJot entity that is plainly <c>ITenantScoped</c>, so the
/// platform's filter, write stamping and generated RLS policy all cover it with nothing hand-written.
/// After JJ-031 these tests are deliberately dull, and that is the news.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class InventoryTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private readonly Guid _household = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    private static InventoryHandler Handler(AppDbContext db, Guid tenantId, TimeProvider? clock = null) =>
        new(new EfRepository<TenantInventory>(db),
            new EfRepository<Ingredient>(db),
            new EfRepository<IngredientCategory>(db),
            new TestCurrentTenant { TenantId = tenantId },
            clock ?? new FakeTimeProvider(Now));

    private async Task SeedAsync()
    {
        await using var db = Fixture.CreateContext();
        await new CatalogSeeder(db, NullLogger<CatalogSeeder>.Instance).SeedAsync();
    }

    private async Task<Guid> IngredientAsync(string name)
    {
        await using var db = Fixture.CreateContext();
        return (await db.Ingredients.IgnoreQueryFilters().FirstAsync(i => i.Name == name)).Id;
    }

    [Fact]
    public async Task Shelf_StartsWithEverythingUnavailable()
    {
        await SeedAsync();

        await using var db = Fixture.CreateContext(_household);
        var shelf = await Handler(db, _household).ListAsync(default);

        // The whole catalog is listed, because you cannot tick what you cannot see — but nothing is
        // ticked, because absence of a row means "not available" (JJ-023).
        Assert.True(shelf.Count > 150);
        Assert.All(shelf, i => Assert.False(i.IsAvailable));
    }

    [Fact]
    public async Task Shelf_IsGroupedByCategory_AndOrderedWithinIt()
    {
        await SeedAsync();

        await using var db = Fixture.CreateContext(_household);
        var shelf = await Handler(db, _household).ListAsync(default);

        // 191 ingredients is far too many to tick down a flat list, so the response carries the
        // category and the UI groups on it. Grouping in the client from an unordered list would put
        // the same work in every future front end.
        Assert.All(shelf, i => Assert.False(string.IsNullOrWhiteSpace(i.Category)));
        var gin = shelf.Where(i => i.Category == "Gin").Select(i => i.Name).ToList();
        Assert.Contains("London dry gin", gin);
        Assert.Equal(gin.OrderBy(n => n, StringComparer.OrdinalIgnoreCase), gin);
    }

    [Fact]
    public async Task Ticking_AnIngredient_MarksItAvailable()
    {
        await SeedAsync();
        var gin = await IngredientAsync("London dry gin");

        await using (var db = Fixture.CreateContext(_household))
            Assert.True(await Handler(db, _household).SetAsync(gin, true, default));

        await using var read = Fixture.CreateContext(_household);
        var shelf = await Handler(read, _household).ListAsync(default);
        Assert.True(shelf.Single(i => i.Id == gin).IsAvailable);
    }

    [Fact]
    public async Task Unticking_LeavesTheRow_RatherThanDeletingIt()
    {
        await SeedAsync();
        var gin = await IngredientAsync("London dry gin");

        await using (var db = Fixture.CreateContext(_household))
        {
            await Handler(db, _household).SetAsync(gin, true, default);
            await Handler(db, _household).SetAsync(gin, false, default);
        }

        // "I checked and I do not have it" is worth keeping apart from "I never looked" — a shopping
        // list would want the difference, and the row costs nothing. Everything that reads the shelf
        // filters on IsAvailable, so absence and false behave identically to it either way.
        await using var read = Fixture.CreateContext(_household);
        var row = await read.TenantInventories.SingleAsync(i => i.IngredientId == gin);
        Assert.False(row.IsAvailable);
    }

    [Fact]
    public async Task Ticking_Twice_UpdatesTheSameRow()
    {
        await SeedAsync();
        var gin = await IngredientAsync("London dry gin");

        await using (var db = Fixture.CreateContext(_household))
        {
            await Handler(db, _household).SetAsync(gin, true, default);
            await Handler(db, _household).SetAsync(gin, true, default);
        }

        // The unique index on (TenantId, IngredientId) would reject a second row, so this failing
        // would be an exception rather than a duplicate — but it is the kind of thing a double-click
        // finds in production and a test should find first.
        await using var read = Fixture.CreateContext(_household);
        Assert.Single(await read.TenantInventories.Where(i => i.IngredientId == gin).ToListAsync());
    }

    [Fact]
    public async Task Ticking_StampsTheTimeFromTheInjectedClock()
    {
        await SeedAsync();
        var gin = await IngredientAsync("London dry gin");

        await using (var db = Fixture.CreateContext(_household))
            await Handler(db, _household).SetAsync(gin, true, default);

        await using var read = Fixture.CreateContext(_household);
        Assert.Equal(Now, (await read.TenantInventories.SingleAsync()).UpdatedAt);
    }

    [Fact]
    public async Task Shelf_IsPrivateToItsHousehold()
    {
        await SeedAsync();
        var gin = await IngredientAsync("London dry gin");
        var stranger = Guid.CreateVersion7();

        await using (var db = Fixture.CreateContext(_household))
            await Handler(db, _household).SetAsync(gin, true, default);

        // Ordinary ITenantScoped data, so this is the platform's global filter doing the work and not
        // anything this slice wrote. Worth asserting anyway: it is the guarantee the feature rests on.
        await using var theirs = Fixture.CreateContext(stranger);
        var shelf = await Handler(theirs, stranger).ListAsync(default);
        Assert.All(shelf, i => Assert.False(i.IsAvailable));
    }

    [Fact]
    public async Task Ticking_SomethingThatIsNotAnIngredient_Fails()
    {
        await SeedAsync();

        await using var db = Fixture.CreateContext(_household);
        Assert.False(await Handler(db, _household).SetAsync(Guid.CreateVersion7(), true, default));
    }

    [Fact]
    public async Task Ticking_AnotherHouseholdsCustomIngredient_Fails()
    {
        await SeedAsync();

        var stranger = Guid.CreateVersion7();
        Guid theirIngredient;
        await using (var db = Fixture.CreateContext())
        {
            var category = await db.IngredientCategories.FirstAsync(c => c.ParentId == null);
            var ingredient = new Ingredient
            {
                Name = "Their secret cordial", TenantId = stranger, CategoryId = category.Id,
            };
            db.Ingredients.Add(ingredient);
            await db.SaveChangesAsync();
            theirIngredient = ingredient.Id;
        }

        // The filter does not return it, so it cannot be ticked — which also means a household cannot
        // discover another's custom ingredients by probing ids.
        await using var db2 = Fixture.CreateContext(_household);
        Assert.False(await Handler(db2, _household).SetAsync(theirIngredient, true, default));
    }

    [Fact]
    public async Task Shelf_CountsWhatIsTicked_AfterAReRead()
    {
        // Mirrors what the shelf screen shows in its counter. The E2E journey asserts the same thing
        // through the browser; this says whether the server half is right, so a browser failure can
        // be read as a UI problem rather than a mystery.
        await SeedAsync();
        var gin = await IngredientAsync("London dry gin");

        await using (var db = Fixture.CreateContext(_household))
            Assert.True(await Handler(db, _household).SetAsync(gin, true, default));

        await using var read = Fixture.CreateContext(_household);
        var shelf = await Handler(read, _household).ListAsync(default);
        Assert.Equal(1, shelf.Count(i => i.IsAvailable));
    }

    [Fact]
    public async Task Shelf_NeverOffersIceOrWater()
    {
        await SeedAsync();

        await using var db = Fixture.CreateContext(_household);
        var shelf = await Handler(db, _household).ListAsync(default);

        // JJ-020: both are always available and are never modelled as blocking inventory. They were
        // kept out of the catalog at seed time, so this is really a guard against putting them back —
        // a shelf that asks whether you have water is a shelf nobody trusts.
        Assert.DoesNotContain(shelf, i => i.Name.Equals("water", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(shelf, i => i.Name.Equals("ice", StringComparison.OrdinalIgnoreCase));
    }
}
