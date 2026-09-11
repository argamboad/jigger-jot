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
/// ONBOARD-1's write: a whole shelf in one request.
/// <para>
/// The shelf screen ticks one ingredient at a time and that is right for it — a tick there is a
/// decision someone just made, and it should be saved before they look away. The wizard is the
/// opposite: it puts a dozen suggestions on screen ALREADY TICKED and asks which are wrong. Saving
/// those one at a time would write twelve rows for a set the household has not confirmed yet, and
/// would leave a half-written shelf behind if the page closed midway.
/// </para>
/// <para>
/// So this is one request, one transaction, all or nothing — and it has to be safe to send twice,
/// because a wizard finishing on a flaky connection is exactly the case that produces a retry.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class BulkInventoryTests(PostgresFixture fixture) : PostgresTestBase(fixture)
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

    private async Task<Guid> IdOfAsync(string name)
    {
        await using var db = Fixture.CreateContext();
        return await db.Ingredients.IgnoreQueryFilters()
            .Where(i => i.TenantId == null && i.Name == name).Select(i => i.Id).SingleAsync();
    }

    private async Task<BulkSetResult> SetAsync(Guid household, params (Guid Id, bool Available)[] items)
    {
        await using var db = Fixture.CreateContext(household);
        return await Handler(db, household).SetManyAsync(
            new BulkSetAvailabilityRequest([.. items.Select(i => new ShelfChange(i.Id, i.Available))]),
            default);
    }

    private async Task<IReadOnlyList<string>> ShelfAsync()
    {
        await using var db = Fixture.CreateContext(_household);
        var items = await Handler(db, _household).ListAsync(default);
        return [.. items.Where(i => i.IsAvailable).Select(i => i.Name).OrderBy(n => n, StringComparer.Ordinal)];
    }

    [Fact]
    public async Task ItWritesAWholeShelfInOneGo()
    {
        await SeedAsync();
        var gin = await IdOfAsync("London dry gin");
        var campari = await IdOfAsync("Campari");
        var vermouth = await IdOfAsync("Sweet vermouth");

        var result = await SetAsync(_household, (gin, true), (campari, true), (vermouth, true));

        Assert.Equal(3, result.Applied);
        Assert.Empty(result.Unknown);
        Assert.Equal(["Campari", "London dry gin", "Sweet vermouth"], await ShelfAsync());
    }

    [Fact]
    public async Task ItSetsTheStateAsked_RatherThanFlipping()
    {
        await SeedAsync();
        var gin = await IdOfAsync("London dry gin");
        var campari = await IdOfAsync("Campari");
        await SetAsync(_household, (gin, true), (campari, true));

        // The wizard shows suggestions already ticked and asks which are WRONG, so the request says
        // what each one should be. A toggle would mean the answer depended on what the shelf happened
        // to hold when the request landed.
        var result = await SetAsync(_household, (gin, true), (campari, false));

        Assert.Equal(2, result.Applied);
        Assert.Equal(["London dry gin"], await ShelfAsync());
    }

    [Fact]
    public async Task SendingItTwiceLeavesTheSameShelf()
    {
        await SeedAsync();
        var gin = await IdOfAsync("London dry gin");
        var campari = await IdOfAsync("Campari");

        await SetAsync(_household, (gin, true), (campari, true));
        await SetAsync(_household, (gin, true), (campari, true));

        // A wizard finishing on a flaky connection is exactly the case that produces a retry, and a
        // second identical request must not leave a second row per ingredient.
        Assert.Equal(["Campari", "London dry gin"], await ShelfAsync());

        await using var db = Fixture.CreateContext(_household);
        Assert.Equal(2, await db.TenantInventories.CountAsync());
    }

    [Fact]
    public async Task AnIngredientThisHouseholdCannotSee_IsReportedRatherThanWritten()
    {
        await SeedAsync();
        var gin = await IdOfAsync("London dry gin");
        var stranger = Guid.CreateVersion7();

        var result = await SetAsync(_household, (gin, true), (stranger, true));

        // Named back rather than silently dropped, and rather than failing the whole request: the
        // catalog can change under a wizard that has been open a while, and losing the eleven good
        // ticks because the twelfth went stale would be the wrong trade.
        Assert.Equal(1, result.Applied);
        Assert.Equal([stranger], result.Unknown);
        Assert.Equal(["London dry gin"], await ShelfAsync());
    }

    [Fact]
    public async Task OneHouseholdsWrite_NeverReachesAnothers()
    {
        await SeedAsync();
        var gin = await IdOfAsync("London dry gin");
        var neighbour = Guid.CreateVersion7();

        await SetAsync(_household, (gin, true));
        await SetAsync(neighbour, (gin, true));

        await using var db = Fixture.CreateContext(_household);
        var mine = await db.TenantInventories.ToListAsync();
        Assert.All(mine, row => Assert.Equal(_household, row.TenantId));
        Assert.Single(mine);
    }

    [Fact]
    public async Task AnEmptyRequestWritesNothing_AndIsNotAnError()
    {
        await SeedAsync();

        // Finishing the wizard having changed nothing is a legitimate answer — every suggestion was
        // wrong, or someone walked through it to look. It is not a reason to hand back a 400.
        var result = await SetAsync(_household);

        Assert.Equal(0, result.Applied);
        Assert.Empty(result.Unknown);
        Assert.Empty(await ShelfAsync());
    }

    [Fact]
    public async Task TheSameIngredientTwiceInOneRequest_TakesTheLastWord()
    {
        await SeedAsync();
        var gin = await IdOfAsync("London dry gin");

        // Not a case the wizard produces, but one a caller can send. Refusing it would mean the
        // endpoint rejected a request it can answer perfectly well.
        var result = await SetAsync(_household, (gin, true), (gin, false));

        Assert.Empty(result.Unknown);
        Assert.Empty(await ShelfAsync());

        await using var db = Fixture.CreateContext(_household);
        Assert.Single(await db.TenantInventories.ToListAsync());
    }
}
