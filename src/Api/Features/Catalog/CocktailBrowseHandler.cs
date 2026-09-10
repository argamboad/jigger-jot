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
public class CocktailBrowseHandler(IRepository<Cocktail> cocktails)
{
    public async Task<PagedResponse<CocktailSummary>> BrowseAsync(
        CocktailBrowseRequest request, CancellationToken cancellationToken)
    {
        var page = request.SafePage;
        var pageSize = request.SafePageSize;

        var query = cocktails.Query();

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
                c.Lines.Count))
            .ToListAsync(cancellationToken);

        return new PagedResponse<CocktailSummary>(items, page, pageSize, total);
    }
}
