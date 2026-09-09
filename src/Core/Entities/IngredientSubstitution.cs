namespace JiggerJot.Core.Entities;

/// <summary>
/// "This ingredient can stand in for that one" — the graph the makeable engine consults when a
/// household lacks exactly what a recipe calls for (JJ-004).
/// <para>
/// Global only in MVP (JJ-005): both ends must be shared catalog ingredients, so this carries no
/// tenant column at all and households cannot add their own. Symmetry is stored as <b>two directed
/// rows</b> rather than a flag (JJ-006) — it makes the makeable query a plain join instead of an OR
/// across two columns.
/// </para>
/// </summary>
public class IngredientSubstitution
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>The ingredient a recipe asks for.</summary>
    public Guid IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    /// <summary>What may be poured instead.</summary>
    public Guid SubstituteIngredientId { get; set; }
    public Ingredient? SubstituteIngredient { get; set; }
}
