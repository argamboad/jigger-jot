using JiggerJot.Core.Catalog;
using JiggerJot.Infrastructure.Persistence.Seed;

namespace JiggerJot.Api.Tests.Catalog;

/// <summary>
/// AUTHORING-3: the seeded catalog and the write form must give a line the same role. The catalog's
/// roles are computed by <c>seed/build_cocktails.py</c> and the form's by <see cref="RecipeRoles"/> in
/// Core — two copies of one rule, so this holds every line of the embedded file to the Core one. A
/// change to either copy that is not made to the other fails here rather than in a recipe that reads
/// its gin as a modifier.
/// </summary>
public sealed class SeedRolesParityTests
{
    [Fact]
    public void EverySeededLine_HasTheRoleAndRequiredFlag_TheCoreRuleGives()
    {
        var categories = CatalogSeeder.LoadIngredients().Ingredients
            .ToDictionary(i => i.Name, i => i.Category, StringComparer.OrdinalIgnoreCase);

        var mismatches = new List<string>();
        foreach (var cocktail in CatalogSeeder.LoadCocktails().Cocktails)
        {
            var lines = cocktail.Lines.OrderBy(l => l.DisplayOrder).ToList();
            var roles = RecipeRoles.Suggest([.. lines.Select(l => categories.GetValueOrDefault(l.Ingredient))]);

            for (var i = 0; i < lines.Count; i++)
            {
                if (!string.Equals(lines[i].Role, roles[i].ToString(), StringComparison.OrdinalIgnoreCase)
                    || lines[i].IsRequired != RecipeRoles.IsRequired(roles[i]))
                {
                    mismatches.Add($"{cocktail.Slug} line {i} ({lines[i].Ingredient}): file says "
                                   + $"{lines[i].Role}/{lines[i].IsRequired}, the rule says {roles[i]}/{RecipeRoles.IsRequired(roles[i])}");
                }
            }
        }

        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
    }
}
