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

        var recipes = CatalogSeeder.LoadCocktails();
        var expected = file.GlassTypes.Count + file.Methods.Count + file.Units.Count
                       + file.IngredientCategories.Count
                       + file.IngredientCategories.Sum(c => c.Children.Count)
                       + CatalogSeeder.LoadIngredients().Ingredients.Count
                       + recipes.Sources.Count
                       + recipes.Cocktails.Count + recipes.Cocktails.Sum(c => c.Lines.Count);
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
    public async Task Seed_WritesTheIngredientCatalog_AsSharedRows()
    {
        var file = CatalogSeeder.LoadIngredients();

        await using (var db = Fixture.CreateContext())
            await Build(db).SeedAsync();

        await using var read = Fixture.CreateContext();

        // Every one of them shared: TenantId null, owned by no household, readable by all (JJ-011).
        // A stamped row would be invisible to every household but the one that seeded it.
        Assert.Equal(file.Ingredients.Count,
            await read.Ingredients.IgnoreQueryFilters().CountAsync(i => i.TenantId == null));
        Assert.Empty(await read.Ingredients.IgnoreQueryFilters().Where(i => i.TenantId != null).ToListAsync());
    }

    [Fact]
    public async Task Seed_HangsEveryIngredientOffARealCategory()
    {
        await using (var db = Fixture.CreateContext())
            await Build(db).SeedAsync();

        await using var read = Fixture.CreateContext();
        var categories = await read.IngredientCategories.ToDictionaryAsync(c => c.Id);
        var ingredients = await read.Ingredients.IgnoreQueryFilters().ToListAsync();

        // The category ids are DERIVED, not looked up, so a typo in the curation file would produce a
        // foreign key pointing at nothing. The database would reject it — this says so out loud, and
        // catches the subtler case where a subcategory is hung off the wrong parent.
        Assert.All(ingredients, i =>
        {
            Assert.True(categories.ContainsKey(i.CategoryId), $"{i.Name}: category row missing");
            Assert.Null(categories[i.CategoryId].ParentId);

            if (i.SubcategoryId is null) return;
            Assert.True(categories.ContainsKey(i.SubcategoryId.Value), $"{i.Name}: subcategory row missing");
            Assert.Equal(i.CategoryId, categories[i.SubcategoryId.Value].ParentId);
        });
    }

    [Fact]
    public async Task Seed_LeavesAHouseholdsOwnIngredientAlone()
    {
        await using (var db = Fixture.CreateContext())
            await Build(db).SeedAsync();

        var household = Guid.CreateVersion7();
        await using (var db = Fixture.CreateContext())
        {
            // A household ingredient sharing a name with a shared one. Nothing stamps these, so the
            // TenantId is set at the call site (JJ-031) — which is also what a real fork will do.
            var category = await db.IngredientCategories.FirstAsync(c => c.ParentId == null);
            db.Ingredients.Add(new Ingredient
            {
                Name = "Absinthe", TenantId = household, CategoryId = category.Id,
            });
            await db.SaveChangesAsync();
        }

        int second;
        await using (var db = Fixture.CreateContext())
            second = await Build(db).SeedAsync();

        // The seeder counts only shared rows as already-seeded, so a household's own row neither
        // suppresses a seed nor gets counted as one. Both directions matter: the reverse bug would
        // have the seeder skip an ingredient because some household happened to add it first.
        Assert.Equal(0, second);

        await using var read = Fixture.CreateContext();
        Assert.Equal(2, await read.Ingredients.IgnoreQueryFilters().CountAsync(i => i.Name == "Absinthe"));
        Assert.Single(await read.Ingredients.IgnoreQueryFilters()
            .Where(i => i.Name == "Absinthe" && i.TenantId == household).ToListAsync());
    }

    [Fact]
    public async Task Seed_WritesTheRecipeCatalog_WithItsLinesAndSources()
    {
        var file = CatalogSeeder.LoadCocktails();

        await using (var db = Fixture.CreateContext())
            await Build(db).SeedAsync();

        await using var read = Fixture.CreateContext();

        Assert.Equal(file.Sources.Count, await read.RecipeSources.CountAsync());
        Assert.Equal(file.Cocktails.Count,
            await read.Cocktails.IgnoreQueryFilters().CountAsync(c => c.TenantId == null));
        Assert.Equal(file.Cocktails.Sum(c => c.Lines.Count),
            await read.CocktailIngredients.IgnoreQueryFilters().CountAsync(l => l.TenantId == null));

        // A line carries its parent's nature (JJ-031). A shared cocktail whose lines were stamped
        // would read as an empty recipe to every household, which is the kind of bug that looks like
        // a UI problem for a week.
        Assert.Empty(await read.CocktailIngredients.IgnoreQueryFilters()
            .Where(l => l.TenantId != null).ToListAsync());
    }

    [Fact]
    public async Task Seed_CreditsEveryRecipeToASource()
    {
        await using (var db = Fixture.CreateContext())
            await Build(db).SeedAsync();

        await using var read = Fixture.CreateContext();
        var sources = await read.RecipeSources.ToDictionaryAsync(s => s.Id);
        var cocktails = await read.Cocktails.IgnoreQueryFilters().ToListAsync();

        // The whole point of the source column (JJ-032): a credit that is a property of the row, so
        // "which of these came from the IBA" is a query rather than an archaeology exercise.
        Assert.All(cocktails, c =>
        {
            Assert.NotNull(c.SourceId);
            Assert.True(sources.ContainsKey(c.SourceId!.Value), $"{c.Name}: source row missing");
        });
        Assert.All(sources.Values, s => Assert.False(string.IsNullOrWhiteSpace(s.Attribution)));
    }

    [Fact]
    public async Task Seed_KeepsBothRecipesWhenTwoSourcesShareAName()
    {
        await using (var db = Fixture.CreateContext())
            await Build(db).SeedAsync();

        await using var read = Fixture.CreateContext();
        var fizzes = await read.Cocktails.IgnoreQueryFilters()
            .Where(c => c.Name == "Gin Fizz")
            .Include(c => c.Source)
            .ToListAsync();

        // Four names appear in both books. A 1930 Gin Fizz and the IBA's are different drinks that
        // share a name, so the seed id is keyed on the source too — keyed on name alone, one would
        // silently replace the other and the loss would show up as a missing drink, never an error.
        Assert.Equal(2, fizzes.Count);
        Assert.Equal(2, fizzes.Select(f => f.SourceId).Distinct().Count());
    }

    [Fact]
    public async Task Seed_LeavesGlassAndMethodNull_WhenTheRecipeDidNotSay()
    {
        await using (var db = Fixture.CreateContext())
            await Build(db).SeedAsync();

        await using var read = Fixture.CreateContext();
        var cocktails = await read.Cocktails.IgnoreQueryFilters().ToListAsync();

        // JJ-034. A quarter of the catalog states no glass or states one that is not a glass type,
        // and filling those in would be indistinguishable afterwards from a fact somebody wrote down.
        Assert.Contains(cocktails, c => c.GlassTypeId is null);
        Assert.Contains(cocktails, c => c.MethodId is null);
        // ...but most DO say, so a mapping that quietly resolved nothing would not pass here.
        Assert.True(cocktails.Count(c => c.GlassTypeId is not null) > cocktails.Count / 2);
    }

    [Fact]
    public async Task Seed_StoresProportionalAmountsAgainstThePartUnit()
    {
        await using (var db = Fixture.CreateContext())
            await Build(db).SeedAsync();

        await using var read = Fixture.CreateContext();
        var part = await read.Units.SingleAsync(u => u.Name == "part");
        var proportional = await read.CocktailIngredients.IgnoreQueryFilters()
            .Where(l => l.UnitId == part.Id)
            .ToListAsync();

        // The 1930 recipes are proportional — "2/3 gin, 1/3 vermouth" with no absolute volume in the
        // book at all. Stored as authored (JJ-007) against a neutral unit that never converts.
        Assert.NotEmpty(proportional);
        Assert.Null(part.MillilitreFactor);
        Assert.All(proportional, l => Assert.True(l.Amount > 0));

        // Both shapes are real and both are as authored: most lines are a bare fraction of the
        // drink, and a handful say "2 Parts" outright. Asserting only the first would have made a
        // rule out of the common case.
        Assert.Contains(proportional, l => l.Amount < 1);
        Assert.Contains(proportional, l => l.Amount >= 1);
    }

    [Fact]
    public async Task Seed_MarksGarnishesOptional_AndEverythingElseRequired()
    {
        await using (var db = Fixture.CreateContext())
            await Build(db).SeedAsync();

        await using var read = Fixture.CreateContext();
        var lines = await read.CocktailIngredients.IgnoreQueryFilters().ToListAsync();

        // A garnish is just an optional line, and optional lines never block makeability (JJ-009).
        // If this inverted, every drink with a mint sprig would become unmakeable without mint.
        Assert.All(lines, l => Assert.Equal(l.Role != RecipeRole.Garnish, l.IsRequired));
        Assert.Contains(lines, l => l.Role == RecipeRole.Garnish);
        Assert.Contains(lines, l => l.Role == RecipeRole.Base);
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
