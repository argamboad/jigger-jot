using System.Security.Claims;
using JiggerJot.Api.Authentication;
using JiggerJot.Api.Endpoints;

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
            CocktailBrowseHandler handler,
            CancellationToken ct) =>
        {
            // Out-of-range paging is clamped rather than rejected. This is a read, and handing a 400
            // to someone who typed page=0 helps nobody; the request record does the clamping so the
            // handler and its tests see one shape.
            var request = new CocktailBrowseRequest(
                search,
                page ?? 1,
                pageSize ?? CocktailBrowseRequest.DefaultPageSize);

            return Results.Ok(await handler.BrowseAsync(request, ct));
        });

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

        return app;
    }
}
