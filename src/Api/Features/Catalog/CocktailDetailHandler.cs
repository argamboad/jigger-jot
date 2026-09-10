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
/// <b>It also says where the drink stands against the shelf</b> (CKTL-4, FEATURES §12): makeable,
/// one ingredient away, or further off — and, on each line, whether the household would pour the
/// bottle the recipe names or something the graph allows instead. Derived at query time and never
/// stored, like everything else about makeability (JJ-003, JJ-019).
/// </para>
/// <para>
/// Like the browse handler, there is no tenant predicate: <c>Query()</c> carries the shared-or-tenant
/// filter, so a cocktail belonging to another household is simply not found (JJ-031). The shelf and
/// the substitution graph are read through repositories for the same reason — the platform's filter
/// decides whose shelf this is, and re-spelling that by hand is how it eventually gets spelled wrong.
/// </para>
/// </summary>
public class CocktailDetailHandler(
    IRepository<Cocktail> cocktails,
    IUserRepository users,
    IRepository<TenantInventory> inventory,
    IRepository<IngredientSubstitution> substitutions)
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
                c.ForkedFromCocktailId,
                Source = c.Source == null
                    ? null
                    : new CocktailSourceView(c.Source.Name, c.Source.Year, c.Source.Url, c.Source.Attribution),
                Lines = c.Lines
                    .OrderBy(l => l.DisplayOrder)
                    .Select(l => new
                    {
                        l.IngredientId,
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

        var wanted = cocktail.Lines.Select(l => l.IngredientId).Distinct().ToList();
        var (available, substitutes, names) = await ShelfAsync(wanted, cancellationToken);

        var lines = cocktail.Lines.Select(l =>
        {
            var unit = l.UnitName is null || l.UnitSystem is null
                ? null
                : new UnitView(l.UnitName, l.UnitSystem.Value, l.MillilitreFactor);

            var stands = Makeability.ForLine(l.IngredientId, available, substitutes);

            return new RecipeLineView(
                l.Ingredient,
                l.Amount,
                l.UnitName,
                AmountDisplay.Format(l.Amount, unit, preferred),
                l.IsRequired,
                l.Role.ToString(),
                l.Notes,
                stands.Availability.ToString(),
                stands.SubstituteIngredientId is { } sub ? names.GetValueOrDefault(sub) : null);
        }).ToList();

        // Read back off the lines rather than computed a second way, so the badge at the top and the
        // marks down the page can never tell the reader two different things.
        var status = Makeability.Overall(
            cocktail.Lines.Zip(lines, (source, view) =>
                (source.IsRequired, Enum.Parse<LineAvailability>(view.Availability))));

        return new CocktailDetail(
            cocktail.Id,
            cocktail.Name,
            cocktail.Glass,
            cocktail.Method,
            cocktail.ServingType.ToString(),
            cocktail.Instructions,
            cocktail.Source,
            cocktail.TenantId != null,
            lines,
            status.ToString(),
            await ForkOriginAsync(cocktail.ForkedFromCocktailId, cancellationToken));
    }

    /// <summary>
    /// What this cocktail was copied from, by name (FORK-1). A second small query rather than a join,
    /// because the link is not a foreign key and EF has no navigation to follow.
    /// </summary>
    /// <remarks>
    /// Null when the original has been deleted, and that is the point of it not being a foreign key
    /// (JJ-013): the copy survives, and only loses the ability to say where it came from. Null too
    /// when the original belongs to a household this one cannot see — <c>Query()</c> decides that,
    /// not a predicate written here.
    /// </remarks>
    private async Task<ForkOriginView?> ForkOriginAsync(Guid? originalId, CancellationToken cancellationToken)
    {
        if (originalId is not { } id) return null;

        return await cocktails.Query()
            .Where(c => c.Id == id)
            .Select(c => new ForkOriginView(c.Id, c.Name))
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// What the household has, what it may pour instead, and what those bottles are called — for this
    /// recipe's ingredients only.
    /// </summary>
    /// <remarks>
    /// Narrowed to the lines on this one page rather than loading a shelf of two hundred ingredients
    /// to answer a question about three. The substitution rows are ordered by the substitute's name so
    /// that a line with several valid stand-ins always suggests the same one, rather than whichever
    /// row the database happened to return first.
    /// </remarks>
    private async Task<(HashSet<Guid> Available,
                       Dictionary<Guid, IReadOnlyList<Guid>> Substitutes,
                       Dictionary<Guid, string> Names)>
        ShelfAsync(List<Guid> wanted, CancellationToken cancellationToken)
    {
        // Directed (JJ-006): keyed by the ingredient a recipe ASKS FOR, and never read the other way.
        var rows = await substitutions.Query()
            .Where(s => wanted.Contains(s.IngredientId))
            .OrderBy(s => s.SubstituteIngredient!.Name)
            .Select(s => new { s.IngredientId, s.SubstituteIngredientId, Name = s.SubstituteIngredient!.Name })
            .ToListAsync(cancellationToken);

        var candidates = rows.Select(r => r.SubstituteIngredientId).Distinct().ToList();

        var available = await inventory.Query()
            .Where(i => i.IsAvailable && (wanted.Contains(i.IngredientId) || candidates.Contains(i.IngredientId)))
            .Select(i => i.IngredientId)
            .ToListAsync(cancellationToken);

        return (
            [.. available],
            rows.GroupBy(r => r.IngredientId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)[.. g.Select(r => r.SubstituteIngredientId)]),
            rows.DistinctBy(r => r.SubstituteIngredientId).ToDictionary(r => r.SubstituteIngredientId, r => r.Name));
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
