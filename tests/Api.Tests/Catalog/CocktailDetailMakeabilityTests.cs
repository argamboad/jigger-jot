using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using JiggerJot.Api.Features.Catalog;
using JiggerJot.Api.Features.Inventory;
using JiggerJot.Api.Tests.Infrastructure;
using JiggerJot.Core.Catalog;
using JiggerJot.Core.Entities;
using JiggerJot.Infrastructure.Persistence;
using JiggerJot.Infrastructure.Persistence.Seed;
using JiggerJot.Infrastructure.Repositories;

namespace JiggerJot.Api.Tests.Catalog;

/// <summary>
/// CKTL-4 (FEATURES §12): the detail view "indicates makeable / almost-makeable status and any
/// substitution in play". CKTL-3 shipped without that sentence and it was logged rather than folded
/// in; this is it.
/// <para>
/// The engines already existed, so almost nothing here is new arithmetic — what these tests are
/// really protecting is that the detail view and the two list filters cannot come to different
/// conclusions about the same drink and the same shelf. The last test in the file walks the entire
/// catalog to say so.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CocktailDetailMakeabilityTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private readonly Guid _household = Guid.CreateVersion7();

    private static CocktailDetailHandler Handler(AppDbContext db) =>
        new(new EfRepository<Cocktail>(db),
            new UserRepository(db),
            new EfRepository<TenantInventory>(db),
            new EfRepository<IngredientSubstitution>(db));

    private async Task SeedAsync()
    {
        await using var db = Fixture.CreateContext();
        await new CatalogSeeder(db, NullLogger<CatalogSeeder>.Instance).SeedAsync();
    }

    private async Task StockAsync(params string[] names) => await StockForAsync(_household, names);

    private async Task StockForAsync(Guid household, params string[] names)
    {
        await using var db = Fixture.CreateContext(household);
        var inventory = new InventoryHandler(
            new EfRepository<TenantInventory>(db),
            new EfRepository<Ingredient>(db),
            new TestCurrentTenant { TenantId = household },
            new FakeTimeProvider(DateTimeOffset.UtcNow));

        foreach (var name in names)
        {
            var id = await db.Ingredients.IgnoreQueryFilters()
                .Where(i => i.TenantId == null && i.Name == name)
                .Select(i => i.Id)
                .SingleAsync();
            Assert.True(await inventory.SetAsync(id, true, default), $"could not stock {name}");
        }
    }

    private async Task<Guid> IdOfAsync(string name)
    {
        await using var db = Fixture.CreateContext();
        return await db.Cocktails.IgnoreQueryFilters().Where(c => c.Name == name).Select(c => c.Id).FirstAsync();
    }

    private async Task<CocktailDetail> DetailAsync(Guid id)
    {
        await using var db = Fixture.CreateContext(_household);
        var detail = await Handler(db).GetAsync(id, null, default);
        return detail!;
    }

    private async Task<CocktailDetail> DetailOfAsync(string name) => await DetailAsync(await IdOfAsync(name));

    private static RecipeLineView LineFor(CocktailDetail detail, string ingredient) =>
        detail.Lines.Single(l => l.Ingredient == ingredient);

    [Fact]
    public async Task AShelfThatCoversEveryLine_SaysMakeable()
    {
        await SeedAsync();
        await StockAsync("London dry gin", "Campari", "Sweet vermouth");

        var negroni = await DetailOfAsync("Negroni");

        Assert.Equal(nameof(MakeabilityStatus.Makeable), negroni.Makeability);
        Assert.All(negroni.Lines, l => Assert.Equal(nameof(LineAvailability.Have), l.Availability));
    }

    [Fact]
    public async Task OneRequiredLineShort_SaysAlmostMakeable_AndMarksThatLine()
    {
        await SeedAsync();
        await StockAsync("London dry gin", "Campari");

        var negroni = await DetailOfAsync("Negroni");

        // The status alone would leave the reader scanning three lines to find the one to buy. On a
        // recipe view the line itself is where the answer belongs.
        Assert.Equal(nameof(MakeabilityStatus.AlmostMakeable), negroni.Makeability);
        Assert.Equal(nameof(LineAvailability.Missing), LineFor(negroni, "Sweet vermouth").Availability);
        Assert.Equal(nameof(LineAvailability.Have), LineFor(negroni, "Campari").Availability);
    }

    [Fact]
    public async Task TwoRequiredLinesShort_SaysNotMakeable()
    {
        await SeedAsync();
        await StockAsync("London dry gin");

        Assert.Equal(nameof(MakeabilityStatus.NotMakeable), (await DetailOfAsync("Negroni")).Makeability);
    }

    [Fact]
    public async Task AnEmptyShelf_LeavesEveryLineMissing()
    {
        await SeedAsync();

        var negroni = await DetailOfAsync("Negroni");

        Assert.Equal(nameof(MakeabilityStatus.NotMakeable), negroni.Makeability);
        Assert.All(negroni.Lines, l => Assert.Equal(nameof(LineAvailability.Missing), l.Availability));
        Assert.All(negroni.Lines, l => Assert.Null(l.SubstituteWith));
    }

    [Fact]
    public async Task ASubstitutedLine_SaysWhatYouWouldActuallyPour()
    {
        await SeedAsync();
        await StockAsync("London dry gin", "Curaçao", "Lemon juice");

        var white = await DetailOfAsync("White Lady");
        var cointreau = LineFor(white, "Cointreau");

        // FEATURES §12 asks for "any substitution in play" by name. This is the screen someone reads
        // with the bottle in their hand, so it is the screen where being vague costs the most.
        Assert.Equal(nameof(MakeabilityStatus.Makeable), white.Makeability);
        Assert.Equal(nameof(LineAvailability.Substitute), cointreau.Availability);
        Assert.Equal("Curaçao", cointreau.SubstituteWith);
    }

    [Fact]
    public async Task AnUnstockedOptionalLine_NeverBlocksTheDrink()
    {
        await SeedAsync();

        // JJ-009: find a drink with an optional line, stock only what is required, and the drink is
        // makeable with the garnish still marked as missing on its own line.
        Guid cocktailId;
        string[] required;
        await using (var db = Fixture.CreateContext())
        {
            var candidate = await db.Cocktails.IgnoreQueryFilters()
                .Where(c => c.TenantId == null && c.Lines.Any(l => !l.IsRequired) && c.Lines.Count <= 4)
                .Select(c => new
                {
                    c.Id,
                    Required = c.Lines.Where(l => l.IsRequired).Select(l => l.Ingredient!.Name).ToList(),
                    Optional = c.Lines.Count(l => !l.IsRequired),
                })
                .FirstAsync(c => c.Required.Count > 0 && c.Optional > 0);
            cocktailId = candidate.Id;
            required = [.. candidate.Required.Distinct()];
        }

        await StockAsync(required);
        var detail = await DetailAsync(cocktailId);

        Assert.Equal(nameof(MakeabilityStatus.Makeable), detail.Makeability);
        Assert.Contains(detail.Lines, l => !l.IsRequired && l.Availability == nameof(LineAvailability.Missing));
    }

    [Fact]
    public async Task AnotherHouseholdsShelf_ChangesNothingHere()
    {
        await SeedAsync();
        await StockForAsync(Guid.CreateVersion7(), "London dry gin", "Campari", "Sweet vermouth");

        // Their full Negroni does not make mine. The tenant filter does this, but the detail view is a
        // second reader of the shelf and had to be told to read it the same way.
        Assert.Equal(nameof(MakeabilityStatus.NotMakeable), (await DetailOfAsync("Negroni")).Makeability);
    }

    [Fact]
    public async Task TheDetailViewAndTheListFilters_NeverDisagree()
    {
        await SeedAsync();
        // A deliberately partial shelf: enough to make a few drinks outright, enough to leave others
        // one bottle short, and enough to put substitutions in play.
        await StockAsync("London dry gin", "Campari", "Curaçao", "Lemon juice", "Dry vermouth");

        await using var db = Fixture.CreateContext(_household);
        var browse = new CocktailBrowseHandler(
            new EfRepository<Cocktail>(db),
            new EfRepository<TenantInventory>(db),
            new EfRepository<IngredientSubstitution>(db));

        var everything = await browse.BrowseAsync(new CocktailBrowseRequest(null, 1, 100), default);
        var makeable = (await browse.BrowseAsync(
            new CocktailBrowseRequest(null, 1, 100, MakeableOnly: true), default))
            .Items.Select(i => i.Id).ToHashSet();
        var almost = (await browse.BrowseAsync(
            new CocktailBrowseRequest(null, 1, 100, AlmostMakeableOnly: true), default))
            .Items.Select(i => i.Id).ToHashSet();

        // One rule, two spellings: the browse filters are set-based SQL because a filter over a paged
        // catalog cannot be a loop, and the detail view walks one drink's lines in memory. This walks
        // the whole catalog and asserts the two never part company — which is the only thing that
        // makes keeping both of them honest.
        foreach (var row in everything.Items)
        {
            var expected = makeable.Contains(row.Id) ? nameof(MakeabilityStatus.Makeable)
                : almost.Contains(row.Id) ? nameof(MakeabilityStatus.AlmostMakeable)
                : nameof(MakeabilityStatus.NotMakeable);

            Assert.Equal(expected, (await DetailAsync(row.Id)).Makeability);
        }

        // And the shelf really was partial, or the loop above proved nothing.
        Assert.NotEmpty(makeable);
        Assert.NotEmpty(almost);
        Assert.True(makeable.Count + almost.Count < everything.Total);
    }
}
