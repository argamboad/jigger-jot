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
/// CKTL-3: one cocktail, whole. The conversion arithmetic has its own tests in Core; what is checked
/// here is that the right preference reaches it, that the authored values survive the round trip, and
/// that a cocktail belonging to someone else is simply not found.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CocktailDetailTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private static CocktailDetailHandler Handler(AppDbContext db) =>
        new(new EfRepository<Cocktail>(db), new UserRepository(db));

    private async Task SeedAsync()
    {
        await using var db = Fixture.CreateContext();
        await new CatalogSeeder(db, NullLogger<CatalogSeeder>.Instance).SeedAsync();
    }

    private async Task<Guid> NegroniIdAsync()
    {
        await using var db = Fixture.CreateContext();
        return (await db.Cocktails.IgnoreQueryFilters().FirstAsync(c => c.Name == "Negroni")).Id;
    }

    private async Task<Guid> UserAsync(UnitSystem? preference)
    {
        await using var db = Fixture.CreateContext();
        var user = new User { Email = $"detail-{Guid.CreateVersion7():N}@example.com", PreferredUnitSystem = preference };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task Detail_ReturnsTheWholeRecipe_InDisplayOrder()
    {
        await SeedAsync();
        var id = await NegroniIdAsync();

        await using var db = Fixture.CreateContext(Guid.CreateVersion7());
        var detail = await Handler(db).GetAsync(id, await UserAsync(null), default);

        Assert.NotNull(detail);
        Assert.Equal("Negroni", detail!.Name);
        Assert.Equal(3, detail.Lines.Count);
        // The curated name, not the book's: SEED-2 maps the IBA's bare "Gin" onto "London dry
        // gin", which is the point of having a curation step between the extraction and the app.
        Assert.Equal("London dry gin", detail.Lines[0].Ingredient);
        Assert.False(detail.IsOwn);
        Assert.Contains("gently", detail.Instructions ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Detail_CreditsItsSource_WithTheAttributionText()
    {
        await SeedAsync();
        var id = await NegroniIdAsync();

        await using var db = Fixture.CreateContext(Guid.CreateVersion7());
        var detail = await Handler(db).GetAsync(id, await UserAsync(null), default);

        // The credit is a property of the row (JJ-032), so it arrives with the recipe rather than
        // being assembled from a lookup the client has to know about.
        Assert.NotNull(detail!.Source);
        Assert.Equal("IBA Official Cocktails", detail.Source!.Name);
        Assert.False(string.IsNullOrWhiteSpace(detail.Source.Attribution));
    }

    [Fact]
    public async Task Detail_ConvertsAmountsToTheReadersPreference_AndKeepsTheAuthoredOnes()
    {
        await SeedAsync();
        var id = await NegroniIdAsync();

        await using var db = Fixture.CreateContext(Guid.CreateVersion7());
        var metric = await Handler(db).GetAsync(id, await UserAsync(UnitSystem.Metric), default);
        var imperial = await Handler(db).GetAsync(id, await UserAsync(UnitSystem.Imperial), default);

        // The Negroni is authored in millilitres, so a metric reader sees it untouched and an
        // imperial reader sees ounces — while BOTH responses still carry the authored 30 ml, because
        // the stored value never changes (JJ-007).
        Assert.Equal("30 ml", metric!.Lines[0].Display);
        Assert.Equal("1 oz", imperial!.Lines[0].Display);

        Assert.Equal(30m, metric.Lines[0].Amount);
        Assert.Equal(30m, imperial.Lines[0].Amount);
        Assert.Equal("ml", imperial.Lines[0].Unit);
    }

    [Fact]
    public async Task Detail_WithNoPreference_ShowsTheRecipeAsAuthored()
    {
        await SeedAsync();
        var id = await NegroniIdAsync();

        await using var db = Fixture.CreateContext(Guid.CreateVersion7());
        var detail = await Handler(db).GetAsync(id, await UserAsync(null), default);

        Assert.Equal("30 ml", detail!.Lines[0].Display);
    }

    [Fact]
    public async Task Detail_ProportionalRecipe_ReadsAsTheFractionTheBookWrote()
    {
        await SeedAsync();

        Guid id;
        await using (var read = Fixture.CreateContext())
        {
            var part = await read.Units.SingleAsync(u => u.Name == "part");
            id = await read.CocktailIngredients.IgnoreQueryFilters()
                .Where(l => l.UnitId == part.Id && l.Amount == 0.6667m)
                .Select(l => l.CocktailId)
                .FirstAsync();
        }

        await using var db = Fixture.CreateContext(Guid.CreateVersion7());
        // Even for a reader who asked for metric: a neutral unit has no factor and never converts, so
        // the 1930 proportion survives intact instead of becoming invented millilitres.
        var detail = await Handler(db).GetAsync(id, await UserAsync(UnitSystem.Metric), default);

        Assert.Contains(detail!.Lines, l => l.Display == "2/3 part");
    }

    [Fact]
    public async Task Detail_MarksGarnishesOptional()
    {
        await SeedAsync();
        var id = await NegroniIdAsync();

        await using var db = Fixture.CreateContext(Guid.CreateVersion7());
        var detail = await Handler(db).GetAsync(id, await UserAsync(null), default);

        Assert.All(detail!.Lines, l => Assert.Equal(l.Role != nameof(RecipeRole.Garnish), l.IsRequired));
    }

    [Fact]
    public async Task Detail_OfAnotherHouseholdsCocktail_IsNotFound()
    {
        await SeedAsync();

        var stranger = Guid.CreateVersion7();
        Guid theirs;
        await using (var db = Fixture.CreateContext())
        {
            var lookups = new CatalogLookups(
                (await db.IngredientCategories.FirstAsync(c => c.ParentId == null)).Id,
                (await db.GlassTypes.FirstAsync()).Id,
                (await db.Methods.FirstAsync()).Id,
                (await db.Units.FirstAsync()).Id);
            var ingredient = await db.Ingredients.FirstAsync(i => i.TenantId == null);
            theirs = (await CatalogSeed.CocktailAsync(db, lookups, "Their Secret", stranger, ingredient.Id)).Id;
        }

        await using var db2 = Fixture.CreateContext(Guid.CreateVersion7());
        // Not found rather than forbidden: the filter simply does not return the row, and saying
        // "forbidden" would confirm it exists.
        Assert.Null(await Handler(db2).GetAsync(theirs, await UserAsync(null), default));

        // ...and its owner still sees it, so this is scoping rather than a broken query.
        await using var owner = Fixture.CreateContext(stranger);
        Assert.NotNull(await Handler(owner).GetAsync(theirs, await UserAsync(null), default));
    }

    [Fact]
    public async Task Detail_OfSomethingThatDoesNotExist_IsNotFound()
    {
        await SeedAsync();

        await using var db = Fixture.CreateContext(Guid.CreateVersion7());
        Assert.Null(await Handler(db).GetAsync(Guid.CreateVersion7(), await UserAsync(null), default));
    }
}
