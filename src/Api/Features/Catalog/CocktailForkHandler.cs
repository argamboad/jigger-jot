using Microsoft.EntityFrameworkCore;
using JiggerJot.Core.Abstractions;
using JiggerJot.Core.Entities;
using JiggerJot.Core.Repositories;

namespace JiggerJot.Api.Features.Catalog;

/// <summary>
/// "Create my own version" (FORK-1, FEATURES §13). A household takes any cocktail it can see and gets
/// its own copy to change.
/// <para>
/// <b>A snapshot, never a reference</b> (JJ-002, JJ-013). The shared catalog is read-only and stays
/// that way; personalising a drink copies it rather than mutating it, and the copy is then wholly the
/// household's. Later edits to the original never propagate here, which is the entire reason the fork
/// is a copy of the rows rather than a pointer at them.
/// </para>
/// <para>
/// <c>ForkedFromCocktailId</c> is provenance and nothing else. It is deliberately not a foreign key,
/// so deleting the original neither blocks nor cascades — the copy simply stops being able to say
/// where it came from.
/// </para>
/// </summary>
public class CocktailForkHandler(IRepository<Cocktail> cocktails, ICurrentTenant tenant)
{
    /// <summary>
    /// Copies <paramref name="id"/> into this household. Returns the new cocktail's id, or null when
    /// the original is not one this household can see — which the endpoint turns into a 404.
    /// </summary>
    public async Task<Guid?> ForkAsync(Guid id, CancellationToken cancellationToken)
    {
        if (tenant.TenantId is not { } tenantId) return null;

        // Query() carries the shared-or-tenant filter, so this reads the shared catalog plus the
        // household's own rows and nothing else. Another household's cocktail is simply not found
        // (JJ-031) — no predicate here to get wrong.
        var original = await cocktails.Query()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Name,
                c.GlassTypeId,
                c.MethodId,
                c.ServingType,
                c.Instructions,
                Lines = c.Lines.Select(l => new
                {
                    l.IngredientId,
                    l.Amount,
                    l.UnitId,
                    l.IsRequired,
                    l.Role,
                    l.DisplayOrder,
                    l.Notes,
                }).ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (original is null) return null;

        var fork = new Cocktail
        {
            // Set by hand on the cocktail AND on every line below. Nothing stamps either: the
            // interceptor keys off ITenantScoped and these tables deliberately are not (JJ-031). A
            // line written without one would land in the shared catalog attached to a household's
            // recipe, which is the worst of both and invisible until it turned up in someone else's
            // app.
            TenantId = tenantId,
            ForkedFromCocktailId = id,
            Name = original.Name,
            GlassTypeId = original.GlassTypeId,
            MethodId = original.MethodId,
            ServingType = original.ServingType,
            Instructions = original.Instructions,

            // SourceId is deliberately NOT copied. The book wrote the original, not this household's
            // version of it, and the credit is a real claim rather than decoration (JJ-032) — carrying
            // it across would attribute whatever the household does next to Craddock or the IBA.
            // Provenance rides on ForkedFromCocktailId instead: "based on", not "written by".
            Lines = [.. original.Lines.Select(l => new CocktailIngredient
            {
                TenantId = tenantId,
                IngredientId = l.IngredientId,
                Amount = l.Amount,
                UnitId = l.UnitId,
                IsRequired = l.IsRequired,
                Role = l.Role,
                DisplayOrder = l.DisplayOrder,
                Notes = l.Notes,
            })],
        };

        await cocktails.AddAsync(fork, cancellationToken);
        await cocktails.SaveChangesAsync(cancellationToken);

        return fork.Id;
    }
}
