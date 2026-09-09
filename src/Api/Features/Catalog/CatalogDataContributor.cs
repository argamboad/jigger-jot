using Microsoft.EntityFrameworkCore;
using JiggerJot.Core.Abstractions;
using JiggerJot.Core.Entities;
using JiggerJot.Core.Repositories;

namespace JiggerJot.Api.Features.Catalog;

/// <summary>
/// Tenant dissolve + export (ADR-011) for the three dual-natured catalog tables — the household's own
/// ingredients, its own and forked cocktails, and their recipe lines (JJ-011, JJ-012, JJ-013).
/// <para>
/// <b>This contributor exists because the platform's canary cannot see these tables.</b>
/// <c>EveryTenantOwnedEntity_IsWiredIntoTenantDissolution</c> only flags entities with a
/// <b>non-nullable</b> <c>Guid TenantId</c>, and every <see cref="ISharedOrTenantScoped"/> entity has a
/// nullable one by definition — so a forgotten contributor here would orphan a dissolved household's
/// rows silently. JJ-031 records the gap; <c>SharedOrTenantDissolutionTests</c> is the app-level canary
/// that replaces it.
/// </para>
/// <para>
/// <b>Every query is constrained to a non-null tenant id, so the shared catalog is untouchable here.</b>
/// The catalog is read-only and referenced, never mutated (JJ-002) — dissolving a household must not
/// remove a single shared row, not least because every other household still reads them. The database
/// agrees: the <c>rls_shared_or_tenant_delete</c> policy admits owned rows only, so this is enforced
/// twice over.
/// </para>
/// </summary>
public sealed class CatalogDataContributor(
    IRepository<Ingredient> ingredients,
    IRepository<Cocktail> cocktails,
    IRepository<CocktailIngredient> lines) : ITenantDataContributor
{
    /// <summary>
    /// True when the household authored anything of its own. This IS tenant content — a solo owner who
    /// leaves would abandon their own recipes — so unlike billing plumbing it blocks the silent path.
    /// </summary>
    public async Task<bool> HasDataAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        // QueryAllTenants: dissolve runs for a tenant other than the current one, so this crosses
        // tenants by design — the audited escape hatch, re-constrained to the target.
        await cocktails.QueryAllTenants().AnyAsync(c => c.TenantId == tenantId, cancellationToken)
        || await ingredients.QueryAllTenants().AnyAsync(i => i.TenantId == tenantId, cancellationToken);

    /// <summary>
    /// Deletes the household's rows deepest-first. Lines before cocktails is belt-and-braces (the FK
    /// cascades), but lines and cocktails before ingredients is required: both reference
    /// <see cref="Ingredient"/> with <c>Restrict</c>, so a household's custom ingredient cannot go while
    /// one of its own recipe lines still points at it.
    /// </summary>
    public async Task WipeAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // Query() (not QueryAllTenants): the dissolve enters the target tenant (RLS-2/T6), so the filter
        // scopes this to it; composing QueryAllTenants() with a set-based write is banned (RLS-4/T7).
        // The explicit non-null predicate is what keeps the shared catalog (TenantId null) out of range.
        await lines.Query().Where(l => l.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await cocktails.Query().Where(c => c.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
        await ingredients.Query().Where(i => i.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
    }

    public string ExportKey => "catalog";

    /// <summary>
    /// The household's own catalog, by name rather than by id wherever a name exists — an export is read
    /// by a person, and a page of GUIDs tells them nothing about what they wrote. Shared rows are
    /// excluded: they are not this household's data to take.
    /// </summary>
    public async Task<object?> ExportAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var ownIngredients = await ingredients.QueryAllTenants()
            .Where(i => i.TenantId == tenantId)
            .OrderBy(i => i.Name)
            .Select(i => new { i.Id, i.Name, Category = i.Category!.Name, Subcategory = i.Subcategory!.Name })
            .ToListAsync(cancellationToken);

        var ownCocktails = await cocktails.QueryAllTenants()
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.Name,
                Glass = c.GlassType!.Name,
                Method = c.Method!.Name,
                ServingType = c.ServingType.ToString(),
                c.Instructions,
                // Provenance only (JJ-013): the id of whatever this was forked from, never a join.
                c.ForkedFromCocktailId,
                Lines = c.Lines
                    .OrderBy(l => l.DisplayOrder)
                    .Select(l => new
                    {
                        Ingredient = l.Ingredient!.Name,
                        l.Amount,
                        Unit = l.Unit!.Name,
                        l.IsRequired,
                        Role = l.Role.ToString(),
                        l.Notes,
                    }),
            })
            .ToListAsync(cancellationToken);

        return new { ingredients = ownIngredients, cocktails = ownCocktails };
    }
}
