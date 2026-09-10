using Microsoft.EntityFrameworkCore;
using JiggerJot.Core.Catalog;
using JiggerJot.Core.Entities;
using JiggerJot.Core.Repositories;

namespace JiggerJot.Api.Features.Catalog;

/// <summary>
/// One cocktail, whole (CKTL-3).
/// <para>
/// <b>Amounts are converted here and stored nowhere.</b> The reader's <c>PreferredUnitSystem</c> picks
/// the system, <see cref="AmountDisplay"/> does the arithmetic, and the authored amount and unit ride
/// along untouched in the response (JJ-007, JJ-008). Converting on the server rather than in each
/// client means one implementation to get right and one to test, which matters more once there is
/// more than one front end.
/// </para>
/// <para>
/// Like the browse handler, there is no tenant predicate: <c>Query()</c> carries the shared-or-tenant
/// filter, so a cocktail belonging to another household is simply not found (JJ-031).
/// </para>
/// </summary>
public class CocktailDetailHandler(IRepository<Cocktail> cocktails, IUserRepository users)
{
    public async Task<CocktailDetail?> GetAsync(Guid id, Guid? userId, CancellationToken cancellationToken)
    {
        var cocktail = await cocktails.Query()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id,
                c.Name,
                Glass = c.GlassType!.Name,
                Method = c.Method!.Name,
                c.ServingType,
                c.Instructions,
                c.TenantId,
                Source = c.Source == null
                    ? null
                    : new CocktailSourceView(c.Source.Name, c.Source.Year, c.Source.Url, c.Source.Attribution),
                Lines = c.Lines
                    .OrderBy(l => l.DisplayOrder)
                    .Select(l => new
                    {
                        Ingredient = l.Ingredient!.Name,
                        l.Amount,
                        UnitName = l.Unit!.Name,
                        UnitSystem = (UnitSystem?)l.Unit!.System,
                        l.Unit!.MillilitreFactor,
                        l.IsRequired,
                        l.Role,
                        l.Notes,
                    })
                    .ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (cocktail is null) return null;

        var preferred = await PreferredSystemAsync(userId, cancellationToken);

        return new CocktailDetail(
            cocktail.Id,
            cocktail.Name,
            cocktail.Glass,
            cocktail.Method,
            cocktail.ServingType.ToString(),
            cocktail.Instructions,
            cocktail.Source,
            cocktail.TenantId != null,
            [.. cocktail.Lines.Select(l =>
            {
                var unit = l.UnitName is null || l.UnitSystem is null
                    ? null
                    : new UnitView(l.UnitName, l.UnitSystem.Value, l.MillilitreFactor);

                return new RecipeLineView(
                    l.Ingredient,
                    l.Amount,
                    l.UnitName,
                    AmountDisplay.Format(l.Amount, unit, preferred),
                    l.IsRequired,
                    l.Role.ToString(),
                    l.Notes);
            })]);
    }

    /// <summary>
    /// The reader's preference, or null when they have never chosen — in which case the recipe is
    /// shown exactly as its book wrote it, which is the honest default rather than a guess at what
    /// they would have picked.
    /// </summary>
    /// <remarks>
    /// Through <see cref="IUserRepository"/> rather than the generic repository. User is a platform
    /// entity with its own repository, and the generic one's cross-tenant escape hatch is banned in
    /// feature slices for good reason — an architecture test says so, and it said so about the first
    /// draft of this method.
    /// </remarks>
    private async Task<UnitSystem?> PreferredSystemAsync(Guid? userId, CancellationToken cancellationToken)
    {
        if (userId is not { } id) return null;
        var user = await users.GetByIdAsync(id, cancellationToken);
        return user?.PreferredUnitSystem;
    }
}
