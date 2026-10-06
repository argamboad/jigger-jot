namespace JiggerJot.Core.Entities;

/// <summary>
/// What a household has on its shelf — the checklist the whole product turns on. Ordinary tenant
/// data, so this is the one JiggerJot entity that implements <see cref="ITenantScoped"/> and gets the
/// platform's filter, stamping and RLS policy for free (JJ-031).
/// <para>
/// Sparse by design: a row exists only for an ingredient the household has actually marked, and the
/// absence of a row means "not available" (JJ-023). Boolean only — no "running low" in MVP.
/// Ice and water are never modelled here; they are assumed always available (JJ-020).
/// </para>
/// </summary>
public class TenantInventory : ITenantScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TenantId { get; set; }

    /// <summary>The ingredient — shared catalog or this household's own.</summary>
    public Guid IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    public bool IsAvailable { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
