using JiggerJot.Core.Catalog;
using JiggerJot.Core.Entities;
using JiggerJot.Infrastructure.Persistence.Seed;

namespace JiggerJot.Api.Tests.Catalog;

/// <summary>
/// AUTHORING-3: the seeded catalog and the write form must give a line the same role. The catalog's
/// roles are computed by <c>seed/build_cocktails.py</c> and the form's by <see cref="RecipeRoles"/> in
/// Core — two copies of one rule, so this holds every line of the embedded file to the Core one. A
/// change to either copy that is not made to the other fails here rather than in a recipe that reads
/// its gin as a modifier.
/// <para>
/// <b>One sanctioned difference (JJ-043):</b> an IBA line is never a garnish. The IBA lists its garnish in a
/// field of its own, so every line of its spec is something the drink is made of — the Mojito's mint, the
/// Caipirinha's lime — and where the rule says Garnish the file says Other, required.
/// </para>
/// </summary>
public sealed class SeedRolesParityTests
{
    private const string Iba = "IBA Official Cocktails";

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
                var expected = cocktail.Source == Iba && roles[i] == RecipeRole.Garnish ? RecipeRole.Other : roles[i];
                if (!string.Equals(lines[i].Role, expected.ToString(), StringComparison.OrdinalIgnoreCase)
                    || lines[i].IsRequired != RecipeRoles.IsRequired(expected))
                {
                    mismatches.Add($"{cocktail.Slug} line {i} ({lines[i].Ingredient}): file says "
                                   + $"{lines[i].Role}/{lines[i].IsRequired}, the rule says {expected}/{RecipeRoles.IsRequired(expected)}");
                }
            }
        }

        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
    }

    [Fact]
    public void EveryIbaLine_IsRequired()
    {
        // The Mojito without mint is not a Mojito. The IBA's garnish is in its instructions, never a line.
        var optional = CatalogSeeder.LoadCocktails().Cocktails
            .Where(c => c.Source == Iba)
            .SelectMany(c => c.Lines.Where(l => !l.IsRequired).Select(l => $"{c.Slug}: {l.Ingredient}"))
            .ToList();

        Assert.True(optional.Count == 0, string.Join(Environment.NewLine, optional));
    }
}
