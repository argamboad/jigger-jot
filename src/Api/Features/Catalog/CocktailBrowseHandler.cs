using Microsoft.EntityFrameworkCore;
using JiggerJot.Core.Entities;
using JiggerJot.Core.Repositories;

namespace JiggerJot.Api.Features.Catalog;

/// <summary>
/// Browsing the cocktail catalog (CKTL-2) — the first feature to read what SEED-1 to SEED-4 wrote.
/// <para>
/// <b>There is no <c>Where</c> on the tenant here, and that is the point.</b> <c>Query()</c> carries
/// the shared-or-tenant filter from <c>AppDbContext</c>, so this sees the shared catalog plus the
/// household's own rows and can no more leak across households than the platform's own slices can
/// (JJ-031). A slice that re-spelled the predicate by hand would be the one that eventually got it
/// wrong.
/// </para>
/// </summary>
public class CocktailBrowseHandler(
    IRepository<Cocktail> cocktails,
    IRepository<TenantInventory> inventory,
    IRepository<IngredientSubstitution> substitutions)
{
    public async Task<PagedResponse<CocktailSummary>> BrowseAsync(
        CocktailBrowseRequest request, CancellationToken cancellationToken)
    {
        var page = request.SafePage;
        var pageSize = request.SafePageSize;

        var query = cocktails.Query();

        // The household's shelf, tenant-filtered by the platform.
        var available = inventory.Query().Where(i => i.IsAvailable).Select(i => i.IngredientId);

        // Directed (JJ-006): a row says "when a recipe asks for Ingredient you may pour Substitute".
        // Reading it in that direction is what keeps a one-way substitution one-way — cognac stands in
        // for brandy, brandy does not stand in for cognac, and a household with only brandy must not be
        // offered a drink it cannot actually make well.
        var subs = substitutions.Query();

        if (request.MakeableOnly)
        {
            // Makeable = every REQUIRED line satisfied, by the exact ingredient or by a valid
            // substitute (DATA_MODEL derived rules, JJ-003). Optional lines never block (JJ-009),
            // which is why a garnish is a line rather than a special case.
            query = query.Where(c => !c.Lines.Any(l =>
                l.IsRequired
                && !available.Contains(l.IngredientId)
                && !subs.Any(s => s.IngredientId == l.IngredientId
                                  && available.Contains(s.SubstituteIngredientId))));
        }

        if (request.SafeSearch is { } search)
        {
            // ILike rather than ToLower().Contains(): it is the Postgres operator for exactly this,
            // it matches anywhere in the name rather than only at the front, and it leaves room for a
            // trigram index later without the query changing shape. The escapes matter — a name with
            // a % in it should search for a %, not for anything at all.
            var pattern = $"%{search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            query = query.Where(c => EF.Functions.ILike(c.Name, pattern, "\\"));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            // Name, then Id. Name alone is NOT a total order in this catalog — four names appear in
            // both books and "Mr. Manhattan Cocktail" appears twice in the Savoy — and a non-total
            // order makes Postgres free to return page 2 overlapping page 1.
            .OrderBy(c => c.Name).ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CocktailSummary(
                c.Id,
                c.Name,
                c.GlassType!.Name,
                c.Method!.Name,
                c.ServingType.ToString(),
                c.Source!.Name,
                c.TenantId != null,
                c.Lines.Count,
                Array.Empty<SubstitutionInPlay>()))
            .ToListAsync(cancellationToken);

        if (request.MakeableOnly && items.Count > 0)
            items = await WithSubstitutionsAsync(items, available, subs, cancellationToken);

        return new PagedResponse<CocktailSummary>(items, page, pageSize, total);
    }

    /// <summary>
    /// Fills in "using X in place of Y" for the rows on this page (FEATURES §9). A drink that
    /// qualified only because the household owns a substitute has to say so, or someone is told they
    /// can make a Margarita and finds out at the shelf that they cannot.
    /// </summary>
    /// <remarks>
    /// A second query over the page's ids rather than a join in the first: the page is at most 100
    /// rows, and folding this into the paged projection would multiply rows and make the paging
    /// arithmetic wrong.
    /// </remarks>
    private async Task<List<CocktailSummary>> WithSubstitutionsAsync(
        List<CocktailSummary> items,
        IQueryable<Guid> available,
        IQueryable<IngredientSubstitution> subs,
        CancellationToken cancellationToken)
    {
        var ids = items.Select(i => i.Id).ToList();

        var inPlay = await cocktails.Query()
            .Where(c => ids.Contains(c.Id))
            .SelectMany(c => c.Lines
                // Exactly the lines the household cannot pour as written. Every one of these is
                // covered by a substitute, or the cocktail would not be on this page at all.
                .Where(l => l.IsRequired && !available.Contains(l.IngredientId))
                .SelectMany(l => subs
                    .Where(s => s.IngredientId == l.IngredientId
                                && available.Contains(s.SubstituteIngredientId))
                    .Select(s => new
                    {
                        CocktailId = c.Id,
                        AsksFor = l.Ingredient!.Name,
                        YouHave = s.SubstituteIngredient!.Name,
                    })))
            .ToListAsync(cancellationToken);

        var byCocktail = inPlay
            .GroupBy(x => x.CocktailId)
            .ToDictionary(
                g => g.Key,
                // One suggestion per asked-for ingredient: several bottles may qualify, and listing
                // them all turns a helpful line into a paragraph. First by name, so it is stable.
                g => (IReadOnlyList<SubstitutionInPlay>)[.. g
                    .GroupBy(x => x.AsksFor)
                    .Select(a => new SubstitutionInPlay(a.Key, a.OrderBy(x => x.YouHave).First().YouHave))
                    .OrderBy(x => x.AsksFor)]);

        return [.. items.Select(i => byCocktail.TryGetValue(i.Id, out var subsFor)
            ? i with { Substitutions = subsFor }
            : i)];
    }
}
