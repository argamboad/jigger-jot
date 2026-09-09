using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using JiggerJot.Api.Tests.Infrastructure;
using JiggerJot.Core.Catalog;
using JiggerJot.Core.Entities;
using JiggerJot.Infrastructure.Persistence;
using JiggerJot.Infrastructure.Persistence.Seed;

namespace JiggerJot.Api.Tests.Catalog;

/// <summary>
/// SEED-1: the curated global lookups (JJ-022) reach the database, and — because this runs on every
/// boot — running it again changes nothing. Also pins the two properties the rest of the catalog will
/// lean on: ids are derived from names and so are stable across environments, and a unit's
/// convertibility is exactly whether it carries a millilitre factor (JJ-007).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CatalogSeederTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private static CatalogSeeder Build(AppDbContext db) => new(db, NullLogger<CatalogSeeder>.Instance);

    [Fact]
    public async Task Seed_WritesEveryLookup_AsSharedRows()
    {
        var file = CatalogSeeder.Load();

        int added;
        await using (var db = Fixture.CreateContext())
            added = await Build(db).SeedAsync();

        var expected = file.GlassTypes.Count + file.Methods.Count + file.Units.Count
                       + file.IngredientCategories.Count
                       + file.IngredientCategories.Sum(c => c.Children.Count);
        Assert.Equal(expected, added);

        await using var read = Fixture.CreateContext();
        Assert.Equal(file.GlassTypes.Count, await read.GlassTypes.CountAsync());
        Assert.Equal(file.Methods.Count, await read.Methods.CountAsync());
        Assert.Equal(file.Units.Count, await read.Units.CountAsync());

        // Lookups have no tenant column at all, so "shared" here means what it means for them: every
        // household reads the same rows, with no filter in the way.
        Assert.Equal(file.IngredientCategories.Count,
            await read.IngredientCategories.CountAsync(c => c.ParentId == null));
    }

    [Fact]
    public async Task Seed_RunAgain_AddsNothing()
    {
        await using (var db = Fixture.CreateContext())
            await Build(db).SeedAsync();

        int second;
        await using (var db = Fixture.CreateContext())
            second = await Build(db).SeedAsync();

        Assert.Equal(0, second);

        // The count check matters as much as the return value: a seeder that "added 0" while quietly
        // duplicating rows would satisfy the assertion above and break every unique index later.
        await using var read = Fixture.CreateContext();
        Assert.Equal(CatalogSeeder.Load().GlassTypes.Count, await read.GlassTypes.CountAsync());
    }

    [Fact]
    public async Task Seed_KeepsTheSameIds_AcrossARebuild()
    {
        await using (var db = Fixture.CreateContext())
            await Build(db).SeedAsync();

        Guid before;
        await using (var read = Fixture.CreateContext())
            before = (await read.GlassTypes.SingleAsync(g => g.Name == "Coupe")).Id;

        // Ids come from the name, not from the insert, so they survive a wipe and re-seed — which is
        // what lets a later seed pass reference a category by deriving its id rather than looking it up.
        await Fixture.ResetAsync();
        await using (var db = Fixture.CreateContext())
            await Build(db).SeedAsync();

        await using var after = Fixture.CreateContext();
        Assert.Equal(before, (await after.GlassTypes.SingleAsync(g => g.Name == "Coupe")).Id);
        Assert.Equal(SeedId.For("glass", "Coupe"), before);
    }

    [Fact]
    public async Task Categories_AreExactlyTwoLevels_AndEveryChildPointsAtARealParent()
    {
        await using (var db = Fixture.CreateContext())
            await Build(db).SeedAsync();

        await using var read = Fixture.CreateContext();
        var all = await read.IngredientCategories.ToListAsync();
        var parentIds = all.Where(c => c.ParentId == null).Select(c => c.Id).ToHashSet();
        var children = all.Where(c => c.ParentId != null).ToList();

        Assert.NotEmpty(children);
        // Two levels, never three (JJ-015): every child's parent must itself be a top-level row, or the
        // parent-matches-all-children filter (JJ-016) would have to become recursive.
        Assert.All(children, c => Assert.Contains(c.ParentId!.Value, parentIds));
    }

    [Fact]
    public async Task Units_AreConvertibleExactlyWhenTheyCarryAFactor()
    {
        await using (var db = Fixture.CreateContext())
            await Build(db).SeedAsync();

        await using var read = Fixture.CreateContext();
        var units = await read.Units.ToListAsync();

        Assert.All(units, u => Assert.Equal(u.System == UnitSystem.Neutral, u.MillilitreFactor is null));

        // A spot check with real numbers, because a wrong factor is invisible until a drink is poured.
        Assert.Equal(29.5735m, (await read.Units.SingleAsync(u => u.Name == "oz")).MillilitreFactor);
        Assert.Equal(1m, (await read.Units.SingleAsync(u => u.Name == "ml")).MillilitreFactor);
        Assert.Null((await read.Units.SingleAsync(u => u.Name == "dash")).MillilitreFactor);
    }

    [Fact]
    public async Task Seed_RefusesToRun_UnderAHousehold()
    {
        // The guard that keeps the JJ-031 write rule honest: a household context cannot write a shared
        // row, because the RLS insert policy admits one only under the bypass GUC that a tenant-less
        // context alone gets. Failing here beats failing with a 42501 deep inside SaveChanges.
        await using var db = Fixture.CreateContext(Guid.CreateVersion7());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Build(db).SeedAsync());
        Assert.Contains("no ambient household", error.Message);
    }

    [Fact]
    public void SeedId_IgnoresCasingAndSurroundingSpace_ButNotTheKind()
    {
        // Names are identity, so the rules about what counts as the same name are worth pinning: a row
        // renamed only in its capitalisation keeps its id and its references.
        Assert.Equal(SeedId.For("glass", "Rocks glass"), SeedId.For("glass", "  rocks GLASS "));
        Assert.NotEqual(SeedId.For("glass", "Coupe"), SeedId.For("method", "Coupe"));
    }
}
