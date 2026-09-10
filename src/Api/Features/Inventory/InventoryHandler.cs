using Microsoft.EntityFrameworkCore;
using JiggerJot.Core.Abstractions;
using JiggerJot.Core.Entities;
using JiggerJot.Core.Repositories;

namespace JiggerJot.Api.Features.Inventory;

/// <summary>
/// The household shelf (INV) — the checklist the whole product turns on. Until a household has said
/// what it owns, "what can I make right now" has no answer.
/// <para>
/// <see cref="TenantInventory"/> is the one JiggerJot entity that is plainly <c>ITenantScoped</c>, so
/// the platform's global filter, write stamping and generated RLS policy cover it with nothing
/// hand-written here. After the shared-catalog machinery of JJ-031, this handler is deliberately
/// ordinary.
/// </para>
/// </summary>
public class InventoryHandler(
    IRepository<TenantInventory> inventory,
    IRepository<Ingredient> ingredients,
    ICurrentTenant tenant,
    TimeProvider clock)
{
    /// <summary>
    /// The whole catalog this household can see, each row carrying whether the household has it. The
    /// full list rather than just what is ticked, because you cannot tick what you cannot see, and
    /// absence of an inventory row is exactly what "not available" means (JJ-023).
    /// </summary>
    public async Task<IReadOnlyList<ShelfItem>> ListAsync(CancellationToken cancellationToken)
    {
        // Both Query() calls are filtered: ingredients by the shared-or-tenant filter (shared catalog
        // plus this household's own), inventory by the platform's tenant filter. Neither needs a
        // predicate here and neither should grow one.
        var available = await inventory.Query()
            .Where(i => i.IsAvailable)
            .Select(i => i.IngredientId)
            .ToHashSetAsync(cancellationToken);

        var rows = await ingredients.Query()
            .OrderBy(i => i.Category!.Name).ThenBy(i => i.Name)
            .Select(i => new
            {
                i.Id,
                i.Name,
                Category = i.Category!.Name,
                Subcategory = i.Subcategory!.Name,
                IsOwn = i.TenantId != null,
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new ShelfItem(
            r.Id, r.Name, r.Category, r.Subcategory, available.Contains(r.Id), r.IsOwn))];
    }

    /// <summary>
    /// Ticks or unticks one ingredient. Returns false when the ingredient is not one this household
    /// can see, which the endpoint turns into a 404 — a household must not be able to discover
    /// another's custom ingredients by probing ids.
    /// </summary>
    public async Task<bool> SetAsync(Guid ingredientId, bool isAvailable, CancellationToken cancellationToken)
    {
        if (tenant.TenantId is not { } tenantId) return false;

        if (!await ingredients.Query().AnyAsync(i => i.Id == ingredientId, cancellationToken))
            return false;

        var row = await inventory.Query()
            .SingleOrDefaultAsync(i => i.IngredientId == ingredientId, cancellationToken);

        if (row is null)
        {
            row = new TenantInventory { TenantId = tenantId, IngredientId = ingredientId };
            await inventory.AddAsync(row, cancellationToken);
        }

        // Unticking updates the row rather than deleting it. "I checked and I do not have it" is
        // worth keeping apart from "I never looked" — a shopping list would want that difference —
        // and everything that reads the shelf filters on IsAvailable, so absence and false behave
        // identically to every reader either way.
        row.IsAvailable = isAvailable;
        row.UpdatedAt = clock.GetUtcNow();

        await inventory.SaveChangesAsync(cancellationToken);
        return true;
    }
}
