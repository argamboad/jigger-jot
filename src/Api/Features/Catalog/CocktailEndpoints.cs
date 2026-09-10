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

        return app;
    }
}
