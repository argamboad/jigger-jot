using System.Security.Claims;
using JiggerJot.Api.Authentication;
using JiggerJot.Api.Endpoints;
using JiggerJot.Api.Services;
using JiggerJot.Core.Entities;

namespace JiggerJot.Api.Features.Catalog;

/// <summary>
/// The catalog's read surface (CKTL-2). A vertical slice registers its own routes through
/// <c>MapTenantFeatureGroup</c>, which applies the shared tenant-API auth policy so the slice never
/// re-spells (or forgets) authorization.
/// </summary>
public static class CocktailEndpoints
{
    public static IEndpointRouteBuilder MapCocktails(this IEndpointRouteBuilder app)
    {
        var group = app.MapTenantFeatureGroup("/api/cocktails");

        group.MapGet("/", async (
            string? search,
            int? page,
            int? pageSize,
            bool? makeable,
            bool? almost,
            string? ingredient,
            Guid? method,
            Guid? glass,
            string? serving,
            CocktailBrowseHandler handler,
            CancellationToken ct) =>
        {
            // Out-of-range paging is clamped rather than rejected. This is a read, and handing a 400
            // to someone who typed page=0 helps nobody; the request record does the clamping so the
            // handler and its tests see one shape.
            var request = new CocktailBrowseRequest(
                search,
                page ?? 1,
                pageSize ?? CocktailBrowseRequest.DefaultPageSize,
                // FEATURES §11: "makeable now" is one combinable filter on this list, not a separate
                // view. Absent means off, which is the whole catalog.
                makeable ?? false,
                // FEATURES §10: one required line short, after substitutions. Adjacent to the filter
                // above and never overlapping it, so asking for both returns nothing — which is the
                // honest answer rather than a precedence rule invented here.
                almost ?? false,
                // FEATURES §11, FILTER-1. All combinable, with each other and with everything above.
                ingredient,
                method,
                glass,
                // An unparseable serving type is treated as no filter rather than a 400, in keeping
                // with the rest of this read: a bad query string should not cost someone their page.
                Enum.TryParse<ServingType>(serving, ignoreCase: true, out var kind) ? kind : null);

            return Results.Ok(await handler.BrowseAsync(request, ct));
        });

        // The dropdowns for the filters above. A separate call because it is the same answer for
        // every page of every search, and folding it into each browse response would send the whole
        // list of glasses down with every keystroke in the search box.
        group.MapGet("/filters", async (CocktailBrowseHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.FilterOptionsAsync(ct)));

        // Everything the authoring form may offer. Deliberately NOT the same list as /filters above:
        // that one is derived from the catalog so a filter never offers a dead end, while this is the
        // whole curated lookup, because someone writing down what they pour must be able to reach a
        // glass no seeded recipe happens to use (JJ-022).
        group.MapGet("/lookups", async (CocktailAuthoringHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.OptionsAsync(ct)));

        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            CocktailDetailHandler handler,
            CancellationToken ct) =>
        {
            // A cocktail the caller may not see is a 404, not a 403. The query filter simply does not
            // return another household's rows, so "forbidden" would be a claim this endpoint is in no
            // position to make — and saying it would confirm the row exists.
            var detail = await handler.GetAsync(id, principal.GetUserId(), ct);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        });

        // FEATURES §14: a household writes its own cocktail. The plain POST on the collection,
        // because that is what this is — everything else here is a read or a copy.
        group.MapPost("/", async (
            AuthorCocktailRequest request,
            CocktailAuthoringHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.CreateAsync(request, ct);

            return result.Outcome switch
            {
                AuthorCocktailOutcome.Created =>
                    Results.Created($"/api/cocktails/{result.Id}", new CocktailCreatedResponse(result.Id!.Value)),

                AuthorCocktailOutcome.InvalidName => Results.BadRequest(new ErrorResponse(
                    "invalid_name", "A cocktail name is required")),

                AuthorCocktailOutcome.NoLines => Results.BadRequest(new ErrorResponse(
                    "no_lines", "A cocktail needs at least one ingredient")),

                AuthorCocktailOutcome.UnknownIngredient => Results.BadRequest(new ErrorResponse(
                    "unknown_ingredient", "One of those ingredients is not in your catalog")),

                AuthorCocktailOutcome.InvalidLine => Results.BadRequest(new ErrorResponse(
                    "invalid_line", "Check the amounts and units on each line")),

                _ => Results.BadRequest(new ErrorResponse(
                    "unknown_lookup", "That glass or method does not exist")),
            };
        });

        // FEATURES §13: "create my own version". A POST because it creates a row, under the cocktail
        // it copies because that is the only thing it needs to know.
        group.MapPost("/{id:guid}/fork", async (
            Guid id,
            CocktailForkHandler handler,
            CancellationToken ct) =>
        {
            var forkId = await handler.ForkAsync(id, ct);

            // 404 for a cocktail this household cannot see, for the same reason the read is: the
            // filter does not return it, and "forbidden" would confirm the row exists.
            return forkId is { } created
                ? Results.Created($"/api/cocktails/{created}", new CocktailCreatedResponse(created))
                : Results.NotFound();
        });

        return app;
    }
}
