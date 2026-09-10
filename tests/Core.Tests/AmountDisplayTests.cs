using JiggerJot.Core.Catalog;
using JiggerJot.Core.Entities;

namespace JiggerJot.Core.Tests;

/// <summary>
/// JJ-007 and JJ-008 made visible: amounts are stored exactly as authored and converted only here,
/// on the way to a screen. Pure logic with no database in it, because the rules are fiddly and each
/// one deserves a name.
/// </summary>
public class AmountDisplayTests
{
    private static readonly UnitView Oz = new("oz", UnitSystem.Imperial, 29.5735m);
    private static readonly UnitView Ml = new("ml", UnitSystem.Metric, 1m);
    private static readonly UnitView Dash = new("dash", UnitSystem.Neutral, null);
    private static readonly UnitView Part = new("part", UnitSystem.Neutral, null);

    [Fact]
    public void NoAmount_ShowsNothing()
    {
        // "to taste", and the garnishes measured by eye.
        Assert.Equal("", AmountDisplay.Format(null, null, UnitSystem.Metric));
        Assert.Equal("", AmountDisplay.Format(null, Oz, UnitSystem.Metric));
    }

    [Fact]
    public void AmountWithNoUnit_IsJustTheNumber()
    {
        // "1 egg". The model allows an amount without a unit and this is what it is for.
        Assert.Equal("1", AmountDisplay.Format(1m, null, UnitSystem.Metric));
        Assert.Equal("2", AmountDisplay.Format(2m, null, UnitSystem.Imperial));
    }

    [Theory]
    [InlineData(1, "1 dash")]
    [InlineData(2, "2 dashes")]
    public void NeutralUnits_PassThroughUnconverted_AndPluralise(int amount, string expected)
    {
        // A neutral unit has no millilitre factor, so it cannot convert and must never try. The
        // viewer's preference is irrelevant here — both systems get the same string.
        Assert.Equal(expected, AmountDisplay.Format(amount, Dash, UnitSystem.Metric));
        Assert.Equal(expected, AmountDisplay.Format(amount, Dash, UnitSystem.Imperial));
    }

    [Fact]
    public void ProportionalAmounts_ComeBackAsTheFractionTheBookWrote()
    {
        // The seed stores 2/3 as 0.6667 because the column is decimal. Showing "0.6667 part" would
        // be technically faithful and useless; this is the display half of the decision SEED-3
        // deliberately left here.
        Assert.Equal("2/3 part", AmountDisplay.Format(0.6667m, Part, UnitSystem.Metric));
        Assert.Equal("1/6 part", AmountDisplay.Format(0.1667m, Part, UnitSystem.Metric));
        Assert.Equal("1/2 part", AmountDisplay.Format(0.5m, Part, UnitSystem.Imperial));
        Assert.Equal("2 parts", AmountDisplay.Format(2m, Part, UnitSystem.Metric));
    }

    [Fact]
    public void MatchingSystem_IsShownAsAuthored()
    {
        Assert.Equal("45 ml", AmountDisplay.Format(45m, Ml, UnitSystem.Metric));
        Assert.Equal("1 1/2 oz", AmountDisplay.Format(1.5m, Oz, UnitSystem.Imperial));
    }

    [Theory]
    [InlineData(1, "30 ml")]
    [InlineData(1.5, "45 ml")]
    [InlineData(0.75, "22.5 ml")]
    [InlineData(0.5, "15 ml")]
    public void ImperialToMetric_RoundsToHowRecipesAreActuallyWritten(decimal oz, string expected)
    {
        // 1 oz is 29.5735 ml. Nobody writes that, and rounding to a whole millilitre gives 44 for
        // 1.5 oz where every metric recipe in the world says 45. Rounding to the nearest 2.5 ml is
        // the granularity bar recipes are actually written at, and it is a display choice — the
        // stored 1.5 oz is untouched.
        Assert.Equal(expected, AmountDisplay.Format(oz, Oz, UnitSystem.Metric));
    }

    [Theory]
    [InlineData(30, "1 oz")]
    [InlineData(45, "1 1/2 oz")]
    [InlineData(20, "3/4 oz")]
    [InlineData(60, "2 oz")]
    public void MetricToImperial_RoundsToQuarterOunces(decimal ml, string expected)
    {
        // A jigger is marked in quarters and halves; "0.68 oz" is not a thing anyone can pour.
        Assert.Equal(expected, AmountDisplay.Format(ml, Ml, UnitSystem.Imperial));
    }

    [Fact]
    public void ConversionRoundsRatherThanTruncating_SoATinyAmountDoesNotVanish()
    {
        // 1 dash-sized 2 ml would floor to 0 oz and render as "0 oz", which reads as "none" — worse
        // than any rounding error. The smallest step is shown instead.
        var result = AmountDisplay.Format(2m, Ml, UnitSystem.Imperial);
        Assert.NotEqual("0 oz", result);
        Assert.Equal("1/4 oz", result);
    }

    [Fact]
    public void NoPreference_ShowsTheRecipeAsAuthored()
    {
        // PreferredUnitSystem is nullable and null means "never chose". A user who has not picked
        // sees what the book wrote, which is the honest default and matches JJ-007.
        Assert.Equal("1 1/2 oz", AmountDisplay.Format(1.5m, Oz, preferred: null));
        Assert.Equal("45 ml", AmountDisplay.Format(45m, Ml, preferred: null));
    }

    [Fact]
    public void AwkwardDecimals_FallBackToADecimal_RatherThanAWrongFraction()
    {
        // 0.37 is not a fraction anyone writes. Inventing "3/8" would be a lie about the recipe;
        // showing the number is merely unlovely.
        Assert.Equal("0.37 part", AmountDisplay.Format(0.37m, Part, UnitSystem.Metric));
    }

    [Fact]
    public void TrailingZeroes_AreNeverShown()
    {
        Assert.Equal("2 oz", AmountDisplay.Format(2.00m, Oz, UnitSystem.Imperial));
        Assert.Equal("30 ml", AmountDisplay.Format(30.0m, Ml, UnitSystem.Metric));
    }
}
