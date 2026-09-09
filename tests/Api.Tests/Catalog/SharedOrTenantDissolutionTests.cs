using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using JiggerJot.Api.Features.Catalog;
using JiggerJot.Api.Features.Inventory;
using JiggerJot.Api.Tests.Infrastructure;
using JiggerJot.Core.Entities;
using JiggerJot.Infrastructure.Persistence;
using JiggerJot.Infrastructure.Repositories;

namespace JiggerJot.Api.Tests.Catalog;

/// <summary>
/// JJ-031, consequence 3: the platform's <c>EveryTenantOwnedEntity_IsWiredIntoTenantDissolution</c> canary
/// only sees a <b>non-nullable</b> <c>Guid TenantId</c>, so every <see cref="ISharedOrTenantScoped"/>
/// entity is invisible to it — a forgotten contributor would orphan a dissolved household's recipes in
/// silence. This is the replacement canary, plus the behavioural proof that the contributor deletes the
/// right rows and, above all, <b>never touches the shared catalog</b>.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SharedOrTenantDissolutionTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private readonly Guid _mine = Guid.CreateVersion7();
    private readonly Guid _theirs = Guid.CreateVersion7();

    [Fact]
    public void EverySharedOrTenantEntity_IsWiredIntoTenantDissolution()
    {
        var handled = new HashSet<string>
        {
            nameof(Ingredient), nameof(Cocktail), nameof(CocktailIngredient), // CatalogDataContributor
        };

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=arch-check") // model-only; never connects
            .Options;
        using var ctx = new AppDbContext(options, new TestCurrentTenant());

        var uncovered = ctx.Model.GetEntityTypes()
            .Where(e => typeof(ISharedOrTenantScoped).IsAssignableFrom(e.ClrType))
            .Select(e => e.ClrType.Name)
            .Where(name => !handled.Contains(name))
            .ToList();

        Assert.True(uncovered.Count == 0,
            "Shared-or-tenant entities not wired into tenant dissolution — the platform canary cannot see "
            + "them (their TenantId is nullable), so add an ITenantDataContributor that wipes the "
            + $"household's rows ONLY and list it here: {string.Join(", ", uncovered)}");
    }

    [Fact]
    public async Task Wipe_RemovesTheHouseholdsOwnCatalog_AndLeavesTheSharedOneIntact()
    {
        await SeedAsync();

        await using (var db = Fixture.CreateContext(_mine))
            await BuildCatalog(db).WipeAsync(_mine);

        await using var read = Fixture.CreateContext();
        var ingredients = await AllOf<Ingredient>(read).Select(i => i.Name).OrderBy(n => n).ToListAsync();
        var cocktails = await AllOf<Cocktail>(read).Select(c => c.Name).OrderBy(n => n).ToListAsync();

        // The shared rows and the other household's survive; only mine are gone.
        Assert.Equal(["shared gin", "their infusion"], ingredients);
        Assert.Equal(["Martini", "Their Martini"], cocktails);
        Assert.Empty(await AllOf<CocktailIngredient>(read).Where(l => l.TenantId == _mine).ToListAsync());
        Assert.Equal(2, await AllOf<CocktailIngredient>(read).CountAsync());
    }

    [Fact]
    public async Task Wipe_RemovesTheHouseholdsShelf_AndLeavesTheOtherHouseholds()
    {
        await SeedAsync();

        await using (var db = Fixture.CreateContext(_mine))
            await new InventoryDataContributor(new EfRepository<TenantInventory>(db)).WipeAsync(_mine);

        await using var read = Fixture.CreateContext();
        Assert.Empty(await AllOf<TenantInventory>(read).Where(i => i.TenantId == _mine).ToListAsync());
        Assert.Single(await AllOf<TenantInventory>(read).Where(i => i.TenantId == _theirs).ToListAsync());
    }

    [Fact]
    public async Task HasData_IsTrueForTheHouseholdThatAuthoredSomething_AndFalseForAStranger()
    {
        await SeedAsync();

        await using var db = Fixture.CreateContext(_mine);
        Assert.True(await BuildCatalog(db).HasDataAsync(_mine));
        Assert.False(await BuildCatalog(db).HasDataAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task Export_CarriesTheHouseholdsOwnRowsOnly()
    {
        await SeedAsync();

        await using var db = Fixture.CreateContext(_mine);
        var export = await BuildCatalog(db).ExportAsync(_mine);
        var json = JsonSerializer.Serialize(export);

        Assert.Contains("My Martini", json);
        Assert.Contains("house infusion", json);
        // The shared catalog is not this household's data to take, and the other household's never was.
        Assert.DoesNotContain("shared gin", json);
        Assert.DoesNotContain("their infusion", json);
        Assert.DoesNotContain("Their Martini", json);
    }

    private static CatalogDataContributor BuildCatalog(AppDbContext db) =>
        new(new EfRepository<Ingredient>(db), new EfRepository<Cocktail>(db), new EfRepository<CocktailIngredient>(db));

    /// <summary>Every row of a table, filters off — the assertions here are about what a dissolve left
    /// behind across all households, which no tenant-scoped view can answer.</summary>
    private static IQueryable<T> AllOf<T>(AppDbContext db) where T : class =>
        db.Set<T>().IgnoreQueryFilters();

    private async Task SeedAsync()
    {
        await using var db = Fixture.CreateContext();
        var lookups = await CatalogSeed.LookupsAsync(db);

        var sharedGin = await CatalogSeed.IngredientAsync(db, lookups, "shared gin", tenantId: null);
        var myInfusion = await CatalogSeed.IngredientAsync(db, lookups, "house infusion", _mine);
        var theirInfusion = await CatalogSeed.IngredientAsync(db, lookups, "their infusion", _theirs);

        // "Martini" is shared and built on a shared ingredient; the two household cocktails are built on
        // their own, so a wipe that reached too far would break a foreign key rather than pass quietly.
        await CatalogSeed.CocktailAsync(db, lookups, "Martini", tenantId: null, sharedGin.Id);
        await CatalogSeed.CocktailAsync(db, lookups, "My Martini", _mine, myInfusion.Id);
        await CatalogSeed.CocktailAsync(db, lookups, "Their Martini", _theirs, theirInfusion.Id);

        db.TenantInventories.AddRange(
            new TenantInventory { TenantId = _mine, IngredientId = sharedGin.Id, IsAvailable = true, UpdatedAt = DateTimeOffset.UtcNow },
            new TenantInventory { TenantId = _theirs, IngredientId = sharedGin.Id, IsAvailable = true, UpdatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }
}
