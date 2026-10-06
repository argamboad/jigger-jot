using Microsoft.EntityFrameworkCore;
using JiggerJot.Core.Abstractions;
using JiggerJot.Core.Entities;
using JiggerJot.Core.Repositories;

namespace JiggerJot.Api.Features.Inventory;

/// <summary>
/// A household deletes a bottle it added (INV-4) — the other half of INV-2's add.
/// </summary>
/// <remarks>
/// <para>
/// <b>A bottle one of the household's own recipes still uses is refused, and the refusal names them.</b>
/// Taking it out of those recipes instead would change what each recipe says, and whether it is makeable,
/// without anyone deciding to — and a recipe left with no lines would be "makeable" out of nothing. The
/// recipe lines reference the ingredient with <c>Restrict</c>, so the database would refuse the delete
/// anyway; this turns that into an answer someone can act on. Only this household's lines can reference
/// its bottle, because no other household can see it (JJ-031).
/// </para>
/// <para>
/// Its shelf row goes with it through the <c>TenantInventory</c> foreign key's cascade. The substitution
/// graph never references a household's bottle — both ends of a substitution are shared rows (JJ-005).
/// </para>
/// <para>
/// A separate handler from <see cref="InventoryHandler"/> because it needs the recipe lines, which nothing
/// else on the shelf reads.
/// </para>
/// </remarks>
public class IngredientRemovalHandler(
    IRepository<Ingredient> ingredients,
    IRepository<CocktailIngredient> recipeLines,
    ICurrentTenant tenant)
{
    public async Task<RemoveIngredientResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        // Query(): the shared catalog plus this household's own (JJ-031), so another household's bottle is
        // simply not found — never forbidden, which would confirm it exists.
        var ingredient = await ingredients.Query().SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (ingredient is null || tenant.TenantId is not { } tenantId)
            return new RemoveIngredientResult(RemoveIngredientOutcome.NotFound);

        // The shared catalog is read-only and referenced, never mutated (JJ-002).
        if (ingredient.TenantId != tenantId)
            return new RemoveIngredientResult(RemoveIngredientOutcome.ReadOnly);

        var usedIn = await recipeLines.Query()
            .Where(l => l.IngredientId == id)
            .Select(l => l.Cocktail!.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);
        if (usedIn.Count > 0)
            return new RemoveIngredientResult(RemoveIngredientOutcome.InUse, usedIn);

        ingredients.Remove(ingredient);
        await ingredients.SaveChangesAsync(cancellationToken);

        return new RemoveIngredientResult(RemoveIngredientOutcome.Deleted);
    }
}
