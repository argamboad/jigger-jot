using JiggerJot.Core.Catalog;
using JiggerJot.Core.Entities;

namespace JiggerJot.Core.Tests;

/// <summary>
/// PREFS-3 (JJ-041): every volume is stored in ounces, on the quarter-ounce marks of a jigger. This is
/// the one place the conversion happens — the seeder, the authoring handler and the data migration's
/// parity test all go through it — so each rule gets its own name here.
/// </summary>
public class BarMeasureTests
{
    private static BarMeasure.Line L(decimal? amount, string? unit) => new(amount, unit);

    [Theory]
    [InlineData("oz", 30)]
    [InlineData("ml", 1)]
    [InlineData("cl", 10)]
    [InlineData("glass", 60)]
    [InlineData("wineglass", 60)]
    [InlineData("liqueur glass", 30)]
    [InlineData("gill", 150)]
    public void VolumeUnits_UseTheBarsThirtyMillilitreOunce(string unit, decimal millilitres)
    {
        // 30, not 29.5735: the bar's ounce, which is what makes 1 1/2 oz read as 45 ml rather than 44.
        // The period glass measures take the volumes the old bar books give them.
        Assert.Equal(millilitres, BarMeasure.MillilitresPer(unit));
    }

    [Theory]
    [InlineData("tsp")]
    [InlineData("tbsp")]
    [InlineData("dash")]
    [InlineData("barspoon")]
    [InlineData("part")]
    [InlineData(null)]
    public void EverythingElse_IsNotAVolumeToConvert(string? unit)
    {
        // Teaspoons stay teaspoons — every bar book in the world writes them that way. A part has no
        // volume of its own; it only means something against the rest of its recipe.
        Assert.Null(BarMeasure.MillilitresPer(unit));
    }

    [Theory]
    [InlineData(45, "ml", 1.5)]
    [InlineData(60, "ml", 2)]
    [InlineData(22.5, "ml", 0.75)]
    [InlineData(20, "ml", 0.75)]
    [InlineData(25, "ml", 0.75)]
    [InlineData(10, "ml", 0.25)]
    [InlineData(40, "ml", 1.25)]
    [InlineData(50, "ml", 1.75)]
    [InlineData(100, "ml", 3.25)]
    [InlineData(3, "cl", 1)]
    [InlineData(1, "glass", 2)]
    [InlineData(0.75, "wineglass", 1.5)]
    [InlineData(1, "liqueur glass", 1)]
    public void Volumes_AreStoredAsOunces_OnTheJiggersQuarterMarks(decimal amount, string unit, decimal ounces)
    {
        // No modern bar book writes 5/6 oz, so 25 ml lands on 3/4 oz like 20 ml does.
        var stored = Assert.Single(BarMeasure.ToStored([L(amount, unit)]));

        Assert.Equal(L(ounces, "oz"), stored);
    }

    [Fact]
    public void AnOunceAmountOffTheMarks_IsRoundedToo()
    {
        Assert.Equal(L(0.75m, "oz"), Assert.Single(BarMeasure.ToStored([L(0.6667m, "oz")])));
    }

    [Fact]
    public void ATinyAmount_NeverRoundsToNothing()
    {
        // 1 ml rounds to zero ounces, and "0 oz" reads as "none" — worse than any rounding error.
        Assert.Equal(L(0.25m, "oz"), Assert.Single(BarMeasure.ToStored([L(1m, "ml")])));
    }

    [Fact]
    public void TeaspoonsDashesAndUnmeasuredLines_StayExactlyAsWritten()
    {
        BarMeasure.Line[] lines = [L(4m, "tsp"), L(0.5m, "tbsp"), L(2m, "dash"), L(1m, null), L(null, null)];

        Assert.Equal(lines, BarMeasure.ToStored(lines));
    }

    [Fact]
    public void Parts_ShareAThreeOunceDrink()
    {
        // The Savoy's Absinthe Special: 2/3 absinthe, 1/6 gin, 1/6 anisette, and two dashes that are
        // not part of the proportion at all.
        var stored = BarMeasure.ToStored(
        [
            L(0.6667m, "part"), L(0.1667m, "part"), L(0.1667m, "part"), L(1m, "dash"), L(1m, "dash"),
        ]);

        Assert.Equal([L(2m, "oz"), L(0.5m, "oz"), L(0.5m, "oz"), L(1m, "dash"), L(1m, "dash")], stored);
    }

    [Fact]
    public void WholeParts_AreTheSameShape()
    {
        // The Hawaiian: 4 parts gin, 2 orange juice, 1 curaçao — sevenths of three ounces, on the marks.
        var stored = BarMeasure.ToStored([L(4m, "part"), L(2m, "part"), L(1m, "part")]);

        Assert.Equal([L(1.75m, "oz"), L(0.75m, "oz"), L(0.5m, "oz")], stored);
    }

    [Fact]
    public void PartsThatDoNotAddUpToOne_KeepTheirRatio()
    {
        // 27 Savoy recipes' fractions add up to less than a whole drink. The ratio is what the book
        // wrote; the three ounces are ours.
        var stored = BarMeasure.ToStored([L(0.5m, "part"), L(0.25m, "part")]);

        Assert.Equal([L(2m, "oz"), L(1m, "oz")], stored);
    }

    [Fact]
    public void StoringWhatIsAlreadyStored_ChangesNothing()
    {
        var once = BarMeasure.ToStored([L(0.6667m, "part"), L(0.3333m, "part"), L(20m, "ml"), L(1m, "tsp")]);

        Assert.Equal(once, BarMeasure.ToStored(once));
    }

    [Theory]
    [InlineData(null, UnitSystem.Imperial)]
    [InlineData(UnitSystem.Imperial, UnitSystem.Imperial)]
    [InlineData(UnitSystem.Metric, UnitSystem.Metric)]
    [InlineData(UnitSystem.Neutral, UnitSystem.Imperial)]
    public void AReaderWhoNeverChose_ReadsOunces(UnitSystem? stored, UnitSystem reads)
    {
        // Ounces are what is stored, so ounces are the default — there is no "as written" to fall back
        // to any more.
        Assert.Equal(reads, BarMeasure.ReaderSystem(stored));
    }

    [Theory]
    [InlineData(UnitSystem.Imperial, "oz", true)]
    [InlineData(UnitSystem.Imperial, "ml", false)]
    [InlineData(UnitSystem.Metric, "ml", true)]
    [InlineData(UnitSystem.Metric, "oz", false)]
    [InlineData(UnitSystem.Metric, "tsp", true)]
    [InlineData(UnitSystem.Imperial, "dash", true)]
    [InlineData(UnitSystem.Imperial, "part", false)]
    [InlineData(UnitSystem.Metric, "glass", false)]
    [InlineData(UnitSystem.Imperial, "cl", false)]
    public void AWriterIsOfferedTheirOwnMeasure_AndNothingThatCannotBeStoredWell(
        UnitSystem reader, string unit, bool offered)
    {
        Assert.Equal(offered, BarMeasure.IsOfferedTo(reader, unit));
    }
}
