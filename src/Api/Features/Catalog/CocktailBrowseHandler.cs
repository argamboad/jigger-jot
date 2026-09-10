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

        if (request.AlmostMakeableOnly)
        {
            // Almost makeable = exactly ONE required line unsatisfied, after substitutions
            // (FEATURES §10, JJ-019). Same predicate as above, counted rather than negated — which is
            // what keeps the two lists adjacent and non-overlapping: zero short is makeable, one short
            // is a shopping list, and no drink is both.
            //
            // Counted over LINES, not over ingredients, because that is what the requirement says and
            // a recipe that asks for the same bottle twice is short of one thing, not two — the count
            // and the name below have to agree, and they do because they run the same filter.
            query = query.Where(c => c.Lines.Count(l =>
                l.IsRequired
                && !available.Contains(l.IngredientId)
                && !subs.Any(s => s.IngredientId == l.IngredientId
                                  && available.Contains(s.SubstituteIngredientId))) == 1);
        }

        if (request.SafeIngredient is { } ingredient)
        {
            // FEATURES §11 and JJ-016: name, category AND subcategory, all at once. Matching the
            // category is what makes a parent catch every child — nobody types "London dry gin" when
            // they mean gin — and matching the name is what finds the elderflower nobody tagged.
            // Read off the recipe lines, because there is no stored classification to read instead
            // and a drink with two spirits or none is exactly why (JJ-014).
            var pattern = Contains(ingredient);
            query = query.Where(c => c.Lines.Any(l =>
                EF.Functions.ILike(l.Ingredient!.Name, pattern, "\\")
                || EF.Functions.ILike(l.Ingredient!.Category!.Name, pattern, "\\")
                || (l.Ingredient!.Subcategory != null
                    && EF.Functions.ILike(l.Ingredient!.Subcategory!.Name, pattern, "\\"))));
        }

        // Straight equality, and an id nobody recognises simply matches nothing. A read with a bad id
        // is a caller's typo rather than a reason to hand back a 400 — the same reasoning that clamps
        // a page number instead of rejecting it. A recipe that never stated a glass or a method
        // (JJ-034) does not match either, which is the honest answer: no glass is not a glass.
        if (request.MethodId is { } methodId) query = query.Where(c => c.MethodId == methodId);
        if (request.GlassTypeId is { } glassId) query = query.Where(c => c.GlassTypeId == glassId);
        if (request.ServingType is { } serving) query = query.Where(c => c.ServingType == serving);

        if (request.SafeSearch is { } search)
        {
            // ILike rather than ToLower().Contains(): it is the Postgres operator for exactly this,
            // it matches anywhere in the name rather than only at the front, and it leaves room for a
            // trigram index later without the query changing shape.
            query = query.Where(c => EF.Functions.ILike(c.Name, Contains(search), "\\"));
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

        if (items.Count > 0)
        {
            // Both lists claim a drink is within reach, so both owe the household the truth about
            // what it would actually pour (FEATURES §9). "One lemon juice away, and the Cointreau
            // will be Curaçao" is one useful row; the same row without the second half sends someone
            // to the shop and still leaves them short.
            if (request.MakeableOnly || request.AlmostMakeableOnly)
                items = await WithSubstitutionsAsync(items, available, subs, cancellationToken);

            if (request.AlmostMakeableOnly)
                items = await WithMissingIngredientAsync(items, available, subs, cancellationToken);
        }

        return new PagedResponse<CocktailSummary>(items, page, pageSize, total);
    }

    /// <summary>
    /// What the filter dropdowns offer (FILTER-1) — read off the catalog rather than off the lookup
    /// tables, so every option returns at least one drink.
    /// </summary>
    /// <remarks>
    /// The curated lookups hold nineteen glasses and ten methods; any given catalog uses a fraction of
    /// them, and a filter whose options mostly return nothing reads as broken rather than as precise.
    /// Deriving them also means the lists grow by themselves when the full 969-recipe catalog is
    /// switched on. <c>Query()</c> carries the shared-or-tenant filter, so a household's own cocktails
    /// contribute their glasses and methods too, and another household's never do.
    /// </remarks>
    public async Task<CatalogFilterOptions> FilterOptionsAsync(CancellationToken cancellationToken)
    {
        // Anonymous types through the DISTINCT, records only once the rows are back: EF cannot
        // translate a Distinct over a projection into a positional record, and finding that out from
        // a runtime exception is worse than writing the extra Select.
        var methods = await cocktails.Query()
            .Where(c => c.MethodId != null)
            .Select(c => new { Id = c.MethodId!.Value, c.Method!.Name })
            .Distinct().OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);

        var glasses = await cocktails.Query()
            .Where(c => c.GlassTypeId != null)
            .Select(c => new { Id = c.GlassTypeId!.Value, c.GlassType!.Name })
            .Distinct().OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);

        var serving = await cocktails.Query()
            .Select(c => c.ServingType).Distinct()
            .ToListAsync(cancellationToken);

        return new CatalogFilterOptions(
            [.. methods.Select(m => new FilterOption(m.Id, m.Name))],
            [.. glasses.Select(g => new FilterOption(g.Id, g.Name))],
            [.. serving.Select(s => s.ToString()).Order(StringComparer.Ordinal)]);
    }

    /// <summary>
    /// A LIKE pattern matching this text anywhere, with the wildcards escaped. The escaping is the
    /// point: a filter for "%" should look for a percent sign, not return the whole catalog and look
    /// for all the world like a working filter.
    /// </summary>
    private static string Contains(string text) =>
        $"%{text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";

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

    /// <summary>
    /// Names the one bottle each row is short of (FEATURES §10). Without this the almost-makeable
    /// list is just a list of drinks you cannot make, which is most of the catalog — the name is what
    /// turns it into "buy this, unlock these".
    /// </summary>
    /// <remarks>
    /// The predicate here is character-for-character the one the filter counted, so every row on the
    /// page yields exactly one unsatisfied line. That is why a second query is safe: it cannot
    /// disagree with the filter about which lines count, and re-deriving it by hand somewhere else is
    /// how the two would eventually drift apart.
    /// </remarks>
    private async Task<List<CocktailSummary>> WithMissingIngredientAsync(
        List<CocktailSummary> items,
        IQueryable<Guid> available,
        IQueryable<IngredientSubstitution> subs,
        CancellationToken cancellationToken)
    {
        var ids = items.Select(i => i.Id).ToList();

        var missing = await cocktails.Query()
            .Where(c => ids.Contains(c.Id))
            .SelectMany(c => c.Lines
                .Where(l => l.IsRequired
                            && !available.Contains(l.IngredientId)
                            && !subs.Any(s => s.IngredientId == l.IngredientId
                                              && available.Contains(s.SubstituteIngredientId)))
                .Select(l => new { CocktailId = c.Id, Name = l.Ingredient!.Name }))
            .ToListAsync(cancellationToken);

        var byCocktail = missing
            .GroupBy(x => x.CocktailId)
            .ToDictionary(g => g.Key, g => g.First().Name);

        return [.. items.Select(i => byCocktail.TryGetValue(i.Id, out var name)
            ? i with { MissingIngredient = name }
            : i)];
    }
}
