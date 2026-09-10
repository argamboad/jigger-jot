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
/// AUTHORING-1 (FEATURES §14): a household writes a cocktail from scratch.
/// <para>
/// The interesting claim is the last line of the flow — "immediately participates in makeable /
/// filtering like any other cocktail". It does, and for free, because both are derived from the
/// recipe lines at query time rather than from anything stored (JJ-003, JJ-014). Two tests here prove
/// that rather than assume it, because "for free" is the kind of thing that stops being true quietly.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CocktailAuthoringTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private readonly Guid _household = Guid.CreateVersion7();

    private static CocktailAuthoringHandler Handler(AppDbContext db, Guid tenantId) =>
        new(new EfRepository<Cocktail>(db),
            new EfRepository<Ingredient>(db),
            new EfRepository<Unit>(db),
            new EfRepository<GlassType>(db),
            new EfRepository<Method>(db),
            new TestCurrentTenant { TenantId = tenantId });

    private async Task SeedAsync()
    {
        await using var db = Fixture.CreateContext();
        await new CatalogSeeder(db, NullLogger<CatalogSeeder>.Instance).SeedAsync();
    }

    private async Task<Guid> IngredientAsync(string name)
    {
        await using var db = Fixture.CreateContext();
        return await db.Ingredients.IgnoreQueryFilters()
            .Where(i => i.TenantId == null && i.Name == name).Select(i => i.Id).SingleAsync();
    }

    private async Task<Guid> UnitAsync(string name)
    {
        await using var db = Fixture.CreateContext();
        return await db.Units.Where(u => u.Name == name).Select(u => u.Id).SingleAsync();
    }

    private async Task<(Guid Glass, Guid Method)> LookupsAsync()
    {
        await using var db = Fixture.CreateContext();
        return (await db.GlassTypes.Select(g => g.Id).FirstAsync(),
                await db.Methods.Select(m => m.Id).FirstAsync());
    }

    private async Task<AuthorLineRequest> LineAsync(
        string ingredient, decimal? amount = 30m, string? unit = "ml",
        bool required = true, RecipeRole role = RecipeRole.Base, string? notes = null) =>
        new(await IngredientAsync(ingredient), amount,
            unit is null ? null : await UnitAsync(unit), required, role, notes);

    private async Task<AuthorCocktailResult> WriteAsync(
        AuthorCocktailRequest request, Guid? household = null)
    {
        await using var db = Fixture.CreateContext(household ?? _household);
        return await Handler(db, household ?? _household).CreateAsync(request, default);
    }

    private async Task<AuthorCocktailRequest> ANegroniOfMyOwnAsync(string name = "House Negroni") =>
        new(name, null, null, ServingType.FullDrink, "Stir over ice.",
        [
            await LineAsync("London dry gin"),
            await LineAsync("Campari", role: RecipeRole.Modifier),
            await LineAsync("Sweet vermouth", role: RecipeRole.Modifier),
        ]);

    private async Task<CocktailDetail?> ReadAsync(Guid id, Guid? household = null)
    {
        await using var db = Fixture.CreateContext(household ?? _household);
        return await new CocktailDetailHandler(
            new EfRepository<Cocktail>(db),
            new UserRepository(db),
            new EfRepository<TenantInventory>(db),
            new EfRepository<IngredientSubstitution>(db)).GetAsync(id, null, default);
    }

    [Fact]
    public async Task WritingOne_KeepsEveryLineInTheOrderIWroteThem()
    {
        await SeedAsync();

        var result = await WriteAsync(await ANegroniOfMyOwnAsync());

        Assert.Equal(AuthorCocktailOutcome.Created, result.Outcome);
        var recipe = (await ReadAsync(result.Id!.Value))!;

        Assert.Equal("House Negroni", recipe.Name);
        Assert.Equal("Stir over ice.", recipe.Instructions);

        // The order is the array's, not something the caller has to number. A form that made someone
        // type display positions would be a form nobody finishes.
        Assert.Equal(["London dry gin", "Campari", "Sweet vermouth"],
                     [.. recipe.Lines.Select(l => l.Ingredient)]);
    }

    [Fact]
    public async Task TheCocktailAndEveryLine_CarryMyTenantId()
    {
        await SeedAsync();
        var id = (await WriteAsync(await ANegroniOfMyOwnAsync())).Id!.Value;

        await using var db = Fixture.CreateContext();
        var cocktail = await db.Cocktails.IgnoreQueryFilters().SingleAsync(c => c.Id == id);
        var lines = await db.CocktailIngredients.IgnoreQueryFilters()
            .Where(l => l.CocktailId == id).ToListAsync();

        // JJ-031 again, on two tables. Nothing stamps either, and a row written without one lands in
        // the shared catalog where every household on the platform would see it.
        Assert.Equal(_household, cocktail.TenantId);
        Assert.All(lines, l => Assert.Equal(_household, l.TenantId));
    }

    [Fact]
    public async Task ItClaimsNoBookAndNoOriginal()
    {
        await SeedAsync();
        var id = (await WriteAsync(await ANegroniOfMyOwnAsync())).Id!.Value;

        await using var db = Fixture.CreateContext();
        var cocktail = await db.Cocktails.IgnoreQueryFilters().SingleAsync(c => c.Id == id);

        // Written here, not copied and not transcribed. A source would misattribute it to a book
        // (JJ-032) and a fork origin would claim a provenance it does not have (JJ-013).
        Assert.Null(cocktail.SourceId);
        Assert.Null(cocktail.ForkedFromCocktailId);
    }

    [Fact]
    public async Task ItIsMine_AndNobodyElsesToSee()
    {
        await SeedAsync();
        var id = (await WriteAsync(await ANegroniOfMyOwnAsync())).Id!.Value;

        Assert.True((await ReadAsync(id))!.IsOwn);
        Assert.Null(await ReadAsync(id, Guid.CreateVersion7()));
    }

    [Fact]
    public async Task ItIsMakeableTheMomentIHaveTheBottles()
    {
        await SeedAsync();
        var id = (await WriteAsync(await ANegroniOfMyOwnAsync())).Id!.Value;

        await using var db = Fixture.CreateContext(_household);
        var inventory = new InventoryHandler(
            new EfRepository<TenantInventory>(db),
            new EfRepository<Ingredient>(db),
            new EfRepository<IngredientCategory>(db),
            new TestCurrentTenant { TenantId = _household },
            new FakeTimeProvider(DateTimeOffset.UtcNow));
        foreach (var name in new[] { "London dry gin", "Campari", "Sweet vermouth" })
            await inventory.SetAsync(await IngredientAsync(name), true, default);

        var browse = new CocktailBrowseHandler(
            new EfRepository<Cocktail>(db),
            new EfRepository<TenantInventory>(db),
            new EfRepository<IngredientSubstitution>(db));
        var makeable = await browse.BrowseAsync(
            new CocktailBrowseRequest(null, 1, 100, MakeableOnly: true), default);

        // FEATURES §14's last line, and it costs nothing: makeability is derived from the lines at
        // query time (JJ-003), so a cocktail written a second ago is exactly as visible to the engine
        // as one seeded from a 1930 book.
        Assert.Contains(makeable.Items, c => c.Id == id);
    }

    [Fact]
    public async Task ItIsFilterableTheMomentItExists()
    {
        await SeedAsync();
        var id = (await WriteAsync(await ANegroniOfMyOwnAsync())).Id!.Value;

        await using var db = Fixture.CreateContext(_household);
        var browse = new CocktailBrowseHandler(
            new EfRepository<Cocktail>(db),
            new EfRepository<TenantInventory>(db),
            new EfRepository<IngredientSubstitution>(db));

        // Filtering is derived from the lines too, and there is no tag to remember to set (JJ-014).
        var page = await browse.BrowseAsync(
            new CocktailBrowseRequest(null, 1, 100) { Ingredient = "Campari" }, default);

        Assert.Contains(page.Items, c => c.Id == id);
    }

    [Fact]
    public async Task GlassAndMethodAreOptional()
    {
        await SeedAsync();

        var recipe = (await ReadAsync((await WriteAsync(await ANegroniOfMyOwnAsync())).Id!.Value))!;

        // JJ-034: a quarter of the seeded catalog never says which glass, and a household writing
        // down what it actually pours should not have to invent one either.
        Assert.Null(recipe.Glass);
        Assert.Null(recipe.Method);
    }

    [Fact]
    public async Task GlassAndMethodAreKeptWhenGiven()
    {
        await SeedAsync();
        var (glass, method) = await LookupsAsync();
        var request = await ANegroniOfMyOwnAsync() with { GlassTypeId = glass, MethodId = method };

        var recipe = (await ReadAsync((await WriteAsync(request)).Id!.Value))!;

        Assert.NotNull(recipe.Glass);
        Assert.NotNull(recipe.Method);
    }

    [Fact]
    public async Task TheSameIngredientMayAppearTwice()
    {
        await SeedAsync();
        var request = await ANegroniOfMyOwnAsync() with
        {
            Lines = [await LineAsync("London dry gin"), await LineAsync("London dry gin", 15m)],
        };

        var recipe = (await ReadAsync((await WriteAsync(request)).Id!.Value))!;

        // FEATURES §14 says so outright, and the schema agrees — there is deliberately no unique
        // index on (cocktail, ingredient). A split pour is a real thing to write down.
        Assert.Equal(2, recipe.Lines.Count);
        Assert.All(recipe.Lines, l => Assert.Equal("London dry gin", l.Ingredient));
    }

    [Fact]
    public async Task MyOwnCustomIngredient_CanGoInMyOwnRecipe()
    {
        await SeedAsync();

        Guid mine;
        await using (var db = Fixture.CreateContext(_household))
        {
            var inventory = new InventoryHandler(
                new EfRepository<TenantInventory>(db),
                new EfRepository<Ingredient>(db),
                new EfRepository<IngredientCategory>(db),
                new TestCurrentTenant { TenantId = _household },
                new FakeTimeProvider(DateTimeOffset.UtcNow));
            var category = await db.IngredientCategories.Where(c => c.ParentId == null).Select(c => c.Id).FirstAsync();
            mine = (await inventory.AddIngredientAsync(
                new AddIngredientRequest("Homemade coffee liqueur", category, null), default)).Item!.Id;
        }

        var request = await ANegroniOfMyOwnAsync() with
        {
            Lines = [new AuthorLineRequest(mine, 30m, await UnitAsync("ml"), true, RecipeRole.Base, null)],
        };

        // The point of INV-2 was that a household can name what it actually owns; this is where that
        // pays off. A custom ingredient satisfies the line by exact match (JJ-018), which is all this
        // recipe needs.
        Assert.Equal(AuthorCocktailOutcome.Created, (await WriteAsync(request)).Outcome);
    }

    [Fact]
    public async Task AnotherHouseholdsIngredient_IsRefused_AndWritesNothing()
    {
        await SeedAsync();

        var stranger = Guid.CreateVersion7();
        Guid theirs;
        await using (var db = Fixture.CreateContext(stranger))
        {
            var inventory = new InventoryHandler(
                new EfRepository<TenantInventory>(db),
                new EfRepository<Ingredient>(db),
                new EfRepository<IngredientCategory>(db),
                new TestCurrentTenant { TenantId = stranger },
                new FakeTimeProvider(DateTimeOffset.UtcNow));
            var category = await db.IngredientCategories.Where(c => c.ParentId == null).Select(c => c.Id).FirstAsync();
            theirs = (await inventory.AddIngredientAsync(
                new AddIngredientRequest("Their secret bitters", category, null), default)).Item!.Id;
        }

        var request = await ANegroniOfMyOwnAsync() with
        {
            Lines = [new AuthorLineRequest(theirs, 30m, null, true, RecipeRole.Base, null)],
        };

        var result = await WriteAsync(request);

        // A recipe line is a reference, and referencing a row you cannot see is how one household
        // learns another exists. The lines are checked against what THIS household can see.
        Assert.Equal(AuthorCocktailOutcome.UnknownIngredient, result.Outcome);

        await using var check = Fixture.CreateContext(_household);
        Assert.Empty(await check.Cocktails.Where(c => c.TenantId == _household).ToListAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ANameThatIsNotAName_IsRefused(string name)
    {
        await SeedAsync();

        Assert.Equal(AuthorCocktailOutcome.InvalidName,
            (await WriteAsync(await ANegroniOfMyOwnAsync(name))).Outcome);
    }

    [Fact]
    public async Task ARecipeWithNoLines_IsRefused()
    {
        await SeedAsync();
        var request = await ANegroniOfMyOwnAsync() with { Lines = [] };

        // Not merely empty — actively misleading. Makeability counts unsatisfied REQUIRED lines, so a
        // cocktail with none is "makeable" by the letter of the rule and would sit in the list of
        // things you can pour tonight, made of nothing.
        Assert.Equal(AuthorCocktailOutcome.NoLines, (await WriteAsync(request)).Outcome);
    }

    [Fact]
    public async Task AnAmountThatIsNotAnAmount_IsRefused()
    {
        await SeedAsync();

        foreach (var bad in new decimal?[] { 0m, -1m })
        {
            var request = await ANegroniOfMyOwnAsync() with
            {
                Lines = [await LineAsync("London dry gin", bad)],
            };
            Assert.Equal(AuthorCocktailOutcome.InvalidLine, (await WriteAsync(request)).Outcome);
        }
    }

    [Fact]
    public async Task AUnitWithNoAmount_IsRefused()
    {
        await SeedAsync();
        var request = await ANegroniOfMyOwnAsync() with
        {
            Lines = [await LineAsync("London dry gin", null, "ml")],
        };

        // "ml of gin" renders as nothing at all — the display formatter shows an empty string when
        // there is no amount — so this would write a line the author could never see. An amount with
        // no unit is fine and means what it says: one egg.
        Assert.Equal(AuthorCocktailOutcome.InvalidLine, (await WriteAsync(request)).Outcome);
    }

    [Fact]
    public async Task AnAmountWithNoUnit_IsFine()
    {
        await SeedAsync();
        var request = await ANegroniOfMyOwnAsync() with
        {
            Lines = [await LineAsync("Egg white", 1m, null)],
        };

        Assert.Equal(AuthorCocktailOutcome.Created, (await WriteAsync(request)).Outcome);
    }

    [Fact]
    public async Task AnUnknownGlassMethodOrUnit_IsRefused()
    {
        await SeedAsync();
        var stranger = Guid.CreateVersion7();

        Assert.Equal(AuthorCocktailOutcome.UnknownLookup,
            (await WriteAsync(await ANegroniOfMyOwnAsync() with { GlassTypeId = stranger })).Outcome);
        Assert.Equal(AuthorCocktailOutcome.UnknownLookup,
            (await WriteAsync(await ANegroniOfMyOwnAsync() with { MethodId = stranger })).Outcome);

        var badUnit = await ANegroniOfMyOwnAsync() with
        {
            Lines = [new AuthorLineRequest(await IngredientAsync("London dry gin"), 30m, stranger, true, RecipeRole.Base, null)],
        };
        Assert.Equal(AuthorCocktailOutcome.InvalidLine, (await WriteAsync(badUnit)).Outcome);
    }

    [Fact]
    public async Task TheFormOffersTheWholeCuratedLookups_NotJustWhatTheCatalogUses()
    {
        await SeedAsync();

        await using var db = Fixture.CreateContext(_household);
        var options = await Handler(db, _household).OptionsAsync(default);
        var browse = new CocktailBrowseHandler(
            new EfRepository<Cocktail>(db),
            new EfRepository<TenantInventory>(db),
            new EfRepository<IngredientSubstitution>(db));
        var filters = await browse.FilterOptionsAsync(default);

        // The two lists answer different questions and must not be quietly merged. A FILTER offering
        // a glass that returns nothing looks broken; a FORM that cannot reach a glass no seeded
        // recipe happens to use is broken (JJ-022).
        Assert.True(options.Glasses.Count > filters.Glasses.Count);
        Assert.NotEmpty(options.Units);
        Assert.Equal(Enum.GetNames<RecipeRole>().Length, options.Roles.Count);
        Assert.Equal(Enum.GetNames<ServingType>().Length, options.ServingTypes.Count);
    }

    [Fact]
    public async Task ItShowsUpWhenBrowsing_AndTwoOfMineMayShareAName()
    {
        await SeedAsync();
        var first = (await WriteAsync(await ANegroniOfMyOwnAsync())).Id!.Value;
        var second = (await WriteAsync(await ANegroniOfMyOwnAsync())).Id!.Value;

        await using var db = Fixture.CreateContext(_household);
        var browse = new CocktailBrowseHandler(
            new EfRepository<Cocktail>(db),
            new EfRepository<TenantInventory>(db),
            new EfRepository<IngredientSubstitution>(db));
        var page = await browse.BrowseAsync(new CocktailBrowseRequest("house negroni", 1, 100), default);

        // Nothing enforces unique cocktail names — the seeded catalog holds four names twice over —
        // and two attempts at the same drink is a normal thing for a person to have.
        Assert.Equal(2, page.Items.Count);
        Assert.All(page.Items, c => Assert.True(c.IsOwn));
        Assert.Contains(page.Items, c => c.Id == first);
        Assert.Contains(page.Items, c => c.Id == second);
    }
}
