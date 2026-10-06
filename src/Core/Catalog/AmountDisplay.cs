using System.Globalization;
using JiggerJot.Core.Entities;

namespace JiggerJot.Core.Catalog;

/// <summary>
/// Renders a recipe amount for a reader (JJ-041, JJ-008). Every volume is stored in ounces by
/// <see cref="BarMeasure"/>; this reads it back in the reader's system — ounces, or millilitres at the
/// bar's 30 to the ounce — and passes everything that is not a volume through as written.
/// <para>
/// This lives in Core, with no database and no HTTP anywhere near it, because the rules are fiddly
/// and each one is a judgement worth being able to test on its own.
/// </para>
/// </summary>
public static class AmountDisplay
{
    /// <summary>Denominators a person actually writes on a recipe. Stored ounces only ever need
    /// quarters; the rest are for the units that pass through as written.</summary>
    private static readonly int[] Denominators = [2, 3, 4, 6, 8];

    /// <summary>How close a decimal must be to a fraction before we claim it IS that fraction. The
    /// seed stores 2/3 as 0.6667, so the tolerance has to clear that rounding and nothing wider.</summary>
    private const decimal FractionTolerance = 0.001m;

    /// <summary>Metric bar recipes are written in steps of 2.5 ml: 7.5, 22.5, 45, 60. Every stored
    /// quarter ounce lands exactly on one.</summary>
    private const decimal MetricStep = 2.5m;

    /// <summary>
    /// The amount and its unit as one string, in the reader's system.
    /// </summary>
    /// <param name="unit">The unit's name, or null for an amount that needs none ("1 egg").</param>
    /// <param name="preferred">Null means the reader never chose, and reads ounces — which is what is
    /// stored (<see cref="BarMeasure.ReaderSystem"/>).</param>
    public static string Format(decimal? amount, string? unit, UnitSystem? preferred)
    {
        if (amount is not { } value) return string.Empty;          // "to taste"
        if (unit is null) return Number(value, fractions: true);    // "1 egg"

        // Teaspoons, dashes, barspoons: not volumes to convert, so both systems read them the same.
        if (BarMeasure.MillilitresPer(unit) is not { } millilitresPerUnit)
            return AsWritten(value, unit);

        // Nothing should be stored in anything but ounces, but the same table reads a stray millilitre
        // or glass row correctly rather than showing a reader a unit they did not choose.
        var ounces = value * millilitresPerUnit / BarMeasure.MillilitresPerOunce;

        return BarMeasure.ReaderSystem(preferred) == UnitSystem.Metric
            ? $"{Number(RoundTo(ounces * BarMeasure.MillilitresPerOunce, MetricStep), fractions: false)} {BarMeasure.Millilitre}"
            : $"{Number(RoundTo(ounces, BarMeasure.OunceStep), fractions: true)} {BarMeasure.Ounce}";
    }

    /// <summary>
    /// A unit that does not convert, in the recipe's own words: "1/2 tbsp", "2 dashes". Fractions,
    /// because "0.5 tbsp" reads like a spreadsheet.
    /// </summary>
    private static string AsWritten(decimal value, string unit) =>
        $"{Number(value, fractions: true)} {Pluralise(unit, value)}";

    /// <summary>
    /// Rounds to the nearest <paramref name="step"/>, but never down to nothing: an amount that rounds
    /// to zero would read as "none", which is worse than any rounding error.
    /// </summary>
    private static decimal RoundTo(decimal value, decimal step)
    {
        var rounded = Math.Round(value / step, MidpointRounding.AwayFromZero) * step;
        return rounded == 0 && value > 0 ? step : rounded;
    }

    /// <summary>A number as a person would write it: a whole number, a fraction, or — when it is
    /// neither — the decimal, because inventing a nearby fraction would misstate the recipe.</summary>
    private static string Number(decimal value, bool fractions)
    {
        var whole = decimal.Truncate(value);
        var remainder = Math.Abs(value - whole);

        if (remainder < FractionTolerance)
            return whole.ToString("0.####", CultureInfo.InvariantCulture);

        if (!fractions) return value.ToString("0.####", CultureInfo.InvariantCulture);

        foreach (var denominator in Denominators)
        {
            var numerator = Math.Round(remainder * denominator, MidpointRounding.AwayFromZero);
            if (Math.Abs(remainder - numerator / denominator) >= FractionTolerance) continue;
            if (numerator == 0 || numerator == denominator) continue;

            var fraction = $"{numerator:0}/{denominator}";
            return whole == 0 ? fraction : $"{whole:0} {fraction}";
        }

        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// English pluralisation, and only English: unit names are catalog data rather than translated
    /// strings, so this is about the data reading naturally, not about localisation. "1 dash" and
    /// "2 dashes"; a fraction of one is still singular, as in "1/2 tbsp".
    /// </summary>
    private static string Pluralise(string unit, decimal amount)
    {
        if (amount <= 1) return unit;
        return unit switch
        {
            _ when unit.EndsWith("s", StringComparison.OrdinalIgnoreCase) => unit,
            // Symbols are never pluralised — "45 mls" is not a thing.
            "ml" or "cl" or "l" or "oz" or "tsp" or "tbsp" => unit,
            _ when unit.EndsWith("sh", StringComparison.OrdinalIgnoreCase)
                || unit.EndsWith("ch", StringComparison.OrdinalIgnoreCase) => unit + "es",
            "leaf" => "leaves",
            _ => unit + "s",
        };
    }
}
