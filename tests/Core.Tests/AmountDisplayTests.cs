using JiggerJot.Core.Catalog;
using JiggerJot.Core.Entities;

namespace JiggerJot.Core.Tests;

/// <summary>
/// JJ-041 made visible: every volume is stored in ounces (see <see cref="BarMeasureTests"/>) and read
/// in the reader's system here, on the way to a screen. Pure logic with no database in it, because the
/// rules are fiddly and each one deserves a name.
/// </summary>
public class AmountDisplayTests
{
    [Fact]
    public void NoAmount_ShowsNothing()
    {
        // "to taste", and the garnishes measured by eye.
        Assert.Equal("", AmountDisplay.Format(null, null, UnitSystem.Metric));
        Assert.Equal("", AmountDisplay.Format(null, "oz", UnitSystem.Metric));
    }

    [Fact]
    public void AmountWithNoUnit_IsJustTheNumber()
    {
        // "1 egg". The model allows an amount without a unit and this is what it is for.
        Assert.Equal("1", AmountDisplay.Format(1m, null, UnitSystem.Metric));
        Assert.Equal("2", AmountDisplay.Format(2m, null, UnitSystem.Imperial));
    }

    [Theory]
    [InlineData(1, "dash", "1 dash")]
    [InlineData(2, "dash", "2 dashes")]
    [InlineData(4, "tsp", "4 tsp")]
    [InlineData(0.5, "tbsp", "1/2 tbsp")]
    public void TeaspoonsAndNeutralUnits_ReadTheSameInBothSystems(decimal amount, string unit, string expected)
    {
        // A teaspoon is a teaspoon in every bar book, and a dash has no volume to convert.
        Assert.Equal(expected, AmountDisplay.Format(amount, unit, UnitSystem.Metric));
        Assert.Equal(expected, AmountDisplay.Format(amount, unit, UnitSystem.Imperial));
    }

    [Fact]
    public void Ounces_ReadAsOunces_ForImperialAndForAReaderWhoNeverChose()
    {
        Assert.Equal("1 1/2 oz", AmountDisplay.Format(1.5m, "oz", UnitSystem.Imperial));
        Assert.Equal("1 1/2 oz", AmountDisplay.Format(1.5m, "oz", preferred: null));
        Assert.Equal("3/4 oz", AmountDisplay.Format(0.75m, "oz", UnitSystem.Imperial));
    }

    [Theory]
    [InlineData(0.25, "7.5 ml")]
    [InlineData(0.5, "15 ml")]
    [InlineData(0.75, "22.5 ml")]
    [InlineData(1, "30 ml")]
    [InlineData(1.5, "45 ml")]
    [InlineData(2, "60 ml")]
    [InlineData(3.25, "97.5 ml")]
    public void Ounces_ReadAsMillilitres_AtTheBarsThirtyToTheOunce(decimal oz, string expected)
    {
        // Every stored quarter ounce lands on the 2.5 ml steps bar books are written in — 22.5, not 22
        // or 23 — and never on the 44 and 59 that the exact 29.5735 would give.
        Assert.Equal(expected, AmountDisplay.Format(oz, "oz", UnitSystem.Metric));
    }

    [Fact]
    public void AVolumeStillStoredInAnotherUnit_ReadsInTheReadersSystemAnyway()
    {
        // Nothing should be stored in millilitres or glasses after JJ-041, but a reader must never see
        // one if something is — the same table converts it on the way out.
        Assert.Equal("45 ml", AmountDisplay.Format(45m, "ml", UnitSystem.Metric));
        Assert.Equal("1 1/2 oz", AmountDisplay.Format(45m, "ml", UnitSystem.Imperial));
        Assert.Equal("60 ml", AmountDisplay.Format(1m, "glass", UnitSystem.Metric));
    }

    [Fact]
    public void AStrayPart_CannotBeConvertedOnItsOwn_SoItReadsAsWritten()
    {
        // A part only has a volume against the rest of its recipe, which a single line does not know.
        // BarMeasure converts them when they are stored; this is the fallback, not the path.
        Assert.Equal("2/3 part", AmountDisplay.Format(0.6667m, "part", UnitSystem.Metric));
    }

    [Fact]
    public void TrailingZeroes_AreNeverShown()
    {
        Assert.Equal("2 oz", AmountDisplay.Format(2.00m, "oz", UnitSystem.Imperial));
        Assert.Equal("30 ml", AmountDisplay.Format(1.0m, "oz", UnitSystem.Metric));
    }
}
