using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using JiggerJot.Api.Features.Catalog;
using JiggerJot.Api.Features.Inventory;
using JiggerJot.Api.Tests.Infrastructure;
using JiggerJot.Core.Entities;
using JiggerJot.Infrastructure.Persistence;
using JiggerJot.Infrastructure.Persistence.Seed;
using JiggerJot.Infrastructure.Repositories;

namespace JiggerJot.Api.Tests.Catalog;

/// <summary>
/// INV-4: a household deletes a bottle it added. A bottle one of its own recipes still uses is refused,
/// and the refusal names those recipes — nothing is taken out of a recipe behind anyone's back. The shared
/// catalog is read-only (JJ-002).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CustomIngredientDeletingTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private readonly Guid _household = Guid.CreateVersion7();

    private static InventoryHandler Inventory(AppDbContext db, Guid tenantId) =>
        new(new EfRepository<TenantInventory>(db),
            new EfRepository<Ingredient>(db),
            new EfRepository<IngredientCategory>(db),
            new TestCurrentTenant { TenantId = tenantId },
            new FakeTimeProvider(new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero)));

    private static IngredientRemovalHandler Removal(AppDbContext db, Guid tenantId) =>
        new(new EfRepository<Ingredient>(db),
            new EfRepository<CocktailIngredient>(db),
            new TestCurrentTenant { TenantId = tenantId });

    private async Task SeedAsync()
    {
        await using var db = Fixture.CreateContext();
        await new CatalogSeeder(db, NullLogger<CatalogSeeder>.Instance).SeedAsync();
    }

    private async Task<Guid> AddAsync(string name, Guid? household = null)
    {
        Guid category;
        await using (var read = Fixture.CreateContext())
            category = await read.IngredientCategories.Where(c => c.ParentId == null).OrderBy(c => c.Name)
                .Select(c => c.Id).FirstAsync();

        await using var db = Fixture.CreateContext(household ?? _household);
        return (await Inventory(db, household ?? _household)
            .AddIngredientAsync(new AddIngredientRequest(name, category, null), default)).Item!.Id;
    }

    private async Task WriteUsingAsync(string cocktail, Guid ingredient)
    {
        await using var db = Fixture.CreateContext(_household);
        var authoring = new CocktailAuthoringHandler(
            new EfRepository<Cocktail>(db), new EfRepository<Ingredient>(db), new EfRepository<Unit>(db),
            new EfRepository<GlassType>(db), new EfRepository<Method>(db), new EfRepository<CocktailIngredient>(db),
            new UserRepository(db), new TestCurrentTenant { TenantId = _household });
        var result = await authoring.CreateAsync(new AuthorCocktailRequest(cocktail, null, null, ServingType.FullDrink, null,
            [new AuthorLineRequest(ingredient, null, null, true, RecipeRole.Syrup, null)]), default);
        Assert.Equal(AuthorCocktailOutcome.Created, result.Outcome);
    }

    private async Task<RemoveIngredientResult> DeleteAsync(Guid id, Guid? household = null)
    {
        await using var db = Fixture.CreateContext(household ?? _household);
        return await Removal(db, household ?? _household).DeleteAsync(id, default);
    }

    private async Task<(bool Ingredient, bool ShelfRow)> ExistsAsync(Guid id)
    {
        await using var db = Fixture.CreateContext();
        return (await db.Ingredients.IgnoreQueryFilters().AnyAsync(i => i.Id == id),
                await db.TenantInventories.IgnoreQueryFilters().AnyAsync(i => i.IngredientId == id));
    }

    [Fact]
    public async Task DeletingMyBottle_TakesItAndItsShelfRow()
    {
        await SeedAsync();
        var id = await AddAsync("House roasted pineapple syrup");
        Assert.Equal((true, true), await ExistsAsync(id));

        var result = await DeleteAsync(id);

        Assert.Equal(RemoveIngredientOutcome.Deleted, result.Outcome);
        Assert.Equal((false, false), await ExistsAsync(id));
    }

    [Fact]
    public async Task ABottleMyRecipesUse_IsRefused_AndNamesThemInOrder()
    {
        await SeedAsync();
        var id = await AddAsync("House roasted pineapple syrup");
        await WriteUsingAsync("Tiki Sour", id);
        await WriteUsingAsync("Painkiller", id);

        var result = await DeleteAsync(id);

        // Refused rather than taken out of the recipes: a bottle quietly vanishing from a recipe would
        // change what that recipe says, and whether it is makeable, without anyone deciding to.
        Assert.Equal(RemoveIngredientOutcome.InUse, result.Outcome);
        Assert.Equal(["Painkiller", "Tiki Sour"], result.UsedIn);
        Assert.Equal((true, true), await ExistsAsync(id));
    }

    [Fact]
    public async Task ASharedBottle_CannotBeDeleted()
    {
        await SeedAsync();
        Guid campari;
        await using (var db = Fixture.CreateContext())
            campari = await db.Ingredients.IgnoreQueryFilters()
                .Where(i => i.TenantId == null && i.Name == "Campari").Select(i => i.Id).SingleAsync();

        Assert.Equal(RemoveIngredientOutcome.ReadOnly, (await DeleteAsync(campari)).Outcome);
        Assert.True((await ExistsAsync(campari)).Ingredient);
    }

    [Fact]
    public async Task AnotherHouseholdsBottle_IsNotFound_AndStays()
    {
        await SeedAsync();
        var theirs = await AddAsync("Their secret bitters", Guid.CreateVersion7());

        // Not found rather than forbidden: a household must not learn another's bottles exist (JJ-031).
        Assert.Equal(RemoveIngredientOutcome.NotFound, (await DeleteAsync(theirs)).Outcome);
        Assert.Equal((true, true), await ExistsAsync(theirs));
    }
}
