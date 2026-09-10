using JiggerJot.Api.Endpoints;

namespace JiggerJot.Api.Features.Inventory;

/// <summary>
/// The shelf's read and write surface (INV). Registered through <c>MapTenantFeatureGroup</c>, which
/// applies the shared tenant-API auth policy so the slice never re-spells authorization.
/// </summary>
public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventory(this IEndpointRouteBuilder app)
    {
        var group = app.MapTenantFeatureGroup("/api/inventory");

        group.MapGet("/", async (InventoryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.ListAsync(ct)));

        group.MapPut("/{ingredientId:guid}", async (
            Guid ingredientId,
            SetAvailabilityRequest request,
            InventoryHandler handler,
            CancellationToken ct) =>
        {
            // 404 rather than 403 for an ingredient this household cannot see: the filter simply does
            // not return it, and a different answer would let someone probe ids to discover another
            // household's custom ingredients.
            return await handler.SetAsync(ingredientId, request.IsAvailable, ct)
                ? Results.NoContent()
                : Results.NotFound();
        });

        return app;
    }
}
