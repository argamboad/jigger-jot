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
/// INV-2 (FEATURES §8): adding a custom ingredient inline — the bullet INV-1 shipped without.
/// <para>
/// This is the app's <b>first write to a dual-natured catalog table</b>, and that is what makes it
/// worth more tests than its size suggests. Three platform guarantees skip these tables (JJ-031):
/// nothing stamps <c>TenantId</c>, no CI gate covers their RLS policy, and the dissolution canary
/// cannot see them. The first is a line of code that is easy to forget and impossible to notice; the
/// third only becomes reachable the moment a household can create one of these rows, which is now.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CustomIngredientTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private readonly Guid _household = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    private static InventoryHandler Handler(AppDbContext db, Guid tenantId) =>
        new(new EfRepository<TenantInventory>(db),
            new EfRepository<Ingredient>(db),
            new EfRepository<IngredientCategory>(db),
            new TestCurrentTenant { TenantId = tenantId },
            new FakeTimeProvider(Now));

    private async Task SeedAsync()
    {
        await using var db = Fixture.CreateContext();
        await new CatalogSeeder(db, NullLogger<CatalogSeeder>.Instance).SeedAsync();
    }

    /// <summary>A top-level category and one of its own subcategories.</summary>
    private async Task<(Guid Category, Guid Subcategory)> ACategoryPairAsync()
    {
        await using var db = Fixture.CreateContext();
        var child = await db.IngredientCategories.Where(c => c.ParentId != null).OrderBy(c => c.Name).FirstAsync();
        return (child.ParentId!.Value, child.Id);
    }

    private async Task<AddIngredientResult> AddAsync(
        string name, Guid category, Guid? subcategory = null, Guid? household = null)
    {
        await using var db = Fixture.CreateContext(household ?? _household);
        return await Handler(db, household ?? _household)
            .AddIngredientAsync(new AddIngredientRequest(name, category, subcategory), default);
    }

    private async Task<IReadOnlyList<ShelfItem>> ShelfAsync(Guid? household = null)
    {
        await using var db = Fixture.CreateContext(household ?? _household);
        return await Handler(db, household ?? _household).ListAsync(default);
    }

    [Fact]
    public async Task AddingOne_PutsItOnMyShelf_Ticked()
    {
        await SeedAsync();
        var (category, subcategory) = await ACategoryPairAsync();

        var result = await AddAsync("Homemade coffee liqueur", category, subcategory);

        Assert.Equal(AddIngredientOutcome.Created, result.Outcome);
        var item = result.Item!;
        Assert.Equal("Homemade coffee liqueur", item.Name);
        Assert.True(item.IsOwn);

        // Ticked, not merely tickable. Someone adds a bottle to their shelf because it is on their
        // shelf; making them add it and then tick it is two actions for one intent.
        Assert.True(item.IsAvailable);
        Assert.Contains(await ShelfAsync(), i => i.Id == item.Id && i.IsAvailable);
    }

    [Fact]
    public async Task TheRowCarriesMyTenantId_BecauseNothingStampsIt()
    {
        await SeedAsync();
        var (category, _) = await ACategoryPairAsync();

        var id = (await AddAsync("Barrel-aged something", category)).Item!.Id;

        await using var db = Fixture.CreateContext();
        var row = await db.Ingredients.IgnoreQueryFilters().SingleAsync(i => i.Id == id);

        // JJ-031: TenantStampingInterceptor keys off ITenantScoped, which this table deliberately is
        // not. A row written without setting this explicitly would land in the SHARED catalog, where
        // every household on the platform would see one person's homemade liqueur.
        Assert.Equal(_household, row.TenantId);
    }

    [Fact]
    public async Task AnotherHouseholdNeverSeesIt()
    {
        await SeedAsync();
        var (category, _) = await ACategoryPairAsync();
        var id = (await AddAsync("My secret bitters", category)).Item!.Id;

        var stranger = Guid.CreateVersion7();

        Assert.DoesNotContain(await ShelfAsync(stranger), i => i.Id == id);

        // And they cannot reach it by id either, which is what keeps the 404 on the tick endpoint
        // from being a polite fiction.
        await using var db = Fixture.CreateContext(stranger);
        Assert.False(await Handler(db, stranger).SetAsync(id, true, default));
    }

    [Fact]
    public async Task TheSameNameTwice_IsRefused_AndPointsAtWhatIsAlreadyThere()
    {
        await SeedAsync();
        var (category, _) = await ACategoryPairAsync();
        var first = (await AddAsync("Sloe gin infusion", category)).Item!.Id;

        // Different casing on purpose: two shelf rows differing only in capitals is a data-entry slip,
        // not a second ingredient.
        var again = await AddAsync("SLOE GIN INFUSION", category);

        Assert.Equal(AddIngredientOutcome.AlreadyExists, again.Outcome);
        Assert.Equal(first, again.ExistingIngredientId);

        await using var db = Fixture.CreateContext(_household);
        Assert.Equal(1, await db.Ingredients.CountAsync(i => i.TenantId == _household));
    }

    [Fact]
    public async Task AShadowOfACatalogIngredient_IsRefused_AndPointsAtTheCatalogRow()
    {
        await SeedAsync();
        var (category, _) = await ACategoryPairAsync();

        Guid shared;
        await using (var db = Fixture.CreateContext())
            shared = await db.Ingredients.IgnoreQueryFilters()
                .Where(i => i.TenantId == null && i.Name == "Campari").Select(i => i.Id).SingleAsync();

        var result = await AddAsync("campari", category);

        // The unique index does not catch this — it is keyed on (TenantId, Name), so a household's
        // "campari" and the catalog's "Campari" are different rows to the database. But two Camparis
        // on one shelf is nobody's intent, and a custom ingredient satisfies a recipe line by exact
        // name only (JJ-018), so the shadow would quietly not do what its owner expects.
        Assert.Equal(AddIngredientOutcome.AlreadyExists, result.Outcome);
        Assert.Equal(shared, result.ExistingIngredientId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ANameThatIsNotAName_IsRefused(string name)
    {
        await SeedAsync();
        var (category, _) = await ACategoryPairAsync();

        Assert.Equal(AddIngredientOutcome.InvalidName, (await AddAsync(name, category)).Outcome);
    }

    [Fact]
    public async Task TheNameIsTrimmed()
    {
        await SeedAsync();
        var (category, _) = await ACategoryPairAsync();

        var item = (await AddAsync("  Spiced pear syrup  ", category)).Item!;

        // Otherwise the duplicate check above is defeated by a space, and the shelf sorts oddly.
        Assert.Equal("Spiced pear syrup", item.Name);
    }

    [Fact]
    public async Task AnUnknownCategory_IsRefused_AndWritesNothing()
    {
        await SeedAsync();

        var result = await AddAsync("Orphan", Guid.CreateVersion7());

        Assert.Equal(AddIngredientOutcome.InvalidCategory, result.Outcome);

        await using var db = Fixture.CreateContext(_household);
        Assert.Empty(await db.Ingredients.Where(i => i.TenantId == _household).ToListAsync());
    }

    [Fact]
    public async Task ASubcategoryOfSomeOtherCategory_IsRefused()
    {
        await SeedAsync();
        var (category, _) = await ACategoryPairAsync();

        Guid foreignChild;
        await using (var db = Fixture.CreateContext())
            foreignChild = await db.IngredientCategories
                .Where(c => c.ParentId != null && c.ParentId != category)
                .Select(c => c.Id).FirstAsync();

        // "Dark rum" under "Bitters" would file the ingredient somewhere nobody would look for it,
        // and JJ-016 makes the parent match all its children — so a mismatched pair breaks filtering
        // as well as browsing.
        Assert.Equal(AddIngredientOutcome.InvalidCategory,
            (await AddAsync("Misfiled", category, foreignChild)).Outcome);
    }

    [Fact]
    public async Task ASubcategoryPassedAsTheCategory_IsRefused()
    {
        await SeedAsync();
        var (_, subcategory) = await ACategoryPairAsync();

        // The tree is deliberately two levels (JJ-015). Accepting a child here would quietly make it
        // three for that one row.
        Assert.Equal(AddIngredientOutcome.InvalidCategory,
            (await AddAsync("Too deep", subcategory)).Outcome);
    }

    [Fact]
    public async Task NoSubcategory_IsFine()
    {
        await SeedAsync();
        var (category, _) = await ACategoryPairAsync();

        var result = await AddAsync("Top level thing", category);

        // Plenty of the shared catalog sits at the top level; a household's own may too.
        Assert.Equal(AddIngredientOutcome.Created, result.Outcome);
        Assert.Null(result.Item!.Subcategory);
    }

    [Fact]
    public async Task TheCategoryList_IsTheTwoLevelTree()
    {
        await SeedAsync();

        await using var db = Fixture.CreateContext(_household);
        var tree = await Handler(db, _household).CategoriesAsync(default);

        // The picker needs the tree, and the shelf response only carries names. Curated and global —
        // no household additions (JJ-022) — so this is a read and nothing else.
        Assert.NotEmpty(tree);
        Assert.All(tree, c => Assert.NotEqual(Guid.Empty, c.Id));
        Assert.Contains(tree, c => c.Subcategories.Count > 0);
        Assert.Equal([.. tree.Select(c => c.Name).Order(StringComparer.Ordinal)], [.. tree.Select(c => c.Name)]);
    }

    [Fact]
    public async Task DissolvingTheHousehold_TakesTheIngredientAndItsShelfRow()
    {
        await SeedAsync();
        var (category, _) = await ACategoryPairAsync();
        var id = (await AddAsync("Goes away with me", category)).Item!.Id;

        int sharedBefore;
        await using (var db = Fixture.CreateContext())
            sharedBefore = await db.Ingredients.IgnoreQueryFilters().CountAsync(i => i.TenantId == null);

        await using (var db = Fixture.CreateContext(_household))
        {
            await new InventoryDataContributor(new EfRepository<TenantInventory>(db)).WipeAsync(_household);
            await new CatalogDataContributor(
                new EfRepository<Ingredient>(db),
                new EfRepository<Cocktail>(db),
                new EfRepository<CocktailIngredient>(db)).WipeAsync(_household);
        }

        // The reason this test exists: the platform's dissolution canary only sees a NON-nullable
        // TenantId, so it cannot see this table at all (JJ-031). Until INV-2 no household could put a
        // row in it, and the gap was theoretical. It is not any more.
        await using (var db = Fixture.CreateContext())
        {
            Assert.Empty(await db.Ingredients.IgnoreQueryFilters().Where(i => i.Id == id).ToListAsync());
            Assert.Empty(await db.TenantInventories.IgnoreQueryFilters()
                .Where(i => i.IngredientId == id).ToListAsync());
            Assert.Equal(sharedBefore, await db.Ingredients.IgnoreQueryFilters().CountAsync(i => i.TenantId == null));
        }
    }
}
