using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using JiggerJot.Core.Catalog;
using JiggerJot.Core.Entities;

namespace JiggerJot.Infrastructure.Persistence.Seed;

/// <summary>
/// Puts the curated global lookups (JJ-022) into the database: glass types, methods, units and the
/// two-level ingredient categories. These are shared catalog rows — owned by no household, readable by
/// all — and they are the vocabulary every later seed pass and every recipe row depends on.
/// <para>
/// <b>Idempotent by construction.</b> Every row's id is derived from its name
/// (<see cref="SeedId"/>), so a re-run recognises what it already wrote rather than guessing from
/// content. Rows the file no longer names are left alone: a curated row may already be referenced by a
/// household's own cocktail, and deleting it out from under one to match a JSON file would be a far
/// worse failure than an unused lookup lingering.
/// </para>
/// <para>
/// <b>It must run without an ambient household.</b> On such a context <c>RlsSessionInterceptor</c> sets
/// the bypass GUC, which is the only way past the <c>rls_shared_or_tenant_insert</c> policy — writing a
/// null-tenant row is deliberately impossible from a household context (JJ-031). Nothing stamps these
/// rows either, so where a tenant column exists this seeder leaves it null on purpose.
/// </para>
/// </summary>
public sealed class CatalogSeeder(AppDbContext db, ILogger<CatalogSeeder> logger)
{
    /// <summary>Config gate; false skips seeding entirely (an operator freezing a curated database).</summary>
    public const string EnabledConfigKey = "Seed:Catalog:Enabled";

    private const string LookupsResource = "JiggerJot.Infrastructure.Persistence.Seed.lookups.json";
    private const string IngredientsResource = "JiggerJot.Infrastructure.Persistence.Seed.ingredients.json";
    private const string CocktailsResource = "JiggerJot.Infrastructure.Persistence.Seed.cocktails.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Writes every lookup row the file names and the database lacks, and returns how many were added.
    /// Safe to call on every boot.
    /// </summary>
    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        if (db.CurrentTenantId != Guid.Empty)
            throw new InvalidOperationException(
                "CatalogSeeder must run on a context with no ambient household. Shared catalog rows carry "
                + "a null TenantId, and the RLS insert policy admits those only under the bypass GUC, which "
                + "the session interceptor sets for a tenant-less context alone (JJ-031).");

        var file = Load();
        var added = 0;

        added += await SeedGlassTypesAsync(file, cancellationToken);
        added += await SeedMethodsAsync(file, cancellationToken);
        added += await SeedUnitsAsync(file, cancellationToken);
        added += await SeedCategoriesAsync(file, cancellationToken);

