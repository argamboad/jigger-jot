namespace JiggerJot.Core.Entities;

/// <summary>
/// How a person wants recipe amounts shown: ounces (<see cref="UnitSystem.Imperial"/>) or millilitres
/// (<see cref="UnitSystem.Metric"/>) — JJ-008, JJ-041. <b>User-keyed, not tenant-scoped</b>: a measuring habit belongs
/// to the person and follows them to every device and household, like the platform's theme and language. The
/// platform's <see cref="User"/> row is not the app's to extend (Arch A3, jigger-jot#164; it used to be a column
/// there), so the preference lives in its own table and is wiped by account erasure through an
/// <c>IUserDataContributor</c>. One row per user; absent = never chose, which reads as Imperial.
/// </summary>
public class UserUnitPreference
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }

    /// <summary>Metric or Imperial — never <see cref="UnitSystem.Neutral"/>, which describes a unit, not a reader.</summary>
    public UnitSystem UnitSystem { get; set; } = UnitSystem.Imperial;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
