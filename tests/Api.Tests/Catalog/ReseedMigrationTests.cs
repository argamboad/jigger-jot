using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using JiggerJot.Api.Tests.Infrastructure;
using JiggerJot.Core.Entities;
using JiggerJot.Infrastructure.Persistence.Migrations;
using JiggerJot.Infrastructure.Persistence.Seed;

namespace JiggerJot.Api.Tests.Catalog;

/// <summary>
/// JJ-043 (#181): the one-off migration that clears the shared catalog so the seeder writes the curated one.
/// <para>
/// A database seeded before JJ-043 is modelled here as it really was: the seeded catalog plus a shared
/// recipe the curated file no longer ships, under its old name. A household has its own recipe and a fork
/// of a catalog drink. The migration's SQL runs, then the seeder, and the shared catalog must be exactly the
/// file — while nothing the household owns has moved.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ReseedMigrationTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private readonly Guid _household = Guid.CreateVersion7();

    [Fact]
    public async Task TheSharedCatalog_BecomesExactlyTheCuratedFile()
    {
        await AnOldDatabaseAsync();

        await RunTheMigrationAsync();
        await SeedAsync();

        await using var db = Fixture.CreateContext();
        var shared = await db.Cocktails.IgnoreQueryFilters().Where(c => c.TenantId == null).ToListAsync();

        Assert.Equal(CatalogSeeder.LoadCocktails().Cocktails.Count, shared.Count);
        Assert.DoesNotContain(shared, c => c.Name == "Daiquiri Cocktail");
        Assert.Equal(shared.Count, shared.Select(c => c.Name.ToLowerInvariant()).Distinct().Count());
    }

    [Fact]
    public async Task WhatAHouseholdOwns_IsUntouched_AndAForkFindsItsOriginalAgain()
    {
        var (own, fork, negroni) = await AnOldDatabaseAsync();

        await RunTheMigrationAsync();

        await using (var db = Fixture.CreateContext())
        {
            // Between the migration and the seeder: the shared catalog is empty, the household's rows are
            // whole, and the fork is still standing although its original is gone (JJ-013).
            Assert.False(await db.Cocktails.IgnoreQueryFilters().AnyAsync(c => c.TenantId == null));
            Assert.False(await db.CocktailIngredients.IgnoreQueryFilters().AnyAsync(l => l.TenantId == null));
            Assert.Equal(2, await db.CocktailIngredients.IgnoreQueryFilters().CountAsync(l => l.CocktailId == own));
            Assert.Equal(1, await db.CocktailIngredients.IgnoreQueryFilters().CountAsync(l => l.CocktailId == fork));
        }

        await SeedAsync();

        await using var read = Fixture.CreateContext();
        var forked = await read.Cocktails.IgnoreQueryFilters().SingleAsync(c => c.Id == fork);
        Assert.Equal(negroni, forked.ForkedFromCocktailId);
        Assert.True(await read.Cocktails.IgnoreQueryFilters().AnyAsync(c => c.Id == negroni && c.TenantId == null));
        Assert.Equal(2, await read.Cocktails.IgnoreQueryFilters().CountAsync(c => c.TenantId == _household));
    }

    [Fact]
    public async Task RunningItAgain_ChangesNothing()
    {
        await AnOldDatabaseAsync();

        await RunTheMigrationAsync();
        await RunTheMigrationAsync();

        await using var db = Fixture.CreateContext();
        Assert.False(await db.Cocktails.IgnoreQueryFilters().AnyAsync(c => c.TenantId == null));
        Assert.Equal(2, await db.Cocktails.IgnoreQueryFilters().CountAsync(c => c.TenantId == _household));
    }

    private async Task RunTheMigrationAsync()
    {
        await using var db = Fixture.CreateContext();
        await db.Database.ExecuteSqlRawAsync(ReseedCuratedCatalog.ClearSharedCatalogSql);
    }

    private async Task SeedAsync()
    {
        await using var db = Fixture.CreateContext();
        await new CatalogSeeder(db, NullLogger<CatalogSeeder>.Instance).SeedAsync();
    }

    /// <summary>
    /// The seeded catalog, a stale shared recipe the curated file no longer has, and a household with a
    /// recipe of its own and a fork of the Negroni.
    /// </summary>
    private async Task<(Guid Own, Guid Fork, Guid Negroni)> AnOldDatabaseAsync()
    {
        await SeedAsync();

        await using var write = Fixture.CreateContext();
        var gin = await write.Ingredients.IgnoreQueryFilters()
            .Where(i => i.TenantId == null && i.Name == "London dry gin").Select(i => i.Id).SingleAsync();
        var lime = await write.Ingredients.IgnoreQueryFilters()
            .Where(i => i.TenantId == null && i.Name == "Lime juice").Select(i => i.Id).SingleAsync();
        var shared = await write.Cocktails.IgnoreQueryFilters()
            .Where(c => c.TenantId == null).Select(c => new { c.Id, c.Name, c.GlassTypeId }).ToListAsync();
        var negroni = shared.Single(c => c.Name == "Negroni");

        CocktailIngredient Line(Guid? tenant, Guid ingredient, int order) => new()
        {
            TenantId = tenant,
            IngredientId = ingredient,
            Role = order == 0 ? RecipeRole.Base : RecipeRole.Juice,
            IsRequired = true,
            DisplayOrder = order,
        };

        // As SEED-3 shipped it: the Savoy's own title, beside the IBA Daiquiri.
        var stale = new Cocktail
        {
            Name = "Daiquiri Cocktail",
            GlassTypeId = negroni.GlassTypeId,
            ServingType = ServingType.FullDrink,
            Lines = [Line(null, gin, 0)],
        };
        var own = new Cocktail
        {
            Name = "House Gimlet",
            TenantId = _household,
            GlassTypeId = negroni.GlassTypeId,
            ServingType = ServingType.FullDrink,
            Lines = [Line(_household, gin, 0), Line(_household, lime, 1)],
        };
        var fork = new Cocktail
        {
            Name = "Negroni",
            TenantId = _household,
            ForkedFromCocktailId = negroni.Id,
            GlassTypeId = negroni.GlassTypeId,
            ServingType = ServingType.FullDrink,
            Lines = [Line(_household, gin, 0)],
        };
        write.Cocktails.AddRange(stale, own, fork);
        await write.SaveChangesAsync();

        return (own.Id, fork.Id, negroni.Id);
    }
}
