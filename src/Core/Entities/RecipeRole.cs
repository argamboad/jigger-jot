namespace JiggerJot.Core.Entities;

/// <summary>
/// What a recipe line contributes to the drink (JJ-010). Display grouping only — it never affects
/// the makeable calculation, which keys off <c>CocktailIngredient.IsRequired</c> (JJ-009).
/// A garnish is simply an optional line whose role happens to be <see cref="Garnish"/>.
/// </summary>
public enum RecipeRole
{
    Base = 0,
    Modifier = 1,
    Juice = 2,
    Syrup = 3,
    Bitters = 4,
    Garnish = 5,
    Mixer = 6,
    Other = 7,
}
