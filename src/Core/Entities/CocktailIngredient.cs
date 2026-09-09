namespace JiggerJot.Core.Entities;

/// <summary>
/// One line of a recipe — the heart of the model. Carries its parent cocktail's nature: a shared
/// cocktail's lines are shared, a household's are that household's, so this is
/// <see cref="ISharedOrTenantScoped"/> too (JJ-031).
/// <para>
/// The same ingredient may appear on several lines of one cocktail — that is not a mistake to
/// constrain away ("2 oz gin" for the build, "1 dash gin" to rinse).
/// </para>
/// </summary>
public class CocktailIngredient : ISharedOrTenantScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Null = belongs to a shared catalog cocktail; set = to that household's.</summary>
    public Guid? TenantId { get; set; }

    public Guid CocktailId { get; set; }
    public Cocktail? Cocktail { get; set; }

    public Guid IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    /// <summary>
    /// How much, as authored — never converted on the way in (JJ-007). Null for "to taste" and for
    /// garnishes measured by eye.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>Pairs with <see cref="Amount"/>; null when the amount is null.</summary>
    public Guid? UnitId { get; set; }
    public Unit? Unit { get; set; }

    /// <summary>
    /// Drives the makeable calculation (JJ-009): every required line must be satisfied, by the exact
    /// ingredient or a valid substitute. Optional lines never block a drink from being makeable, which
    /// is the whole reason a garnish is modelled as a line rather than a special case.
    /// </summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>Display grouping only (JJ-010).</summary>
    public RecipeRole Role { get; set; } = RecipeRole.Other;

    /// <summary>Stable ordering for display; recipes read in a deliberate order.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Free text qualifying the line, e.g. "freshly squeezed".</summary>
    public string? Notes { get; set; }
}
