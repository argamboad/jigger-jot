namespace JiggerJot.Core.Entities;

/// <summary>
/// A measure used on a recipe line. Amounts are stored exactly as authored and converted only at
/// display time, to the viewing user's <c>PreferredUnitSystem</c> (JJ-007, JJ-008).
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
