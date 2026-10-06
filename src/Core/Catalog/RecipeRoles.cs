using JiggerJot.Core.Entities;

namespace JiggerJot.Core.Catalog;

/// <summary>
/// The role a recipe line takes from what its ingredient IS (AUTHORING-3; JJ-010, JJ-014 in spirit) —
/// derived from the ingredient's top-level category, never tagged by hand.
/// <para>
/// <b>Two copies of one rule, held together by a test.</b> <c>seed/build_cocktails.py</c> gave every
/// seeded line its role with this mapping, and the write form now suggests roles through this class.
/// <c>SeedRolesParityTests</c> checks every line of the embedded catalog against it, so neither can
/// change alone.
/// </para>
/// <para>
/// <b>The first spirit is the base; every later spirit modifies it.</b> Position among the spirits is
/// what counts, not position in the recipe: vermouth written first does not make the gin a modifier.
/// </para>
/// </summary>
public static class RecipeRoles
{
    private static readonly HashSet<string> SpiritCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Gin", "Whisky", "Rum", "Agave", "Brandy", "Vodka", "Absinthe and pastis", "Other spirits",
    };

    private static readonly Dictionary<string, RecipeRole> RoleByCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Bitters"] = RecipeRole.Bitters,
        ["Juice"] = RecipeRole.Juice,
        ["Syrup"] = RecipeRole.Syrup,
        ["Soda and mixer"] = RecipeRole.Mixer,
        ["Garnish"] = RecipeRole.Garnish,
        ["Fruit"] = RecipeRole.Garnish,
        ["Herb and spice"] = RecipeRole.Garnish,
        ["Vermouth"] = RecipeRole.Modifier,
        ["Fortified wine"] = RecipeRole.Modifier,
        ["Liqueur"] = RecipeRole.Modifier,
        ["Amaro and bitter"] = RecipeRole.Modifier,
        ["Wine"] = RecipeRole.Modifier,
        ["Beer and cider"] = RecipeRole.Modifier,
    };

    /// <summary>
    /// A role for each line, in the order given.
    /// </summary>
    /// <param name="categories">Each line's ingredient's top-level category name, in recipe order. Null
    /// for an ingredient the caller cannot see: it is <see cref="RecipeRole.Other"/> and never takes the
    /// base.</param>
    public static IReadOnlyList<RecipeRole> Suggest(IReadOnlyList<string?> categories)
    {
        var seenSpirit = false;
        var roles = new RecipeRole[categories.Count];

        for (var i = 0; i < categories.Count; i++)
        {
            var category = categories[i];
            if (category is not null && SpiritCategories.Contains(category))
            {
                roles[i] = seenSpirit ? RecipeRole.Modifier : RecipeRole.Base;
                seenSpirit = true;
            }
            else
            {
                roles[i] = category is not null && RoleByCategory.TryGetValue(category, out var role)
                    ? role
                    : RecipeRole.Other;
            }
        }

        return roles;
    }

    /// <summary>A garnish is just an optional line, and optional lines never block makeability
    /// (JJ-009); every other role starts required.</summary>
    public static bool IsRequired(RecipeRole role) => role != RecipeRole.Garnish;
}
