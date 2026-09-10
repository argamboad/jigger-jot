using System.Globalization;
using JiggerJot.Core.Entities;

namespace JiggerJot.Core.Catalog;

/// <summary>A unit as far as display is concerned: what it is called, which system it belongs to, and
/// whether it converts at all.</summary>
/// <param name="MillilitreFactor">Millilitres in one of this unit; null makes the unit neutral, and a
/// neutral unit never converts (JJ-007).</param>
public record UnitView(string Name, UnitSystem System, decimal? MillilitreFactor);

/// <summary>
/// Renders a recipe amount for a reader (JJ-007, JJ-008). <b>The stored value is never touched</b> —
/// amounts are kept exactly as the author wrote them and converted only here, on the way to a screen.
/// <para>
/// This lives in Core, with no database and no HTTP anywhere near it, because the rules are fiddly
/// and each one is a judgement worth being able to test on its own.
/// </para>
/// </summary>
public static class AmountDisplay
{
    /// <summary>Denominators a person actually writes on a recipe. 1/6 is here because the 1930
    /// proportional recipes use sixths constantly; 1/7 is not, because nobody has ever poured one.</summary>
    private static readonly int[] Denominators = [2, 3, 4, 6, 8];

    /// <summary>How close a decimal must be to a fraction before we claim it IS that fraction. The
    /// seed stores 2/3 as 0.6667, so the tolerance has to clear that rounding and nothing wider.</summary>
    private const decimal FractionTolerance = 0.001m;

    /// <summary>Metric bar recipes are written in steps of 2.5 ml: 22.5, 30, 45, 60.</summary>
    private const decimal MetricStep = 2.5m;

    /// <summary>A jigger is marked in quarters, so quarters are the smallest step worth showing.</summary>
    private const decimal ImperialStep = 0.25m;

    /// <summary>
    /// The amount and its unit as one string, converted to <paramref name="preferred"/> when both the
    /// unit and the preference allow it.
    /// </summary>
    /// <param name="preferred">Null means the reader has never chosen, so the recipe is shown as
    /// authored — the honest default, and the one JJ-007 already argues for.</param>
    public static string Format(decimal? amount, UnitView? unit, UnitSystem? preferred)
    {
        if (amount is not { } value) return string.Empty;          // "to taste"
        if (unit is null) return Number(value, fractions: true);    // "1 egg"

        // Neutral units carry no factor and must never try to convert: rendering "1 barspoon" as
        // "5 ml" would invent a precision the recipe never had.
        if (unit.MillilitreFactor is not { } factor || unit.System == UnitSystem.Neutral)
            return AsAuthored(value, unit);

        if (preferred is not { } target || target == unit.System || target == UnitSystem.Neutral)
            return AsAuthored(value, unit);

        var millilitres = value * factor;

        return target switch
        {
            UnitSystem.Metric => $"{Number(RoundTo(millilitres, MetricStep), fractions: false)} ml",
            UnitSystem.Imperial => $"{Number(RoundTo(millilitres / 29.5735m, ImperialStep), fractions: true)} oz",
            _ => AsAuthored(value, unit),
        };
    }

    /// <summary>
    /// The recipe's own words. Fractions everywhere EXCEPT metric: "22 1/2 ml" is not something
    /// anyone has ever written, because metric is decimal by construction. Ounces and the
    /// proportional "part" are the opposite — "0.75 oz" reads like a spreadsheet, "3/4 oz" reads
    /// like a recipe.
    /// </summary>
    private static string AsAuthored(decimal value, UnitView unit) =>
        $"{Number(value, fractions: unit.System != UnitSystem.Metric)} {Pluralise(unit.Name, value)}";

    /// <summary>
    /// Rounds to the nearest <paramref name="step"/>, but never down to nothing: a 2 ml dash converted
    /// to ounces would land on zero and read as "none", which is worse than any rounding error.
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
    /// "2 dashes"; a fraction of one is still singular, as in "1/2 part".
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
