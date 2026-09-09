namespace JiggerJot.Core.Entities;

/// <summary>
/// Which measurement system a unit belongs to (JJ-007, JJ-008). <see cref="Neutral"/> units — dash,
/// barspoon, piece, leaves, to-taste — are non-convertible and always display exactly as authored,
/// whatever the viewing user's preference.
/// </summary>
public enum UnitSystem
{
    Metric = 0,
    Imperial = 1,
    Neutral = 2,
}