        // Ingredients then recipes: each pass points at rows the one before it may have only just
        // created, and they all land in the same SaveChanges below.
        added += await SeedIngredientsAsync(cancellationToken);
        added += await SeedCocktailsAsync(cancellationToken);

        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Catalog seed: added {Added} lookup rows.", added);
        }
        else
        {
            logger.LogDebug("Catalog seed: lookups already present, nothing to add.");
        }

        return added;
    }

    private async Task<int> SeedGlassTypesAsync(LookupFile file, CancellationToken ct)
    {
        var existing = await db.GlassTypes.Select(g => g.Id).ToHashSetAsync(ct);
        var added = 0;
        foreach (var name in file.GlassTypes)
        {
            var id = SeedId.For("glass", name);
            if (existing.Contains(id)) continue;
            db.GlassTypes.Add(new GlassType { Id = id, Name = name });
            added++;
        }
        return added;
    }

    private async Task<int> SeedMethodsAsync(LookupFile file, CancellationToken ct)
    {
        var existing = await db.Methods.Select(m => m.Id).ToHashSetAsync(ct);
        var added = 0;
        foreach (var name in file.Methods)
        {
            var id = SeedId.For("method", name);
            if (existing.Contains(id)) continue;
            db.Methods.Add(new Method { Id = id, Name = name });
            added++;
        }
        return added;
    }

    private async Task<int> SeedUnitsAsync(LookupFile file, CancellationToken ct)
    {
        var existing = await db.Units.Select(u => u.Id).ToHashSetAsync(ct);
        var added = 0;
        foreach (var unit in file.Units)
        {
            var id = SeedId.For("unit", unit.Name);
            if (existing.Contains(id)) continue;

            var system = Enum.Parse<UnitSystem>(unit.System, ignoreCase: true);

            // The one invariant worth failing the boot over: convertibility IS the millilitre factor
            // (JJ-007). A Neutral unit carrying one would silently start being rewritten at display, and
            // a Metric or Imperial unit missing one would silently stop converting — both look like a
            // formatting bug months later, a long way from this file.
            if ((system == UnitSystem.Neutral) != (unit.MillilitreFactor is null))
                throw new InvalidOperationException(
                    $"Seed unit '{unit.Name}' is inconsistent: a Neutral unit must have no millilitre "
                    + "factor, and a Metric or Imperial unit must have one.");

            db.Units.Add(new Unit
            {
                Id = id,
                Name = unit.Name,
                System = system,
                MillilitreFactor = unit.MillilitreFactor,
            });
            added++;
        }
        return added;
    }

    private async Task<int> SeedCategoriesAsync(LookupFile file, CancellationToken ct)
    {
        var existing = await db.IngredientCategories.Select(c => c.Id).ToHashSetAsync(ct);
        var added = 0;
        foreach (var category in file.IngredientCategories)
        {
            // Parent first: a child's ParentId must point at a row that exists by the time this saves,
            // and both live in the same SaveChanges.
            var parentId = SeedId.For("category", category.Name);
            if (!existing.Contains(parentId))
            {
                db.IngredientCategories.Add(new IngredientCategory { Id = parentId, Name = category.Name });
                existing.Add(parentId);
                added++;
            }

            foreach (var child in category.Children)
            {
                // Scoped by the parent name, so "Absinthe" as a child of "Absinthe and pastis" cannot
                // collide with a same-named child elsewhere in the tree.
                var childId = SeedId.For($"category:{category.Name}", child);
                if (existing.Contains(childId)) continue;
                db.IngredientCategories.Add(new IngredientCategory
                {
                    Id = childId,
                    Name = child,
                    ParentId = parentId,
                });
                existing.Add(childId);
                added++;
            }
        }
        return added;
    }

    /// <summary>
    /// The shared ingredient catalog (JJ-011): 175 curated ingredients, each under a category from the
    /// pass above. <b>These are the first rows the seeder writes that carry a tenant column</b>, and they
    /// carry it null — shared, owned by no household. Nothing stamps them (JJ-031), and the
    /// <c>rls_shared_or_tenant_insert</c> policy would reject them outright from a household context,
    /// which is what the ambient-household check at the top of <see cref="SeedAsync"/> is guarding.
    /// </summary>
    private async Task<int> SeedIngredientsAsync(CancellationToken ct)
    {
        var file = LoadIngredients();

        // QueryAllTenants would be wrong here and Query() is right: with no ambient household the
        // shared-or-tenant filter admits exactly the shared rows, which is precisely the set this pass
        // owns. A household's own ingredient must never count as "already seeded".
        var existing = await db.Ingredients
            .Where(i => i.TenantId == null)
            .Select(i => i.Id)
            .ToHashSetAsync(ct);

        var added = 0;
        foreach (var ingredient in file.Ingredients)
        {
            var id = SeedId.For("ingredient", ingredient.Name);
            if (existing.Contains(id)) continue;

            db.Ingredients.Add(new Ingredient
            {
                Id = id,
                TenantId = null,
                Name = ingredient.Name,
                CategoryId = SeedId.For("category", ingredient.Category),
                SubcategoryId = ingredient.Subcategory is null
                    ? null
                    : SeedId.For($"category:{ingredient.Category}", ingredient.Subcategory),
            });
            added++;
        }
        return added;
    }

    /// <summary>
    /// The shared recipe catalog (JJ-012): 969 cocktails and their lines, plus the sources they are
    /// credited to (JJ-032). Shared rows, like the ingredients — <c>TenantId</c> null on both the
    /// cocktail and every one of its lines, since a line carries its parent's nature (JJ-031).
    /// <para>
    /// Glass and method are optional (JJ-034): a recipe that did not say gets null rather than a
    /// plausible guess. A cocktail is written once, whole, or not at all — a partially seeded recipe
    /// is worse than an absent one, so a cocktail already present is skipped rather than reconciled.
    /// </para>
    /// </summary>
    private async Task<int> SeedCocktailsAsync(CancellationToken ct)
    {
        var file = LoadCocktails();
        var added = 0;

        var existingSources = await db.RecipeSources.Select(s => s.Id).ToHashSetAsync(ct);
        foreach (var source in file.Sources)
        {
            var id = SeedId.For("source", source.Name);
            if (existingSources.Contains(id)) continue;
            db.RecipeSources.Add(new RecipeSource
            {
                Id = id,
                Name = source.Name,
                Year = source.Year,
                Url = source.Url,
                Attribution = source.Attribution,
            });
            added++;
        }

        var existing = await db.Cocktails
            .Where(c => c.TenantId == null)
            .Select(c => c.Id)
            .ToHashSetAsync(ct);

        foreach (var cocktail in file.Cocktails)
        {
            // Identity is the SOURCE plus that source's own slug, never the name. Four names appear
            // in both books, and the Savoy alone has "Mr. Manhattan Cocktail" twice, in different
            // chapters and with different recipes. Keyed on the name, one of each pair would
            // silently replace the other, and the loss would surface as a missing drink rather than
            // an error.
            var id = SeedId.For($"cocktail:{cocktail.Source}", cocktail.Slug);
            if (existing.Contains(id)) continue;

            db.Cocktails.Add(new Cocktail
            {
                Id = id,
                TenantId = null,
                Name = cocktail.Name,
                SourceId = SeedId.For("source", cocktail.Source),
                GlassTypeId = cocktail.GlassType is null ? null : SeedId.For("glass", cocktail.GlassType),
                MethodId = cocktail.Method is null ? null : SeedId.For("method", cocktail.Method),
                ServingType = Enum.Parse<ServingType>(cocktail.ServingType, ignoreCase: true),
                Instructions = cocktail.Instructions,
                Lines = [.. cocktail.Lines.Select(line => new CocktailIngredient
                {
                    Id = SeedId.For($"line:{cocktail.Source}:{cocktail.Slug}", $"{line.DisplayOrder}"),
                    TenantId = null,
                    IngredientId = SeedId.For("ingredient", line.Ingredient),
                    Amount = line.Amount,
                    UnitId = line.Unit is null ? null : SeedId.For("unit", line.Unit),
                    IsRequired = line.IsRequired,
                    Role = Enum.Parse<RecipeRole>(line.Role, ignoreCase: true),
                    DisplayOrder = line.DisplayOrder,
                })],
            });
            added += 1 + cocktail.Lines.Count;
        }

        return added;
    }

    /// <summary>Reads the embedded seed file. A missing or malformed file is a build error, not a runtime
    /// condition to tolerate — the app has no catalog vocabulary without it.</summary>
    public static LookupFile Load() => Read<LookupFile>(LookupsResource);

    /// <summary>Reads the embedded ingredient catalog. Same contract as <see cref="Load"/>.</summary>
    public static IngredientFile LoadIngredients() => Read<IngredientFile>(IngredientsResource);

    /// <summary>Reads the embedded recipe catalog. Same contract as <see cref="Load"/>.</summary>
    public static CocktailFile LoadCocktails() => Read<CocktailFile>(CocktailsResource);

    private static T Read<T>(string resource)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException(
                $"Embedded seed resource '{resource}' is missing. It is declared as an EmbeddedResource "
                + "in JiggerJot.Infrastructure.csproj; check that entry before anything else.");

        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Embedded seed resource '{resource}' is empty.");
    }

    public sealed record LookupFile(
        IReadOnlyList<string> GlassTypes,
        IReadOnlyList<string> Methods,
        IReadOnlyList<SeedUnit> Units,
        IReadOnlyList<SeedCategory> IngredientCategories);

    public sealed record SeedUnit(string Name, string System, decimal? MillilitreFactor);

    public sealed record SeedCategory(string Name, IReadOnlyList<string> Children);

    public sealed record IngredientFile(IReadOnlyList<SeedIngredient> Ingredients);

    public sealed record SeedIngredient(string Name, string Category, string? Subcategory);

    public sealed record CocktailFile(
        IReadOnlyList<SeedSource> Sources,
        IReadOnlyList<SeedCocktail> Cocktails);

    public sealed record SeedSource(string Name, int? Year, string? Url, string? Attribution);

    public sealed record SeedCocktail(
        string Slug,
        string Name,
        string Source,
        string? GlassType,
        string? Method,
        string ServingType,
        string? Instructions,
        IReadOnlyList<SeedLine> Lines);

    public sealed record SeedLine(
        string Ingredient,
        decimal? Amount,
        string? Unit,
        bool IsRequired,
        string Role,
        int DisplayOrder);
}
