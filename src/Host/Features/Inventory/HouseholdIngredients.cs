using JiggerJot.Host.Catalog;
using JiggerJot.Host.Entities;
using Perezosoft.Core.Entities;
using Perezosoft.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JiggerJot.Host.Features.Inventory;

/// <summary>The shelf's implementation of <see cref="IHouseholdIngredients"/>: the slice that owns a household's bottles wipes them (Arch A8).</summary>
public sealed class HouseholdIngredients(IRepository<Ingredient> ingredients) : IHouseholdIngredients
{
    public Task WipeAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        ingredients.Query().Where(i => i.TenantId == tenantId).ExecuteDeleteAsync(cancellationToken);
}
