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
    IRepository<IngredientCategory> categories,
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

    /// <summary>
    /// A whole shelf in one request (ONBOARD-1, FEATURES §7 — "optimized for fast bulk-checking").
    /// </summary>
    /// <remarks>
    /// One round trip and one <c>SaveChanges</c>, so the shelf either arrives as the wizard left it
    /// or not at all. The per-ingredient <see cref="SetAsync"/> stays exactly as it is: on the shelf
    /// screen a tick is a decision someone just made and should be saved before they look away, while
    /// nothing in the wizard is confirmed until Finish.
    /// <para>
    /// Safe to send twice, because a wizard finishing on a flaky connection is the case that produces
    /// a retry: each ingredient's row is found or created once and then SET to the state asked for,
    /// so a repeat lands on the same shelf rather than a second row per ingredient.
    /// </para>
    /// <para>
    /// An id this household cannot see is reported rather than written, and rather than failing the
    /// whole request — the catalog can change under a wizard that has been open a while, and losing
    /// eleven good ticks because the twelfth went stale is the wrong trade.
    /// </para>
    /// </remarks>
    public async Task<BulkSetResult> SetManyAsync(
        BulkSetAvailabilityRequest request, CancellationToken cancellationToken)
    {
        if (tenant.TenantId is not { } tenantId) return new BulkSetResult(0, []);

        // Last word wins for a repeated id. Not a case the wizard produces, but one a caller can
        // send, and refusing it would mean rejecting a request that has an obvious answer.
        var wanted = new Dictionary<Guid, bool>();
        foreach (var item in request.Items) wanted[item.IngredientId] = item.IsAvailable;
        if (wanted.Count == 0) return new BulkSetResult(0, []);

        var ids = wanted.Keys.ToList();

        // Query() carries the shared-or-tenant filter, so this is also the authorization check: an id
        // belonging to another household's custom ingredient simply is not in the result.
        var visible = await ingredients.Query()
            .Where(i => ids.Contains(i.Id))
            .Select(i => i.Id)
            .ToHashSetAsync(cancellationToken);

        var existing = await inventory.Query()
            .Where(i => ids.Contains(i.IngredientId))
            .ToDictionaryAsync(i => i.IngredientId, cancellationToken);

        var now = clock.GetUtcNow();
        var applied = 0;

        foreach (var (ingredientId, isAvailable) in wanted)
        {
            if (!visible.Contains(ingredientId)) continue;

            if (!existing.TryGetValue(ingredientId, out var row))
            {
                row = new TenantInventory { TenantId = tenantId, IngredientId = ingredientId };
                await inventory.AddAsync(row, cancellationToken);
            }

            // Set, never flipped, and unticking updates the row rather than deleting it — the same
            // rule the single-ingredient write follows, for the same reason (INV-1, JJ-023).
            row.IsAvailable = isAvailable;
            row.UpdatedAt = now;
            applied++;
        }

        await inventory.SaveChangesAsync(cancellationToken);

        return new BulkSetResult(applied, [.. wanted.Keys.Where(id => !visible.Contains(id))]);
    }

    /// <summary>
    /// The two-level category tree, for the picker on the add form (JJ-015). Curated and global —
    /// no household additions in MVP (JJ-022) — so this is a read and will stay one.
    /// </summary>
    public async Task<IReadOnlyList<CategoryOption>> CategoriesAsync(CancellationToken cancellationToken)
    {
        var rows = await categories.Query()
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.ParentId })
            .ToListAsync(cancellationToken);

        var byParent = rows.Where(c => c.ParentId is not null).ToLookup(c => c.ParentId!.Value);

        return [.. rows
            .Where(c => c.ParentId is null)
            .Select(c => new CategoryOption(c.Id, c.Name,
                [.. byParent[c.Id].Select(s => new CategoryOption(s.Id, s.Name, []))]))];
    }

    /// <summary>
    /// Adds an ingredient this household owns, and ticks it (INV-2, FEATURES §8).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ticked, not merely tickable.</b> Someone adds a bottle to their shelf because it is on their
    /// shelf; making them add it and then tick it is two actions for one intent. It unticks like any
    /// other row.
    /// </para>
    /// <para>
    /// <b><c>TenantId</c> is set by hand and that is not an oversight.</b> The stamping interceptor
    /// keys off <c>ITenantScoped</c>, which this table deliberately is not (JJ-031) — a row written
    /// without this line would land in the SHARED catalog, visible to every household on the platform.
    /// The database would allow the insert to be attempted and its policy would refuse it, which is
    /// the backstop working, but the fix belongs here.
    /// </para>
    /// </remarks>
    public async Task<AddIngredientResult> AddIngredientAsync(
        AddIngredientRequest request, CancellationToken cancellationToken)
    {
        if (tenant.TenantId is not { } tenantId)
            return new AddIngredientResult(AddIngredientOutcome.InvalidCategory);

        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 200)
            return new AddIngredientResult(AddIngredientOutcome.InvalidName);

        if (!await IsValidPlacementAsync(request.CategoryId, request.SubcategoryId, cancellationToken))
            return new AddIngredientResult(AddIngredientOutcome.InvalidCategory);

        // Case-insensitively, and across everything this household can see — its own rows AND the
        // shared catalog. The unique index only covers the first of those: it is keyed on
        // (TenantId, Name), so a household's "campari" and the catalog's "Campari" are different rows
        // to the database. Two Camparis on one shelf is nobody's intent, and the custom one would
        // satisfy recipe lines by exact name only (JJ-018) — so it would quietly not do what its
        // owner expected.
        var existing = await ingredients.Query()
            .Where(i => EF.Functions.ILike(i.Name, name.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_"), "\\"))
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is { } already)
            return new AddIngredientResult(AddIngredientOutcome.AlreadyExists, ExistingIngredientId: already);

        var ingredient = new Ingredient
        {
            TenantId = tenantId,   // see the remarks — JJ-031, nothing stamps this
            Name = name,
            CategoryId = request.CategoryId,
            SubcategoryId = request.SubcategoryId,
        };
        await ingredients.AddAsync(ingredient, cancellationToken);
        await ingredients.SaveChangesAsync(cancellationToken);

        await SetAsync(ingredient.Id, true, cancellationToken);

        var placement = await categories.Query()
            .Where(c => c.Id == request.CategoryId || c.Id == request.SubcategoryId)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(cancellationToken);

        return new AddIngredientResult(
            AddIngredientOutcome.Created,
            new ShelfItem(
                ingredient.Id,
                ingredient.Name,
                placement.Single(c => c.Id == request.CategoryId).Name,
                request.SubcategoryId is { } sub ? placement.Single(c => c.Id == sub).Name : null,
                IsAvailable: true,
                IsOwn: true));
    }

    /// <summary>
    /// A top-level category, and a subcategory that is one of ITS children. The tree is deliberately
    /// two levels (JJ-015), and a parent matches all its children when filtering (JJ-016), so a
    /// mismatched pair breaks browsing and filtering both.
    /// </summary>
    private async Task<bool> IsValidPlacementAsync(
        Guid categoryId, Guid? subcategoryId, CancellationToken cancellationToken)
    {
        if (!await categories.Query().AnyAsync(c => c.Id == categoryId && c.ParentId == null, cancellationToken))
            return false;

        return subcategoryId is not { } sub
            || await categories.Query().AnyAsync(c => c.Id == sub && c.ParentId == categoryId, cancellationToken);
    }
}
