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
/// FORK-1 (FEATURES §13): "create my own version". A full snapshot copy — a new household-owned
/// cocktail plus copies of every recipe line — with the original recorded as provenance and nothing
/// else (JJ-002, JJ-013).
/// <para>
/// <b>Snapshot, not reference</b>, is the whole slice. Half of these tests exist to prove the copy
/// stays put when the original moves: edit the original, delete the original, fork it twice. A
/// reference would pass the first test in this file and fail every one of those.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CocktailForkTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private readonly Guid _household = Guid.CreateVersion7();

    private static CocktailForkHandler Handler(AppDbContext db, Guid tenantId) =>
        new(new EfRepository<Cocktail>(db), new TestCurrentTenant { TenantId = tenantId });

    private static CocktailDetailHandler Detail(AppDbContext db) =>
        new(new EfRepository<Cocktail>(db),
            new UserRepository(db),
            new EfRepository<TenantInventory>(db),
            new EfRepository<IngredientSubstitution>(db));

    private async Task SeedAsync()
    {
        await using var db = Fixture.CreateContext();
        await new CatalogSeeder(db, NullLogger<CatalogSeeder>.Instance).SeedAsync();
    }

    private async Task<Guid> NegroniAsync()
    {
        await using var db = Fixture.CreateContext();
        return await db.Cocktails.IgnoreQueryFilters()
            .Where(c => c.Name == "Negroni").Select(c => c.Id).FirstAsync();
    }

    private async Task<Guid?> ForkAsync(Guid id, Guid? household = null)
    {
        await using var db = Fixture.CreateContext(household ?? _household);
        return await Handler(db, household ?? _household).ForkAsync(id, default);
    }

    private async Task<CocktailDetail?> ReadAsync(Guid id, Guid? household = null)
    {
        await using var db = Fixture.CreateContext(household ?? _household);
        return await Detail(db).GetAsync(id, null, default);
    }

    [Fact]
    public async Task ForkingCopiesTheWholeRecipe()
    {
        await SeedAsync();
        var original = await NegroniAsync();

        var forkId = await ForkAsync(original);

        var source = (await ReadAsync(original))!;
        var fork = (await ReadAsync(forkId!.Value))!;

        Assert.Equal(source.Name, fork.Name);
        Assert.Equal(source.Glass, fork.Glass);
        Assert.Equal(source.Method, fork.Method);
        Assert.Equal(source.ServingType, fork.ServingType);
        Assert.Equal(source.Instructions, fork.Instructions);

        // Every line, in order, with its amounts as authored (JJ-007) — a copy that quietly rounded
        // or reordered would be a different drink.
        Assert.Equal(source.Lines.Count, fork.Lines.Count);
        Assert.Equal([.. source.Lines.Select(l => (l.Ingredient, l.Amount, l.Unit, l.IsRequired, l.Role))],
                     [.. fork.Lines.Select(l => (l.Ingredient, l.Amount, l.Unit, l.IsRequired, l.Role))]);
    }

    [Fact]
    public async Task TheCopyAndEveryLine_CarryMyTenantId()
    {
        await SeedAsync();
        var forkId = (await ForkAsync(await NegroniAsync()))!.Value;

        await using var db = Fixture.CreateContext();
        var cocktail = await db.Cocktails.IgnoreQueryFilters().SingleAsync(c => c.Id == forkId);
        var lines = await db.CocktailIngredients.IgnoreQueryFilters()
            .Where(l => l.CocktailId == forkId).ToListAsync();

        // JJ-031, on two tables at once this time. Nothing stamps either of them, and a line written
        // without a tenant would land in the SHARED catalog attached to a household's cocktail — the
        // worst of both, and invisible until someone else's app showed it.
        Assert.Equal(_household, cocktail.TenantId);
        Assert.NotEmpty(lines);
        Assert.All(lines, l => Assert.Equal(_household, l.TenantId));
    }

    [Fact]
    public async Task TheCopy_RecordsWhatItCameFrom()
    {
        await SeedAsync();
        var original = await NegroniAsync();

        var forkId = (await ForkAsync(original))!.Value;

        await using var db = Fixture.CreateContext();
        var fork = await db.Cocktails.IgnoreQueryFilters().SingleAsync(c => c.Id == forkId);

        Assert.Equal(original, fork.ForkedFromCocktailId);
        Assert.Equal(original, (await ReadAsync(forkId))!.ForkedFrom!.Id);
    }

    [Fact]
    public async Task TheCopy_TakesNoCreditFromTheBook()
    {
        await SeedAsync();
        var original = await NegroniAsync();

        var forkId = (await ForkAsync(original))!.Value;

        // The book wrote the original, not this household's version of it, and the credit is a real
        // claim rather than decoration (JJ-032). Carrying the source across would attribute whatever
        // the household does next to Craddock or the IBA. Provenance rides on ForkedFromCocktailId
        // instead, which says "based on" rather than "written by".
        Assert.NotNull((await ReadAsync(original))!.Source);
        Assert.Null((await ReadAsync(forkId))!.Source);
    }

    [Fact]
    public async Task EditingTheOriginal_NeverTouchesTheCopy()
    {
        await SeedAsync();
        var original = await NegroniAsync();
        var forkId = (await ForkAsync(original))!.Value;
        var before = (await ReadAsync(forkId))!;

        // Rewrite the original as the catalog's owner would: a new name and one line fewer.
        await using (var db = Fixture.CreateContext())
        {
            var cocktail = await db.Cocktails.IgnoreQueryFilters()
                .Include(c => c.Lines).SingleAsync(c => c.Id == original);
            cocktail.Name = "Negroni, revised";
            db.CocktailIngredients.Remove(cocktail.Lines.First());
            await db.SaveChangesAsync();
        }

        var after = (await ReadAsync(forkId))!;

        // JJ-013: a snapshot, not a live reference. Upstream edits must not silently change a
        // household's drink — this is the assertion a reference implementation would fail.
        Assert.Equal(before.Name, after.Name);
        Assert.Equal(before.Lines.Count, after.Lines.Count);
    }

    [Fact]
    public async Task DeletingTheOriginal_LeavesTheCopyStanding()
    {
        await SeedAsync();
        var original = await NegroniAsync();
        var forkId = (await ForkAsync(original))!.Value;

        await using (var db = Fixture.CreateContext())
        {
            db.Cocktails.Remove(await db.Cocktails.IgnoreQueryFilters().SingleAsync(c => c.Id == original));
            await db.SaveChangesAsync();
        }

        var fork = await ReadAsync(forkId);

        // Which is exactly why ForkedFromCocktailId is not a foreign key: an FK would either block
        // this delete or cascade a household's own cocktail away with it. The provenance goes quiet
        // rather than taking the recipe down with it.
        Assert.NotNull(fork);
        Assert.NotEmpty(fork.Lines);
        Assert.Null(fork.ForkedFrom);
    }

    [Fact]
    public async Task TheCopyIsMine_AndNobodyElsesToSee()
    {
        await SeedAsync();
        var forkId = (await ForkAsync(await NegroniAsync()))!.Value;

        var mine = (await ReadAsync(forkId))!;
        Assert.True(mine.IsOwn);

        // The dual-natured filter does this, but a personalised recipe leaking to another household
        // is the failure this whole table shape exists to prevent (JJ-031).
        Assert.Null(await ReadAsync(forkId, Guid.CreateVersion7()));
    }

    [Fact]
    public async Task AnotherHouseholdsCocktail_CannotBeForked()
    {
        await SeedAsync();
        var stranger = Guid.CreateVersion7();
        var theirs = (await ForkAsync(await NegroniAsync(), stranger))!.Value;

        // Null, which the endpoint turns into a 404 rather than a 403 — saying "forbidden" would
        // confirm the row exists.
        Assert.Null(await ForkAsync(theirs));
    }

    [Fact]
    public async Task ForkingTwice_MakesTwoIndependentCopies()
    {
        await SeedAsync();
        var original = await NegroniAsync();

        var first = (await ForkAsync(original))!.Value;
        var second = (await ForkAsync(original))!.Value;

        Assert.NotEqual(first, second);

        // Renaming one leaves the other alone. Nothing enforces unique names on a cocktail — the
        // seeded catalog holds four names twice over — so two versions of the same drink is a
        // legitimate thing for a household to want.
        await using (var db = Fixture.CreateContext(_household))
        {
            var fork = await db.Cocktails.SingleAsync(c => c.Id == first);
            fork.Name = "Negroni, stronger";
            await db.SaveChangesAsync();
        }

        Assert.Equal("Negroni, stronger", (await ReadAsync(first))!.Name);
        Assert.Equal("Negroni", (await ReadAsync(second))!.Name);
    }

    [Fact]
    public async Task ForkingAFork_RecordsTheOneItCameFrom()
    {
        await SeedAsync();
        var original = await NegroniAsync();
        var first = (await ForkAsync(original))!.Value;

        var second = (await ForkAsync(first))!.Value;

        // Provenance is one hop, not a chain to walk. Recording the root instead would claim the
        // second copy came from a recipe it has never seen.
        Assert.Equal(first, (await ReadAsync(second))!.ForkedFrom!.Id);
    }

    [Fact]
    public async Task TheCopy_ShowsUpWhenBrowsing()
    {
        await SeedAsync();
        var forkId = (await ForkAsync(await NegroniAsync()))!.Value;

        await using var db = Fixture.CreateContext(_household);
        var browse = new CocktailBrowseHandler(
            new EfRepository<Cocktail>(db),
            new EfRepository<TenantInventory>(db),
            new EfRepository<IngredientSubstitution>(db));
        var page = await browse.BrowseAsync(new CocktailBrowseRequest("negroni", 1, 100), default);

        // FEATURES §13 step 4: "the fork appears among the household's custom cocktails". It is not a
        // separate list — it sits in the catalog beside the drink it came from, marked as yours.
        var row = Assert.Single(page.Items, c => c.Id == forkId);
        Assert.True(row.IsOwn);
        Assert.Equal(page.Total, await db.Cocktails.CountAsync(c => c.Name.ToLower().Contains("negroni")));
    }

    [Fact]
    public async Task DissolvingTheHousehold_TakesTheCopyAndItsLines()
    {
        await SeedAsync();
        var forkId = (await ForkAsync(await NegroniAsync()))!.Value;

        int sharedBefore;
        await using (var db = Fixture.CreateContext())
            sharedBefore = await db.Cocktails.IgnoreQueryFilters().CountAsync(c => c.TenantId == null);

        await using (var db = Fixture.CreateContext(_household))
            await new CatalogDataContributor(
                new EfRepository<Ingredient>(db),
                new EfRepository<Cocktail>(db),
                new EfRepository<CocktailIngredient>(db)).WipeAsync(_household);

        // The platform's dissolution canary cannot see either of these tables — a nullable TenantId is
        // invisible to it (JJ-031) — so this is the app's own proof that a dissolved household takes
        // its recipes with it and leaves the shared catalog exactly as it was.
        await using (var db = Fixture.CreateContext())
        {
            Assert.Empty(await db.Cocktails.IgnoreQueryFilters().Where(c => c.Id == forkId).ToListAsync());
            Assert.Empty(await db.CocktailIngredients.IgnoreQueryFilters()
                .Where(l => l.CocktailId == forkId).ToListAsync());
            Assert.Equal(sharedBefore, await db.Cocktails.IgnoreQueryFilters().CountAsync(c => c.TenantId == null));
        }
    }
}
