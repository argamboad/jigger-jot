namespace JiggerJot.Core.Entities;

/// <summary>
/// A drink recipe. One table holds both the shared seed catalog and every household's own
/// (JJ-012): <c>TenantId</c> null = shared, set = that household's. Deliberately
/// <see cref="ISharedOrTenantScoped"/> — see JJ-031.
/// <para>
/// There is no "main spirit" column. What a drink is made of is derived from its recipe lines and
/// their ingredients' categories (JJ-014), which is what makes "everything with elderflower" work
/// without anyone tagging it.
/// </para>
/// </summary>
public class Cocktail : ISharedOrTenantScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Null = shared catalog cocktail; set = owned by that household.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Provenance only (JJ-013). Set when this row was created by "Create my own version" of another
    /// cocktail. A fork is a full snapshot copy — the original's later edits never propagate here,
    /// and deleting the original never orphans this.
    /// </summary>
    public Guid? ForkedFromCocktailId { get; set; }

    public required string Name { get; set; }

    public Guid GlassTypeId { get; set; }
    public GlassType? GlassType { get; set; }

    public Guid MethodId { get; set; }
    public Method? Method { get; set; }

    public ServingType ServingType { get; set; }

    /// <summary>Preparation steps, free text.</summary>
    public string? Instructions { get; set; }

    public ICollection<CocktailIngredient> Lines { get; set; } = [];
}
