namespace JiggerJot.Core.Entities;

/// <summary>
/// How a cocktail is prepared (shake, stir, build, blend, muddle, …). Curated global lookup — no
/// household additions in MVP (JJ-022), so no tenant column. Drives a browse filter (FEATURES §11).
/// </summary>
public class Method
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Name { get; set; }
}
