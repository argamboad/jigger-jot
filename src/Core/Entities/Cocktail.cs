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

    /// <summary>
    /// Required (JJ-043): the glass is part of the cocktail. A seeded recipe whose source names no
    /// glass does not ship rather than ship with a guess, and a household cannot save one without it.
    /// </summary>
    public Guid GlassTypeId { get; set; }
    public GlassType? GlassType { get; set; }

    /// <summary>Nullable because a recipe may simply not say (JJ-034): a method is the source's to
    /// state, and picking one for it would be inventing a fact.</summary>
    public Guid? MethodId { get; set; }
    public Method? Method { get; set; }

    /// <summary>
    /// The book or list this recipe came from, and the credit owed to it (JJ-032). Null for a cocktail
    /// a household wrote itself — provenance from OUTSIDE the app, unlike
    /// <see cref="ForkedFromCocktailId"/>, which is provenance from inside it.
    /// </summary>
    public Guid? SourceId { get; set; }
    public RecipeSource? Source { get; set; }

    public ServingType ServingType { get; set; }

    /// <summary>Preparation steps, free text.</summary>
    public string? Instructions { get; set; }

    public ICollection<CocktailIngredient> Lines { get; set; } = [];
}
