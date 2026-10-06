using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using JiggerJot.Api.Tests.Infrastructure;
using JiggerJot.Core.Catalog;
using JiggerJot.Core.Entities;
using JiggerJot.Infrastructure.Persistence.Migrations;
using JiggerJot.Infrastructure.Persistence.Seed;

namespace JiggerJot.Api.Tests.Catalog;

/// <summary>
/// PREFS-3 (JJ-041): the one-off migration that converts rows already in a database to ounces.
/// <para>
/// It is SQL, because a migration cannot call <see cref="BarMeasure"/> — so this is the test that holds
/// the two to one answer. Rows are written the way they were stored before JJ-041 (millilitres, glasses,
/// parts), the migration's SQL runs over them, and every line must come out exactly as
/// <see cref="BarMeasure.ToStored"/> says it should.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class OunceMigrationTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private static readonly BarMeasure.Line[] Proportional =
    [
        new(0.6667m, "part"), new(0.1667m, "part"), new(0.1667m, "part"), new(1m, "dash"),
    ];

    private static readonly BarMeasure.Line[] Measured =
    [
        new(45m, "ml"), new(25m, "ml"), new(3m, "cl"), new(1m, "glass"), new(0.75m, "wineglass"),
        new(1m, "liqueur glass"), new(0.6667m, "oz"), new(0.5m, "tbsp"), new(2m, "tsp"), new(1m, null),
        new(null, null),
    ];

    [Fact]
    public async Task ExistingRows_ComeOutExactlyAsBarMeasureWouldStoreThem()
    {
        var (proportional, measured) = await WriteTheOldShapeAsync();

        await RunTheMigrationAsync();

        Assert.Equal(BarMeasure.ToStored(Proportional), await LinesAsync(proportional));
        Assert.Equal(BarMeasure.ToStored(Measured), await LinesAsync(measured));
    }

    [Fact]
    public async Task RunningItAgain_ChangesNothing()
    {
        var (proportional, measured) = await WriteTheOldShapeAsync();

        await RunTheMigrationAsync();
        var once = (await LinesAsync(proportional), await LinesAsync(measured));
        await RunTheMigrationAsync();

        Assert.Equal(once.Item1, await LinesAsync(proportional));
        Assert.Equal(once.Item2, await LinesAsync(measured));
    }

    private async Task RunTheMigrationAsync()
    {
        await using var db = Fixture.CreateContext();
        await db.Database.ExecuteSqlRawAsync(OunceCanonicalAmounts.ConvertSql);
    }

    /// <summary>Two household recipes stored as they were before JJ-041, straight into the table.</summary>
    private async Task<(Guid Proportional, Guid Measured)> WriteTheOldShapeAsync()
    {
        await using (var db = Fixture.CreateContext())
            await new CatalogSeeder(db, NullLogger<CatalogSeeder>.Instance).SeedAsync();

        var household = Guid.CreateVersion7();
        await using var write = Fixture.CreateContext();
        var units = await write.Units.ToDictionaryAsync(u => u.Name, u => u.Id);
        var gin = await write.Ingredients.IgnoreQueryFilters()
            .Where(i => i.TenantId == null && i.Name == "London dry gin").Select(i => i.Id).SingleAsync();

        Cocktail Recipe(string name, BarMeasure.Line[] lines) => new()
        {
            Name = name,
            TenantId = household,
            ServingType = ServingType.FullDrink,
            Lines = [.. lines.Select((line, index) => new CocktailIngredient
            {
                TenantId = household,
                IngredientId = gin,
                Amount = line.Amount,
                UnitId = line.Unit is { } unit ? units[unit] : null,
                Role = RecipeRole.Base,
                IsRequired = true,
                DisplayOrder = index,
            })],
        };

        var proportional = Recipe("Old Proportional", Proportional);
        var measured = Recipe("Old Measured", Measured);
        write.Cocktails.AddRange(proportional, measured);
        await write.SaveChangesAsync();
        return (proportional.Id, measured.Id);
    }

    private async Task<List<BarMeasure.Line>> LinesAsync(Guid cocktailId)
    {
        await using var db = Fixture.CreateContext();
        var rows = await db.CocktailIngredients.IgnoreQueryFilters()
            .Where(l => l.CocktailId == cocktailId)
            .OrderBy(l => l.DisplayOrder)
            .Select(l => new { l.Amount, Unit = l.Unit == null ? null : l.Unit.Name })
            .ToListAsync();
        return [.. rows.Select(r => new BarMeasure.Line(r.Amount, r.Unit))];
    }
}
