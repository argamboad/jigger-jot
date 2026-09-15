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
/// AUTHORING-2 (FEATURES §13, §14): a household edits a cocktail it owns — one it wrote, or one it
/// forked. The shared catalog stays read-only (JJ-002): the way to change a book's recipe is to fork it,
/// and the fork is then the household's to edit. Last save wins; there is no lock.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CocktailEditingTests(PostgresFixture fixture) : PostgresTestBase(fixture)
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

    private async Task<Guid> CoupeAsync()
    {
        await using var db = Fixture.CreateContext();
        return await db.GlassTypes.Where(g => g.Name == "Coupe").Select(g => (Guid?)g.Id).SingleOrDefaultAsync()
               ?? await db.GlassTypes.Select(g => g.Id).FirstAsync();
    }

    private async Task<Guid> ReaderAsync(UnitSystem? preference)
    {
        await using var db = Fixture.CreateContext();
        var user = new User { Email = $"editor-{Guid.CreateVersion7():N}@example.com", PreferredUnitSystem = preference };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<AuthorLineRequest> LineAsync(string ingredient, decimal? amount, string? unit,
        RecipeRole role = RecipeRole.Base, bool required = true) =>
        new(await IngredientAsync(ingredient), amount, unit is null ? null : await UnitAsync(unit), required, role, null);

    private async Task<Guid> WriteAsync(AuthorCocktailRequest request, Guid? household = null)
    {
        await using var db = Fixture.CreateContext(household ?? _household);
        return (await Authoring(db, household ?? _household).CreateAsync(request, default)).Id!.Value;
    }

    private async Task<AuthorCocktailResult> EditAsync(Guid id, AuthorCocktailRequest request, Guid? household = null)
    {
        await using var db = Fixture.CreateContext(household ?? _household);
        return await Authoring(db, household ?? _household).UpdateAsync(id, request, default);
    }

    private async Task<CocktailDraftResult> DraftAsync(Guid id, Guid? reader = null, Guid? household = null)
    {
        await using var db = Fixture.CreateContext(household ?? _household);
        return await Authoring(db, household ?? _household).DraftAsync(id, reader, default);
    }

    private async Task<AuthorCocktailRequest> HouseSourAsync(string name = "House Sour") =>
        new(name, null, null, ServingType.FullDrink, "Shake hard.",
        [
            await LineAsync("London dry gin", 2m, "oz"),
            await LineAsync("Lemon juice", 1m, "oz", RecipeRole.Juice),
        ]);

    // Lines as one string, not a List: a tuple compares its members with their own Equals, and a List's
    // is reference equality, so two identical recipes would never compare equal.
    private async Task<(string Name, string? Instructions, Guid? Glass, Guid? Forked, Guid? Tenant, string Lines)> StoredAsync(Guid id)
    {
        await using var db = Fixture.CreateContext();
        var c = await db.Cocktails.IgnoreQueryFilters().SingleAsync(x => x.Id == id);
        var lines = await db.CocktailIngredients.IgnoreQueryFilters()
            .Where(l => l.CocktailId == id)
            .OrderBy(l => l.DisplayOrder)
            .Select(l => new { l.DisplayOrder, Ingredient = l.Ingredient!.Name, l.Amount, Unit = l.Unit == null ? null : l.Unit.Name, l.Role, l.IsRequired, l.TenantId })
            .ToListAsync();
        Assert.All(lines, l => Assert.Equal(c.TenantId, l.TenantId));
        return (c.Name, c.Instructions, c.GlassTypeId, c.ForkedFromCocktailId, c.TenantId,
                string.Join(" | ", lines.Select(l => $"{l.DisplayOrder}:{l.Ingredient} {l.Amount:0.##} {l.Unit} {l.Role}{(l.IsRequired ? "" : " optional")}")));
    }

    [Fact]
    public async Task EditingMyCocktail_ReplacesItsFieldsAndItsLines()
    {
        await SeedAsync();
        var id = await WriteAsync(await HouseSourAsync());
        var coupe = await CoupeAsync();

        var result = await EditAsync(id, new AuthorCocktailRequest("House Sour, Stronger", coupe, null,
            ServingType.FullDrink, "Shake harder.",
            [
                await LineAsync("London dry gin", 60m, "ml"),
                await LineAsync("Lime juice", 0.75m, "oz", RecipeRole.Juice),
                await LineAsync("Angostura bitters", 2m, "dash", RecipeRole.Bitters),
            ]));

        Assert.Equal(AuthorCocktailOutcome.Updated, result.Outcome);
        Assert.Equal(id, result.Id);

        var stored = await StoredAsync(id);
        Assert.Equal("House Sour, Stronger", stored.Name);
        Assert.Equal("Shake harder.", stored.Instructions);
        Assert.Equal(coupe, stored.Glass);
        // Every line replaced, in the new order, and the 60 ml stored as ounces like any write (JJ-041).
        Assert.Equal("0:London dry gin 2 oz Base | 1:Lime juice 0.75 oz Juice | 2:Angostura bitters 2 dash Bitters",
                     stored.Lines);
    }

    [Fact]
    public async Task EditingAFork_KeepsWhatItWasBasedOn_AndLeavesTheOriginalAlone()
    {
        await SeedAsync();
        Guid negroni;
        await using (var read = Fixture.CreateContext())
            negroni = await read.Cocktails.IgnoreQueryFilters().Where(c => c.Name == "Negroni").Select(c => c.Id).FirstAsync();

        Guid fork;
        await using (var db = Fixture.CreateContext(_household))
            fork = (await new CocktailForkHandler(new EfRepository<Cocktail>(db), new TestCurrentTenant { TenantId = _household })
                .ForkAsync(negroni, default))!.Value;
        var originalBefore = await StoredAsync(negroni);

        var result = await EditAsync(fork, new AuthorCocktailRequest("Our Negroni", null, null, ServingType.FullDrink, null,
            [await LineAsync("London dry gin", 1.5m, "oz"), await LineAsync("Campari", 1m, "oz", RecipeRole.Modifier)]));

        Assert.Equal(AuthorCocktailOutcome.Updated, result.Outcome);
        var edited = await StoredAsync(fork);
        Assert.Equal(negroni, edited.Forked);
        Assert.Equal(_household, edited.Tenant);
        // A snapshot, not a reference (JJ-013): the book's Negroni is exactly as it was.
        Assert.Equal(originalBefore, await StoredAsync(negroni));
    }

    [Fact]
    public async Task TheSharedCatalog_IsReadOnly_ForkItToChangeIt()
    {
        await SeedAsync();
        Guid negroni;
        await using (var read = Fixture.CreateContext())
            negroni = await read.Cocktails.IgnoreQueryFilters().Where(c => c.Name == "Negroni").Select(c => c.Id).FirstAsync();
        var before = await StoredAsync(negroni);

        // Visible to the household, so not a 404 — but not theirs to change (JJ-002).
        Assert.Equal(AuthorCocktailOutcome.ReadOnly, (await EditAsync(negroni, await HouseSourAsync())).Outcome);
        Assert.Equal(CocktailDraftOutcome.ReadOnly, (await DraftAsync(negroni)).Outcome);
        Assert.Equal(before, await StoredAsync(negroni));
    }

    [Fact]
    public async Task AnotherHouseholdsCocktail_IsNotFound_ForEditingOrOpening()
    {
        await SeedAsync();
        var stranger = Guid.CreateVersion7();
        var theirs = await WriteAsync(await HouseSourAsync("Their Sour"), stranger);
        var before = await StoredAsync(theirs);

        // Not found rather than forbidden: saying "forbidden" would confirm it exists (JJ-031).
        Assert.Equal(AuthorCocktailOutcome.NotFound, (await EditAsync(theirs, await HouseSourAsync())).Outcome);
        Assert.Equal(CocktailDraftOutcome.NotFound, (await DraftAsync(theirs)).Outcome);
        Assert.Equal(before, await StoredAsync(theirs));
    }

    [Fact]
    public async Task AnEditIsHeldToTheSameRulesAsWriting()
    {
        await SeedAsync();
        var id = await WriteAsync(await HouseSourAsync());
        var before = await StoredAsync(id);
        var sour = await HouseSourAsync();

        Assert.Equal(AuthorCocktailOutcome.InvalidName, (await EditAsync(id, sour with { Name = "  " })).Outcome);
        Assert.Equal(AuthorCocktailOutcome.NoLines, (await EditAsync(id, sour with { Lines = [] })).Outcome);
        Assert.Equal(AuthorCocktailOutcome.InvalidLine,
            (await EditAsync(id, sour with { Lines = [await LineAsync("London dry gin", null, "oz")] })).Outcome);

        // A refused edit writes nothing — least of all half a set of lines.
        Assert.Equal(before, await StoredAsync(id));
    }

    [Fact]
    public async Task OpeningToEdit_ReadsTheStoredAmountsInTheWritersOwnUnit()
    {
        await SeedAsync();
        var id = await WriteAsync(await HouseSourAsync());
        var ml = await UnitAsync("ml");
        var oz = await UnitAsync("oz");

        var metric = await DraftAsync(id, await ReaderAsync(UnitSystem.Metric));
        var imperial = await DraftAsync(id, await ReaderAsync(UnitSystem.Imperial));

        Assert.Equal(CocktailDraftOutcome.Found, metric.Outcome);
        var draft = metric.Draft!;
        Assert.Equal("House Sour", draft.Name);
        Assert.Equal("Shake hard.", draft.Instructions);
        Assert.Equal("FullDrink", draft.ServingType);

        // 2 oz and 1 oz stored; a metric writer types millilitres, so that is what the form opens with.
        Assert.Equal([(60m, (Guid?)ml), (30m, ml)], draft.Lines.Select(l => (l.Amount!.Value, l.UnitId)).ToList());
        Assert.Equal([(2m, (Guid?)oz), (1m, oz)], imperial.Draft!.Lines.Select(l => (l.Amount!.Value, l.UnitId)).ToList());
        Assert.Equal(["Base", "Juice"], draft.Lines.Select(l => l.Role));
    }

    [Fact]
    public async Task TwoEditsInARow_TheLastOneWins()
    {
        await SeedAsync();
        var id = await WriteAsync(await HouseSourAsync());

        await EditAsync(id, await HouseSourAsync("First Edit"));
        await EditAsync(id, await HouseSourAsync("Second Edit"));

        // No lock and no version check, deliberately: a household editing its own recipe book.
        Assert.Equal("Second Edit", (await StoredAsync(id)).Name);
    }
}
