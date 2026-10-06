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
/// AUTHORING-5: a household deletes a cocktail it owns — one it wrote, or one it forked. The shared
/// catalog stays read-only (JJ-002), and a fork of the deleted recipe stays standing, because a fork is a
/// snapshot rather than a reference (JJ-013).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CocktailDeletingTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private readonly Guid _household = Guid.CreateVersion7();

    private static CocktailAuthoringHandler Authoring(AppDbContext db, Guid tenantId) =>
        new(new EfRepository<Cocktail>(db),
            new EfRepository<Ingredient>(db),
            new EfRepository<Unit>(db),
            new EfRepository<GlassType>(db),
            new EfRepository<Method>(db),
            new EfRepository<CocktailIngredient>(db),
            new UserRepository(db),
            new TestCurrentTenant { TenantId = tenantId });

    private async Task SeedAsync()
    {
        await using var db = Fixture.CreateContext();
        await new CatalogSeeder(db, NullLogger<CatalogSeeder>.Instance).SeedAsync();
    }

    private async Task<Guid> SharedCocktailAsync(string name)
    {
        await using var db = Fixture.CreateContext();
        return await db.Cocktails.IgnoreQueryFilters()
            .Where(c => c.TenantId == null && c.Name == name).Select(c => c.Id).FirstAsync();
    }

    private async Task<Guid> WriteAsync(string name, Guid? household = null)
    {
        Guid gin;
        await using (var read = Fixture.CreateContext())
            gin = await read.Ingredients.IgnoreQueryFilters()
                .Where(i => i.TenantId == null && i.Name == "London dry gin").Select(i => i.Id).SingleAsync();

        await using var db = Fixture.CreateContext(household ?? _household);
        return (await Authoring(db, household ?? _household).CreateAsync(
            new AuthorCocktailRequest(name, null, null, ServingType.FullDrink, null,
                [new AuthorLineRequest(gin, null, null, true, RecipeRole.Base, null)]), default)).Id!.Value;
    }

    private async Task<Guid> ForkAsync(Guid id)
    {
        await using var db = Fixture.CreateContext(_household);
        return (await new CocktailForkHandler(new EfRepository<Cocktail>(db), new TestCurrentTenant { TenantId = _household })
            .ForkAsync(id, default))!.Value;
    }

    private async Task<AuthorCocktailOutcome> DeleteAsync(Guid id, Guid? household = null)
    {
        await using var db = Fixture.CreateContext(household ?? _household);
        return await Authoring(db, household ?? _household).DeleteAsync(id, default);
    }

    private async Task<(bool Cocktail, int Lines)> ExistsAsync(Guid id)
    {
        await using var db = Fixture.CreateContext();
        return (await db.Cocktails.IgnoreQueryFilters().AnyAsync(c => c.Id == id),
                await db.CocktailIngredients.IgnoreQueryFilters().CountAsync(l => l.CocktailId == id));
    }

    [Fact]
    public async Task DeletingMyCocktail_TakesItAndItsLines()
    {
        await SeedAsync();
        var id = await WriteAsync("House Sour");

        Assert.Equal(AuthorCocktailOutcome.Deleted, await DeleteAsync(id));

        Assert.Equal((false, 0), await ExistsAsync(id));
    }

    [Fact]
    public async Task DeletingAFork_LeavesTheBooksRecipeAlone()
    {
        await SeedAsync();
        var negroni = await SharedCocktailAsync("Negroni");
        var fork = await ForkAsync(negroni);
        var before = await ExistsAsync(negroni);

        Assert.Equal(AuthorCocktailOutcome.Deleted, await DeleteAsync(fork));

        Assert.Equal((false, 0), await ExistsAsync(fork));
        Assert.Equal(before, await ExistsAsync(negroni));
    }

    [Fact]
    public async Task DeletingARecipeSomethingWasForkedFrom_LeavesTheForkStanding()
    {
        await SeedAsync();
        var original = await WriteAsync("House Sour");
        var copy = await ForkAsync(original);

        Assert.Equal(AuthorCocktailOutcome.Deleted, await DeleteAsync(original));

        // A snapshot, not a reference (JJ-013): ForkedFromCocktailId is not a foreign key, so the copy
        // keeps every line and simply stops linking anywhere.
        var (exists, lines) = await ExistsAsync(copy);
        Assert.True(exists);
        Assert.Equal(1, lines);
    }

    [Fact]
    public async Task TheSharedCatalog_CannotBeDeleted()
    {
        await SeedAsync();
        var negroni = await SharedCocktailAsync("Negroni");
        var before = await ExistsAsync(negroni);

        // Visible to every household, so not a 404 — but not theirs to remove (JJ-002).
        Assert.Equal(AuthorCocktailOutcome.ReadOnly, await DeleteAsync(negroni));
        Assert.Equal(before, await ExistsAsync(negroni));
    }

    [Fact]
    public async Task AnotherHouseholdsCocktail_IsNotFound_AndStays()
    {
        await SeedAsync();
        var theirs = await WriteAsync("Their Sour", Guid.CreateVersion7());

        // Not found rather than forbidden: saying "forbidden" would confirm it exists (JJ-031).
        Assert.Equal(AuthorCocktailOutcome.NotFound, await DeleteAsync(theirs));
        Assert.True((await ExistsAsync(theirs)).Cocktail);
    }
}
