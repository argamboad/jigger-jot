namespace JiggerJot.Core.Entities;

/// <summary>
/// Something that goes in a drink. One table holds both the shared seed catalog and every
/// household's own additions (JJ-011): <c>TenantId</c> null = shared, set = that household's.
/// <para>
/// Deliberately <see cref="ISharedOrTenantScoped"/> rather than <c>ITenantScoped</c> — see JJ-031 for
/// why the latter cannot express a shared row, and for the three platform guarantees that therefore
/// do not apply here.
/// </para>
/// <para>
/// Ingredients are generic, never brands ("vodka", not a particular distillery) — JJ-017. A
/// household's own ingredient satisfies a recipe line by exact match only: custom ingredients do not
/// participate in the substitution graph (JJ-018).
/// </para>
/// </summary>
public class Ingredient : ISharedOrTenantScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Null = shared catalog ingredient; set = owned by that household.</summary>
    public Guid? TenantId { get; set; }

    public required string Name { get; set; }

    /// <summary>The top-level category (e.g. Rum). Filtering a parent matches every child (JJ-016).</summary>
    public Guid CategoryId { get; set; }
    public IngredientCategory? Category { get; set; }

    /// <summary>The child category (e.g. Dark Rum); null when the ingredient sits at the top level.</summary>
    public Guid? SubcategoryId { get; set; }
    public IngredientCategory? Subcategory { get; set; }
}
