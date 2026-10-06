namespace JiggerJot.Core.Entities;

/// <summary>
/// The glass a cocktail is served in (coupe, rocks, highball, …). Curated global lookup — no
/// household additions in MVP (JJ-022), so no tenant column. Drives a browse filter (FEATURES §11).
/// </summary>
public class GlassType
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Name { get; set; }
}
