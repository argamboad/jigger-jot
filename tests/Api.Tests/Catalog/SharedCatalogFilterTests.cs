using Microsoft.EntityFrameworkCore;
using JiggerJot.Api.Tests.Infrastructure;
using JiggerJot.Core.Entities;

namespace JiggerJot.Api.Tests.Catalog;

/// <summary>
/// JJ-031, consequence 1: the parallel query filter on <see cref="ISharedOrTenantScoped"/> entities —
/// <c>TenantId == null || TenantId == current</c>. This is the whole reason the interface exists, so it
/// gets a behavioural test rather than a look at the model: a household must see the shared catalog and
/// its own rows, and never another household's.
/// <para>
/// These run on the model-built database (EF filter only, superuser connection). The database-level half
/// of the same guarantee is <c>SharedCatalogRlsTests</c> — deliberately a separate suite, because a wall
/// that only holds when the other wall holds is not a second wall.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SharedCatalogFilterTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private readonly Guid _mine = Guid.CreateVersion7();
    private readonly Guid _theirs = Guid.CreateVersion7();

    [Fact]
    public async Task Ingredients_Query_SeesSharedAndOwn_NeverAnotherHousehold()
    {
        await SeedAsync();

        await using var db = Fixture.CreateContext(_mine);
        var names = await db.Ingredients.Select(i => i.Name).OrderBy(n => n).ToListAsync();

        Assert.Equal(["house infusion", "shared gin"], names);
    }

    [Fact]
    public async Task Cocktails_Query_SeesSharedAndOwn_NeverAnotherHousehold()
    {
        await SeedAsync();

        await using var db = Fixture.CreateContext(_mine);
        var names = await db.Cocktails.Select(c => c.Name).OrderBy(n => n).ToListAsync();

        Assert.Equal(["Martini", "My Martini"], names);
    }

    [Fact]
    public async Task RecipeLines_Query_FollowTheirCocktailsNature()
    {
        await SeedAsync();

        await using var db = Fixture.CreateContext(_mine);
        var owners = await db.CocktailIngredients.Select(l => l.TenantId).ToListAsync();

        // One shared line and one of mine; the other household's line is filtered out entirely.
        Assert.Equal(2, owners.Count);
        Assert.Contains(null, owners);
        Assert.Contains(_mine, owners);
        Assert.DoesNotContain(_theirs, owners);
    }

    [Fact]
    public async Task WithNoAmbientTenant_OnlySharedRowsAreVisible()
    {
        await SeedAsync();

        // The pre-auth / system view: browsing the catalog before choosing a household must still work,
        // and must show the seeded catalog and nothing else. (An ITenantScoped entity would show
        // nothing at all here — which is precisely why these tables could not be ITenantScoped.)
        await using var db = Fixture.CreateContext();
        var names = await db.Cocktails.Select(c => c.Name).ToListAsync();

        Assert.Equal(["Martini"], names);
    }

    [Fact]
    public async Task TenantInventory_StaysStrictlyTenantScoped()
    {
        var lookups = await SeedAsync();

        await using (var db = Fixture.CreateContext(_theirs))
        {
            var theirIngredient = await db.Ingredients.SingleAsync(i => i.Name == "their infusion");
            db.TenantInventories.Add(new TenantInventory
            {
                IngredientId = theirIngredient.Id,
                IsAvailable = true,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        // The shelf is ordinary ITenantScoped data (JJ-031): no shared rows exist for it, and the
        // platform's own filter — not the parallel one — is what scopes it.
        await using var mine = Fixture.CreateContext(_mine);
        Assert.Empty(await mine.TenantInventories.ToListAsync());

        _ = lookups;
    }

    /// <summary>
    /// One shared catalog row of each kind, one of mine, one of another household's. Seeded with no
    /// ambient tenant and explicit ids, because nothing stamps these rows (JJ-031).
    /// </summary>
    private async Task<CatalogLookups> SeedAsync()
    {
        await using var db = Fixture.CreateContext();
        var lookups = await CatalogSeed.LookupsAsync(db);

        var sharedGin = await CatalogSeed.IngredientAsync(db, lookups, "shared gin", tenantId: null);
        var myInfusion = await CatalogSeed.IngredientAsync(db, lookups, "house infusion", _mine);
        var theirInfusion = await CatalogSeed.IngredientAsync(db, lookups, "their infusion", _theirs);

        await CatalogSeed.CocktailAsync(db, lookups, "Martini", tenantId: null, sharedGin.Id);
        await CatalogSeed.CocktailAsync(db, lookups, "My Martini", _mine, myInfusion.Id);
        await CatalogSeed.CocktailAsync(db, lookups, "Their Martini", _theirs, theirInfusion.Id);

        return lookups;
    }
}
