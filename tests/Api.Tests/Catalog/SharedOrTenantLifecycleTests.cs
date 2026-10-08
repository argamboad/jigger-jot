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
/// The lifecycle of the shared-or-tenant catalog (JJ-031; the platform's <see cref="ISharedOrTenantScoped"/> since
/// Arch A4). The platform's <c>EverySharedOrTenantEntity_ShipsItsLifecycleSpec</c> asks each such entity for four
/// facets, <c>&lt;Entity&gt;_SharedOrTenant_{Dissolve,Export,SharedWrites,Erasure}_*</c>; these are them, for
/// <see cref="Ingredient"/>, <see cref="Cocktail"/> and <see cref="CocktailIngredient"/>. Above all a dissolve
/// <b>never touches the shared catalog</b>, and a household can never write a shared row.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SharedOrTenantLifecycleTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private readonly Guid _mine = Guid.CreateVersion7();
    private readonly Guid _theirs = Guid.CreateVersion7();

    // ── Dissolve: the household's rows go, the shared and the other household's stay ──

    [Fact]
    public async Task Ingredient_SharedOrTenant_Dissolve_RemovesTheHouseholdsRows_AndKeepsTheShared()
    {
        await SeedAndWipeMineAsync();
        await using var read = Fixture.CreateContext();
        Assert.Equal(["shared gin", "their infusion"], await AllOf<Ingredient>(read).Select(i => i.Name).OrderBy(n => n).ToListAsync());
    }

    [Fact]
    public async Task Cocktail_SharedOrTenant_Dissolve_RemovesTheHouseholdsRows_AndKeepsTheShared()
    {
        await SeedAndWipeMineAsync();
        await using var read = Fixture.CreateContext();
        Assert.Equal(["Martini", "Their Martini"], await AllOf<Cocktail>(read).Select(c => c.Name).OrderBy(n => n).ToListAsync());
    }

    [Fact]
    public async Task CocktailIngredient_SharedOrTenant_Dissolve_RemovesTheHouseholdsRows_AndKeepsTheShared()
    {
        await SeedAndWipeMineAsync();
        await using var read = Fixture.CreateContext();
        Assert.Empty(await AllOf<CocktailIngredient>(read).Where(l => l.TenantId == _mine).ToListAsync());
        Assert.Equal(2, await AllOf<CocktailIngredient>(read).CountAsync()); // the shared Martini's line and theirs
    }

    // ── Export: the household's own rows, never the shared catalog or another household's ──

    [Fact]
    public async Task Ingredient_SharedOrTenant_Export_CarriesTheHouseholdsRowsOnly()
    {
        var json = await ExportMineAsync();
        Assert.Contains("house infusion", json);
        Assert.DoesNotContain("shared gin", json);
        Assert.DoesNotContain("their infusion", json);
    }

    [Fact]
    public async Task Cocktail_SharedOrTenant_Export_CarriesTheHouseholdsRowsOnly()
    {
        var json = await ExportMineAsync();
        Assert.Contains("My Martini", json);
        Assert.DoesNotContain("Their Martini", json);
        Assert.DoesNotContain("\"Martini\"", json); // the shared one, by its exact name
    }

    [Fact]
    public async Task CocktailIngredient_SharedOrTenant_Export_CarriesTheHouseholdsRowsOnly()
    {
        // A cocktail's lines travel inside it, so the household's export names the ingredient of its own line and of
        // no other line.
        var export = JsonDocument.Parse(await ExportMineAsync()).RootElement;
        var lines = export.EnumerateObject().SelectMany(p => p.Value.ValueKind == JsonValueKind.Array ? p.Value.EnumerateArray() : Enumerable.Empty<JsonElement>())
            .Where(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("Lines", out _))
            .SelectMany(c => c.GetProperty("Lines").EnumerateArray())
            .Select(l => l.GetProperty("Ingredient").GetString())
            .ToList();
        Assert.Equal(["house infusion"], lines);
    }

    // ── SharedWrites: only a tenant-less context writes a shared row ──

    [Fact]
    public async Task Ingredient_SharedOrTenant_SharedWrites_ComeFromTenantlessContextsOnly()
    {
        var lookups = await SeedAsync(); // SeedAsync wrote the shared rows from a tenant-less context
        await using var household = Fixture.CreateContext(_mine);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CatalogSeed.IngredientAsync(household, lookups, "sneaky shared", tenantId: null));
    }

    [Fact]
    public async Task Cocktail_SharedOrTenant_SharedWrites_ComeFromTenantlessContextsOnly()
    {
        var lookups = await SeedAsync();
        await using var household = Fixture.CreateContext(_mine);
        var gin = await household.Ingredients.SingleAsync(i => i.Name == "shared gin");
        await Assert.ThrowsAsync<InvalidOperationException>(() => CatalogSeed.CocktailAsync(household, lookups, "Sneaky", tenantId: null, gin.Id));
    }

    [Fact]
    public async Task CocktailIngredient_SharedOrTenant_SharedWrites_ComeFromTenantlessContextsOnly()
    {
        var lookups = await SeedAsync();
        await using var household = Fixture.CreateContext(_mine);
        var martini = await household.Cocktails.SingleAsync(c => c.Name == "Martini");
        var gin = await household.Ingredients.SingleAsync(i => i.Name == "shared gin");
        household.Add(new CocktailIngredient { TenantId = null, CocktailId = martini.Id, IngredientId = gin.Id, Amount = 1m, UnitId = lookups.UnitId });
        await Assert.ThrowsAsync<InvalidOperationException>(() => household.SaveChangesAsync());
    }

    // ── Erasure: an account erasure never addresses these tables ──
    // An erasure removes a USER's data (GDPR-2): identity rows and entities keyed by a user. The catalog's rows carry no
    // user key — they belong to the household, which outlives any one member — so the erasure leaves them, and the
    // user-keyed canary (EveryUserKeyedEntity_IsWiredIntoAccountErasure) is what would change if one ever grew one.

    [Fact]
    public void Ingredient_SharedOrTenant_Erasure_LeavesTheHouseholdsRows() => AssertNoUserKey<Ingredient>();

    [Fact]
    public void Cocktail_SharedOrTenant_Erasure_LeavesTheHouseholdsRows() => AssertNoUserKey<Cocktail>();

    [Fact]
    public void CocktailIngredient_SharedOrTenant_Erasure_LeavesTheHouseholdsRows() => AssertNoUserKey<CocktailIngredient>();

    // ── the household's other content ──

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

    private static void AssertNoUserKey<T>()
    {
        Assert.DoesNotContain(typeof(T).GetProperties(), p => p.Name.EndsWith("UserId", StringComparison.Ordinal));
        Assert.DoesNotContain(typeof(T).GetInterfaces(), i => i.Name.Contains("UserKeyed", StringComparison.Ordinal));
    }

    private async Task SeedAndWipeMineAsync()
    {
        await SeedAsync();
        await using var db = Fixture.CreateContext(_mine);
        await BuildCatalog(db).WipeAsync(_mine);
    }

    private async Task<string> ExportMineAsync()
    {
        await SeedAsync();
        await using var db = Fixture.CreateContext(_mine);
        return JsonSerializer.Serialize(await BuildCatalog(db).ExportAsync(_mine));
    }

    private static CatalogDataContributor BuildCatalog(AppDbContext db) =>
        new(new EfRepository<Ingredient>(db), new EfRepository<Cocktail>(db), new EfRepository<CocktailIngredient>(db),
            new HouseholdIngredients(new EfRepository<Ingredient>(db)));

    /// <summary>Every row of a table, filters off — the assertions here are about what a dissolve left
    /// behind across all households, which no tenant-scoped view can answer.</summary>
    private static IQueryable<T> AllOf<T>(AppDbContext db) where T : class =>
        db.Set<T>().IgnoreQueryFilters();

    private async Task<CatalogLookups> SeedAsync()
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
        return lookups;
    }
}
