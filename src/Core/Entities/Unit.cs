namespace JiggerJot.Core.Entities;

/// <summary>
/// A measure used on a recipe line. Every volume is stored in ounces and read in the viewing user's
/// <c>PreferredUnitSystem</c> (JJ-041, JJ-008); the other volume units stay in the lookup so the seed
/// file can name what the books wrote, but no line is stored in them.
/// <para>
/// <see cref="MillilitreFactor"/> is what makes a unit convertible: it is the number of millilitres
/// in one of this unit, so any two convertible units interconvert through it. A
/// <see cref="UnitSystem.Neutral"/> unit leaves it null and always displays as authored — "2 dashes"
/// is never rewritten into millilitres.
/// </para>
/// </summary>
public class Unit
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public UnitSystem System { get; set; }

    /// <summary>Millilitres in one of this unit; null for non-convertible (neutral) units.</summary>
    public decimal? MillilitreFactor { get; set; }
}
