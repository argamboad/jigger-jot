using Microsoft.EntityFrameworkCore;
using JiggerJot.Core.Abstractions;
using JiggerJot.Core.Entities;
using JiggerJot.Core.Repositories;

namespace JiggerJot.Api.Features.Inventory;

/// <summary>
/// Tenant dissolve + export (ADR-011) for the household shelf. <see cref="TenantInventory"/> is ordinary
/// <c>ITenantScoped</c> data with no FK to Tenants (ADR-003 plain-Guid tenancy), so nothing cascades and
/// without this the shelf would outlive the household it belonged to.
/// <para>
/// <see cref="HasDataAsync"/> is <c>true</c> when the shelf has any row: a household's inventory is the
/// thing the whole product is built on, so a solo owner should not walk away from it silently.
/// </para>
/// </summary>
public sealed class InventoryDataContributor(IRepository<TenantInventory> inventory) : ITenantDataContributor
{
    public Task<bool> HasDataAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        // QueryAllTenants: dissolve runs for a tenant other than the current one, so this crosses
        // tenants by design — the audited escape hatch, re-constrained to the target.
        inventory.QueryAllTenants().AnyAsync(i => i.TenantId == tenantId, cancellationToken);

    public async Task WipeAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        // Query() (not QueryAllTenants): the dissolve enters the target tenant (RLS-2/T6), so the filter
        // scopes this to it; composing QueryAllTenants() with a set-based write is banned (RLS-4/T7).
        await inventory.Query()
            .Where(i => i.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

    public string ExportKey => "inventory";

    /// <summary>
    /// The shelf by ingredient name — a list of ids would be useless to the person reading the export.
    /// Absence of a row means "not available" (JJ-023), so an empty list is the honest answer for a
    /// household that never marked anything.
    /// </summary>
    public async Task<object?> ExportAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await inventory.QueryAllTenants()
            .Where(i => i.TenantId == tenantId)
            .OrderBy(i => i.Ingredient!.Name)
            .Select(i => new { Ingredient = i.Ingredient!.Name, i.IsAvailable, i.UpdatedAt })
            .ToListAsync(cancellationToken);
}
