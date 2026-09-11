using JiggerJot.Api.Endpoints;
using JiggerJot.Api.Services;

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

        group.MapGet("/categories", async (InventoryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.CategoriesAsync(ct)));

        // INV-2, FEATURES §8: add a custom ingredient inline. Under /api/inventory rather than a
        // catalog route because this is the shelf's own affordance — the person is standing at their
        // checklist holding a bottle the catalog does not know about.
        group.MapPost("/ingredients", async (
            AddIngredientRequest request,
            InventoryHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.AddIngredientAsync(request, ct);

            return result.Outcome switch
            {
                AddIngredientOutcome.Created =>
                    Results.Created($"/api/inventory/ingredients/{result.Item!.Id}", result.Item),

                // 409 with the id of what is already there, so the client can offer to tick that
                // instead of leaving someone to hunt for a name they just typed.
                AddIngredientOutcome.AlreadyExists => Results.Conflict(new IngredientExistsResponse(
                    "ingredient_exists",
                    "That ingredient is already on your shelf",
                    result.ExistingIngredientId!.Value)),

                AddIngredientOutcome.InvalidName => Results.BadRequest(new ErrorResponse(
                    "invalid_name", "An ingredient name is required")),

                _ => Results.BadRequest(new ErrorResponse(
                    "invalid_category", "Choose a category, and a subcategory that belongs to it")),
            };
        });

        // ONBOARD-1, FEATURES §7: the wizard's write. A whole shelf in one request, because nothing
        // in the wizard is confirmed until Finish — writing as it goes would leave a half-filled
        // shelf behind for anyone who closed the tab midway. The per-ingredient PUT below stays for
        // the shelf screen, where a tick IS the decision and should be saved before someone looks
        // away. Always 200: an id this household cannot see is named in the body rather than failing
        // the request, so a wizard left open across a catalog change does not lose its good ticks.
        group.MapPut("/", async (
            BulkSetAvailabilityRequest request,
            InventoryHandler handler,
            CancellationToken ct) =>
            Results.Ok(await handler.SetManyAsync(request, ct)));

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
