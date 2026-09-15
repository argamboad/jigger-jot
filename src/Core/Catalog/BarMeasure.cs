using JiggerJot.Core.Entities;

namespace JiggerJot.Core.Catalog;

/// <summary>
/// How a recipe's volumes are stored (JJ-041): <b>every volume in ounces, on the quarter-ounce marks of
/// a jigger</b>. The seeder and the authoring handler both write through <see cref="ToStored"/>, and
/// <see cref="AmountDisplay"/> reads through the same table, so there is one definition of what a
/// measure is worth rather than one per caller.
/// <para>
/// <b>An ounce is 30 ml here, not 29.5735.</b> That is the bar's ounce — the one every metric
/// specification is written against — and it is what lets a stored 1 1/2 oz read as 45 ml instead of
/// 44. The exact factor still sits on the <c>Unit</c> row; nothing reads it for a recipe.
/// </para>
/// <para>
/// <b>Teaspoons, tablespoons and the neutral units are not volumes to convert.</b> "1/2 tsp" is how
/// every bar book writes it, and a dash or a barspoon has no honest volume at all.
/// </para>
/// </summary>
public static class BarMeasure
{
    public const string Ounce = "oz";
    public const string Millilitre = "ml";
    public const string Part = "part";

    /// <summary>The bar's ounce.</summary>
    public const decimal MillilitresPerOunce = 30m;

    /// <summary>What a proportional 1930 recipe is poured as: its parts share three ounces.</summary>
    public const decimal ProportionalDrinkOunces = 3m;

    /// <summary>A jigger is marked in quarters, so no stored amount is finer than one.</summary>
    public const decimal OunceStep = 0.25m;

    /// <summary>One recipe line's measure: an amount and the name of its unit, either possibly null.</summary>
    public readonly record struct Line(decimal? Amount, string? Unit);

    /// <summary>
    /// Millilitres in one of each volume unit, on the 30 ml ounce. The period glass measures take the
    /// volumes the old bar books give them — a liqueur glass (a pony) is an ounce, a wineglass two, and
    /// the Savoy's bare "glass" is used like a wineglass. The pint, quart and gill are British, like the
    /// book that uses them.
    /// </summary>
    private static readonly Dictionary<string, decimal> Millilitres = new(StringComparer.OrdinalIgnoreCase)
    {
        [Ounce] = 30m,
        [Millilitre] = 1m,
        ["cl"] = 10m,
        ["l"] = 1000m,
        ["cup"] = 240m,
        ["gill"] = 150m,
        ["pint"] = 600m,
        ["quart"] = 1200m,
        ["glass"] = 60m,
        ["wineglass"] = 60m,
        ["liqueur glass"] = 30m,
    };

    /// <summary>Millilitres in one <paramref name="unit"/>, or null when it is not a volume this app
    /// converts — a teaspoon, a dash, a part, or no unit at all.</summary>
    public static decimal? MillilitresPer(string? unit) =>
        unit is not null && Millilitres.TryGetValue(unit, out var ml) ? ml : null;

    /// <summary>
    /// The system a reader sees. Ounces are what is stored, so a reader who never chose reads ounces;
    /// only an explicit metric preference reads millilitres.
    /// </summary>
    public static UnitSystem ReaderSystem(UnitSystem? preference) =>
        preference == UnitSystem.Metric ? UnitSystem.Metric : UnitSystem.Imperial;

    /// <summary>The one volume unit a reader writes in: millilitres for metric, ounces otherwise.</summary>
    public static string VolumeUnitFor(UnitSystem? preference) =>
        ReaderSystem(preference) == UnitSystem.Metric ? Millilitre : Ounce;

    /// <summary>
    /// Whether the authoring form offers <paramref name="unit"/> to this reader: their own volume unit,
    /// and every unit that is not a volume. Never a part — a household writing down what it pours knows
    /// how much — and never the other volume units, which would only be converted away on save.
    /// </summary>
    public static bool IsOfferedTo(UnitSystem? preference, string unit)
    {
        if (string.Equals(unit, Part, StringComparison.OrdinalIgnoreCase)) return false;
        return MillilitresPer(unit) is null
               || string.Equals(unit, VolumeUnitFor(preference), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A recipe's lines as they are stored. Volumes become ounces on the quarter marks; parts become
    /// their share of <see cref="ProportionalDrinkOunces"/>; everything else is returned untouched.
    /// Takes the whole recipe rather than a line, because a part only has a volume against the others.
    /// Storing what is already stored changes nothing.
    /// </summary>
    public static IReadOnlyList<Line> ToStored(IReadOnlyList<Line> lines)
    {
        var partTotal = lines.Where(IsPart).Sum(l => l.Amount!.Value);

        return [.. lines.Select(line =>
        {
            if (IsPart(line))
                return new Line(RoundToStep(line.Amount!.Value / partTotal * ProportionalDrinkOunces), Ounce);

            if (line.Amount is > 0 && MillilitresPer(line.Unit) is { } ml)
                return new Line(RoundToStep(line.Amount.Value * ml / MillilitresPerOunce), Ounce);

            return line;
        })];
    }

    private static bool IsPart(Line line) =>
        line.Amount is > 0 && string.Equals(line.Unit, Part, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The nearest quarter ounce, but never nothing: 1 ml would round to "0 oz", which reads as "none"
    /// and is worse than any rounding error.
    /// </summary>
    private static decimal RoundToStep(decimal ounces)
    {
        var rounded = Math.Round(ounces / OunceStep, MidpointRounding.AwayFromZero) * OunceStep;
        return rounded == 0 && ounces > 0 ? OunceStep : rounded;
    }
}
