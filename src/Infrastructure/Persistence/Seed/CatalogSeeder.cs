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

    private const string ResourceName = "JiggerJot.Infrastructure.Persistence.Seed.lookups.json";

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

    /// <summary>Reads the embedded seed file. A missing or malformed file is a build error, not a runtime
    /// condition to tolerate — the app has no catalog vocabulary without it.</summary>
    public static LookupFile Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded seed resource '{ResourceName}' is missing. It is declared as an EmbeddedResource "
                + "in JiggerJot.Infrastructure.csproj; check that entry before anything else.");

        return JsonSerializer.Deserialize<LookupFile>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Embedded seed resource '{ResourceName}' is empty.");
    }

    public sealed record LookupFile(
        IReadOnlyList<string> GlassTypes,
        IReadOnlyList<string> Methods,
        IReadOnlyList<SeedUnit> Units,
        IReadOnlyList<SeedCategory> IngredientCategories);

    public sealed record SeedUnit(string Name, string System, decimal? MillilitreFactor);

    public sealed record SeedCategory(string Name, IReadOnlyList<string> Children);
}
